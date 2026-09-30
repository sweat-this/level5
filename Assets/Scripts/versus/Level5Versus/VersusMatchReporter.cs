using System;
using Level5.Core.Match;
using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// Hands a finished match's numbers to the series it was played for, if it was played for one.
///
/// This is the entire footprint the versus system has inside gameplay: one call, at the end of a
/// match, that returns true immediately when there is no attempt outstanding. Every existing mode
/// takes that first branch and behaves exactly as it did before.
///
/// It reports whether the work is durably done, so it can sit inside the match-end retry loop
/// <c>GameRules</c> already runs. A submission that could not be saved leaves
/// <see cref="ActiveVersusAttempt"/> intact and comes back on the next pass; the attempt is
/// completed exactly once because the domain refuses a second completion, so a retry after a
/// partial success cannot double-count a game.
///
/// A simultaneous game reports both participants' stats through one aggregate submission, so the
/// two results become durable together or not at all. Which stats belong to which attempt is
/// decided by roster slot (slot 0 is <see cref="ActiveVersusAttempt.ParticipantId"/>, slot 1 is
/// <see cref="ActiveVersusAttempt.SecondParticipantId"/>), never by score order.
/// </summary>
public static class VersusMatchReporter
{
    /// <summary>
    /// Submits the run, if this match was a competitive attempt.
    ///
    /// Returns true when there is nothing to do or when the result is stored, and false only when
    /// the caller should try again.
    /// </summary>
    public static bool TryReport(GameStats stats, GameModeId modeId, float completionTimeSeconds)
    {
        return TryReport(stats, null, modeId, completionTimeSeconds);
    }

    /// <summary>
    /// Submits the run - or, for a simultaneous game, both runs - if this match was competitive.
    /// <paramref name="secondStats"/> is roster slot 1's stats and is read only for a simultaneous
    /// game; a single-attempt match ignores it.
    /// </summary>
    public static bool TryReport(
        GameStats stats,
        GameStats secondStats,
        GameModeId modeId,
        float completionTimeSeconds)
    {
        if (!ActiveVersusAttempt.IsActive)
        {
            return true;
        }

        if (ActiveVersusAttempt.IsSimultaneous)
        {
            return TryReportSimultaneous(stats, secondStats, modeId, completionTimeSeconds);
        }

        SeriesId seriesId = ActiveVersusAttempt.SeriesId;
        AttemptId attemptId = ActiveVersusAttempt.AttemptId;
        ParticipantId participantId = ActiveVersusAttempt.ParticipantId;

        try
        {
            AttemptResult result = GameStatsAttemptResults.Build(
                ActiveVersusAttempt.RulesetId,
                ActiveVersusAttempt.RulesetVersion,
                modeId,
                stats,
                completionTimeSeconds);

            SubmissionOperation submission = VersusRuntime.Coordinator.SubmitResult(
                seriesId,
                attemptId,
                participantId,
                result);

            if (submission.Succeeded)
            {
                ActiveVersusAttempt.Clear();
                return true;
            }

            if (submission.Validation.HasError(VersusValidationCode.PersistenceFailed))
            {
                // Worth another go: the domain refused nothing, the disk did.
                Debug.LogWarning(
                    $"The result for versus attempt {attemptId} could not be saved and will be retried.");
                return false;
            }

            // Anything else is the domain refusing this submission, and it will refuse it again -
            // the attempt was already completed, or belongs to someone else, or the series is over.
            // Retrying would spin forever, so the attempt is released and the match ends normally.
            Debug.LogError(
                $"The result for versus attempt {attemptId} was refused: {submission.Validation}");
            ActiveVersusAttempt.Clear();
            return true;
        }
        catch (Exception exception)
        {
            // A match must always be able to finish. Losing the competitive turn is bad; leaving the
            // player stuck on a results screen that never completes is worse, and the attempt is
            // still outstanding in the stored series either way.
            Debug.LogError($"Reporting versus attempt {attemptId} failed unexpectedly: {exception}");
            ActiveVersusAttempt.Clear();
            return true;
        }
    }

    private static bool TryReportSimultaneous(
        GameStats firstStats,
        GameStats secondStats,
        GameModeId modeId,
        float completionTimeSeconds)
    {
        SeriesId seriesId = ActiveVersusAttempt.SeriesId;
        AttemptSubmission first = default;
        AttemptSubmission second = default;

        try
        {
            // The same completion time for both: the simultaneous slice is Most Points, where time is
            // not a comparison key, and both players finished because the one match did.
            first = new AttemptSubmission(
                ActiveVersusAttempt.AttemptId,
                ActiveVersusAttempt.ParticipantId,
                GameStatsAttemptResults.Build(
                    ActiveVersusAttempt.RulesetId,
                    ActiveVersusAttempt.RulesetVersion,
                    modeId,
                    firstStats,
                    completionTimeSeconds));

            second = new AttemptSubmission(
                ActiveVersusAttempt.SecondAttemptId,
                ActiveVersusAttempt.SecondParticipantId,
                GameStatsAttemptResults.Build(
                    ActiveVersusAttempt.RulesetId,
                    ActiveVersusAttempt.RulesetVersion,
                    modeId,
                    secondStats,
                    completionTimeSeconds));

            SubmissionOperation submission = VersusRuntime.Coordinator.SubmitSimultaneousResults(
                seriesId,
                first,
                second);

            if (submission.Succeeded)
            {
                ActiveVersusAttempt.Clear();
                return true;
            }

            if (submission.Validation.HasError(VersusValidationCode.PersistenceFailed))
            {
                // Neither result is durable - the pair is saved together or not at all - so the same
                // pair is submitted again on the next pass.
                Debug.LogWarning(
                    $"The results for simultaneous versus attempts {first.AttemptId}/{second.AttemptId} "
                    + "could not be saved and will be retried.");
                return false;
            }

            Debug.LogError(
                $"The results for simultaneous versus attempts {first.AttemptId}/{second.AttemptId} "
                + $"were refused: {submission.Validation}");
            ActiveVersusAttempt.Clear();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Reporting simultaneous versus attempts {first.AttemptId}/{second.AttemptId} failed unexpectedly: {exception}");
            ActiveVersusAttempt.Clear();
            return true;
        }
    }
}
