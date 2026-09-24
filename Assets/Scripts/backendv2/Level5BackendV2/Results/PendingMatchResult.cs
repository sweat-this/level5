using System;

namespace Level5.BackendV2
{
    /// <summary>
    /// One durably-queued general match-result submission, bound to exactly one Backend V2 player.
    /// <see cref="PendingMatchResultStore"/>'s queue key is
    /// <c>(OwnerPlayerId, Request.ClientResultId)</c> - never rewritten to a different owner, and
    /// never sent using another player's session (see <see cref="MatchResultSubmissionCoordinator"/>).
    /// </summary>
    public sealed class PendingMatchResult
    {
        public PendingMatchResult()
        {
        }

        public PendingMatchResult(Guid ownerPlayerId, SubmitMatchResultDto request)
        {
            OwnerPlayerId = ownerPlayerId;
            Request = request;
        }

        public Guid OwnerPlayerId { get; set; }

        public SubmitMatchResultDto Request { get; set; }

        /// <summary>Set once Backend V2 has refused this exact result with a definitive
        /// (non-retryable) failure - <c>Validation</c> or <c>Conflict</c>. The entry is kept, never
        /// silently dropped, but <see cref="MatchResultSubmissionCoordinator"/> stops sending it
        /// automatically so a permanently-bad request is not retried on every future drain.</summary>
        public bool DefinitiveFailure { get; set; }
    }
}
