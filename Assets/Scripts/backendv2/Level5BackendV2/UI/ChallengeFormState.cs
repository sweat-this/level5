using System;

namespace Level5.BackendV2
{
    /// <summary>
    /// Draft state for the "create challenge" form: the player's selections plus the idempotency id
    /// for the create command itself.
    ///
    /// <see cref="ClientRequestId"/> is generated once per logical "create this challenge" action and
    /// reused across a retry of that same action - a fresh id on retry is what turns a safe retry
    /// into an accidental duplicate challenge (see <c>CreateChallengeDto</c>,
    /// <c>ClientRequestIdGenerator</c>). The panel controller is the one that decides whether a given
    /// failure is retryable (network/timeout) or definitive (validation, or success): call
    /// <see cref="CompleteSubmission"/> only for a definitive outcome, never for one the player might
    /// still retry.
    /// </summary>
    public sealed class ChallengeFormState
    {
        public Guid? OpponentId { get; set; }

        public string RulesetId { get; set; }

        public int TotalGames { get; set; } = 3;

        public string InformationPolicy { get; set; }

        public Guid? ClientRequestId { get; private set; }

        /// <summary>The id to send for this submission attempt: the existing one if a submission is
        /// already pending retry, otherwise a freshly generated one. Call this, not
        /// <c>ClientRequestIdGenerator.NewId()</c> directly, every time a create is (re)sent.</summary>
        public Guid GetOrBeginClientRequestId()
        {
            return ClientRequestId ??= ClientRequestIdGenerator.NewId();
        }

        /// <summary>Clears the pending client request id after a definitive success or failure the
        /// player has acted on. A later create (even for the same opponent/ruleset) is then a
        /// genuinely new logical action and gets a new id.</summary>
        public void CompleteSubmission()
        {
            ClientRequestId = null;
        }
    }
}
