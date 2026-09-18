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
    /// is logged and left for the (future, #159) UI adapter to retry - never silently discarded, and
    /// never turned into a second, differently-shaped submission.
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

            BackendV2CoroutineHost.Instance.StartCoroutine(Submit(context, stats, modeId, completionTimeSeconds));
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

        private static IEnumerator Submit(
            RemoteAttemptContext context, GameStats stats, GameModeId modeId, float completionTimeSeconds)
        {
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
                yield break;
            }

            ApiResponse<SeriesResponseDto> response = null;
            yield return BackendV2Runtime.Correspondence.CompleteAttempt(
                context.SeriesId, context.GameNumber, context.AttemptId, metrics, result => response = result);

            // Released as soon as the network call settles, regardless of outcome: a genuine
            // failure should be free to try again on the next match-end retry pass, not locked out
            // by a claim that only ever meant "don't send a second one while this one is in flight".
            Release(context.AttemptId);

            if (response != null && response.Success)
            {
                ActiveRemoteAttempt.Clear();
                yield break;
            }

            if (response != null && response.ErrorKind == ApiErrorKind.Conflict)
            {
                // The domain already decided this attempt; resubmitting would only be refused again.
                Debug.LogWarning(
                    $"Remote attempt {context.AttemptId} result conflicted and will not be retried "
                    + $"({response.Problem?.Code}, correlation {response.CorrelationId}).");
                ActiveRemoteAttempt.Clear();
                yield break;
            }

            // Network, server, validation or auth failure: left active on purpose. The turn is still
            // outstanding server-side, and only the (future, #159) UI adapter should decide whether
            // and how to retry it.
            Debug.LogError(
                $"Submitting the remote attempt {context.AttemptId} result failed: "
                + $"{response?.ErrorKind} ({response?.Problem?.Code}, correlation {response?.CorrelationId}).");
        }
    }
}
