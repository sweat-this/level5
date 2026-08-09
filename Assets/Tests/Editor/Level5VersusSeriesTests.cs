using Level5.Core.Versus;
using NUnit.Framework;

/// <summary>
/// Best-of-N progression, and the rule that matters most: a series ends the moment it is decided.
///
/// The tests that check games are never activated are as important as the ones that check the
/// score. A best of seven that stops at 4-0 but still issues an attempt for game five has quietly
/// given somebody a turn in a competition that is already over.
/// </summary>
public class Level5VersusSeriesTests
{
    private FakeVersusClock clock;
    private SequentialVersusIdSource ids;

    [SetUp]
    public void SetUp()
    {
        clock = new FakeVersusClock();
        ids = new SequentialVersusIdSource();
    }

    // ---- formats ------------------------------------------------------------------------------

    [Test]
    public void OnlyOddSeriesLengthsUpToSevenExist()
    {
        Assert.That(SeriesFormat.BestOf1.RequiredWins, Is.EqualTo(1));
        Assert.That(SeriesFormat.BestOf3.RequiredWins, Is.EqualTo(2));
        Assert.That(SeriesFormat.BestOf5.RequiredWins, Is.EqualTo(3));
        Assert.That(SeriesFormat.BestOf7.RequiredWins, Is.EqualTo(4));

        Assert.Throws<VersusDomainException>(() => SeriesFormat.FromGameCount(2), "even cannot be settled by wins");
        Assert.Throws<VersusDomainException>(() => SeriesFormat.FromGameCount(4));
        Assert.Throws<VersusDomainException>(() => SeriesFormat.FromGameCount(9));
        Assert.Throws<VersusDomainException>(() => SeriesFormat.FromGameCount(0));
    }

    [Test]
    public void APlaylistMustBeExactlyAsLongAsTheFormat()
    {
        Assert.Throws<VersusDomainException>(
            () => new SeriesSnapshot(
                SeriesFormat.BestOf3,
                new[] { VersusTestFixtures.ScoreRuleset() },
                InformationPolicy.SealedAttempt),
            "a best of three with one game in its playlist");
    }

    // ---- best of 1 ----------------------------------------------------------------------------

    [Test]
    public void BestOf1IsSettledByTheFirstGame()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(series.Score.ToString(), Is.EqualTo("1-0"));
    }

    // ---- best of 3 ----------------------------------------------------------------------------

    [Test]
    public void BestOf3EndsAtTwoNilAndNeverActivatesGameThree()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);
        VersusTestFixtures.PlayGame(series, clock, ids, 52, 40);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Score.FirstWins, Is.EqualTo(2));
        Assert.That(series.Games[2].Status, Is.EqualTo(VersusGameStatus.Pending), "game three never started");
        Assert.That(series.CurrentGame, Is.Null);
    }

    [Test]
    public void NoAttemptCanBeIssuedForAGameTheSeriesNoLongerNeeds()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);
        VersusTestFixtures.PlayGame(series, clock, ids, 52, 40);

        Assert.That(series.CanIssueAttempt(VersusTestFixtures.PatrickId, out _), Is.False);
        Assert.Throws<VersusDomainException>(
            () => series.IssueAttempt(VersusTestFixtures.AlexId, ids, clock));

        // Nobody was ever handed a turn at game three, so neither participant has an attempt there.
        ParticipantGameView unplayed = series.ViewFor(VersusTestFixtures.PatrickId).Games[2];
        Assert.That(unplayed.OwnAttemptState, Is.EqualTo(AttemptState.Created));
        Assert.That(unplayed.OpponentAttemptState, Is.EqualTo(AttemptState.Created));
        Assert.That(unplayed.Status, Is.EqualTo(VersusGameStatus.Pending));
    }

    [Test]
    public void BestOf3GoesToThreeGamesWhenItIsOneAll()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);

        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);
        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Active));

        VersusTestFixtures.PlayGame(series, clock, ids, 20, 44);
        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Active), "one all, so it goes on");
        Assert.That(series.CurrentGame.Number, Is.EqualTo(3));

        VersusTestFixtures.PlayGame(series, clock, ids, 60, 12);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(series.Score.ToString(), Is.EqualTo("2-1"));
    }

    // ---- best of 5 ----------------------------------------------------------------------------

    [Test]
    public void BestOf5EndsAtThreeNil()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf5);

        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);

        AssertCompletedAfter(series, gamesPlayed: 3, winner: VersusTestFixtures.PatrickId, score: "3-0");
    }

    [Test]
    public void BestOf5EndsAtThreeOne()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf5);

        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);
        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);

        AssertCompletedAfter(series, gamesPlayed: 4, winner: VersusTestFixtures.PatrickId, score: "3-1");
    }

    [Test]
    public void BestOf5GoesTheDistanceAtThreeTwo()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf5);

        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);
        VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Active), "two all going into the last game");
        Assert.That(series.CurrentGame.Number, Is.EqualTo(5));

        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);

        Assert.That(series.Result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId));
        Assert.That(series.Score.ToString(), Is.EqualTo("2-3"));
    }

    // ---- best of 7 ----------------------------------------------------------------------------

    [Test]
    public void BestOf7EndsAtFourNilWithThreeGamesNeverActivated()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf7);

        for (int game = 0; game < 4; game++)
        {
            VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        }

        AssertCompletedAfter(series, gamesPlayed: 4, winner: VersusTestFixtures.PatrickId, score: "4-0");
        Assert.That(series.Games[4].Status, Is.EqualTo(VersusGameStatus.Pending));
        Assert.That(series.Games[5].Status, Is.EqualTo(VersusGameStatus.Pending));
        Assert.That(series.Games[6].Status, Is.EqualTo(VersusGameStatus.Pending));
    }

    [Test]
    public void BestOf7EndsAtFourOne()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf7);

        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);
        for (int game = 0; game < 4; game++)
        {
            VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        }

        AssertCompletedAfter(series, gamesPlayed: 5, winner: VersusTestFixtures.PatrickId, score: "4-1");
    }

    [Test]
    public void BestOf7EndsAtFourTwo()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf7);

        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);
        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);
        for (int game = 0; game < 4; game++)
        {
            VersusTestFixtures.PlayGame(series, clock, ids, 10, 1);
        }

        AssertCompletedAfter(series, gamesPlayed: 6, winner: VersusTestFixtures.PatrickId, score: "4-2");
    }

    [Test]
    public void ThreeAllActivatesGameSevenAndItsWinnerTakesTheSeries()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf7);

        // Alternate the first six games to reach three all.
        for (int game = 0; game < 6; game++)
        {
            bool patrickWins = game % 2 == 0;
            VersusTestFixtures.PlayGame(
                series,
                clock,
                ids,
                patrickWins ? 10 : 1,
                patrickWins ? 1 : 10);
        }

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Active));
        Assert.That(series.Score.ToString(), Is.EqualTo("3-3"));
        Assert.That(series.CurrentGame, Is.Not.Null);
        Assert.That(series.CurrentGame.Number, Is.EqualTo(7), "game seven is the decider");

        VersusTestFixtures.PlayGame(series, clock, ids, 1, 10);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Result.WinnerId, Is.EqualTo(VersusTestFixtures.AlexId));
        Assert.That(series.Score.ToString(), Is.EqualTo("3-4"));
    }

    // ---- draws --------------------------------------------------------------------------------

    [Test]
    public void ASeriesFullOfDrawsFinishesRatherThanWaitingForAWinThatCannotCome()
    {
        // Every game drawn in a best of three: nobody reaches two wins, so completion has to come
        // from the playlist running out. Without that, the series would sit active forever.
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);

        VersusTestFixtures.PlayGame(series, clock, ids, 20, 20);
        VersusTestFixtures.PlayGame(series, clock, ids, 20, 20);
        VersusTestFixtures.PlayGame(series, clock, ids, 20, 20);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Result.Kind, Is.EqualTo(SeriesOutcomeKind.Draw));
        Assert.That(series.Result.HasWinner, Is.False);
        Assert.That(series.Score.Draws, Is.EqualTo(3));
    }

    [Test]
    public void WithDrawsInItTheSeriesGoesToWhoeverWonMoreGames()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf3);

        VersusTestFixtures.PlayGame(series, clock, ids, 20, 20);
        VersusTestFixtures.PlayGame(series, clock, ids, 20, 20);
        VersusTestFixtures.PlayGame(series, clock, ids, 30, 10);

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(series.Score.ToString(), Is.EqualTo("1-0 (2 drawn)"));
    }

    // ---- invitations --------------------------------------------------------------------------

    [Test]
    public void AnInvitedSeriesPlaysNothingUntilItIsAccepted()
    {
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            initialStatus: SeriesStatus.Invited);

        Assert.That(series.CurrentGame, Is.Null);
        Assert.That(series.CanIssueAttempt(VersusTestFixtures.PatrickId, out _), Is.False);

        series.Accept();

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Active));
        Assert.That(series.CurrentGame.Number, Is.EqualTo(1));
    }

    [Test]
    public void ADeclinedSeriesIsOverAndCannotBeAccepted()
    {
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            initialStatus: SeriesStatus.Invited);

        series.Decline();

        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Declined));
        Assert.That(series.IsOver, Is.True);
        Assert.Throws<VersusDomainException>(() => series.Accept());
    }

    [Test]
    public void ACompletedSeriesRefusesEveryFurtherOperation()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf1);
        VersusTestFixtures.PlayGame(series, clock, ids, 47, 31);

        Assert.Throws<VersusDomainException>(() => series.IssueAttempt(VersusTestFixtures.PatrickId, ids, clock));
        Assert.Throws<VersusDomainException>(() => series.Forfeit(VersusTestFixtures.PatrickId, clock));
        Assert.Throws<VersusDomainException>(() => series.Accept());
    }

    // ---- turn order ---------------------------------------------------------------------------

    [Test]
    public void TheRightToAttemptFirstAlternatesBetweenGames()
    {
        VersusSeries series = VersusTestFixtures.Series(SeriesFormat.BestOf7);

        Assert.That(series.Games[0].FirstAttemptParticipantIndex, Is.EqualTo(0));
        Assert.That(series.Games[1].FirstAttemptParticipantIndex, Is.EqualTo(1));
        Assert.That(series.Games[2].FirstAttemptParticipantIndex, Is.EqualTo(0));
        Assert.That(series.Games[6].FirstAttemptParticipantIndex, Is.EqualTo(0));
    }

    [Test]
    public void ASeriesCanPinTheFirstAttemptToOneParticipant()
    {
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3,
            alternatesFirstAttempt: false);

        Assert.That(series.Games[0].FirstAttemptParticipantIndex, Is.EqualTo(0));
        Assert.That(series.Games[1].FirstAttemptParticipantIndex, Is.EqualTo(0));
        Assert.That(series.Games[2].FirstAttemptParticipantIndex, Is.EqualTo(0));
    }

    private static void AssertCompletedAfter(
        VersusSeries series,
        int gamesPlayed,
        ParticipantId winner,
        string score)
    {
        Assert.That(series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(series.Result.WinnerId, Is.EqualTo(winner));
        Assert.That(series.Score.ToString(), Is.EqualTo(score));
        Assert.That(series.CurrentGame, Is.Null);

        for (int index = gamesPlayed; index < series.Games.Count; index++)
        {
            Assert.That(
                series.Games[index].Status,
                Is.EqualTo(VersusGameStatus.Pending),
                $"game {index + 1} should never have been reached");
        }
    }
}
