using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using NUnit.Framework;

/// <summary>
/// Ruleset versions and the series snapshot.
///
/// The behaviour these protect is easy to state and easy to lose: a correspondence series can
/// outlive a patch, and a balance change must never rescore a competition two people are in the
/// middle of. A series resolves against the rules it started under - full stop.
/// </summary>
public class Level5VersusVersioningTests
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
    public void AnActiveSeriesKeepsTheRulesVersionItStartedUnder()
    {
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            VersusTestFixtures.ScoreRuleset(version: 3));

        Assert.That(series.Snapshot.GameAt(0).Version, Is.EqualTo(3));

        Attempt attempt = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        Assert.That(attempt.RulesetVersion, Is.EqualTo(3), "the attempt is played under the frozen version");
    }

    [Test]
    public void UpdatingTheCatalogDoesNotTouchASeriesAlreadyUnderWay()
    {
        // The series was created under version 1. The catalog then moves to version 2 with a
        // different tie-break. The series must not notice.
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            VersusTestFixtures.ScoreRuleset(version: 1));

        CompetitiveRulesetCatalog updated = VersusTestFixtures.Catalog(
            VersusTestFixtures.ContestRuleset("most-points", version: 2));

        Assert.That(updated.Find(new RulesetId("most-points")).Version, Is.EqualTo(2));
        Assert.That(series.Snapshot.GameAt(0).Version, Is.EqualTo(1), "unchanged");
        Assert.That(
            series.Snapshot.GameAt(0).ComparisonKeys.Count,
            Is.EqualTo(1),
            "and still scored on points alone, not on the new tie-break");
    }

    [Test]
    public void ARestoredSeriesResolvesOnItsOwnFrozenRulesNotOnWhateverTheBuildNowThinks()
    {
        // Set a series up on a points-only ruleset, save it, and read it back on a build whose
        // catalog has replaced that id with something scored differently. The restored series must
        // still be scored the old way.
        VersusSeries original = VersusTestFixtures.Series(
            SeriesFormat.BestOf1,
            VersusTestFixtures.ScoreRuleset(version: 1));

        VersusSeries restored = VersusSeriesSerializer.FromJson(VersusSeriesSerializer.ToJson(original));
        CompetitiveRuleset frozen = restored.Snapshot.GameAt(0);

        Assert.That(frozen.Version, Is.EqualTo(1));
        Assert.That(frozen.ComparisonKeys.Count, Is.EqualTo(1));
        Assert.That(frozen.PrimaryMetric, Is.EqualTo(AttemptMetric.Score));

        // Level on points is a draw under version 1, whatever a later version might say.
        VersusTestFixtures.PlayGame(restored, clock, ids, 20, 20);
        Assert.That(restored.Games[0].Result.Kind, Is.EqualTo(GameOutcomeKind.Draw));
    }

    [Test]
    public void AResultProducedUnderTheWrongVersionIsRejected()
    {
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf1,
            VersusTestFixtures.ScoreRuleset(version: 1));

        Attempt attempt = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        CompetitiveRuleset newerBuild = VersusTestFixtures.ScoreRuleset(version: 2);

        Assert.Throws<VersusDomainException>(
            () => series.SubmitResult(
                attempt.Id,
                VersusTestFixtures.PatrickId,
                VersusTestFixtures.Result(newerBuild, 47),
                clock));
    }

    [Test]
    public void ABuildThatCanNoLongerScoreASeriesRefusesToIssueAnAttemptForIt()
    {
        // Version 1 has aged out: the current build is on version 5 and only supports 4 upward.
        // Refusing here is what stops a series being finished under rules it never agreed to.
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            VersusTestFixtures.ScoreRuleset(version: 1));
        repository.Save(series);

        VersusMatchCoordinator coordinator = VersusTestFixtures.Coordinator(
            repository,
            VersusTestFixtures.Catalog(
                VersusTestFixtures.ScoreRuleset(version: 5, minimumCompatibleVersion: 4)),
            clock,
            ids);

        AttemptOperation issued = coordinator.IssueAttempt(series.Id, VersusTestFixtures.PatrickId);

        Assert.That(issued.Succeeded, Is.False);
        Assert.That(issued.Validation.HasError(VersusValidationCode.RulesetVersionUnsupported), Is.True);
    }

    [Test]
    public void AnAgedOutSeriesCanStillBeReadEvenThoughItCannotBePlayedOn()
    {
        // Being unable to finish a competition is bad. Losing the record of it is worse, so loading
        // stays possible and only playing on is blocked.
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            VersusTestFixtures.ScoreRuleset(version: 1));
        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);
        repository.Save(series);

        VersusMatchCoordinator coordinator = VersusTestFixtures.Coordinator(
            repository,
            VersusTestFixtures.Catalog(
                VersusTestFixtures.ScoreRuleset(version: 5, minimumCompatibleVersion: 4)),
            clock,
            ids);

        VersusSeries loaded = coordinator.Load(series.Id);

        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded.Score.ToString(), Is.EqualTo("1-0"));
        Assert.That(loaded.ViewFor(VersusTestFixtures.PatrickId).Games[0].IsRevealed, Is.True);
    }

    [Test]
    public void ASeriesNamingARulesetThisBuildNoLongerHasIsRefusedRatherThanGuessedAt()
    {
        InMemoryVersusSeriesRepository repository = VersusTestFixtures.Repository();
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            VersusTestFixtures.ScoreRuleset("retired-mode"));
        repository.Save(series);

        VersusMatchCoordinator coordinator = VersusTestFixtures.Coordinator(
            repository,
            VersusTestFixtures.Catalog(VersusTestFixtures.ScoreRuleset()),
            clock,
            ids);

        AttemptOperation issued = coordinator.IssueAttempt(series.Id, VersusTestFixtures.PatrickId);

        Assert.That(issued.Succeeded, Is.False);
        Assert.That(issued.Validation.HasError(VersusValidationCode.UnknownRuleset), Is.True);
    }

    [Test]
    public void TheBuildVersionAndTheRulesVersionAreSeparateNumbers()
    {
        // Nothing in this domain reads Application.version, and a ruleset's version is its own. Two
        // builds can both play three-point-contest version 4, and that is the point.
        CompetitiveRuleset ruleset = VersusTestFixtures.ContestRuleset(version: 4);

        Assert.That(ruleset.Version, Is.EqualTo(4));
        Assert.That(ruleset.CanPlayVersion(4), Is.True);
        Assert.That(ruleset.ToString(), Is.EqualTo("three-point-contest v4"));
    }

    [Test]
    public void TheSnapshotFormatCarriesItsOwnVersionSeparateFromTheRules()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);

        Assert.That(series.Snapshot.FormatVersion, Is.EqualTo(SeriesSnapshot.CurrentFormatVersion));

        VersusSeries restored = VersusSeriesSerializer.FromJson(VersusSeriesSerializer.ToJson(series));
        Assert.That(restored.Snapshot.FormatVersion, Is.EqualTo(SeriesSnapshot.CurrentFormatVersion));
    }
}
