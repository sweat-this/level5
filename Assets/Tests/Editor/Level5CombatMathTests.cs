using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Regression cover for the progression and randomness bugs found in the 2026-08-06 deep audit
/// (AUD-022, AUD-026). Both were pure functions that no test exercised.
/// </summary>
public class Level5CombatMathTests
{
    // MatchExperienceInput defaults DifficultySelected to 0, which is "easy" and halves the
    // award. Tests asserting exact totals opt into normal difficulty explicitly.
    private const int NormalDifficulty = 1;

    // ---------- AUD-026: percentage rolls ----------

    [Test]
    public void ZeroChanceNeverSucceeds()
    {
        // the old form was Random.Range(0, 100) <= chance, so a 0 chance fired on a roll of 0
        Assert.That(PercentChance.Succeeds(0f, 0f), Is.False);
        Assert.That(PercentChance.Succeeds(0f, 0.5f), Is.False);
        Assert.That(PercentChance.Succeeds(0f, 1f), Is.False);
    }

    [Test]
    public void FullChanceAlwaysSucceeds()
    {
        Assert.That(PercentChance.Succeeds(100f, 0f), Is.True);
        Assert.That(PercentChance.Succeeds(100f, 0.999f), Is.True);
        Assert.That(PercentChance.Succeeds(100f, 1f), Is.True);
    }

    [Test]
    public void NinetyNinePercentIsNotCertain()
    {
        // Random.Range(1, 100) is max-exclusive, so the old form made 99 a guaranteed success
        Assert.That(PercentChance.Succeeds(99f, 0.995f), Is.False);
        Assert.That(PercentChance.Succeeds(99f, 0.98f), Is.True);
    }

    [Test]
    public void RollIsComparedAgainstItsOwnPercentage()
    {
        Assert.That(PercentChance.Succeeds(25f, 0.24f), Is.True);
        Assert.That(PercentChance.Succeeds(25f, 0.25f), Is.False);
        Assert.That(PercentChance.Succeeds(25f, 0.26f), Is.False);
    }

    [Test]
    public void ChanceOutsideZeroToOneHundredIsClampedByTheEndpoints()
    {
        Assert.That(PercentChance.Succeeds(-10f, 0f), Is.False);
        Assert.That(PercentChance.Succeeds(150f, 1f), Is.True);
    }

    // ---------- AUD-022: sniper evasion bonus ----------

    [Test]
    public void NoSniperFireAwardsNoEvasionBonus()
    {
        // this was the bug: getPercentageFloat returns 0-100, and 1 - 0 = 1 handed every
        // match the full bonus even in modes with no sniper at all
        Assert.That(MatchExperience.SniperEvasionBonus(0, 0), Is.EqualTo(0));
    }

    [Test]
    public void PerfectEvasionAwardsTheFullBonus()
    {
        Assert.That(
            MatchExperience.SniperEvasionBonus(0, 12),
            Is.EqualTo(MatchExperience.MaxSniperEvasionBonus));
    }

    [Test]
    public void BeingHitByEverySniperShotAwardsNothing()
    {
        Assert.That(MatchExperience.SniperEvasionBonus(12, 12), Is.EqualTo(0));
    }

    [Test]
    public void EvasionBonusScalesWithTheShareOfShotsDodged()
    {
        // the old code could only ever pay 500 or 0 - never anything between
        Assert.That(MatchExperience.SniperEvasionBonus(3, 10), Is.EqualTo(350));
        Assert.That(MatchExperience.SniperEvasionBonus(5, 10), Is.EqualTo(250));
        Assert.That(MatchExperience.SniperEvasionBonus(7, 10), Is.EqualTo(150));
    }

    [Test]
    public void MoreHitsThanShotsIsClampedRatherThanGoingNegative()
    {
        Assert.That(MatchExperience.SniperEvasionBonus(20, 10), Is.EqualTo(0));
    }

    // ---------- AUD-022: the whole award ----------

    [Test]
    public void AMatchWithNoSniperGetsNoHiddenBonus()
    {
        MatchExperienceInput input = new MatchExperienceInput
        {
            ShotAttempts = 10,
            ThreePointerMade = 4,
            TotalPoints = 12,
            DifficultySelected = NormalDifficulty
        };

        // 10 attempts * 10 + 4 threes * 30 + 12 points, and nothing else
        Assert.That(MatchExperience.Calculate(input), Is.EqualTo(100 + 120 + 12));
    }

    [Test]
    public void ArcadeModeAwardsNothingRegardlessOfPerformance()
    {
        MatchExperienceInput input = new MatchExperienceInput
        {
            ShotAttempts = 50,
            SevenPointerMade = 10,
            TotalPoints = 400,
            ArcadeMode = true
        };

        Assert.That(MatchExperience.Calculate(input), Is.EqualTo(0));
    }

    [Test]
    public void EasyDifficultyHalvesTheAwardAndHardRaisesIt()
    {
        MatchExperienceInput input = new MatchExperienceInput
        {
            ShotAttempts = 10,
            DifficultySelected = NormalDifficulty
        };

        int normal = MatchExperience.Calculate(input);

        input.DifficultySelected = 0;
        Assert.That(MatchExperience.Calculate(input), Is.EqualTo(normal / 2));

        input.DifficultySelected = 2;
        Assert.That(MatchExperience.Calculate(input), Is.GreaterThan(normal));
    }

    [Test]
    public void EnemyKillsOnlyCountWhenEnemiesAreEnabled()
    {
        MatchExperienceInput input = new MatchExperienceInput
        {
            ShotAttempts = 10,
            MinionsKilled = 3,
            BossKilled = 1,
            DifficultySelected = NormalDifficulty
        };

        int withoutEnemies = MatchExperience.Calculate(input);

        input.EnemiesEnabled = true;
        Assert.That(MatchExperience.Calculate(input), Is.GreaterThan(withoutEnemies));
    }

    [Test]
    public void ModeMultipliersCompound()
    {
        MatchExperienceInput baseline = new MatchExperienceInput
        {
            ShotAttempts = 100,
            DifficultySelected = NormalDifficulty
        };
        MatchExperienceInput hardcore = new MatchExperienceInput
        {
            ShotAttempts = 100,
            HardcoreEnabled = true,
            DifficultySelected = NormalDifficulty
        };
        MatchExperienceInput everything = new MatchExperienceInput
        {
            ShotAttempts = 100,
            HardcoreEnabled = true,
            TrafficEnabled = true,
            SniperEnabled = true,
            DifficultySelected = NormalDifficulty
        };

        int plain = MatchExperience.Calculate(baseline);
        Assert.That(MatchExperience.Calculate(hardcore), Is.GreaterThan(plain));
        Assert.That(MatchExperience.Calculate(everything), Is.GreaterThan(MatchExperience.Calculate(hardcore)));
    }

    // ---------- AUD-036: experience-to-level curve ----------

    [Test]
    public void LevelIsExperienceDividedByTheCurveConstant()
    {
        Assert.That(CharacterLevel.FromExperience(0), Is.EqualTo(0));
        Assert.That(CharacterLevel.FromExperience(CharacterLevel.ExperiencePerLevel - 1), Is.EqualTo(0));
        Assert.That(CharacterLevel.FromExperience(CharacterLevel.ExperiencePerLevel), Is.EqualTo(1));
        Assert.That(CharacterLevel.FromExperience(CharacterLevel.ExperiencePerLevel * 7), Is.EqualTo(7));
    }

    [Test]
    public void NegativeExperienceNeverProducesANegativeLevel()
    {
        Assert.That(CharacterLevel.FromExperience(-1), Is.EqualTo(0));
        Assert.That(CharacterLevel.FromExperience(-5000f), Is.EqualTo(0));
    }

    [Test]
    public void FloatAndIntCurvesAgree()
    {
        // DBHelper accumulates in float when applying a match award; the menus use int
        for (int experience = 0; experience < CharacterLevel.ExperiencePerLevel * 4; experience += 617)
        {
            Assert.That(
                CharacterLevel.FromExperience((float)experience),
                Is.EqualTo(CharacterLevel.FromExperience(experience)),
                "curves disagree at " + experience);
        }
    }

    [Test]
    public void ExperienceToNextLevelCountsDownAndNeverReadsZero()
    {
        Assert.That(
            CharacterLevel.ExperienceToNextLevel(0),
            Is.EqualTo(CharacterLevel.ExperiencePerLevel));
        Assert.That(CharacterLevel.ExperienceToNextLevel(2999), Is.EqualTo(1));

        // exactly on a boundary shows a full level remaining, not 0
        Assert.That(
            CharacterLevel.ExperienceToNextLevel(CharacterLevel.ExperiencePerLevel),
            Is.EqualTo(CharacterLevel.ExperiencePerLevel));
    }

    // ---------- AUD-034: match clock ----------

    [Test]
    public void ModesWithoutACustomTimerUseTheDefaultMatchLength()
    {
        Assert.That(MatchClock.StartSeconds(0f), Is.EqualTo(MatchClock.DefaultMatchSeconds));
        Assert.That(MatchClock.StartSeconds(-30f), Is.EqualTo(MatchClock.DefaultMatchSeconds));
    }

    [Test]
    public void ACustomTimerWinsWhenTheModeSetsOne()
    {
        Assert.That(MatchClock.StartSeconds(60f), Is.EqualTo(60f));
    }

    [Test]
    public void TheMatchClockNeverStartsAtZero()
    {
        // a zero-length clock ends the match on the first frame, which is what the old
        // contest-mode branch in Timer.Start could produce
        foreach (float customTimer in new[] { -1f, 0f, 1f, 45f, 180f, 999f })
        {
            Assert.That(MatchClock.StartSeconds(customTimer), Is.GreaterThan(0f));
        }
    }

    // ---------- AUD-038: queued touch input does not outlive its scene ----------

    [Test]
    public void ClearDropsEveryQueuedAndHeldTouchInput()
    {
        PlayerTouchInputState.QueueJumpOrShoot(new Vector2(12f, 34f));
        PlayerTouchInputState.QueueAttack();
        PlayerTouchInputState.QueueSpecial();
        PlayerTouchInputState.BlockHeld = true;

        PlayerTouchInputState.Clear();

        Assert.That(PlayerTouchInputState.ConsumeJumpOrShoot(out _), Is.False);
        Assert.That(PlayerTouchInputState.ConsumeAttack(), Is.False);
        Assert.That(PlayerTouchInputState.ConsumeSpecial(), Is.False);
        Assert.That(PlayerTouchInputState.BlockHeld, Is.False);
    }

    [Test]
    public void AQueuedTouchInputIsConsumedExactlyOnce()
    {
        PlayerTouchInputState.Clear();
        PlayerTouchInputState.QueueAttack();

        Assert.That(PlayerTouchInputState.ConsumeAttack(), Is.True);
        Assert.That(PlayerTouchInputState.ConsumeAttack(), Is.False);
    }

    [TearDown]
    public void ClearTouchInputState()
    {
        PlayerTouchInputState.Clear();
    }

    // ---------- AUD-041/042: stats paging ----------

    [Test]
    public void AnEmptyResultSetStillHasOnePage()
    {
        // PageCount(0) used to be 0, which made the wrap-around land on page -1
        Assert.That(StatsPaging.PageCount(0), Is.EqualTo(1));
        Assert.That(StatsPaging.PageCount(-5), Is.EqualTo(1));
        Assert.That(StatsPaging.DisplayLabel(0, 0), Is.EqualTo("page 1 / 1"));
    }

    [Test]
    public void PageCountRoundsUpAndDoesNotAddAnEmptyPageOnAnExactFit()
    {
        Assert.That(StatsPaging.PageCount(1), Is.EqualTo(1));
        Assert.That(StatsPaging.PageCount(StatsPaging.ResultsPerPage), Is.EqualTo(1));
        Assert.That(StatsPaging.PageCount(StatsPaging.ResultsPerPage + 1), Is.EqualTo(2));
        Assert.That(StatsPaging.PageCount(StatsPaging.ResultsPerPage * 3), Is.EqualTo(3));
    }

    [Test]
    public void PagingLeftWithNoResultsStaysOnAValidPage()
    {
        // the bug: numPages - 1 with numPages == 0 gave -1, and a negative SQL offset
        Assert.That(StatsPaging.PreviousPage(0, 0), Is.EqualTo(0));
        Assert.That(StatsPaging.NextPage(0, 0), Is.EqualTo(0));
    }

    [Test]
    public void PagingWrapsAtBothEnds()
    {
        int total = StatsPaging.ResultsPerPage * 3;   // pages 0, 1, 2

        Assert.That(StatsPaging.NextPage(0, total), Is.EqualTo(1));
        Assert.That(StatsPaging.NextPage(2, total), Is.EqualTo(0));
        Assert.That(StatsPaging.PreviousPage(2, total), Is.EqualTo(1));
        Assert.That(StatsPaging.PreviousPage(0, total), Is.EqualTo(2));
    }

    [Test]
    public void AnOutOfRangePageIsBroughtBackIntoRange()
    {
        int total = StatsPaging.ResultsPerPage * 2;   // pages 0, 1

        Assert.That(StatsPaging.NextPage(99, total), Is.EqualTo(0));
        Assert.That(StatsPaging.PreviousPage(-4, total), Is.EqualTo(1));
        Assert.That(StatsPaging.DisplayLabel(99, total), Is.EqualTo("page 2 / 2"));
    }

    [Test]
    public void OffsetIsNeverNegative()
    {
        Assert.That(StatsPaging.OffsetFor(0), Is.EqualTo(0));
        Assert.That(StatsPaging.OffsetFor(3), Is.EqualTo(3 * StatsPaging.ResultsPerPage));
        Assert.That(StatsPaging.OffsetFor(-1), Is.EqualTo(0));
    }

    // ---------- AUD-028: scene contracts are named in one place ----------

    [Test]
    public void RequiredSceneObjectNamesAreNonEmptyAndUnique()
    {
        AssertNamesAreUsable(GameRules.RequiredHudObjectNames, "GameRules.RequiredHudObjectNames");
        AssertNamesAreUsable(Pause.RequiredPauseObjectNames, "Pause.RequiredPauseObjectNames");
        AssertNamesAreUsable(
            ProgressionManager.RequiredProgressionObjectNames,
            "ProgressionManager.RequiredProgressionObjectNames");
    }

    // ---------- AUD-046: the progression menu shares the one XP curve ----------

    [Test]
    public void ExperienceToNextLevelNeverExceedsOneLevel()
    {
        for (int experience = 0; experience <= CharacterLevel.ExperiencePerLevel * 4; experience += 137)
        {
            int remaining = CharacterLevel.ExperienceToNextLevel(experience);
            Assert.That(remaining, Is.GreaterThan(0), "experience " + experience);
            Assert.That(remaining, Is.LessThanOrEqualTo(CharacterLevel.ExperiencePerLevel), "experience " + experience);

            // reaching the remaining amount must advance exactly one level
            Assert.That(
                CharacterLevel.FromExperience(experience + remaining),
                Is.EqualTo(CharacterLevel.FromExperience(experience) + 1),
                "experience " + experience);
        }
    }

    private static void AssertNamesAreUsable(string[] names, string label)
    {
        Assert.That(names, Is.Not.Null, label + " must be declared.");
        Assert.That(names.Length, Is.GreaterThan(0), label + " must not be empty.");

        HashSet<string> seen = new HashSet<string>();
        foreach (string name in names)
        {
            Assert.That(string.IsNullOrWhiteSpace(name), Is.False, label + " contains a blank name.");
            Assert.That(seen.Add(name), Is.True, label + " lists '" + name + "' more than once.");
        }
    }
}
