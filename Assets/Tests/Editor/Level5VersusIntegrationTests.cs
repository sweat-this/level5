using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The vertical slice: a real gameplay mode's stats, through the join, into a resolved series.
///
/// Everything above this file tests the domain on invented numbers. This one starts from the actual
/// <c>GameStats</c> component a match fills in and follows it all the way to a series winner, which
/// is the only way to know the two halves actually meet.
/// </summary>
public class Level5VersusIntegrationTests
{
    private readonly List<GameObject> hosts = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject host in hosts)
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        hosts.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        MatchCatalogs.Reset();
    }

    /// <summary>
    /// A throwaway configuration, built in code so it does not depend on the authored catalogs.
    ///
    /// Only its identity matters to the tests that use it: <c>ActiveMatch</c> replaces the object on
    /// every launch, which is how "a different match is running now" is detected.
    /// </summary>
    private static MatchConfiguration BuildAnyMatch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        PlayerRoster roster = TestDefinitions.SoloRoster();

        return new MatchConfiguration(
            mode,
            level,
            roster,
            MatchModifiers.Default,
            MatchConfigurationBuilder.Resolve(mode, level, roster, MatchModifiers.Default),
            CheerleaderSelection.None,
            "versus integration test");
    }

    [Test]
    public void TheShippedThreePointContestRulesetPointsAtTheRealGameMode()
    {
        CompetitiveRuleset ruleset = VersusCatalogs.Rulesets.Find(new RulesetId("three-point-contest"));

        Assert.That(ruleset, Is.Not.Null, "the catalog is live without any authored asset");
        Assert.That(ruleset.ModeId, Is.EqualTo(GameModeId.ThreePointContest));
        Assert.That((int)ruleset.ModeId, Is.EqualTo(Modes.ThreePointContest), "and at the stored mode number");
        Assert.That(ruleset.SupportsAsync, Is.True);
    }

    [Test]
    public void AMatchsStatsBecomeAComparableResult()
    {
        CompetitiveRuleset ruleset = VersusCatalogs.Rulesets.Find(new RulesetId("three-point-contest"));
        GameStats stats = BuildStats(totalPoints: 21, shotMade: 7, shotAttempt: 10, streak: 4);

        AttemptResult result = GameStatsAttemptResults.Build(
            ruleset.Id,
            ruleset.Version,
            GameModeId.ThreePointContest,
            stats,
            completionTimeSeconds: 62.5f);

        Assert.That(result.RulesetId, Is.EqualTo(ruleset.Id));
        Assert.That(result.RulesetVersion, Is.EqualTo(ruleset.Version));
        Assert.That(result.Get(AttemptMetric.Score), Is.EqualTo(21f));
        Assert.That(result.Get(AttemptMetric.ShotsMade), Is.EqualTo(7f));
        Assert.That(result.Get(AttemptMetric.Accuracy), Is.EqualTo(70f));
        Assert.That(result.Get(AttemptMetric.LongestStreak), Is.EqualTo(4f));
        Assert.That(result.Get(AttemptMetric.CompletionTimeSeconds), Is.EqualTo(62.5f).Within(0.001f));
    }

    [Test]
    public void AMakeCountModeIsMeasuredOnItsOwnShotRatherThanOnEveryShot()
    {
        // "Most 3 pointers" is about threes. Reading the total made count would let a run full of
        // twos beat a run full of threes.
        GameStats stats = BuildStats(totalPoints: 30, shotMade: 12, shotAttempt: 20);
        stats.ThreePointerMade = 4;
        stats.ThreePointerAttempts = 9;

        AttemptResult result = GameStatsAttemptResults.Build(
            new RulesetId("most-3-pointers"),
            1,
            GameModeId.Total3Pointers,
            stats,
            0f);

        Assert.That(result.Get(AttemptMetric.ShotsMade), Is.EqualTo(4f));
        Assert.That(result.Get(AttemptMetric.ShotsAttempted), Is.EqualTo(9f));
    }

    [Test]
    public void AMatchWithNoStatsStillProducesAResultSoTheSeriesCanMoveOn()
    {
        // A stuck series is unrecoverable; losing a game honestly is not.
        AttemptResult result = GameStatsAttemptResults.Build(
            new RulesetId("most-points"),
            1,
            GameModeId.TotalPoints,
            null,
            0f);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Get(AttemptMetric.Score), Is.EqualTo(0f));
    }

    [Test]
    public void TwoRealMatchesResolveAGameThroughTheWholeStack()
    {
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusRuntime.Override(repository);

        SeriesOperation created = VersusRuntime.Coordinator.CreateSeries(
            new SeriesRequest(
                VersusTestFixtures.Patrick(),
                VersusTestFixtures.Alex(),
                SeriesFormat.BestOf1,
                new[] { new RulesetId("three-point-contest") },
                VersusMode.Asynchronous,
                InformationPolicy.SealedAttempt,
                false,
                true,
                "integration test"));

        Assert.That(created.Succeeded, Is.True, created.Validation.ToString());
        SeriesId seriesId = created.Series.Id;

        ReportOneMatch(seriesId, VersusTestFixtures.PatrickId, totalPoints: 21, timePlayed: 55f);
        ReportOneMatch(seriesId, VersusTestFixtures.AlexId, totalPoints: 18, timePlayed: 48f);

        VersusSeries finished = VersusRuntime.Coordinator.Load(seriesId);

        Assert.That(finished.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(finished.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId), "more points");
    }

    [Test]
    public void TheReporterDoesNothingAtAllWhenTheMatchIsNotPartOfASeries()
    {
        // Every existing game mode takes this branch. If it did anything, every ordinary match
        // would be paying for a feature it is not using.
        ActiveVersusAttempt.Clear();

        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(
            VersusMatchReporter.TryReport(BuildStats(10, 1, 2), GameModeId.TotalPoints, 30f),
            Is.True,
            "and it reports the match-end work as finished");
    }

    [Test]
    public void AFailedSaveLeavesTheAttemptInPlaceSoTheMatchEndLoopRetriesIt()
    {
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusRuntime.Override(repository);
        SeriesId seriesId = CreateSoloContest();

        AttemptOperation issued = VersusRuntime.Coordinator.IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        ActiveVersusAttempt.Begin(seriesId, issued.Attempt);

        repository.FailNextSave = true;
        bool done = VersusMatchReporter.TryReport(BuildStats(21, 7, 10), GameModeId.ThreePointContest, 60f);

        Assert.That(done, Is.False, "not durable yet");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True, "so the ids the retry needs are still here");

        bool retried = VersusMatchReporter.TryReport(BuildStats(21, 7, 10), GameModeId.ThreePointContest, 60f);

        Assert.That(retried, Is.True);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "and now it is released");
    }

    [Test]
    public void ARefusedSubmissionReleasesTheAttemptRatherThanRetryingForever()
    {
        // The domain refusing a submission will refuse it again. Spinning on that would leave the
        // player on a results screen that never finishes.
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusRuntime.Override(repository);
        SeriesId seriesId = CreateSoloContest();

        AttemptOperation issued = VersusRuntime.Coordinator.IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        ActiveVersusAttempt.Begin(seriesId, issued.Attempt);

        VersusMatchReporter.TryReport(BuildStats(21, 7, 10), GameModeId.ThreePointContest, 60f);

        // Submitting the same finished attempt again: the domain refuses it, and the reporter has
        // to give up rather than block the match from ending.
        ActiveVersusAttempt.Begin(seriesId, issued.Attempt);
        LogAssert.ignoreFailingMessages = true;
        bool done = VersusMatchReporter.TryReport(BuildStats(99, 9, 10), GameModeId.ThreePointContest, 60f);
        LogAssert.ignoreFailingMessages = false;

        Assert.That(done, Is.True);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
    }

    [Test]
    public void AnAbandonedTurnDoesNotCaptureTheNextOrdinaryMatch()
    {
        // Quit a competitive match to the menu, then play something else. The turn is still
        // outstanding in the stored series - but this match is not it, and reporting this match's
        // score as that turn would hand somebody a result they never played for.
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusRuntime.Override(repository);
        SeriesId seriesId = CreateSoloContest();

        AttemptOperation issued = VersusRuntime.Coordinator.IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        MatchConfiguration versusMatch = BuildAnyMatch();
        ActiveMatch.Begin(versusMatch);
        ActiveVersusAttempt.Begin(seriesId, issued.Attempt, versusMatch);

        Assert.That(ActiveVersusAttempt.IsActive, Is.True, "while the competitive match is the one running");

        // Back to the menu, then an ordinary match: a different configuration becomes current.
        ActiveMatch.Begin(BuildAnyMatch());

        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(
            VersusMatchReporter.TryReport(BuildStats(99, 9, 10), GameModeId.TotalPoints, 30f),
            Is.True,
            "and the ordinary match ends normally");

        VersusSeries series = VersusRuntime.Coordinator.Load(seriesId);
        Assert.That(
            series.ViewFor(VersusTestFixtures.PatrickId).CurrentGame.OwnAttemptState,
            Is.Not.EqualTo(AttemptState.Completed),
            "the turn is still outstanding, waiting to be played properly");
    }

    [Test]
    public void TheLauncherBuildsAnOrdinaryMatchForTheSeriesFrozenMode()
    {
        // The gameplay scene is handed a normal MatchConfiguration and never learns a series exists.
        CompetitiveRuleset ruleset = VersusCatalogs.Rulesets.Find(new RulesetId("most-points"));

        MatchCatalogs.Override(
            new GameModeCatalog(new[] { TestDefinitions.Mode(GameModeId.TotalPoints) }),
            new LevelDefinitionCatalog(new[] { TestDefinitions.Level(1) }));

        try
        {
            MatchConfiguration configuration = VersusLauncher.BuildMatch(
                ruleset,
                levelId: 1,
                participantId: VersusTestFixtures.PatrickId,
                character: TestCharacter());

            Assert.That(configuration, Is.Not.Null);
            Assert.That(configuration.ModeId, Is.EqualTo(GameModeId.TotalPoints));
            Assert.That(configuration.Roster.Count, Is.EqualTo(1), "an attempt is one participant's run");
            Assert.That(configuration.Roster.Players[0].IsLocalHuman, Is.True);
            Assert.That(
                configuration.Roster.Players[0].ParticipantId,
                Is.EqualTo(VersusTestFixtures.PatrickId.Value),
                "the competitive identity travels onto the roster slot");
            Assert.That(configuration.Source, Is.EqualTo("versus series"));
        }
        finally
        {
            MatchCatalogs.Reset();
        }
    }

    // ---- helpers ------------------------------------------------------------------------------

    private SeriesId CreateSoloContest()
    {
        SeriesOperation created = VersusRuntime.Coordinator.CreateSeries(
            new SeriesRequest(
                VersusTestFixtures.Patrick(),
                VersusTestFixtures.Alex(),
                SeriesFormat.BestOf1,
                new[] { new RulesetId("three-point-contest") },
                VersusMode.Asynchronous,
                InformationPolicy.SealedAttempt,
                false,
                true,
                "integration test"));

        Assert.That(created.Succeeded, Is.True, created.Validation.ToString());
        return created.Series.Id;
    }

    /// <summary>
    /// One participant's whole turn, exactly as the launch path and <c>GameRules</c> drive it:
    /// issue, remember, play, report.
    /// </summary>
    private void ReportOneMatch(SeriesId seriesId, ParticipantId participantId, int totalPoints, float timePlayed)
    {
        AttemptOperation issued = VersusRuntime.Coordinator.IssueAttempt(seriesId, participantId);
        Assert.That(issued.Succeeded, Is.True, issued.Validation.ToString());

        ActiveVersusAttempt.Begin(seriesId, issued.Attempt);
        VersusRuntime.Coordinator.StartAttempt(seriesId, issued.Attempt.Id);

        GameStats stats = BuildStats(totalPoints, shotMade: 7, shotAttempt: 10);
        bool reported = VersusMatchReporter.TryReport(stats, GameModeId.ThreePointContest, timePlayed);

        Assert.That(reported, Is.True);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "the attempt is released once it is stored");
    }

    /// <summary>
    /// A stats component on its own object, torn down afterwards.
    ///
    /// One object per call rather than several components on one, so a test that builds two runs
    /// cannot accidentally read the first one's numbers.
    /// </summary>
    private GameStats BuildStats(int totalPoints, int shotMade, int shotAttempt, int streak = 0)
    {
        GameObject host = new GameObject("versus-integration-stats");
        hosts.Add(host);

        GameStats stats = host.AddComponent<GameStats>();
        stats.TotalPoints = totalPoints;
        stats.ShotMade = shotMade;
        stats.ShotAttempt = shotAttempt;
        stats.MostConsecutiveShots = streak;
        return stats;
    }

    private static CharacterSelection TestCharacter()
    {
        return new CharacterSelection(1, "drblood", "Dr Blood", true, true);
    }
}
