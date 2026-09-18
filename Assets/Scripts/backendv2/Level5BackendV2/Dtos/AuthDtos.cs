using System;

namespace Level5.BackendV2
{
    // Wire DTOs for api/v2/auth/*, mirrored field-for-field from
    // Level5Backend/v2/src/Level5.Api/Controllers/AuthController.cs. Kept as plain classes (not the
    // gameplay/domain types) so this boundary never leaks a Backend V2 shape into
    // Level5.Core.Versus or local persistence.

    public sealed class RegisterRequestDto
    {
        public RegisterRequestDto(string username, string password, string displayName)
        {
            Username = username;
            Password = password;
            DisplayName = displayName;
        }

        public string Username { get; }

        public string Password { get; }

        public string DisplayName { get; }
    }

    public sealed class LoginRequestDto
    {
        public LoginRequestDto(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }

        public string Password { get; }
    }

    public sealed class RefreshRequestDto
    {
        public RefreshRequestDto(string refreshToken)
        {
            RefreshToken = refreshToken;
        }

        public string RefreshToken { get; }
    }

    public sealed class LogoutRequestDto
    {
        public LogoutRequestDto(string refreshToken)
        {
            RefreshToken = refreshToken;
        }

        public string RefreshToken { get; }
    }

    /// <summary>Deserialization target for Backend V2's <c>AccessTokenResponseDto</c>. Needs a
    /// settable/parameterless shape for Newtonsoft; construct <see cref="BackendV2Session"/> from it
    /// rather than passing it around as the session itself.</summary>
    public sealed class AccessTokenResponseDto
    {
        public string AccessToken { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }

        public Guid PlayerId { get; set; }

        public string RefreshToken { get; set; }

        public DateTimeOffset RefreshTokenExpiresAt { get; set; }
    }
}
