using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// Delivers durably-queued general match results to Backend V2 and retries them across process
    /// restarts, relying entirely on the server's own <c>(PlayerId, ClientResultId)</c> idempotency
    /// contract rather than generating replacement ids after a failure.
    ///
    /// A single in-process drain at a time: <see cref="Drain"/> refuses a second concurrent call
    /// while one is already running, the same idea as <c>RemoteAttemptResultSubmitter.TryClaim</c>.
    /// A result <see cref="Enqueue"/>d while a drain is already in flight is never stranded - the
    /// active drain notices the queue changed and makes another pass before finishing, so a second,
    /// overlapping coroutine is never needed to pick it up.
    ///
    /// Every entry is owner-bound: only <see cref="BackendV2SessionStore.Current"/>'s player's own
    /// entries are ever sent, on their own bearer token. An entry queued under a different Backend V2
    /// player - because that player has since signed out, or a different player has since signed in
    /// on this device - is left pending, untouched: never rewritten to a new owner, deleted, or sent
    /// under someone else's session.
    /// </summary>
    public static class MatchResultSubmissionCoordinator
    {
        private static bool draining;
        private static bool queueChangedDuringDrain;

        /// <summary>Durably queues <paramref name="request"/> for <paramref name="ownerPlayerId"/>
        /// and kicks off a delivery attempt. Safe to call repeatedly with the exact same request - a
        /// retry of the same logical enqueue is a no-op, never a duplicate entry (see
        /// <see cref="PendingMatchResultStore.Enqueue"/>).</summary>
        public static PendingMatchResultEnqueueResult Enqueue(Guid ownerPlayerId, SubmitMatchResultDto request)
        {
            PendingMatchResultEnqueueResult result = PendingMatchResultStore.Enqueue(ownerPlayerId, request);
            if (result != PendingMatchResultEnqueueResult.Added
                && result != PendingMatchResultEnqueueResult.AlreadyQueued)
            {
                return result;
            }

            if (draining)
            {
                // Picked up by the active drain's re-check before it finishes (see Drain below) -
                // never stranded until the next unrelated trigger.
                queueChangedDuringDrain = true;
            }
            else
            {
                TriggerDrain();
            }

            return result;
        }

        /// <summary>Starts <see cref="Drain"/> on <see cref="BackendV2CoroutineHost"/> if nothing is
        /// already draining; a no-op otherwise. The restart-recovery entry point (called from
        /// <c>LoadManager</c>, independent of local SQLite readiness) as well as <see cref="Enqueue"/>'s
        /// own trigger.
        ///
        /// Never lets an exception escape: <c>StartCoroutine</c> runs a coroutine's first leg
        /// synchronously, inline, so a failure constructing/starting it (or anything reached before
        /// the first real yield) would otherwise propagate into this method's caller - for
        /// <c>LoadManager.LoadAllDataCoroutine</c>, that caller has no try/catch of its own, and this
        /// call is meant to be a background, best-effort retry that can never abort the rest of that
        /// coroutine (database wait, catalog loading, <c>PendingMatchPersistenceStore.Repair()</c>).</summary>
        public static void TriggerDrain()
        {
            if (draining)
            {
                return;
            }

            // EditMode has no player loop and no scene to attach a coroutine host to -
            // BackendV2CoroutineHost.Instance would still construct one via Object.DontDestroyOnLoad,
            // which logs an engine error outside Play mode. EditMode tests drive Drain() directly
            // (CoroutineTestRunner.RunToCompletion) instead of relying on this background trigger, so
            // skipping it here does not change EditMode behavior, only silences that spurious error.
            if (!Application.isPlaying)
            {
                return;
            }

            try
            {
                BackendV2CoroutineHost.Instance.StartCoroutine(Drain());
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "MatchResultSubmissionCoordinator.TriggerDrain failed; this must never block or "
                    + "abort its caller. " + exception);
            }
        }

        /// <summary>The actual drain: sends every retryable entry owned by the current Backend V2
        /// player, sequentially, then re-checks the queue once more before finishing in case
        /// <see cref="Enqueue"/> added something new mid-drain. No-ops with no Backend V2 session.
        /// Exposed (rather than private) so it can be driven directly by
        /// <c>CoroutineTestRunner.RunToCompletion</c> in EditMode tests - the same testability seam
        /// every typed client already gets.</summary>
        public static IEnumerator Drain()
        {
            if (draining)
            {
                yield break;
            }

            draining = true;
            try
            {
                bool runAgain;
                do
                {
                    queueChangedDuringDrain = false;

                    Guid? currentPlayer = BackendV2SessionStore.Current?.PlayerId;
                    if (currentPlayer == null)
                    {
                        yield break;
                    }

                    List<PendingMatchResult> batch = PendingMatchResultStore.GetRetryable(currentPlayer.Value);
                    foreach (PendingMatchResult entry in batch)
                    {
                        yield return SendOne(currentPlayer.Value, entry);
                    }

                    runAgain = queueChangedDuringDrain;
                }
                while (runAgain);
            }
            finally
            {
                draining = false;
            }
        }

        private static IEnumerator SendOne(Guid currentPlayer, PendingMatchResult entry)
        {
            ApiResponse<MatchResultResponseDto> response = null;
            yield return BackendV2Runtime.MatchResults.Submit(entry.Request, result => response = result);

            switch (Classify(response))
            {
                case MatchResultSubmissionOutcome.Success:
                    PendingMatchResultStore.Remove(currentPlayer, entry.Request.ClientResultId);
                    break;

                case MatchResultSubmissionOutcome.Definitive:
                    // Validation: the request itself is malformed and will fail identically forever.
                    // Conflict: the server already holds a different payload under this exact
                    // (PlayerId, ClientResultId) - not an idempotent replay, and resubmitting the
                    // same bytes would only be refused again. Neither is retryable.
                    PendingMatchResultStore.MarkDefinitiveFailure(currentPlayer, entry.Request.ClientResultId);
                    Debug.LogError(
                        "Match result " + entry.Request.ClientResultId + " was refused with a definitive "
                        + response.ErrorKind + " (" + response.Problem?.Code + ", correlation "
                        + response.CorrelationId + ") and will not be retried automatically.");
                    break;

                default:
                    // Network, timeout, server, rate-limit or auth failure (or any other/unclassified
                    // outcome): left pending on purpose. The next trigger - the next match-end
                    // result queued, or the next app-start recovery pass - will retry it; never a
                    // tight in-place retry loop here.
                    Debug.LogWarning(
                        "Submitting match result " + entry.Request.ClientResultId + " failed and "
                        + "will be retried later: " + response?.ErrorKind + " (correlation "
                        + response?.CorrelationId + ").");
                    break;
            }
        }

        /// <summary>
        /// Pure classification of one submission outcome, with no network/coroutine/store
        /// involvement - exercised directly in EditMode tests against a hand-built
        /// <see cref="ApiResponse{T}"/> for every <see cref="ApiErrorKind"/>, the same testability
        /// seam <see cref="ApiResponseMapper.Map{T}"/> already gets.
        /// </summary>
        public static MatchResultSubmissionOutcome Classify(ApiResponse<MatchResultResponseDto> response)
        {
            if (response != null && response.Success)
            {
                return MatchResultSubmissionOutcome.Success;
            }

            if (response != null
                && (response.ErrorKind == ApiErrorKind.Validation || response.ErrorKind == ApiErrorKind.Conflict))
            {
                return MatchResultSubmissionOutcome.Definitive;
            }

            return MatchResultSubmissionOutcome.Retryable;
        }
    }

    public enum MatchResultSubmissionOutcome
    {
        Success,
        Retryable,
        Definitive
    }
}
