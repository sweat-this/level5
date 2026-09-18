using System;

namespace Level5.BackendV2
{
    /// <summary>
    /// The Backend V2 session, held in memory here and mirrored to disk by
    /// <see cref="BackendV2SessionPersistenceStore"/> (via <see cref="Changed"/>) so a player is not
    /// signed out of correspondence on every app restart. That mirroring is a deliberate, documented
    /// security tradeoff (plaintext-on-disk, the same storage this project already gives every other
    /// local save) - see docs/backend-v2-client.md.
    ///
    /// Never touches <c>GameOptions</c> or <c>UserModel</c> - the same discipline
    /// <c>APIHelper.bearerToken</c> already follows for the legacy session, for the same reason: a
    /// token that leaks onto a model that gets serialized, logged or displayed stops being a secret.
    /// </summary>
    public static class BackendV2SessionStore
    {
        public static BackendV2Session Current { get; private set; }

        public static bool IsAuthenticated => Current != null;

        /// <summary>Fires on every <see cref="Set"/> and <see cref="Clear"/>, including a silent
        /// background refresh inside <c>AuthenticatedApiClientBase</c> that never goes through an
        /// explicit login/logout call - the only reliable point to keep a persisted copy in sync.
        /// Argument is the new <see cref="Current"/> (null on clear).</summary>
        public static event Action<BackendV2Session> Changed;

        public static void Set(BackendV2Session session)
        {
            Current = session ?? throw new ArgumentNullException(nameof(session));
            Changed?.Invoke(Current);
        }

        public static void Clear()
        {
            Current = null;
            Changed?.Invoke(null);
        }

        /// <summary>Whether the access token is already expired or will expire within <paramref name="lead"/>.</summary>
        public static bool IsAccessTokenExpiringSoon(TimeSpan lead)
        {
            return Current == null || DateTimeOffset.UtcNow + lead >= Current.ExpiresAt;
        }
    }
}
