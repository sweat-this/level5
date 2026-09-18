using System;

namespace Level5.BackendV2
{
    /// <summary>Generates the idempotency id a caller passes into <see cref="CreateChallengeDto"/>.
    /// Generate once per logical "create this challenge" user action, and reuse the same id if that
    /// exact create is retried after a network failure - a fresh id on retry is what turns a safe
    /// retry into an accidental duplicate challenge.</summary>
    public static class ClientRequestIdGenerator
    {
        public static Guid NewId()
        {
            return Guid.NewGuid();
        }
    }
}
