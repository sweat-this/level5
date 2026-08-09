using System;
using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;

/// <summary>
/// Builders and doubles for the versus tests.
///
/// The clock and the id source are deterministic on purpose. A correspondence series is a sequence
/// of timestamps and ids, and a test that cannot say what they are cannot check that a series
/// restored from a file is the same series that was saved.
/// </summary>
public static class VersusTestFixtures
{
    public static readonly ParticipantId PatrickId = new ParticipantId("patrick");
    public static readonly ParticipantId AlexId = new ParticipantId("alex");

    public static MatchParticipant Patrick()
    {
        return new MatchParticipant(PatrickId, "Patrick");
    }

    public static MatchParticipant Alex()
    {
        return new MatchParticipant(AlexId, "Alex");
    }

    /// <summary>A points-wins ruleset. The simplest thing that can decide a game.</summary>
    public static CompetitiveRuleset ScoreRuleset(
        string id = "most-points",
        int version = 1,
        VersusCapability capabilities = VersusCapability.LocalAlternating | VersusCapability.Asynchronous,
        int minimumCompatibleVersion = 1)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            version,
            GameModeId.TotalPoints,
            capabilities,
            new[] { ComparisonKey.Highest(AttemptMetric.Score) },
            minimumCompatibleVersion,
            "Most Points");
    }

    /// <summary>Points, then the faster run. Used to check that tie-breaks come from the ruleset.</summary>
    public static CompetitiveRuleset ContestRuleset(string id = "three-point-contest", int version = 1)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            version,
            GameModeId.ThreePointContest,
            VersusCapability.LocalAlternating | VersusCapability.Asynchronous,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.Score),
                ComparisonKey.Lowest(AttemptMetric.CompletionTimeSeconds)
            },
            1,
            "3 Point Contest");
    }

    public static CompetitiveRulesetCatalog Catalog(params CompetitiveRuleset[] rulesets)
    {
        return new CompetitiveRulesetCatalog(rulesets);
    }

    public static SeriesSnapshot Snapshot(
        SeriesFormat format,
        CompetitiveRuleset ruleset = null,
        InformationPolicy policy = InformationPolicy.SealedAttempt,
        bool alternatesFirstAttempt = true)
    {
        ruleset ??= ScoreRuleset();
        List<CompetitiveRuleset> games = new List<CompetitiveRuleset>();
        for (int index = 0; index < format.GameCount; index++)
        {
            games.Add(ruleset);
        }

        return new SeriesSnapshot(format, games, policy, alternatesFirstAttempt);
    }

    public static VersusSeries Series(
        SeriesFormat format,
        CompetitiveRuleset ruleset = null,
        InformationPolicy policy = InformationPolicy.SealedAttempt,
        VersusMode mode = VersusMode.Asynchronous,
        SeriesStatus initialStatus = SeriesStatus.Active,
        bool alternatesFirstAttempt = true)
    {
        return VersusSeries.Create(
            new SeriesId("series-test"),
            Snapshot(format, ruleset, policy, alternatesFirstAttempt),
            new VersusParticipants(Patrick(), Alex()),
            mode,
            new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc),
            initialStatus);
    }

    public static AttemptResult Result(CompetitiveRuleset ruleset, float score, float completionTime = 0f)
    {
        return new AttemptResult.Builder(ruleset.Id, ruleset.Version)
            .Set(AttemptMetric.Score, score)
            .Set(AttemptMetric.CompletionTimeSeconds, completionTime)
            .Build();
    }

    /// <summary>
    /// Plays one whole game, both participants, in whichever order the game designates.
    ///
    /// Following the designated order rather than always starting with Patrick matters under an
    /// open-target game, where the responder is refused until the target exists. Under a sealed
    /// game the order makes no difference, so one helper covers both.
    ///
    /// Used by the series tests so each one reads as the sequence of results it is about rather
    /// than as four lines of issuing and submitting per game.
    /// </summary>
    public static void PlayGame(
        VersusSeries series,
        FakeVersusClock clock,
        IVersusIdSource ids,
        float patrickScore,
        float alexScore)
    {
        VersusGame game = series.CurrentGame;
        CompetitiveRuleset ruleset = game.Ruleset;

        ParticipantId leader = series.Participants.At(game.FirstAttemptParticipantIndex).Id;
        ParticipantId responder = series.Participants.Opponent(leader).Id;

        Submit(series, clock, ids, ruleset, leader, patrickScore, alexScore);
        Submit(series, clock, ids, ruleset, responder, patrickScore, alexScore);
    }

    private static void Submit(
        VersusSeries series,
        FakeVersusClock clock,
        IVersusIdSource ids,
        CompetitiveRuleset ruleset,
        ParticipantId participantId,
        float patrickScore,
        float alexScore)
    {
        Attempt attempt = series.IssueAttempt(participantId, ids, clock);
        float score = participantId == PatrickId ? patrickScore : alexScore;
        series.SubmitResult(attempt.Id, participantId, Result(ruleset, score), clock);
    }

    public static InMemoryVersusSeriesRepository Repository()
    {
        return new InMemoryVersusSeriesRepository();
    }

    public static VersusMatchCoordinator Coordinator(
        IVersusSeriesRepository repository,
        CompetitiveRulesetCatalog catalog,
        FakeVersusClock clock = null,
        IVersusIdSource ids = null)
    {
        return new VersusMatchCoordinator(
            repository,
            catalog,
            clock ?? new FakeVersusClock(),
            ids ?? new SequentialVersusIdSource());
    }

    public static SeriesRequest Request(
        SeriesFormat format,
        IEnumerable<RulesetId> playlist,
        VersusMode mode = VersusMode.Asynchronous,
        InformationPolicy policy = InformationPolicy.SealedAttempt,
        bool requiresInvitation = false)
    {
        return new SeriesRequest(
            Patrick(),
            Alex(),
            format,
            playlist,
            mode,
            policy,
            requiresInvitation,
            true,
            "tests");
    }

    /// <summary>A playlist of one ruleset repeated to fill the format.</summary>
    public static List<RulesetId> Playlist(SeriesFormat format, RulesetId id)
    {
        List<RulesetId> playlist = new List<RulesetId>();
        for (int index = 0; index < format.GameCount; index++)
        {
            playlist.Add(id);
        }

        return playlist;
    }
}

/// <summary>A clock the test moves by hand, so a correspondence delay can actually be a delay.</summary>
public sealed class FakeVersusClock : IVersusClock
{
    public FakeVersusClock()
    {
        UtcNow = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow { get; private set; }

    /// <summary>Moves time on. Two attempts a week apart are two calls to this.</summary>
    public void Advance(TimeSpan amount)
    {
        UtcNow = UtcNow.Add(amount);
    }

    public void AdvanceDays(int days)
    {
        Advance(TimeSpan.FromDays(days));
    }
}

/// <summary>Ids that count up, so a failure names something readable rather than a guid.</summary>
public sealed class SequentialVersusIdSource : IVersusIdSource
{
    private int seriesCount;
    private int attemptCount;

    public SeriesId NewSeriesId()
    {
        seriesCount++;
        return new SeriesId("series-" + seriesCount);
    }

    public AttemptId NewAttemptId()
    {
        attemptCount++;
        return new AttemptId("attempt-" + attemptCount);
    }
}
