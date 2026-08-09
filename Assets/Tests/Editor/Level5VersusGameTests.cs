using Level5.Core.Versus;
using NUnit.Framework;

/// <summary>
/// Resolving a single game: when it resolves, who won, and what a tie does.
///
/// A game resolves once and only when both required attempts are in. Everything the series does
/// with wins depends on that being exactly true.
/// </summary>
public class Level5VersusGameTests
{
    [Test]
    public void AGameWithNoAttemptsIsUnresolved()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        Assert.That(series.CurrentGame.Status, Is.EqualTo(VersusGameStatus.Active));
        Assert.That(series.CurrentGame.Result, Is.Null);
        Assert.That(series.CurrentGame.IsResolved, Is.False);
    }

    [Test]
    public void AGameWithOneAttemptIsStillUnresolved()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        SeriesSubmission submission = series.SubmitResult(
            patrick.Id,
            VersusTestFixtures.PatrickId,
            VersusTestFixtures.Result(ruleset, 47),
            clock);

        Assert.That(submission.ResolvedGame, Is.False);
        Assert.That(series.CurrentGame.Result, Is.Null);
        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Active));
    }

    [Test]
    public void AGameResolvesWhenBothAttemptsAreIn()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        VersusTestFixtures.PlayGame(series, clock, ids, patrickScore: 47, alexScore: 31);

        VersusGame game = series.Games[0];
        Assert.That(game.Status, Is.EqualTo(VersusGameStatus.Resolved));
        Assert.That(game.Result.Kind, Is.EqualTo(GameOutcomeKind.Decided));
        Assert.That(game.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
    }

    [Test]
    public void EitherParticipantCanWin()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        VersusTestFixtures.PlayGame(series, clock, ids, patrickScore: 12, alexScore: 40);

        Assert.That(series.Games[0].Result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId));
    }

    [Test]
    public void LevelScoresUnderAPointsOnlyRulesetAreADraw()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        VersusTestFixtures.PlayGame(series, clock, ids, patrickScore: 20, alexScore: 20);

        GameResult result = series.Games[0].Result;
        Assert.That(result.Kind, Is.EqualTo(GameOutcomeKind.Draw));
        Assert.That(result.HasWinner, Is.False);
        Assert.That(series.Score.Draws, Is.EqualTo(1));
    }

    [Test]
    public void TheRulesetsOwnTieBreakDecidesLevelScores()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        CompetitiveRuleset contest = VersusTestFixtures.ContestRuleset();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1, contest);

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(
            patrick.Id,
            VersusTestFixtures.PatrickId,
            VersusTestFixtures.Result(contest, 30, completionTime: 55f),
            clock);

        Attempt alex = series.IssueAttempt(VersusTestFixtures.AlexId, ids, clock);
        series.SubmitResult(
            alex.Id,
            VersusTestFixtures.AlexId,
            VersusTestFixtures.Result(contest, 30, completionTime: 42f),
            clock);

        Assert.That(series.Games[0].Result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId), "quicker run");
    }

    [Test]
    public void AResolvedGameCannotBeResolvedAgain()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(patrick.Id, VersusTestFixtures.PatrickId, VersusTestFixtures.Result(ruleset, 47), clock);
        Attempt alex = series.IssueAttempt(VersusTestFixtures.AlexId, ids, clock);
        series.SubmitResult(alex.Id, VersusTestFixtures.AlexId, VersusTestFixtures.Result(ruleset, 31), clock);

        // Game one is resolved and game two is now current. Resubmitting game one's attempt has to
        // fail: the win it produced has already been counted.
        Assert.Throws<VersusDomainException>(
            () => series.SubmitResult(
                patrick.Id,
                VersusTestFixtures.PatrickId,
                VersusTestFixtures.Result(ruleset, 99),
                clock));

        Assert.That(series.Score.FirstWins, Is.EqualTo(1));
    }

    [Test]
    public void AParticipantCannotSubmitTheOpponentsAttempt()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);

        Assert.Throws<VersusDomainException>(
            () => series.SubmitResult(
                patrick.Id,
                VersusTestFixtures.AlexId,
                VersusTestFixtures.Result(ruleset, 99),
                clock));
    }

    [Test]
    public void SomebodyOutsideTheSeriesCannotTakePartInIt()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        Assert.Throws<VersusDomainException>(
            () => series.IssueAttempt(new ParticipantId("stranger"), ids, clock));
    }

    [Test]
    public void AParticipantWhoHasFinishedCannotHaveAnotherGoAtTheSameGame()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        CompetitiveRuleset ruleset = series.CurrentGame.Ruleset;

        Attempt patrick = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.SubmitResult(patrick.Id, VersusTestFixtures.PatrickId, VersusTestFixtures.Result(ruleset, 5), clock);

        Assert.That(series.CanIssueAttempt(VersusTestFixtures.PatrickId, out string reason), Is.False);
        Assert.That(reason, Does.Contain("already completed"));
        Assert.Throws<VersusDomainException>(
            () => series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock));
    }

    [Test]
    public void IssuingTwiceHandsBackTheSameOutstandingAttempt()
    {
        // A failed save, a double-tapped button, or the application dying between issuing and
        // loading the scene all come back here. None of them should mint a second turn.
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        Attempt first = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        Attempt second = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);

        Assert.That(second.Id, Is.EqualTo(first.Id));
    }

    [Test]
    public void AnAbandonedAttemptIsReplacedByAFreshOne()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        Attempt first = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        series.AbandonAttempt(first.Id);

        Attempt second = series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        Assert.That(second.State, Is.EqualTo(AttemptState.Ready));
    }

    [Test]
    public void ForfeitingHandsTheGameAndTheSeriesToTheOpponent()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);

        series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock);
        SeriesResult result = series.Forfeit(VersusTestFixtures.PatrickId, clock);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Forfeited));
        Assert.That(series.Games[0].Status, Is.EqualTo(VersusGameStatus.Forfeited));
        Assert.That(result.Kind, Is.EqualTo(SeriesOutcomeKind.Forfeit));
        Assert.That(result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId));
        Assert.That(series.Games[0].Result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId));
    }

    [Test]
    public void AForfeitedSeriesAcceptsNothingFurther()
    {
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);
        series.Forfeit(VersusTestFixtures.PatrickId, clock);

        Assert.Throws<VersusDomainException>(
            () => series.IssueAttempt(VersusTestFixtures.AlexId, ids, clock));
    }
}
