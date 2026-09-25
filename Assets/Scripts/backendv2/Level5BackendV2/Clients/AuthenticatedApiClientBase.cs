using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// Shared request plumbing for every client that calls an <c>[Authorize]</c> Backend V2 endpoint
    /// (players, friends, correspondence): proactively refreshes a token that is about to expire,
    /// sends the request, and on an <see cref="ApiErrorKind.Expired"/> response refreshes once more
    /// and retries exactly once before surfacing the failure. Centralized here so none of the ~20
    /// endpoint methods across those three clients hand-roll refresh/retry themselves.
    /// </summary>
    public abstract class AuthenticatedApiClientBase
    {
        private readonly IApiTransport transport;
        private readonly BackendV2SessionManager sessionManager;

        protected AuthenticatedApiClientBase(IApiTransport transport, BackendV2SessionManager sessionManager)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        }

        protected IEnumerator SendAuthorized<TResponse>(
            ApiHttpMethod method,
            string path,
            object body,
            Action<ApiResponse<TResponse>> completed,
            IReadOnlyDictionary<string, string> query = null)
        {
            // The outcome of the proactive refresh must be captured, not discarded: a transient
            // failure (network, timeout, 5xx, ...) now preserves the session instead of clearing
            // it, so IsAuthenticated alone can no longer tell "refresh failed" apart from "refresh
            // never happened to be needed". Sending the protected request anyway after a failed
            // refresh would use a token already known to be stale, guaranteeing a doomed request
            // and a second, redundant refresh attempt inside this same logical operation.
            ApiResponse<bool> proactiveRefresh = null;
            yield return sessionManager.EnsureFreshAccessToken(result => proactiveRefresh = result);

            if (proactiveRefresh != null && !proactiveRefresh.Success)
            {
                completed?.Invoke(
                    ApiResponse<TResponse>.Fail(proactiveRefresh.ErrorKind ?? ApiErrorKind.Network, proactiveRefresh.Problem));
                yield break;
            }

            if (!BackendV2SessionStore.IsAuthenticated)
            {
                // There was never a session at all: sending the request now is guaranteed to come
                // back unauthenticated. Fail fast with the accurate error instead of a doomed round
                // trip classified as "Expired" for what is really "never signed in".
                completed?.Invoke(ApiResponse<TResponse>.Fail(ApiErrorKind.Unauthenticated));
                yield break;
            }

            ApiRequest request = new ApiRequest(method, path, body, query, requiresAuth: true);
            RawApiResponse raw = null;
            yield return transport.Send(request, response => raw = response);
            ApiResponse<TResponse> response1 = ApiResponseMapper.Map<TResponse>(raw, requestRequiredAuth: true);

            if (!response1.Success && response1.ErrorKind == ApiErrorKind.Expired)
            {
                ApiResponse<bool> forcedRefresh = null;
                yield return sessionManager.ForceRefresh(result => forcedRefresh = result);

                if (forcedRefresh != null && forcedRefresh.Success && forcedRefresh.Value)
                {
                    RawApiResponse retryRaw = null;
                    yield return transport.Send(request, response => retryRaw = response);
                    response1 = ApiResponseMapper.Map<TResponse>(retryRaw, requestRequiredAuth: true);
                }
                else if (forcedRefresh == null || !forcedRefresh.Success)
                {
                    // The refresh itself is why this operation cannot proceed - surface that
                    // failure (transient, or a definitive Unauthenticated once the session has
                    // been cleared) rather than the original access-token Expired response, which
                    // was never the real blocker.
                    response1 = ApiResponse<TResponse>.Fail(
                        forcedRefresh?.ErrorKind ?? ApiErrorKind.Network, forcedRefresh?.Problem);
                }
                else
                {
                    // Refresh reported success but not for a session this request can use (e.g. a
                    // logout/new-session race completed the refresh for a session nothing points
                    // at anymore) - nothing usable to retry with.
                    response1 = ApiResponse<TResponse>.Fail(ApiErrorKind.Unauthenticated);
                }
            }

            completed?.Invoke(response1);
        }
    }
}
