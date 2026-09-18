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
            yield return sessionManager.EnsureFreshAccessToken(completed: null);

            if (!BackendV2SessionStore.IsAuthenticated)
            {
                // The proactive refresh above ran and failed (or there was never a session at
                // all): sending the request now is guaranteed to come back unauthenticated. Fail
                // fast with the accurate error instead of a doomed round trip classified as
                // "Expired" for what is really "never signed in".
                completed?.Invoke(ApiResponse<TResponse>.Fail(ApiErrorKind.Unauthenticated));
                yield break;
            }

            ApiRequest request = new ApiRequest(method, path, body, query, requiresAuth: true);
            RawApiResponse raw = null;
            yield return transport.Send(request, response => raw = response);
            ApiResponse<TResponse> response1 = ApiResponseMapper.Map<TResponse>(raw, requestRequiredAuth: true);

            if (!response1.Success && response1.ErrorKind == ApiErrorKind.Expired)
            {
                bool refreshed = false;
                yield return sessionManager.ForceRefresh(result => refreshed = result.Success && result.Value);

                if (refreshed)
                {
                    RawApiResponse retryRaw = null;
                    yield return transport.Send(request, response => retryRaw = response);
                    response1 = ApiResponseMapper.Map<TResponse>(retryRaw, requestRequiredAuth: true);
                }
            }

            completed?.Invoke(response1);
        }
    }
}
