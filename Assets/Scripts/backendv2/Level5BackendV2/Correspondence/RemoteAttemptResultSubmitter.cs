using System;
using System.Collections;
using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Versus;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// Hands a finished match's numbers to Backend V2, if it was played for a remote correspondence
    /// attempt.
    ///
    /// The Backend V2 counterpart to <c>VersusMatchReporter</c>: a no-op for every match that is not
    /// an active remote attempt. Unlike the local reporter, submission is network I/O and cannot
    /// join <c>GameRules</c>' own synchronous match-end retry loop without blocking it, so this
    /// fires the submission on its own coroutine and lets the match finish normally. A failure here
    /// is logged and left pending in <see cref="PendingRemoteAttemptResult"/> for the correspondence
    /// UI to retry via <see cref="TryRetryPending"/> - never silently discarded, and never turned
    /// into a second, differently-shaped submission.
    ///
    /// The exact result is durably persisted (<see cref="PendingRemoteAttemptResult.Stash"/>) before
    /// the first <c>CompleteAttempt</c> call, never after: an application termination between "the
    /// server accepted the result" and "the client processed that response" must converge safely
    /// through Backend V2's idempotent replay of this exact same payload on the next resend, which is
    /// only possible if the payload was already durable before that request was ever sent. If the
    /// durable persist itself fails, <c>CompleteAttempt</c> is never called for that attempt this
    /// pass - see <see cref="TrySubmit"/>.
    ///
    /// Never sends a winner, score, current game, revision, frozen rules or the opponent's result -
    /// only the named metrics the descriptor required. Those decisions belong to Backend V2.
    /// </summary>
    public static class RemoteAttemptResultSubmitter
    {
        private static Guid? submittingAttemptId;

        public static void TrySubmit(GameStats stats, GameModeId modeId, float completionTimeSeconds)
        {
            if (!ActiveRemoteAttempt.IsActive)
            {
                return;
            }

            RemoteAttemptContext context = ActiveRemoteAttempt.Context;
            if (!TryClaim(context.AttemptId))
            {
                // Already submitting this exact attempt. GameRules' own match-end retry loop can
                // call TrySubmit again about once a second while a prior submission's network
                // round trip (up to the configured timeout) is still outstanding - without this
                // guard that would fire a second, concurrent CompleteAttempt for the same attempt.
                return;
            }

            IReadOnlyDictionary<string, double> metrics;
            try
            {
                AttemptResult result = GameStatsAttemptResults.Build(
                    new RulesetId(context.RulesetId), context.RulesetVersion, modeId, stats, completionTimeSeconds);
                metrics = RemoteAttemptResultBuilder.BuildMetrics(result, context.RequiredResultMetrics);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Building the remote attempt result for {context.AttemptId} failed: {exception}");
                Release(context.AttemptId);
                return;
            }

            PendingRemoteAttemptResultEnqueueResult stashOutcome = PendingRemoteAttemptResult.Stash(context, metrics);
            if (stashOutcome == PendingRemoteAttemptResultEnqueueResult.Failed
                || stashOutcome == PendingRemoteAttemptResultEnqueueResult.ConflictingPayload)
            {
                // Never make an ambiguous network submission without first durably retaining the
                // exact replay payload. GameRules' own match-end retry loop calls TrySubmit again
                // about once a second while ActiveRemoteAttempt stays active and GameStats still
                // exists, so releasing the claim here (rather than leaving some separate in-memory
                // stash) is enough to let the very next pass try the persist again from the same
                // deterministic GameStats - no rebuild-from-nothing risk, and no second code path to
                // keep in sync with the durable store.
                Debug.LogError(
                    $"Could not durably persist the remote attempt {context.AttemptId} result "
                    + $"({stashOutcome}); it was not submitted. It will be retried automatically.");
                Release(context.AttemptId);
                return;
            }

            BackendV2CoroutineHost.Instance.StartCoroutine(Submit(context, metrics));
        }

        /// <summary>
        /// Resends the exact metrics last built for the currently pending remote attempt result, if
        /// any - never rebuilds a payload. For the correspondence UI's retry action after a failed
        /// submission (network, server, validation or auth failure left it pending on purpose).
        /// Returns false when there is nothing pending, or a submission for it is already in flight.
        /// </summary>
        public static bool TryRetryPending()
        {
            if (!PendingRemoteAttemptResult.HasPending)
            {
                return false;
            }

            RemoteAttemptContext context = PendingRemoteAttemptResult.Context;
            if (!TryClaim(context.AttemptId))
            {
                return false;
            }

            BackendV2CoroutineHost.Instance.StartCoroutine(Submit(context, PendingRemoteAttemptResult.Metrics));
            return true;
        }

        /// <summary>Claims the right to submit this attempt, refusing a second concurrent claim for
        /// the same one. Exposed (rather than folded into the coroutine) so the guard itself is
        /// testable without driving a real Unity coroutine, which does not advance in EditMode.</summary>
        public static bool TryClaim(Guid attemptId)
        {
            if (submittingAttemptId == attemptId)
            {
                return false;
            }

            submittingAttemptId = attemptId;
            return true;
        }

        public static void Release(Guid attemptId)
        {
            if (submittingAttemptId == attemptId)
            {
                submittingAttemptId = null;
            }
        }

        private static IEnumerator Submit(RemoteAttemptContext context, IReadOnlyDictionary<string, double> metrics)
        {
            ApiResponse<SeriesResponseDto> response = null;
            yield return BackendV2Runtime.Correspondence.CompleteAttempt(
                context.SeriesId, context.GameNumber, context.AttemptId, metrics, result => response = result);

            // Released as soon as the network call settles, regardless of outcome: a genuine
            // failure should be free to try again on the next match-end retry pass, not locked out
            // by a claim that only ever meant "don't send a second one while this one is in flight".
            Release(context.AttemptId);

            switch (Classify(response))
            {
                case RemoteAttemptSubmissionOutcome.Success:
                    ActiveRemoteAttempt.Clear();
                    PendingRemoteAttemptResult.Clear(context.PlayerId, context.AttemptId);
                    break;

                case RemoteAttemptSubmissionOutcome.Definitive:
                    // The domain already decided this attempt (an authoritative refusal, not an
                    // ambiguous transport failure); resubmitting the identical payload would only be
                    // refused again. Removed rather than retried forever on every future resend.
                    Debug.LogWarning(
                        $"Remote attempt {context.AttemptId} result was refused with a definitive "
                        + $"{response?.ErrorKind} ({response?.Problem?.Code}, correlation "
                        + $"{response?.CorrelationId}) and will not be retried.");
                    ActiveRemoteAttempt.Clear();
                    PendingRemoteAttemptResult.Clear(context.PlayerId, context.AttemptId);
                    break;

                default:
                    // Network, timeout, server, rate-limit, auth or malformed-response failure (or
                    // any other/unclassified outcome): left pending on purpose. The turn may already
                    // be settled server-side (in particular MalformedResponse, where a 2xx body just
                    // failed to parse) - PendingRemoteAttemptResult keeps the exact durably-persisted
                    // payload so a resend is a safe idempotent replay, never a rebuild.
                    Debug.LogError(
                        $"Submitting the remote attempt {context.AttemptId} result failed: "
                        + $"{response?.ErrorKind} ({response?.Problem?.Code}, correlation {response?.CorrelationId}).");
                    break;
            }
        }

        /// <summary>
        /// Pure classification of one <c>CompleteAttempt</c> outcome, with no network/coroutine/store
        /// involvement - exercised directly in EditMode tests against a hand-built
        /// <see cref="ApiResponse{T}"/> for every <see cref="ApiErrorKind"/>, the same testability
        /// seam <see cref="MatchResultSubmissionCoordinator.Classify"/> already gets.
        ///
        /// <c>Validation</c>/<c>Forbidden</c>/<c>NotFound</c>/<c>Conflict</c> are terminal for
        /// replaying this exact request: identical malformed/missing/invalid metrics or attempt state
        /// cannot heal through retry (<c>Validation</c>); the authenticated player is not authorized
        /// for this attempt (<c>Forbidden</c>); the series/attempt is absent or hidden from this
        /// participant (<c>NotFound</c>); or authoritative server state has already, definitively
        /// rejected this payload/transition (<c>Conflict</c>). Every other kind - including
        /// <c>Unauthenticated</c>/<c>Expired</c> (the owning player may sign back in) and
        /// <c>MalformedResponse</c> (the server may have committed the result before the client
        /// failed to deserialize the response, so an exact idempotent replay is the safe recovery) -
        /// remains retryable.
        /// </summary>
        public static RemoteAttemptSubmissionOutcome Classify(ApiResponse<SeriesResponseDto> response)
        {
            if (response != null && response.Success)
            {
                return RemoteAttemptSubmissionOutcome.Success;
            }

            if (response != null && IsDefinitive(response.ErrorKind))
            {
                return RemoteAttemptSubmissionOutcome.Definitive;
            }

            return RemoteAttemptSubmissionOutcome.Retryable;
        }

        private static bool IsDefinitive(ApiErrorKind? errorKind)
        {
            return errorKind == ApiErrorKind.Validation
                || errorKind == ApiErrorKind.Forbidden
                || errorKind == ApiErrorKind.NotFound
                || errorKind == ApiErrorKind.Conflict;
        }
    }

    public enum RemoteAttemptSubmissionOutcome
    {
        Success,
        Retryable,
        Definitive
    }
}
