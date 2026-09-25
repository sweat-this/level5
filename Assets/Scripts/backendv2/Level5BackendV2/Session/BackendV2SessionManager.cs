using System;
using System.Collections;

namespace Level5.BackendV2
{
    /// <summary>
    /// Drives <see cref="BackendV2SessionStore"/> through the auth client: register, login, logout,
    /// and refreshing an access token before it expires.
    /// </summary>
    public sealed class BackendV2SessionManager
    {
        private static readonly TimeSpan RefreshLeadTime = TimeSpan.FromSeconds(30);

        private readonly IAuthApiClient authClient;
        private bool refreshInProgress;
        private ApiResponse<bool> lastRefreshOutcome;

        public BackendV2SessionManager(IAuthApiClient authClient)
        {
            this.authClient = authClient ?? throw new ArgumentNullException(nameof(authClient));
        }

        public IEnumerator Register(
            string username, string password, string displayName, Action<ApiResponse<BackendV2Session>> completed)
        {
            ApiResponse<AccessTokenResponseDto> result = null;
            yield return authClient.Register(
                new RegisterRequestDto(username, password, displayName), response => result = response);
            StoreOrFail(result, completed);
        }

        public IEnumerator Login(string username, string password, Action<ApiResponse<BackendV2Session>> completed)
        {
            ApiResponse<AccessTokenResponseDto> result = null;
            yield return authClient.Login(new LoginRequestDto(username, password), response => result = response);
            StoreOrFail(result, completed);
        }

        /// <summary>Clears the local session regardless of whether the server call succeeds - a
        /// player must always be able to sign out locally, even offline. Only clears the session
        /// that initiated this logout: if a newer session has since replaced it (e.g. the player
        /// signed back in while an offline logout request was still in flight), that newer session
        /// is left untouched rather than being wiped by a stale response.</summary>
        public IEnumerator Logout(Action<ApiResponse<ApiVoid>> completed = null)
        {
            BackendV2Session originalSession = BackendV2SessionStore.Current;
            if (originalSession == null)
            {
                completed?.Invoke(ApiResponse<ApiVoid>.Ok(ApiVoid.Instance));
                yield break;
            }

            ApiResponse<ApiVoid> result = null;
            yield return authClient.Logout(
                new LogoutRequestDto(originalSession.RefreshToken), response => result = response);

            if (ReferenceEquals(BackendV2SessionStore.Current, originalSession))
            {
                BackendV2SessionStore.Clear();
            }

            completed?.Invoke(result);
        }

        /// <summary>No-ops when there is no session or the access token is not close to expiring
        /// by this client's own clock. Otherwise refreshes it. Use this for the proactive check
        /// before sending a request; use <see cref="ForceRefresh"/> once the server itself has
        /// already rejected the token, since that overrides any local belief about how fresh it
        /// looked.</summary>
        public IEnumerator EnsureFreshAccessToken(Action<ApiResponse<bool>> completed)
        {
            if (!BackendV2SessionStore.IsAuthenticated)
            {
                completed?.Invoke(ApiResponse<bool>.Fail(ApiErrorKind.Unauthenticated));
                yield break;
            }

            if (!BackendV2SessionStore.IsAccessTokenExpiringSoon(RefreshLeadTime))
            {
                completed?.Invoke(ApiResponse<bool>.Ok(false));
                yield break;
            }

            yield return RefreshNow(completed);
        }

        /// <summary>Refreshes unconditionally, regardless of how close to expiry this client
        /// thought the access token was. For the retry path after a server has already answered
        /// with an expired-token failure - that is authoritative information a local expiry
        /// estimate (clock skew, early revocation, a server restart) cannot override.</summary>
        public IEnumerator ForceRefresh(Action<ApiResponse<bool>> completed)
        {
            if (!BackendV2SessionStore.IsAuthenticated)
            {
                completed?.Invoke(ApiResponse<bool>.Fail(ApiErrorKind.Unauthenticated));
                yield break;
            }

            yield return RefreshNow(completed);
        }

        /// <summary>
        /// Does the actual refresh call, single-flight: if one is already running (a concurrent
        /// caller got here first), waits for it and shares its outcome instead of issuing a second
        /// refresh request against the same refresh token. Without this, two authorized calls
        /// firing close together whenever the token is near expiry - a normal, not a rare, access
        /// pattern for a screen that lists several things at once - would race two refreshes
        /// against the same token, and a losing one's failure would clear a session a winning one
        /// had just legitimately set.
        ///
        /// A session is cleared only when its refresh credential is definitively known to be
        /// unusable: locally expired, or rejected by the server as <see
        /// cref="ApiErrorKind.Unauthenticated"/>. Every other failure (network, timeout, 5xx, rate
        /// limiting, a malformed response, ...) preserves the session and hands the real failure
        /// back to the caller - Backend V2 never actually rejected the credential, so there is no
        /// basis for signing the player out or deleting their persisted login.
        /// </summary>
        private IEnumerator RefreshNow(Action<ApiResponse<bool>> completed)
        {
            if (refreshInProgress)
            {
                while (refreshInProgress)
                {
                    yield return null;
                }

                completed?.Invoke(lastRefreshOutcome);
                yield break;
            }

            refreshInProgress = true;

            // Captured once, at the instant this refresh claims the in-flight guard, so every
            // decision below - whether to apply a rotated token, whether to clear on definitive
            // rejection - is about the session that actually started this refresh, not whatever
            // BackendV2SessionStore.Current happens to be by the time the response arrives.
            BackendV2Session originalSession = BackendV2SessionStore.Current;

            ApiResponse<bool> outcome;
            if (BackendV2SessionStore.IsRefreshTokenExpired(originalSession))
            {
                // Locally known to be unusable - no point asking the server to confirm what this
                // client's own clock already knows.
                outcome = ClearIfStillOwned(originalSession, ApiErrorKind.Unauthenticated, problem: null);
            }
            else
            {
                ApiResponse<AccessTokenResponseDto> result = null;
                yield return authClient.Refresh(
                    new RefreshRequestDto(originalSession.RefreshToken), response => result = response);
                outcome = ApplyRefreshResult(originalSession, result);
            }

            lastRefreshOutcome = outcome;
            refreshInProgress = false;
            completed?.Invoke(outcome);
        }

        private static ApiResponse<bool> ApplyRefreshResult(
            BackendV2Session originalSession, ApiResponse<AccessTokenResponseDto> result)
        {
            if (result != null && result.Success)
            {
                if (ReferenceEquals(BackendV2SessionStore.Current, originalSession))
                {
                    BackendV2SessionStore.Set(BackendV2Session.FromResponse(result.Value));
                    return ApiResponse<bool>.Ok(true);
                }

                // The store no longer owns the session this refresh started for - a newer login,
                // logout, or another refresh already replaced or cleared it while this one was in
                // flight. Applying the rotated token now would resurrect a superseded session or
                // overwrite newer session state with a stale one; do neither. Report "no usable
                // refresh for you" (false) unconditionally, even if some other session now happens
                // to be authenticated - that session belongs to a different caller. Reporting
                // success here would let AuthenticatedApiClientBase retry the original protected
                // request, which - since the transport always reads whichever session is currently
                // ambient - would then silently execute under a different player's identity than
                // the one that made the request.
                return ApiResponse<bool>.Ok(false);
            }

            ApiErrorKind errorKind = result?.ErrorKind ?? ApiErrorKind.Network;
            if (errorKind == ApiErrorKind.Unauthenticated)
            {
                // Definitive: Backend V2 itself rejected the refresh credential as invalid,
                // expired, or revoked.
                return ClearIfStillOwned(originalSession, errorKind, result?.Problem);
            }

            // Transient/ambiguous (Network, Timeout, ServerError, RateLimited, MalformedResponse,
            // Validation, Forbidden, NotFound, Conflict, ...): the refresh credential itself was
            // never rejected, so the session is preserved and the real failure is handed back.
            return ApiResponse<bool>.Fail(errorKind, result?.Problem);
        }

        private static ApiResponse<bool> ClearIfStillOwned(
            BackendV2Session originalSession, ApiErrorKind errorKind, ApiProblem problem)
        {
            if (ReferenceEquals(BackendV2SessionStore.Current, originalSession))
            {
                BackendV2SessionStore.Clear();
            }

            return ApiResponse<bool>.Fail(errorKind, problem);
        }

        private static void StoreOrFail(
            ApiResponse<AccessTokenResponseDto> result, Action<ApiResponse<BackendV2Session>> completed)
        {
            if (result != null && result.Success)
            {
                BackendV2Session session = BackendV2Session.FromResponse(result.Value);
                BackendV2SessionStore.Set(session);
                completed?.Invoke(ApiResponse<BackendV2Session>.Ok(session));
            }
            else
            {
                completed?.Invoke(ApiResponse<BackendV2Session>.Fail(
                    result?.ErrorKind ?? ApiErrorKind.Network, result?.Problem));
            }
        }
    }
}
