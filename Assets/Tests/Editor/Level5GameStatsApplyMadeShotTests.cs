using Level5.Core;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-065: <see cref="GameStats.ApplyMadeShot"/> is the extracted, testable form of what used to be
/// inline in <c>BasketBallShotMade.updateShotMadeBasketBallStats</c>. These tests exist because the
/// bug was entirely about call order - the pure <see cref="ShotScoring"/> arithmetic was already
/// correct and already tested (<see cref="Level5ShotScoringTests"/>) - so what needs pinning down is
/// that <see cref="GameStats.calculateConsecutiveShot"/> runs after the made-shot counter is
/// finalized but before the final score is computed, using state only <see cref="GameStats"/> and
/// <see cref="BasketBallState"/> own. No <c>GameRules</c>/<c>MatchRuntime</c> singleton needed.
/// </summary>
public class Level5GameStatsApplyMadeShotTests
{
    private GameObject statsObject;
    private GameObject stateObject;
    private GameStats stats;
    private BasketBallState state;

    [SetUp]
    public void SetUp()
    {
        statsObject = new GameObject("stats");
        stats = statsObject.AddComponent<GameStats>();
        stateObject = new GameObject("basketball-state");
        state = stateObject.AddComponent<BasketBallState>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(statsObject);
        Object.DestroyImmediate(stateObject);
    }

    /// <summary>Simulates one attempt-then-make: ShotAttempt increments at "launch", exactly like
    /// BasketBall.shootBasketBall does before ApplyMadeShot ever runs for that shot.</summary>
    private ShotScore MakeThree(bool hasStreakBonus, int threshold)
    {
        state.TwoAttempt = false;
        state.ThreeAttempt = true;
        stats.ShotAttempt++;

        return stats.ApplyMadeShot(state, new ShotScoringInput
        {
            Kind = ShotKind.Three,
            HasStreakBonus = hasStreakBonus,
            StreakBonusThreshold = threshold
        });
    }

    private ShotScore MakeTwo()
    {
        state.TwoAttempt = true;
        state.ThreeAttempt = false;
        stats.ShotAttempt++;

        return stats.ApplyMadeShot(state, new ShotScoringInput
        {
            Kind = ShotKind.Two,
            HasStreakBonus = true,
            StreakBonusThreshold = 3
        });
    }

    [Test]
    public void StreakBonusAppliesOnTheThresholdShotItselfNotOneLate()
    {
        // Before the AUD-065 fix, ConsecutiveShotsMade was read before this shot's own increment, so
        // the third consecutive three scored as if the streak were still 2 - the bonus only ever
        // landed on the fourth shot. Asserting all four in sequence pins the off-by-one directly.
        Assert.That(MakeThree(hasStreakBonus: true, threshold: 3).Points, Is.EqualTo(3), "1st: no bonus yet");
        Assert.That(MakeThree(hasStreakBonus: true, threshold: 3).Points, Is.EqualTo(3), "2nd: no bonus yet");
        Assert.That(MakeThree(hasStreakBonus: true, threshold: 3).Points, Is.EqualTo(4), "3rd: bonus starts here, not the 4th");
        Assert.That(MakeThree(hasStreakBonus: true, threshold: 3).Points, Is.EqualTo(4), "4th: bonus continues");
    }

    [Test]
    public void MadeShotCounterAdvancesByOnePerShotAlongsideTheStreak()
    {
        MakeThree(hasStreakBonus: true, threshold: 3);
        MakeThree(hasStreakBonus: true, threshold: 3);

        Assert.That(stats.ShotMade, Is.EqualTo(2));
        Assert.That(stats.ThreePointerMade, Is.EqualTo(2));
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(2));
    }

    [Test]
    public void MadeTwoPointerBreaksTheStreakInsteadOfExtendingIt()
    {
        // Before the AUD-065 fix, calculateConsecutiveShot ran after BasketballState.ResetShotAttemptSnapshot
        // had already cleared TwoAttempt back to false, so this check could never see a two-pointer as
        // one - the streak would have kept extending through it instead of resetting.
        MakeThree(hasStreakBonus: true, threshold: 3);
        MakeThree(hasStreakBonus: true, threshold: 3);
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(2), "two in a row before the break");

        MakeTwo();
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(1), "the made two-pointer resets the streak");

        MakeThree(hasStreakBonus: true, threshold: 3);
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(2), "counting resumes from the two-pointer, not from before it");
    }
}
