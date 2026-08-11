using Level5.Core;
using NUnit.Framework;

/// <summary>
/// Characterization tests for what a made shot is worth.
///
/// Every expected value here was computed by hand from the original expressions in
/// <c>BasketBallShotMade.OnTriggerEnter</c> before that method was changed. They exist to prove the
/// extraction changed nothing, so a failure means scoring moved - not that a test needs updating.
/// </summary>
public class Level5ShotScoringTests
{
    // ---- open play ----------------------------------------------------------------------------

    [Test]
    public void OpenPlayScoresTheLine()
    {
        Assert.That(Score(OpenPlay(ShotKind.Two)).Points, Is.EqualTo(2));
        Assert.That(Score(OpenPlay(ShotKind.Three)).Points, Is.EqualTo(3));
        Assert.That(Score(OpenPlay(ShotKind.Four)).Points, Is.EqualTo(4));
        Assert.That(Score(OpenPlay(ShotKind.Seven)).Points, Is.EqualTo(7));
    }

    [Test]
    public void OpenPlayCountsTheShotAgainstItsOwnLine()
    {
        Assert.That(Score(OpenPlay(ShotKind.Three)).CountedAs, Is.EqualTo(ShotKind.Three));
        Assert.That(Score(OpenPlay(ShotKind.Seven)).CountedAs, Is.EqualTo(ShotKind.Seven));
    }

    [Test]
    public void TheStreakBonusRaisesEveryLineExceptTheTwo()
    {
        // In the Pocket: +1 on a three, +2 on a four, +3 on a seven, nothing on a two.
        Assert.That(Score(Streak(ShotKind.Two, streak: 5, threshold: 5)).Points, Is.EqualTo(2));
        Assert.That(Score(Streak(ShotKind.Three, streak: 5, threshold: 5)).Points, Is.EqualTo(4));
        Assert.That(Score(Streak(ShotKind.Four, streak: 5, threshold: 5)).Points, Is.EqualTo(6));
        Assert.That(Score(Streak(ShotKind.Seven, streak: 5, threshold: 5)).Points, Is.EqualTo(10));
    }

    [Test]
    public void TheStreakBonusStartsAtTheThresholdNotAfterIt()
    {
        Assert.That(Score(Streak(ShotKind.Three, streak: 4, threshold: 5)).Points, Is.EqualTo(3), "below");
        Assert.That(Score(Streak(ShotKind.Three, streak: 5, threshold: 5)).Points, Is.EqualTo(4), "at");
        Assert.That(Score(Streak(ShotKind.Three, streak: 9, threshold: 5)).Points, Is.EqualTo(4), "above");
    }

    [Test]
    public void AStreakPaysNothingExtraInAModeWithoutTheBonus()
    {
        ShotScoringInput input = OpenPlay(ShotKind.Seven);
        input.ConsecutiveShotsMade = 99;
        input.StreakBonusThreshold = 5;
        input.HasStreakBonus = false;

        Assert.That(Score(input).Points, Is.EqualTo(7));
    }

    // ---- marker contests ----------------------------------------------------------------------

    [Test]
    public void AContestShotOnAnEnabledMarkerScoresItsLine()
    {
        Assert.That(Score(Marker(ShotKind.Three, onEnabledMarker: true)).Points, Is.EqualTo(3));
        Assert.That(Score(Marker(ShotKind.Four, onEnabledMarker: true)).Points, Is.EqualTo(4));
        Assert.That(Score(Marker(ShotKind.Seven, onEnabledMarker: true)).Points, Is.EqualTo(7));
    }

    [Test]
    public void AContestShotOffTheMarkerScoresNothingAndCountsAsNothing()
    {
        // The made-shot counter deliberately does not move either - which is why a stray shot
        // during a contest cannot inflate the shooting percentage.
        ShotScore score = Score(Marker(ShotKind.Three, onEnabledMarker: false));

        Assert.That(score.Points, Is.EqualTo(0));
        Assert.That(score.CountedAs, Is.EqualTo(ShotKind.None));
    }

    [Test]
    public void TheFinalMarkerShotScoresDoubleInTheContestsThatSaySo()
    {
        ShotScoringInput input = Marker(ShotKind.Four, onEnabledMarker: true);
        input.IsFinalMarkerAttempt = true;
        input.MarkerFinalShotScoresDouble = true;

        ShotScore score = Score(input);

        Assert.That(score.Points, Is.EqualTo(8));
        Assert.That(score.MoneyBallMade, Is.EqualTo(1), "the doubled shot is itself a money ball");
    }

    [Test]
    public void TheFinalMarkerShotIsNotDoubledInAContestThatDoesNotSaySo()
    {
        // The all-point contest reaches the marker branch but is not in the doubling list.
        ShotScoringInput input = Marker(ShotKind.Four, onEnabledMarker: true);
        input.IsFinalMarkerAttempt = true;
        input.MarkerFinalShotScoresDouble = false;

        ShotScore score = Score(input);

        Assert.That(score.Points, Is.EqualTo(4));
        Assert.That(score.MoneyBallMade, Is.EqualTo(0));
    }

    // ---- points by distance -------------------------------------------------------------------

    [Test]
    public void DistanceScoringIgnoresTheLineAndFloorsSixTenthsOfTheDistance()
    {
        Assert.That(Score(Distance(ShotKind.Two, 10f)).Points, Is.EqualTo(6));
        Assert.That(Score(Distance(ShotKind.Seven, 10f)).Points, Is.EqualTo(6), "the line does not matter");
        Assert.That(Score(Distance(ShotKind.Three, 25f)).Points, Is.EqualTo(15));
        Assert.That(Score(Distance(ShotKind.Three, 0f)).Points, Is.EqualTo(0));
    }

    [Test]
    public void DistanceScoringFloorsRatherThanRounds()
    {
        // 9 * 6 / 10 = 5.4 -> 5, not 5.4 and not 6.
        Assert.That(Score(Distance(ShotKind.Three, 9f)).Points, Is.EqualTo(5));
        Assert.That(Score(Distance(ShotKind.Three, 16.6f)).Points, Is.EqualTo(9), "9.96 floors to 9");
    }

    [Test]
    public void DistanceScoringStillMovesTheLinesMadeCounter()
    {
        Assert.That(Score(Distance(ShotKind.Four, 20f)).CountedAs, Is.EqualTo(ShotKind.Four));
    }

    [Test]
    public void MadeShotResultCarriesDistanceInSceneAndFeetUnits()
    {
        ShotScore score = new ShotScore(15, ShotKind.Three, moneyBallMade: 0);
        MadeShotResult result = new MadeShotResult(
            playerId: 2,
            isCpu: true,
            kind: ShotKind.Three,
            score: score,
            shotDistance: 25f,
            totalPointsAfter: 42);

        Assert.That(result.PlayerId, Is.EqualTo(2));
        Assert.That(result.IsCpu, Is.True);
        Assert.That(result.Kind, Is.EqualTo(ShotKind.Three));
        Assert.That(result.Score, Is.EqualTo(score));
        Assert.That(result.ShotDistance, Is.EqualTo(25f));
        Assert.That(result.ShotDistanceFeet, Is.EqualTo(150f));
        Assert.That(result.TotalPointsAfter, Is.EqualTo(42));
    }

    // ---- money ball ---------------------------------------------------------------------------

    [Test]
    public void AnActiveMoneyBallCreditsOne()
    {
        ShotScoringInput input = OpenPlay(ShotKind.Three);
        input.MoneyBallActive = true;

        Assert.That(Score(input).MoneyBallMade, Is.EqualTo(1));
    }

    [Test]
    public void AFinalMarkerShotTakenWithTheMoneyBallActiveCreditsTwo()
    {
        // Preserved oddity, pinned deliberately. The original credited a money ball once for the
        // doubled marker shot and again for the active money ball, with no check that they were the
        // same shot. Named for what it does, not for what anyone intended, so that changing it later
        // is a visible decision rather than a silent one.
        ShotScoringInput input = Marker(ShotKind.Four, onEnabledMarker: true);
        input.IsFinalMarkerAttempt = true;
        input.MarkerFinalShotScoresDouble = true;
        input.MoneyBallActive = true;

        Assert.That(Score(input).MoneyBallMade, Is.EqualTo(2));
    }

    [Test]
    public void NoMoneyBallCreditWhenNeitherConditionHolds()
    {
        Assert.That(Score(OpenPlay(ShotKind.Seven)).MoneyBallMade, Is.EqualTo(0));
        Assert.That(Score(Marker(ShotKind.Seven, onEnabledMarker: true)).MoneyBallMade, Is.EqualTo(0));
    }

    // ---- builders -----------------------------------------------------------------------------

    private static ShotScore Score(ShotScoringInput input)
    {
        return ShotScoring.Score(input);
    }

    private static ShotScoringInput OpenPlay(ShotKind kind)
    {
        return new ShotScoringInput { Kind = kind };
    }

    private static ShotScoringInput Streak(ShotKind kind, int streak, int threshold)
    {
        return new ShotScoringInput
        {
            Kind = kind,
            HasStreakBonus = true,
            ConsecutiveShotsMade = streak,
            StreakBonusThreshold = threshold
        };
    }

    private static ShotScoringInput Marker(ShotKind kind, bool onEnabledMarker)
    {
        return new ShotScoringInput
        {
            Kind = kind,
            IsMarkerContest = true,
            OnEnabledMarker = onEnabledMarker
        };
    }

    private static ShotScoringInput Distance(ShotKind kind, float distance)
    {
        return new ShotScoringInput
        {
            Kind = kind,
            ScoresByDistance = true,
            ShotDistance = distance
        };
    }
}
