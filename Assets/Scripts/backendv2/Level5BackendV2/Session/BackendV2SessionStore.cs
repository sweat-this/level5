using System;

namespace Level5.BackendV2
{
    /// <summary>
    /// The Backend V2 session, held in memory only.
    ///
    /// Deliberately not persisted here: refresh-token persistence across app restarts is a real
    /// product/security decision (device binding, revocation, secure storage), not something to
    /// default into by writing it wherever is convenient. Until that decision is made, logging out
    /// of the app means logging out of Backend V2 - documented as a known gap in
    /// docs/backend-v2-client.md, not a silent omission.
    ///
    /// Never touches <c>GameOptions</c> or <c>UserModel</c> - the same discipline
    /// <c>APIHelper.bearerToken</c> already follows for the legacy session, for the same reason: a
    /// token that leaks onto a model that gets serialized, logged or displayed stops being a secret.
    /// </summary>
    public static class BackendV2SessionStore
    {
        public static BackendV2Session Current { get; private set; }

        public static bool IsAuthenticated => Current != null;

        public static void Set(BackendV2Session session)
        {
            Current = session ?? throw new ArgumentNullException(nameof(session));
        }

        public static void Clear()
        {
            Current = null;
        }

        /// <summary>Whether the access token is already expired or will expire within <paramref name="lead"/>.</summary>
        public static bool IsAccessTokenExpiringSoon(TimeSpan lead)
        {
            return Current == null || DateTimeOffset.UtcNow + lead >= Current.ExpiresAt;
        }
    }
}
