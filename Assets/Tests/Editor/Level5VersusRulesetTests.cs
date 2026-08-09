using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;

/// <summary>
/// The competitive ruleset: identity, versions, capabilities and comparison.
///
/// The identity and version tests matter most. A ruleset id is written into every correspondence
/// document, and a version decides whether a series two people are halfway through can still be
/// scored - both are contract with stored data, not implementation detail.
/// </summary>
public class Level5VersusRulesetTests
{
    [Test]
    public void ARulesetNeedsAWellFormedId()
    {
        Assert.That(new RulesetId("three-point-contest").IsWellFormed(), Is.True);
        Assert.That(new RulesetId("most-points-120s").IsWellFormed(), Is.True);

        Assert.That(new RulesetId("Three Point Contest").IsWellFormed(), Is.False, "spaces and capitals");
        Assert.That(new RulesetId("-leading").IsWellFormed(), Is.False, "leading hyphen");
        Assert.That(new RulesetId("trailing-").IsWellFormed(), Is.False, "trailing hyphen");
        Assert.That(new RulesetId("double--hyphen").IsWellFormed(), Is.False);
        Assert.That(new RulesetId(string.Empty).IsWellFormed(), Is.False);
    }

    [Test]
    public void ARulesetRefusesToExistWithoutAUsableIdentity()
    {
        Assert.Throws<VersusDomainException>(
            () => Build(id: string.Empty),
            "a ruleset with no id cannot be persisted or looked up");

        Assert.Throws<VersusDomainException>(
            () => Build(id: "Not Kebab Case"),
            "an off-convention id would be inconsistent with every other stored id");

        Assert.Throws<VersusDomainException>(() => Build(version: 0));
    }

    [Test]
    public void ARulesetWithNoComparisonKeysIsRefused()
    {
        // Without a key there is no way to decide a winner, so every game would be a draw and every
        // series would run to its full length and end level. Failing at construction says so.
        Assert.Throws<VersusDomainException>(
            () => new CompetitiveRuleset(
                new RulesetId("no-keys"),
                1,
                GameModeId.TotalPoints,
                VersusCapability.Asynchronous,
                new ComparisonKey[0]));
    }

    [Test]
    public void CapabilitiesAreExplicitAndDefaultToNothing()
    {
        CompetitiveRuleset localOnly = VersusTestFixtures.ScoreRuleset(
            capabilities: VersusCapability.LocalAlternating);

        Assert.That(localOnly.Supports(VersusCapability.LocalAlternating), Is.True);
        Assert.That(localOnly.SupportsAsync, Is.False, "a mode must opt in to correspondence play");
        Assert.That(localOnly.Supports(VersusCapability.OnlineRealtime), Is.False);

        CompetitiveRuleset nothing = VersusTestFixtures.ScoreRuleset(capabilities: VersusCapability.None);
        Assert.That(nothing.Supports(VersusCapability.LocalSimultaneous), Is.False);
        Assert.That(nothing.Supports(VersusCapability.None), Is.False, "None is not a capability to hold");
    }

    [Test]
    public void ARulesetSaysWhichOfItsOwnVersionsItCanStillPlay()
    {
        CompetitiveRuleset current = VersusTestFixtures.ScoreRuleset(version: 4, minimumCompatibleVersion: 3);

        Assert.That(current.CanPlayVersion(4), Is.True);
        Assert.That(current.CanPlayVersion(3), Is.True);
        Assert.That(current.CanPlayVersion(2), Is.False, "older than the declared floor");
        Assert.That(current.CanPlayVersion(5), Is.False, "from a build newer than this one");
    }

    [Test]
    public void AMinimumCompatibleVersionAboveTheVersionItselfIsRefused()
    {
        Assert.Throws<VersusDomainException>(() => Build(version: 2, minimumCompatible: 3));
    }

    [Test]
    public void TheHighestScoreWins()
    {
        CompetitiveRuleset ruleset = VersusTestFixtures.ScoreRuleset();

        Assert.That(
            ruleset.Compare(
                VersusTestFixtures.Result(ruleset, 47),
                VersusTestFixtures.Result(ruleset, 31)),
            Is.GreaterThan(0));

        Assert.That(
            ruleset.Compare(
                VersusTestFixtures.Result(ruleset, 31),
                VersusTestFixtures.Result(ruleset, 47)),
            Is.LessThan(0));
    }

    [Test]
    public void EqualOnEveryKeyIsADraw()
    {
        CompetitiveRuleset ruleset = VersusTestFixtures.ScoreRuleset();

        Assert.That(
            ruleset.Compare(
                VersusTestFixtures.Result(ruleset, 47),
                VersusTestFixtures.Result(ruleset, 47)),
            Is.EqualTo(0));
    }

    [Test]
    public void TieBreaksComeFromTheRulesetAndCanRunTheOtherWay()
    {
        // The contest ruleset breaks a tie on the faster run, which is the opposite direction from
        // its primary key. A single global tie-break rule could not express this.
        CompetitiveRuleset contest = VersusTestFixtures.ContestRuleset();

        Assert.That(
            contest.Compare(
                VersusTestFixtures.Result(contest, 30, completionTime: 42f),
                VersusTestFixtures.Result(contest, 30, completionTime: 55f)),
            Is.GreaterThan(0),
            "level on points, so the quicker run takes it");

        // The same two results under a points-only ruleset are a draw, which proves the behaviour
        // belongs to the ruleset rather than to the comparison machinery.
        CompetitiveRuleset points = VersusTestFixtures.ScoreRuleset();
        Assert.That(
            points.Compare(
                VersusTestFixtures.Result(points, 30, completionTime: 42f),
                VersusTestFixtures.Result(points, 30, completionTime: 55f)),
            Is.EqualTo(0));
    }

    [Test]
    public void ComparingResultsFromAnotherRulesetIsRefused()
    {
        CompetitiveRuleset points = VersusTestFixtures.ScoreRuleset();
        CompetitiveRuleset contest = VersusTestFixtures.ContestRuleset();

        Assert.Throws<VersusDomainException>(
            () => points.Compare(
                VersusTestFixtures.Result(points, 10),
                VersusTestFixtures.Result(contest, 10)));
    }

    [Test]
    public void ComparingResultsFromDifferentVersionsIsRefused()
    {
        // Two runs scored under different rules are not comparable, and calling the difference a
        // draw would be worse than refusing.
        CompetitiveRuleset version1 = VersusTestFixtures.ScoreRuleset(version: 1);
        CompetitiveRuleset version2 = VersusTestFixtures.ScoreRuleset(version: 2);

        Assert.Throws<VersusDomainException>(
            () => version2.Compare(
                VersusTestFixtures.Result(version2, 10),
                VersusTestFixtures.Result(version1, 10)));
    }

    [Test]
    public void TheCatalogRefusesDuplicateIdsRatherThanLettingTheLastOneWin()
    {
        CompetitiveRulesetCatalog catalog = VersusTestFixtures.Catalog(
            VersusTestFixtures.ScoreRuleset(version: 1),
            VersusTestFixtures.ScoreRuleset(version: 9));

        Assert.That(catalog.Count, Is.EqualTo(1));
        Assert.That(catalog.Problems, Is.Not.Empty);
        Assert.That(catalog.Find(new RulesetId("most-points")).Version, Is.EqualTo(1), "the first one is kept");
    }

    [Test]
    public void TheCatalogListsOnlyWhatSupportsTheAskedForCompetition()
    {
        CompetitiveRulesetCatalog catalog = VersusTestFixtures.Catalog(
            VersusTestFixtures.ScoreRuleset("most-points"),
            VersusTestFixtures.ScoreRuleset("local-only", capabilities: VersusCapability.LocalAlternating));

        Assert.That(catalog.Supporting(VersusCapability.Asynchronous).Count, Is.EqualTo(1));
        Assert.That(catalog.Supporting(VersusCapability.LocalAlternating).Count, Is.EqualTo(2));
        Assert.That(catalog.Supporting(VersusCapability.OnlineRealtime), Is.Empty);
    }

    [Test]
    public void TheShippedRegistryIsSoundAndNamesRealModes()
    {
        CompetitiveRulesetCatalog catalog =
            new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll());

        Assert.That(catalog.Problems, Is.Empty, "duplicate or empty entries in the shipped rulesets");
        Assert.That(catalog.Count, Is.GreaterThan(0));

        foreach (CompetitiveRuleset ruleset in catalog.Rulesets)
        {
            Assert.That(ruleset.Id.IsWellFormed(), Is.True, $"'{ruleset.Id}' is not a well formed id");
            Assert.That(
                ruleset.ModeId,
                Is.Not.EqualTo(GameModeId.None),
                $"'{ruleset.Id}' does not name a real game mode");
            Assert.That(
                ruleset.Capabilities,
                Is.Not.EqualTo(VersusCapability.None),
                $"'{ruleset.Id}' declares no competition it can be played as");
        }
    }

    [Test]
    public void ModesThatNeedBothPlayersAtOnceAreNotOfferedAsCompetitiveRulesets()
    {
        // The point of this test is that the absence is deliberate. A battle royal is not two
        // separate runs, so there is nothing for this domain to compare, and quietly allowing one
        // into a correspondence series would invent gameplay that does not exist.
        CompetitiveRulesetCatalog catalog =
            new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll());

        Assert.That(catalog.FindByMode(GameModeId.BattleRoyal), Is.Null);
        Assert.That(catalog.FindByMode(GameModeId.CageMatch), Is.Null);
        Assert.That(catalog.FindByMode(GameModeId.VersusCpu), Is.Null);
        Assert.That(catalog.FindByMode(GameModeId.Lockdown), Is.Null);
        Assert.That(catalog.FindByMode(GameModeId.BeatThaComputahs), Is.Null, "a campaign is not a rivalry");
        Assert.That(catalog.FindByMode(GameModeId.FreePlay), Is.Null);
    }

    [Test]
    public void AModeCanSupportLocalVersusAndStillRefuseCorrespondence()
    {
        CompetitiveRulesetCatalog catalog =
            new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll());

        CompetitiveRuleset bash = catalog.FindByMode(GameModeId.BashUpSomeNerds);

        Assert.That(bash, Is.Not.Null, "it is competitive");
        Assert.That(bash.Supports(VersusCapability.LocalAlternating), Is.True, "taking turns is fine");
        Assert.That(bash.SupportsAsync, Is.False, "but a run a week apart is not a fair contest");
    }

    [Test]
    public void AContestIsScoredOnPointsWithTimeOnlyBreakingATie()
    {
        // Scoring a contest on time alone would hand the win to whoever failed fastest, because a
        // run that never cleared its markers still has a completion time.
        CompetitiveRulesetCatalog catalog =
            new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll());
        CompetitiveRuleset contest = catalog.Find(new RulesetId("three-point-contest"));

        Assert.That(contest, Is.Not.Null);
        Assert.That(contest.PrimaryMetric, Is.EqualTo(AttemptMetric.Score));
        Assert.That(contest.ComparisonKeys[1].Metric, Is.EqualTo(AttemptMetric.CompletionTimeSeconds));
        Assert.That(contest.ComparisonKeys[1].Direction, Is.EqualTo(MetricDirection.LowerWins));

        Assert.That(
            contest.Compare(
                VersusTestFixtures.Result(contest, 21, completionTime: 60f),
                VersusTestFixtures.Result(contest, 6, completionTime: 4f)),
            Is.GreaterThan(0),
            "more points beats a quicker failure");
    }

    private static CompetitiveRuleset Build(
        string id = "most-points",
        int version = 1,
        int minimumCompatible = 1)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            version,
            GameModeId.TotalPoints,
            VersusCapability.Asynchronous,
            new[] { ComparisonKey.Highest(AttemptMetric.Score) },
            minimumCompatible);
    }
}
