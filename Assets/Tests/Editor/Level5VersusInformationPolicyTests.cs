using Level5.Core.Versus;
using NUnit.Framework;

/// <summary>
/// Sealed attempts and open targets.
///
/// The sealed-attempt tests are the ones that protect the product's distinctive feature. They check
/// absence, not blanking: the opponent's result is not present in what a participant is given, so
/// there is no arrangement of screen code that could show it early.
/// </summary>
public class Level5VersusInformationPolicyTests
{
    private FakeVersusClock clock;
    private SequentialVersusIdSource ids;

    [SetUp]
    public void SetUp()
    {
        clock = new FakeVersusClock();
        ids = new SequentialVersusIdSource();
    }

    // ---- sealed attempt -----------------------------------------------------------------------

    [Test]
    public void BeforeEitherAttemptNeitherParticipantSeesAnything()
    {
        VersusSeries series = Sealed();

        ParticipantGameView patrick = series.ViewFor(VersusTestFixtures.PatrickId).CurrentGame;

        Assert.That(patrick.OpponentResult, Is.Null);
        Assert.That(patrick.OwnResult, Is.Null);
        Assert.That(patrick.Target, Is.Null);
        Assert.That(patrick.IsRevealed, Is.False);
        Assert.That(patrick.IsYourTurn, Is.True);
    }

    [Test]
    public void AfterOneAttemptTheOpponentsScoreAndEveryDerivedNumberAreUnavailable()
    {
        VersusSeries series = Sealed();
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(
            patrick.Id,
            VersusTestFixtures.PatrickId,
            new AttemptResult.Builder(ruleset.Id, ruleset.Version)
                .Set(AttemptMetric.Score, 47)
                .SetShooting(9, 12)
                .Set(AttemptMetric.CompletionTimeSeconds, 61f)
                .Build(),
            clock);

        ParticipantGameView alexSees = series.ViewFor(VersusTestFixtures.AlexId).CurrentGame;

        Assert.That(alexSees.OpponentResult, Is.Null, "no score");
        Assert.That(alexSees.Target, Is.Null, "no target either - this is a sealed game");
        Assert.That(alexSees.Result, Is.Null, "no verdict, because there is not one yet");
        Assert.That(alexSees.IsRevealed, Is.False);

        // What Alex is allowed to know is that Patrick has finished. That is what a turn list is
        // made of and it says nothing about how the run went.
        Assert.That(alexSees.OpponentAttemptState, Is.EqualTo(AttemptState.Completed));
        Assert.That(alexSees.IsYourTurn, Is.True);
    }

    [Test]
    public void TheParticipantWhoHasFinishedStillSeesTheirOwnResultAndNotTheOpponents()
    {
        VersusSeries series = Sealed();
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(
            patrick.Id,
            VersusTestFixtures.PatrickId,
            VersusTestFixtures.Result(ruleset, 47),
            clock);

        ParticipantGameView patrickSees = series.ViewFor(VersusTestFixtures.PatrickId).CurrentGame;

        Assert.That(patrickSees.OwnResult.Get(AttemptMetric.Score), Is.EqualTo(47));
        Assert.That(patrickSees.OpponentResult, Is.Null);
        Assert.That(patrickSees.IsAwaitingOpponent, Is.True);
        Assert.That(patrickSees.OpponentAttemptState, Is.EqualTo(AttemptState.Created));
    }

    [Test]
    public void AnOpponentWhoIsMidRunLeaksNothingBeyondBeingMidRun()
    {
        VersusSeries series = Sealed();

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.StartAttempt(patrick.Id, clock);

        ParticipantGameView alexSees = series.ViewFor(VersusTestFixtures.AlexId).CurrentGame;

        Assert.That(alexSees.OpponentAttemptState, Is.EqualTo(AttemptState.Started));
        Assert.That(alexSees.OpponentResult, Is.Null);
        Assert.That(alexSees.Target, Is.Null);
    }

    [Test]
    public void BothResultsRevealTogetherTheMomentTheGameResolves()
    {
        VersusSeries series = Sealed();

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);

        ParticipantGameView patrickSees = series.ViewFor(VersusTestFixtures.PatrickId).Games[0];
        ParticipantGameView alexSees = series.ViewFor(VersusTestFixtures.AlexId).Games[0];

        Assert.That(patrickSees.IsRevealed, Is.True);
        Assert.That(alexSees.IsRevealed, Is.True);

        Assert.That(patrickSees.OwnResult.Get(AttemptMetric.Score), Is.EqualTo(47));
        Assert.That(patrickSees.OpponentResult.Get(AttemptMetric.Score), Is.EqualTo(31));

        Assert.That(alexSees.OwnResult.Get(AttemptMetric.Score), Is.EqualTo(31));
        Assert.That(alexSees.OpponentResult.Get(AttemptMetric.Score), Is.EqualTo(47));

        Assert.That(patrickSees.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(alexSees.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
    }

    [Test]
    public void AResolvedGameDoesNotRevealAnythingAboutALaterUnplayedOne()
    {
        VersusSeries series = Sealed(SeriesFormat.BestOf3);

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;
        series.SubmitResult(
            patrick.Id,
            VersusTestFixtures.PatrickId,
            VersusTestFixtures.Result(ruleset, 90),
            clock);

        ParticipantSeriesView alexSees = series.ViewFor(VersusTestFixtures.AlexId);

        Assert.That(alexSees.Games[0].OpponentResult, Is.Not.Null, "game one is settled");
        Assert.That(alexSees.Games[1].OpponentResult, Is.Null, "game two is not");
        Assert.That(alexSees.Games[1].Target, Is.Null);
    }

    // ---- open target --------------------------------------------------------------------------

    [Test]
    public void TheFirstCompletedAttemptSetsATargetTheResponderCanSee()
    {
        VersusSeries series = OpenTarget();
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(
            patrick.Id,
            VersusTestFixtures.PatrickId,
            new AttemptResult.Builder(ruleset.Id, ruleset.Version)
                .Set(AttemptMetric.Score, 47)
                .SetShooting(9, 12)
                .Build(),
            clock);

        ParticipantGameView alexSees = series.ViewFor(VersusTestFixtures.AlexId).CurrentGame;

        Assert.That(alexSees.Target, Is.EqualTo(47f), "beat 47");
        Assert.That(alexSees.TargetMetric, Is.EqualTo(AttemptMetric.Score));

        // The target is the number to beat and nothing else. Accuracy and shot count are not part
        // of the format's promise and stay unavailable.
        Assert.That(alexSees.OpponentResult, Is.Null);
    }

    [Test]
    public void UnderOpenTargetTheResponderCannotStartUntilTheTargetExists()
    {
        // Without this, both participants could hold live attempts at once and the format would be
        // a sealed attempt wearing a different name.
        VersusSeries series = OpenTarget();

        Assert.That(series.CanIssueAttempt(VersusTestFixtures.AlexId, out string reason), Is.False);
        Assert.That(reason, Does.Contain("target"));
        Assert.Throws<VersusDomainException>(
            () => series.IssueAttempt(VersusTestFixtures.AlexId, ids, clock));

        Assert.That(series.CanIssueAttempt(VersusTestFixtures.PatrickId, out _), Is.True, "but the leader can");
    }

    [Test]
    public void OnceTheTargetIsSetTheResponderCanPlayAndTheGameResolvesNormally()
    {
        VersusSeries series = OpenTarget();
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(patrick.Id, VersusTestFixtures.PatrickId, VersusTestFixtures.Result(ruleset, 47), clock);

        Assert.That(series.CanIssueAttempt(VersusTestFixtures.AlexId, out _), Is.True);

        Attempt alex = series.IssueAttempt(VersusTestFixtures.AlexId, ids, clock);
        series.SubmitResult(alex.Id, VersusTestFixtures.AlexId, VersusTestFixtures.Result(ruleset, 48), clock);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId), "beat the target by one");
    }

    [Test]
    public void UnderOpenTargetTheRightToSetTheTargetAlternates()
    {
        // Going first is a disadvantage under this format - the leader shoots blind. Alternating it
        // is the only arrangement that is fair over a series.
        VersusSeries series = OpenTarget(SeriesFormat.BestOf3);

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);

        Assert.That(series.CurrentGame.Number, Is.EqualTo(2));
        Assert.That(series.CanIssueAttempt(VersusTestFixtures.AlexId, out _), Is.True, "Alex leads game two");
        Assert.That(series.CanIssueAttempt(VersusTestFixtures.PatrickId, out _), Is.False);
    }

    [Test]
    public void TheTargetDisappearsOnceTheGameIsSettledAndTheRealResultTakesOver()
    {
        VersusSeries series = OpenTarget();

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);

        ParticipantGameView alexSees = series.ViewFor(VersusTestFixtures.AlexId).Games[0];

        Assert.That(alexSees.Target, Is.Null, "no longer a target, it is a result");
        Assert.That(alexSees.OpponentResult.Get(AttemptMetric.Score), Is.EqualTo(47));
    }

    [Test]
    public void SomebodyOutsideTheSeriesCannotAskForAView()
    {
        VersusSeries series = Sealed();

        Assert.Throws<VersusDomainException>(() => series.ViewFor(new ParticipantId("stranger")));
    }

    private static VersusSeries Sealed(SeriesFormat? format = null)
    {
        return VersusTestFixtures.Series(
            format ?? SeriesFormat.BestOf1,
            policy: InformationPolicy.SealedAttempt);
    }

    private static VersusSeries OpenTarget(SeriesFormat? format = null)
    {
        return VersusTestFixtures.Series(
            format ?? SeriesFormat.BestOf1,
            policy: InformationPolicy.OpenTarget);
    }
}
