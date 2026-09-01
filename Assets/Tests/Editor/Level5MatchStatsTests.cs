using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// <see cref="MatchStats"/> is the owner half of phase 1b: the match-stats state and the arithmetic
/// that maintains it, moved off the <c>GameStats</c> MonoBehaviour into <c>Level5.Core</c>.
///
/// These were originally translated from the now-retired <c>Level5GameStatsApplyMadeShotTests</c>,
/// which exercised the same three cases through the temporary <c>GameStats.ApplyMadeShot(BasketBallState,
/// ...)</c> seam. That seam is gone as of AUD-010 Phase 1c - the live made-shot path now calls
/// <see cref="MatchStats.ApplyMadeShot"/> directly - so this suite is this behaviour's sole owner now,
/// not a translated duplicate of a suite that still runs elsewhere.
///
/// **Invariant: this file constructs no <c>GameObject</c>.** The original needed two of them and an
/// <c>AddComponent</c> per test to exercise pure counter arithmetic. Needing a scene object to check
/// a counter is the signal that ownership has regressed, so it is asserted structurally in
/// <see cref="TheOwnerIsNotAUnityObjectAtAll"/> rather than left as a convention.
/// </summary>
public class Level5MatchStatsTests
{
    private MatchStats stats;

    [SetUp]
    public void SetUp()
    {
        stats = new MatchStats();
    }

    /// <summary>Simulates one attempt-then-make: ShotAttempt increments at "launch", exactly like
    /// BasketBall.shootBasketBall does before ApplyMadeShot ever runs for that shot.</summary>
    private ShotScore MakeThree(bool hasStreakBonus, int threshold)
    {
        stats.ShotAttempt++;

        return stats.ApplyMadeShot(false, new ShotScoringInput
        {
            Kind = ShotKind.Three,
            HasStreakBonus = hasStreakBonus,
            StreakBonusThreshold = threshold
        });
    }

    private ShotScore MakeTwo()
    {
        stats.ShotAttempt++;

        return stats.ApplyMadeShot(true, new ShotScoringInput
        {
            Kind = ShotKind.Two,
            HasStreakBonus = true,
            StreakBonusThreshold = 3
        });
    }

    /// <summary>A miss is an attempt that never reaches ApplyMadeShot - that asymmetry is the whole
    /// mechanism the predictive streak tracker relies on.</summary>
    private void Miss()
    {
        stats.ShotAttempt++;
    }

    [Test]
    public void TheOwnerIsNotAUnityObjectAtAll()
    {
        Assert.That(typeof(MatchStats).IsSubclassOf(typeof(UnityEngine.Object)), Is.False,
            "MatchStats must stay constructible without a scene - that is the point of phase 1b");
    }

    [Test]
    public void StreakBonusAppliesOnTheThresholdShotItselfNotOneLate()
    {
        // AUD-065: ConsecutiveShotsMade used to be read before this shot's own increment, so the
        // third consecutive three scored as if the streak were still 2 - the bonus only ever landed
        // on the fourth shot. Asserting all four in sequence pins the off-by-one directly.
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
        // AUD-065: calculateConsecutiveShot used to run after BasketballState.ResetShotAttemptSnapshot
        // had already cleared TwoAttempt back to false, so this check could never see a two-pointer as
        // one - the streak would have kept extending through it instead of resetting. The bool
        // parameter is that snapshot, which is why it must be passed as of launch.
        MakeThree(hasStreakBonus: true, threshold: 3);
        MakeThree(hasStreakBonus: true, threshold: 3);
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(2), "two in a row before the break");

        MakeTwo();
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(1), "the made two-pointer resets the streak");

        MakeThree(hasStreakBonus: true, threshold: 3);
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(2), "counting resumes from the two-pointer, not from before it");
    }

    [Test]
    public void MissBreaksTheStreakBecauseAttemptsMovedWithoutMakes()
    {
        // The tracker never sees the miss. It infers it: attempts advanced by two since the last
        // make while makes advanced by one, so the prediction fails and the streak restarts.
        MakeThree(hasStreakBonus: true, threshold: 3);
        MakeThree(hasStreakBonus: true, threshold: 3);
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(2));

        Miss();
        MakeThree(hasStreakBonus: true, threshold: 3);

        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(1), "the miss restarted the streak");
        Assert.That(stats.ShotMade, Is.EqualTo(3), "the miss did not touch the made counter");
        Assert.That(stats.ShotAttempt, Is.EqualTo(4), "but it did count as an attempt");
    }

    [Test]
    public void BrokenStreakResetsToOneNotZero()
    {
        // Preserved oddity, named for the behaviour rather than the intent. Both branches of
        // CalculateConsecutiveShot set the same _expected* values, so they differ only in this
        // reset - which means a broken streak is immediately "1 in a row" rather than "none".
        // It looks like a bug. Changing it is a scoring decision, not a refactoring one.
        Miss();
        MakeThree(hasStreakBonus: false, threshold: 3);

        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(1));
    }

    [Test]
    public void MostConsecutiveShotsKeepsTheHighWaterMarkAcrossABreak()
    {
        MakeThree(hasStreakBonus: false, threshold: 3);
        MakeThree(hasStreakBonus: false, threshold: 3);
        MakeThree(hasStreakBonus: false, threshold: 3);
        Assert.That(stats.MostConsecutiveShots, Is.EqualTo(3));

        Miss();
        MakeThree(hasStreakBonus: false, threshold: 3);

        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(1), "current streak restarted");
        Assert.That(stats.MostConsecutiveShots, Is.EqualTo(3), "but the best is remembered");
    }

    [Test]
    public void TotalPointAccuracyIsZeroBeforeAnyAttempt()
    {
        Assert.That(stats.TotalPointAccuracy, Is.EqualTo(0f));
    }

    [Test]
    public void TotalPointAccuracyIsMadeOverAttemptedAsAPercentage()
    {
        stats.ShotAttempt = 4;
        stats.ShotMade = 3;

        Assert.That(stats.TotalPointAccuracy, Is.EqualTo(75f).Within(0.0001f));
    }

    // ---------------------------------------------------------------------------------------
    // Accumulate - the contract from PlayerData.updateCampaignStats, enumerated per rule.
    // ---------------------------------------------------------------------------------------

    /// <summary>Every field set to a distinct non-zero value, so a rule applied to the wrong field
    /// shows up as a wrong number rather than coincidentally matching.</summary>
    private static MatchStats Populated()
    {
        return new MatchStats
        {
            TotalPoints = 11,
            TotalDistance = 12f,
            ThreePointerMade = 13,
            FourPointerMade = 14,
            SevenPointerMade = 15,
            ThreePointerAttempts = 16,
            FourPointerAttempts = 17,
            SevenPointerAttempts = 18,
            TimePlayed = 19f,
            CriticalRolled = 20,
            EnemiesKilled = 21,
            BossKilled = 22,
            MinionsKilled = 23,
            MoneyBallMade = 24,
            MoneyBallAttempts = 25,
            ShotMade = 26,
            ShotAttempt = 27,
            SniperHits = 28,
            SniperShots = 29,
            TwoPointerMade = 30,
            TwoPointerAttempts = 31,

            LongestShotMade = 32f,
            MostConsecutiveShots = 33,

            ExperienceGained = 34,
            BonusPoints = 35,
            BlockedShots = 36,
            MakeThreePointersLowTime = 37f,
            MakeFourPointersLowTime = 38f,
            MakeAllPointersLowTime = 39f,
            MakeThreePointersMoneyBallLowTime = 40f,
            MakeFourPointersMoneyBallLowTime = 41f,
            MakeAllPointersMoneyBallLowTime = 42f,

            CampaignWins = 43,
            CampaignLosses = 44,
            CampaignTies = 45,
            CampaignGamesPlayed = 46
        };
    }

    [Test]
    public void AccumulateOntoAnEmptyCampaignCopiesTheSummedFields()
    {
        MatchStats campaign = new MatchStats();

        campaign.Accumulate(Populated());

        Assert.That(campaign.TotalPoints, Is.EqualTo(11));
        Assert.That(campaign.TotalDistance, Is.EqualTo(12f));
        Assert.That(campaign.ThreePointerMade, Is.EqualTo(13));
        Assert.That(campaign.FourPointerMade, Is.EqualTo(14));
        Assert.That(campaign.SevenPointerMade, Is.EqualTo(15));
        Assert.That(campaign.ThreePointerAttempts, Is.EqualTo(16));
        Assert.That(campaign.FourPointerAttempts, Is.EqualTo(17));
        Assert.That(campaign.SevenPointerAttempts, Is.EqualTo(18));
        Assert.That(campaign.TimePlayed, Is.EqualTo(19f));
        Assert.That(campaign.CriticalRolled, Is.EqualTo(20));
        Assert.That(campaign.EnemiesKilled, Is.EqualTo(21));
        Assert.That(campaign.BossKilled, Is.EqualTo(22));
        Assert.That(campaign.MinionsKilled, Is.EqualTo(23));
        Assert.That(campaign.MoneyBallMade, Is.EqualTo(24));
        Assert.That(campaign.MoneyBallAttempts, Is.EqualTo(25));
        Assert.That(campaign.ShotMade, Is.EqualTo(26));
        Assert.That(campaign.ShotAttempt, Is.EqualTo(27));
        Assert.That(campaign.SniperHits, Is.EqualTo(28));
        Assert.That(campaign.SniperShots, Is.EqualTo(29));
        Assert.That(campaign.TwoPointerMade, Is.EqualTo(30));
        Assert.That(campaign.TwoPointerAttempts, Is.EqualTo(31));
    }

    [Test]
    public void AccumulateAddsOntoAnExistingCampaignRatherThanReplacingIt()
    {
        MatchStats campaign = Populated();

        campaign.Accumulate(Populated());

        Assert.That(campaign.TotalPoints, Is.EqualTo(22));
        Assert.That(campaign.TotalDistance, Is.EqualTo(24f));
        Assert.That(campaign.ShotMade, Is.EqualTo(52));
        Assert.That(campaign.ShotAttempt, Is.EqualTo(54));
        Assert.That(campaign.SniperShots, Is.EqualTo(58));
        Assert.That(campaign.TwoPointerAttempts, Is.EqualTo(62));
    }

    [Test]
    public void ThreeSequentialSessionsAccumulateCumulatively()
    {
        MatchStats campaign = new MatchStats();

        campaign.Accumulate(Populated());
        campaign.Accumulate(Populated());
        campaign.Accumulate(Populated());

        Assert.That(campaign.TotalPoints, Is.EqualTo(33));
        Assert.That(campaign.ShotAttempt, Is.EqualTo(81));
    }

    [Test]
    public void CareerBestsTakeTheMaximumWhenTheSessionIsHigher()
    {
        MatchStats campaign = new MatchStats { LongestShotMade = 10f, MostConsecutiveShots = 4 };

        campaign.Accumulate(new MatchStats { LongestShotMade = 25f, MostConsecutiveShots = 9 });

        Assert.That(campaign.LongestShotMade, Is.EqualTo(25f), "a longer shot replaces the record");
        Assert.That(campaign.MostConsecutiveShots, Is.EqualTo(9));
    }

    [Test]
    public void CareerBestsAreKeptWhenTheSessionIsLower()
    {
        MatchStats campaign = new MatchStats { LongestShotMade = 25f, MostConsecutiveShots = 9 };

        campaign.Accumulate(new MatchStats { LongestShotMade = 10f, MostConsecutiveShots = 4 });

        Assert.That(campaign.LongestShotMade, Is.EqualTo(25f), "a worse session must not lower the record");
        Assert.That(campaign.MostConsecutiveShots, Is.EqualTo(9));
    }

    [Test]
    public void CareerBestsAreNotSummed()
    {
        // The regression this guards: "max two, sum the rest" is easy to write as "sum everything",
        // and 10 + 25 = 35 would look plausible in a career-stats screen.
        MatchStats campaign = new MatchStats { LongestShotMade = 10f, MostConsecutiveShots = 4 };

        campaign.Accumulate(new MatchStats { LongestShotMade = 25f, MostConsecutiveShots = 9 });

        Assert.That(campaign.LongestShotMade, Is.Not.EqualTo(35f));
        Assert.That(campaign.MostConsecutiveShots, Is.Not.EqualTo(13));
    }

    [Test]
    public void FieldsTheOriginalNeverAccumulatedAreStillNotAccumulated()
    {
        // Preserved omission, not an oversight in the port. PlayerData.updateCampaignStats listed 23
        // fields and silently skipped these nine. Summing the low-time fields would be meaningless -
        // they are best-times - and whether experience and bonus points belong in a campaign total is
        // a scoring question this slice does not answer. Pinned so that answering it later is visible.
        MatchStats campaign = new MatchStats();

        campaign.Accumulate(Populated());

        Assert.That(campaign.ExperienceGained, Is.EqualTo(0));
        Assert.That(campaign.BonusPoints, Is.EqualTo(0));
        Assert.That(campaign.BlockedShots, Is.EqualTo(0));
        Assert.That(campaign.MakeThreePointersLowTime, Is.EqualTo(0f));
        Assert.That(campaign.MakeFourPointersLowTime, Is.EqualTo(0f));
        Assert.That(campaign.MakeAllPointersLowTime, Is.EqualTo(0f));
        Assert.That(campaign.MakeThreePointersMoneyBallLowTime, Is.EqualTo(0f));
        Assert.That(campaign.MakeFourPointersMoneyBallLowTime, Is.EqualTo(0f));
        Assert.That(campaign.MakeAllPointersMoneyBallLowTime, Is.EqualTo(0f));
    }

    [Test]
    public void CampaignTalliesBelongToTheAccumulatorAndAreNotFoldedIn()
    {
        // EndRoundMenuManager increments these directly on the campaign record. A session's copy is
        // meaningless, so folding it in would double-count every round played.
        MatchStats campaign = new MatchStats
        {
            CampaignWins = 2,
            CampaignLosses = 1,
            CampaignTies = 0,
            CampaignGamesPlayed = 3
        };

        campaign.Accumulate(Populated());

        Assert.That(campaign.CampaignWins, Is.EqualTo(2));
        Assert.That(campaign.CampaignLosses, Is.EqualTo(1));
        Assert.That(campaign.CampaignTies, Is.EqualTo(0));
        Assert.That(campaign.CampaignGamesPlayed, Is.EqualTo(3));
    }

    [Test]
    public void AccumulatingAnEmptySessionChangesNothing()
    {
        MatchStats campaign = Populated();

        campaign.Accumulate(new MatchStats());

        Assert.That(campaign.TotalPoints, Is.EqualTo(11));
        Assert.That(campaign.LongestShotMade, Is.EqualTo(32f));
        Assert.That(campaign.MostConsecutiveShots, Is.EqualTo(33));
    }

    [Test]
    public void AccumulatingNullIsIgnoredRatherThanThrowing()
    {
        MatchStats campaign = Populated();

        Assert.DoesNotThrow(() => campaign.Accumulate(null));
        Assert.That(campaign.TotalPoints, Is.EqualTo(11));
    }

    [Test]
    public void AccumulateDoesNotMutateTheSession()
    {
        MatchStats campaign = Populated();
        MatchStats session = Populated();

        campaign.Accumulate(session);

        Assert.That(session.TotalPoints, Is.EqualTo(11), "the finished session is a read-only input");
        Assert.That(session.ShotAttempt, Is.EqualTo(27));
    }
}
