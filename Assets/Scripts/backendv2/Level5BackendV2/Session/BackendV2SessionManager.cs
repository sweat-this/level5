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
        /// player must always be able to sign out locally, even offline.</summary>
        public IEnumerator Logout(Action<ApiResponse<ApiVoid>> completed = null)
        {
            BackendV2Session session = BackendV2SessionStore.Current;
            if (session == null)
            {
                completed?.Invoke(ApiResponse<ApiVoid>.Ok(ApiVoid.Instance));
                yield break;
            }

            ApiResponse<ApiVoid> result = null;
            yield return authClient.Logout(new LogoutRequestDto(session.RefreshToken), response => result = response);

            BackendV2SessionStore.Clear();
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
            string refreshToken = BackendV2SessionStore.Current.RefreshToken;
            ApiResponse<AccessTokenResponseDto> result = null;
            yield return authClient.Refresh(new RefreshRequestDto(refreshToken), response => result = response);

            ApiResponse<bool> outcome;
            if (result != null && result.Success)
            {
                BackendV2SessionStore.Set(BackendV2Session.FromResponse(result.Value));
                outcome = ApiResponse<bool>.Ok(true);
            }
            else
            {
                BackendV2SessionStore.Clear();
                outcome = ApiResponse<bool>.Fail(result?.ErrorKind ?? ApiErrorKind.Network, result?.Problem);
            }

            lastRefreshOutcome = outcome;
            refreshInProgress = false;
            completed?.Invoke(outcome);
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
