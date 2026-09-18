using System;
using System.Collections;

namespace Level5.BackendV2
{
    public interface IAuthApiClient
    {
        IEnumerator Register(RegisterRequestDto request, Action<ApiResponse<AccessTokenResponseDto>> completed);

        IEnumerator Login(LoginRequestDto request, Action<ApiResponse<AccessTokenResponseDto>> completed);

        IEnumerator Refresh(RefreshRequestDto request, Action<ApiResponse<AccessTokenResponseDto>> completed);

        IEnumerator Logout(LogoutRequestDto request, Action<ApiResponse<ApiVoid>> completed);
    }
}
