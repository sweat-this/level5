using Level5.Core.Match;
using Level5.Core.Versus;

/// <summary>
/// Turns what a match recorded into what the competition compares.
///
/// This is the whole of the gameplay-to-versus contract, and it is deliberately the only place that
/// knows both sides. <c>GameStats</c> is a scene component full of mutable fields; an
/// <see cref="AttemptResult"/> is an immutable value that can be written to a file and read back
/// next week. Nothing above this line has ever heard of <c>GameStats</c>, and nothing below it has
/// heard of a series.
///
/// It reads. It does not score: every number here already exists on the match, and no rule is
/// applied on the way through. A mode that needs a metric this does not produce needs a change here
/// and a ruleset version bump, in that order.
/// </summary>
public static class GameStatsAttemptResults
{
    /// <summary>
    /// Builds the result for a finished run.
    ///
    /// <paramref name="modeId"/> decides which shot counter fills
    /// <see cref="AttemptMetric.ShotsMade"/>: a "most 3 pointers" competition is about threes, not
    /// about every shot that went in. That is the one mode-specific decision in the mapping, and it
    /// belongs here rather than in a ruleset, because it is a fact about where the number is stored
    /// rather than about how the competition works.
    /// </summary>
    public static AttemptResult Build(
        RulesetId rulesetId,
        int rulesetVersion,
        GameModeId modeId,
        GameStats stats,
        float completionTimeSeconds)
    {
        AttemptResult.Builder builder = new AttemptResult.Builder(rulesetId, rulesetVersion);

        if (stats == null)
        {
            // A run with no stats component still has to produce a result, otherwise the attempt
            // stays outstanding forever and the series can never move on. An empty result loses the
            // game honestly, which is a recoverable outcome; a stuck series is not.
            return builder.Build();
        }

        builder.Set(AttemptMetric.Score, stats.TotalPoints);
        builder.Set(AttemptMetric.BonusPoints, stats.BonusPoints);
        builder.Set(AttemptMetric.TotalDistance, stats.TotalDistance);
        builder.Set(AttemptMetric.LongestStreak, stats.MostConsecutiveShots);
        builder.Set(
            AttemptMetric.CompletionTimeSeconds,
            completionTimeSeconds > 0f ? completionTimeSeconds : stats.TimePlayed);

        builder.SetShooting(MadeFor(modeId, stats), AttemptedFor(modeId, stats));

        return builder.Build();
    }

    private static int MadeFor(GameModeId modeId, GameStats stats)
    {
        switch (modeId)
        {
            case GameModeId.Total3Pointers:
            case GameModeId.SpotUp3s:
                return stats.ThreePointerMade;
            case GameModeId.Total4Pointers:
            case GameModeId.SpotUp4s:
                return stats.FourPointerMade;
            case GameModeId.Total7Pointers:
            case GameModeId.SpotUp7s:
                return stats.SevenPointerMade;
            default:
                return stats.ShotMade;
        }
    }

    private static int AttemptedFor(GameModeId modeId, GameStats stats)
    {
        switch (modeId)
        {
            case GameModeId.Total3Pointers:
            case GameModeId.SpotUp3s:
                return stats.ThreePointerAttempts;
            case GameModeId.Total4Pointers:
            case GameModeId.SpotUp4s:
                return stats.FourPointerAttempts;
            case GameModeId.Total7Pointers:
            case GameModeId.SpotUp7s:
                return stats.SevenPointerAttempts;
            default:
                return stats.ShotAttempt;
        }
    }
}
