using System;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using NUnit.Framework;

/// <summary>
/// Saving and restoring a series.
///
/// Every test here interrupts the series at a different point, restores it from JSON, and carries
/// on. That is the whole of the correspondence promise: the application can stop anywhere and the
/// competition survives, because the document is the truth and nothing is reconstructed from a
/// scene or a screen.
/// </summary>
public class Level5VersusPersistenceTests
{
    private FakeVersusClock clock;
    private SequentialVersusIdSource ids;

    [SetUp]
    public void SetUp()
    {
        clock = new FakeVersusClock();
        ids = new SequentialVersusIdSource();
    }

    [Test]
    public void ASeriesSurvivesBeingSavedAndReadBackBeforeAnythingHappens()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf7);

        VersusSeries restored = RoundTrip(original);

        Assert.That(restored.Id, Is.EqualTo(original.Id));
        Assert.That(restored.Status, Is.EqualTo(SeriesStatus.Active));
        Assert.That(restored.Mode, Is.EqualTo(VersusMode.Asynchronous));
        Assert.That(restored.Snapshot.Format, Is.EqualTo(SeriesFormat.BestOf7));
        Assert.That(restored.Snapshot.InformationPolicy, Is.EqualTo(InformationPolicy.SealedAttempt));
        Assert.That(restored.Participants.First.Id, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(restored.Participants.Second.DisplayName, Is.EqualTo("Alex"));
        Assert.That(restored.CurrentGame.Number, Is.EqualTo(1));
        Assert.That(restored.CreatedAtUtc, Is.EqualTo(original.CreatedAtUtc));
    }

    [Test]
    public void AnOutstandingAttemptSurvivesAndIsHandedBackRatherThanReissued()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        Attempt issued = original.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);

        VersusSeries restored = RoundTrip(original);
        Attempt again = restored.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);

        Assert.That(again.Id, Is.EqualTo(issued.Id), "the same turn, not a second one");
        Assert.That(again.State, Is.EqualTo(AttemptState.Ready));
        Assert.That(again.IssuedAtUtc, Is.EqualTo(issued.IssuedAtUtc));
    }

    [Test]
    public void AnInterruptedRunComesBackAsStartedRatherThanUntouched()
    {
        // The application died mid-run. Telling "started and abandoned" from "never begun" is the
        // difference between offering to resume and silently handing out a fresh go.
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        Attempt issued = original.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        original.StartAttempt(issued.Id, clock);

        VersusSeries restored = RoundTrip(original);
        ParticipantGameView view = restored.ViewFor(VersusTestFixtures.PatrickId).CurrentGame;

        Assert.That(view.OwnAttemptState, Is.EqualTo(AttemptState.Started));
    }

    [Test]
    public void ACompletedAttemptAndItsMetricsSurviveIntact()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        CompetitiveRuleset ruleset = original.CurrentGame.Ruleset;
        Attempt issued = original.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        original.SubmitResult(
            issued.Id,
            VersusTestFixtures.PatrickId,
            new AttemptResult.Builder(ruleset.Id, ruleset.Version)
                .Set(AttemptMetric.Score, 47)
                .SetShooting(9, 12)
                .Set(AttemptMetric.CompletionTimeSeconds, 61.5f)
                .Set(AttemptMetric.LongestStreak, 4)
                .Build(),
            clock);

        VersusSeries restored = RoundTrip(original);
        AttemptResult result = restored.ViewFor(VersusTestFixtures.PatrickId).CurrentGame.OwnResult;

        Assert.That(result.Get(AttemptMetric.Score), Is.EqualTo(47f));
        Assert.That(result.Get(AttemptMetric.ShotsMade), Is.EqualTo(9f));
        Assert.That(result.Get(AttemptMetric.ShotsAttempted), Is.EqualTo(12f));
        Assert.That(result.Get(AttemptMetric.Accuracy), Is.EqualTo(75f));
        Assert.That(result.Get(AttemptMetric.CompletionTimeSeconds), Is.EqualTo(61.5f).Within(0.001f));
        Assert.That(result.Get(AttemptMetric.LongestStreak), Is.EqualTo(4f));
        Assert.That(result.RulesetVersion, Is.EqualTo(1));
    }

    [Test]
    public void TheScoreAndTheCurrentGameSurviveBetweenGames()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf5);
        VersusTestFixtures.PlayGame(original, clock, ids, 47, 31);
        VersusTestFixtures.PlayGame(original, clock, ids, 12, 40);

        VersusSeries restored = RoundTrip(original);

        Assert.That(restored.Score.ToString(), Is.EqualTo("1-1"));
        Assert.That(restored.CurrentGame.Number, Is.EqualTo(3));
        Assert.That(restored.Games[0].Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(restored.Games[1].Result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId));
    }

    [Test]
    public void ThreeAllRestoresIntoGameSevenAndFinishesCorrectly()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf7);
        for (int game = 0; game < 6; game++)
        {
            bool patrickWins = game % 2 == 0;
            VersusTestFixtures.PlayGame(original, clock, ids, patrickWins ? 10 : 1, patrickWins ? 1 : 10);
        }

        VersusSeries restored = RoundTrip(original);

        Assert.That(restored.Score.ToString(), Is.EqualTo("3-3"));
        Assert.That(restored.CurrentGame.Number, Is.EqualTo(7));

        VersusTestFixtures.PlayGame(restored, clock, ids, 10, 1);

        Assert.That(restored.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(restored.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(restored.Score.ToString(), Is.EqualTo("4-3"));
    }

    [Test]
    public void ACompletedSeriesStaysCompletedAndStillRefusesEverything()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        VersusTestFixtures.PlayGame(original, clock, ids, 47, 31);

        VersusSeries restored = RoundTrip(original);

        Assert.That(restored.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(restored.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(restored.Result.CompletedAtUtc, Is.EqualTo(original.Result.CompletedAtUtc));
        Assert.Throws<VersusDomainException>(
            () => restored.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock));
    }

    [Test]
    public void AForfeitedSeriesSurvivesAsForfeited()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        original.Forfeit(VersusTestFixtures.AlexId, clock);

        VersusSeries restored = RoundTrip(original);

        Assert.That(restored.Status, Is.EqualTo(SeriesStatus.Forfeited));
        Assert.That(restored.Result.Kind, Is.EqualTo(SeriesOutcomeKind.Forfeit));
        Assert.That(restored.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
    }

    [Test]
    public void AnInvitationSurvivesAsAnInvitation()
    {
        VersusSeries original = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            initialStatus: SeriesStatus.Invited);

        VersusSeries restored = RoundTrip(original);

        Assert.That(restored.Status, Is.EqualTo(SeriesStatus.Invited));
        Assert.That(restored.CurrentGame, Is.Null);

        restored.Accept();
        Assert.That(restored.CurrentGame.Number, Is.EqualTo(1));
    }

    [Test]
    public void EnumsAreStoredByNameSoReorderingOneCannotRewriteHistory()
    {
        // The single most dangerous thing that could happen to this data is an enum gaining a
        // member in the middle. Names survive that; numbers do not.
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            policy: InformationPolicy.OpenTarget,
            mode: VersusMode.Asynchronous);

        string json = VersusSeriesSerializer.ToJson(series);

        Assert.That(json, Does.Contain("\"OpenTarget\""));
        Assert.That(json, Does.Contain("\"Asynchronous\""));
        Assert.That(json, Does.Contain("\"Active\""));
        Assert.That(json, Does.Contain("\"LocalHuman\""));
    }

    [Test]
    public void TheFrozenRulesAreStoredWithTheSeriesRatherThanReferenced()
    {
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            VersusTestFixtures.ContestRuleset());

        string json = VersusSeriesSerializer.ToJson(series);

        Assert.That(json, Does.Contain("three-point-contest"));
        Assert.That(json, Does.Contain("CompletionTimeSeconds"), "the tie-break travels with it");
        Assert.That(json, Does.Contain("LowerWins"), "and so does its direction");
    }

    [Test]
    public void ASummaryCanBeReadWithoutRebuildingTheSeries()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf5);
        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);

        SeriesSummary summary = VersusSeriesSerializer.SummaryFromJson(
            VersusSeriesSerializer.ToJson(series));

        Assert.That(summary.Id, Is.EqualTo(series.Id));
        Assert.That(summary.FirstWins, Is.EqualTo(1));
        Assert.That(summary.SecondWins, Is.EqualTo(0));
        Assert.That(summary.CurrentGameNumber, Is.EqualTo(2));
        Assert.That(summary.Status, Is.EqualTo(SeriesStatus.Active));
        Assert.That(summary.Involves(VersusTestFixtures.AlexId), Is.True);
    }

    [Test]
    public void ASummaryCarriesNoResultsSoListingSeriesCannotLeakASealedAttempt()
    {
        // A turn list renders twenty of these. If a summary carried scores, the list itself would
        // be the leak.
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;
        Attempt attempt = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(
            attempt.Id,
            VersusTestFixtures.PatrickId,
            VersusTestFixtures.Result(ruleset, 47),
            clock);

        SeriesSummary summary = VersusSeriesSerializer.SummaryFromJson(
            VersusSeriesSerializer.ToJson(series));

        Assert.That(summary.FirstWins, Is.EqualTo(0), "an unresolved game is not a win");
        Assert.That(
            typeof(SeriesSummary).GetProperty("Result"),
            Is.Null,
            "a summary must not be able to carry a result at all");
    }

    [Test]
    public void ASeriesStoredWithFewerGamesThanItsSnapshotIsTreatedAsCorrupt()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        VersusSeriesDocument document = VersusSeriesSerializer.ToDocument(series);
        document.games = new[] { document.games[0] };

        Assert.Throws<VersusDomainException>(() => VersusSeriesSerializer.FromDocument(document));
    }

    [Test]
    public void ASeriesStoredWithoutItsFrozenRulesIsRefused()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        VersusSeriesDocument document = VersusSeriesSerializer.ToDocument(series);
        document.rulesets = new VersusRulesetDocument[0];

        Assert.Throws<VersusDomainException>(() => VersusSeriesSerializer.FromDocument(document));
    }

    [Test]
    public void TheRepositoryStoresSerializedBytesRatherThanTheObjectItWasGiven()
    {
        // If the store handed back the instance it was given, every restore test above would pass
        // while the real, file-backed path silently could not round-trip.
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        repository.Save(series);

        VersusSeries loaded = repository.Load(series.Id);

        Assert.That(loaded, Is.Not.SameAs(series));
        Assert.That(repository.RawDocument(series.Id), Does.Contain(series.Id.Value));
    }

    [Test]
    public void ArchivingKeepsTheSeriesReadable()
    {
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);
        repository.Save(series);

        Assert.That(repository.Archive(series.Id), Is.True);
        Assert.That(repository.Load(series.Id), Is.Not.Null, "archiving is never a delete");
        Assert.That(repository.ListSummaries()[0].Archived, Is.True);
    }

    [Test]
    public void ArchivedStaysArchivedAcrossALaterSave()
    {
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        repository.Save(series);
        repository.Archive(series.Id);

        repository.Save(repository.Load(series.Id));

        Assert.That(repository.ListSummaries()[0].Archived, Is.True);
    }

    [Test]
    public void TimesRoundTripAsUtcRatherThanDriftingWithTheMachinesTimeZone()
    {
        VersusSeries original = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        clock.AdvanceDays(3);
        Attempt attempt = original.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);

        VersusSeries restored = RoundTrip(original);
        ParticipantGameView view = restored.ViewFor(VersusTestFixtures.PatrickId).CurrentGame;

        Assert.That(view.OwnAttemptId, Is.EqualTo(attempt.Id));
        Assert.That(restored.CreatedAtUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(restored.CreatedAtUtc, Is.EqualTo(original.CreatedAtUtc));
    }

    private static VersusSeries RoundTrip(VersusSeries series)
    {
        return VersusSeriesSerializer.FromJson(VersusSeriesSerializer.ToJson(series));
    }
}
