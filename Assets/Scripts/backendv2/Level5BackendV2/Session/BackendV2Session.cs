using System;

namespace Level5.BackendV2
{
    /// <summary>What Backend V2 returns from register/login/refresh, held as-is - mirrors
    /// <c>AccessTokenResponseDto</c> field for field.</summary>
    public sealed class BackendV2Session
    {
        public BackendV2Session(
            string accessToken,
            DateTimeOffset expiresAt,
            Guid playerId,
            string refreshToken,
            DateTimeOffset refreshTokenExpiresAt)
        {
            AccessToken = accessToken;
            ExpiresAt = expiresAt;
            PlayerId = playerId;
            RefreshToken = refreshToken;
            RefreshTokenExpiresAt = refreshTokenExpiresAt;
        }

        public string AccessToken { get; }

        public DateTimeOffset ExpiresAt { get; }

        public Guid PlayerId { get; }

        public string RefreshToken { get; }

        public DateTimeOffset RefreshTokenExpiresAt { get; }

        public static BackendV2Session FromResponse(AccessTokenResponseDto response)
        {
            return new BackendV2Session(
                response.AccessToken,
                response.ExpiresAt,
                response.PlayerId,
                response.RefreshToken,
                response.RefreshTokenExpiresAt);
        }
    }
}
