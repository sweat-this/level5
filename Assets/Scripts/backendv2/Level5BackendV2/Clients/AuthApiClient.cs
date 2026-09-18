using System;
using System.Collections;

namespace Level5.BackendV2
{
    /// <summary>
    /// api/v2/auth/*. None of these calls carry a bearer token - even logout, deliberately, since
    /// Backend V2 accepts it with an already-expired access token and only needs the refresh token
    /// in the body (it must work precisely when the access token can no longer authenticate).
    /// </summary>
    public sealed class AuthApiClient : IAuthApiClient
    {
        private readonly IApiTransport transport;

        public AuthApiClient(IApiTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public IEnumerator Register(RegisterRequestDto request, Action<ApiResponse<AccessTokenResponseDto>> completed)
        {
            return Post("api/v2/auth/register", request, completed);
        }

        public IEnumerator Login(LoginRequestDto request, Action<ApiResponse<AccessTokenResponseDto>> completed)
        {
            return Post("api/v2/auth/login", request, completed);
        }

        public IEnumerator Refresh(RefreshRequestDto request, Action<ApiResponse<AccessTokenResponseDto>> completed)
        {
            return Post("api/v2/auth/refresh", request, completed);
        }

        public IEnumerator Logout(LogoutRequestDto request, Action<ApiResponse<ApiVoid>> completed)
        {
            return Post("api/v2/auth/logout", request, completed);
        }

        private IEnumerator Post<TResponse>(string path, object body, Action<ApiResponse<TResponse>> completed)
        {
            ApiRequest request = new ApiRequest(ApiHttpMethod.Post, path, body, requiresAuth: false);
            RawApiResponse raw = null;
            yield return transport.Send(request, response => raw = response);
            completed?.Invoke(ApiResponseMapper.Map<TResponse>(raw, requestRequiredAuth: false));
        }
    }
}
