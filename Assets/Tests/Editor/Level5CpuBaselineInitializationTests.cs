using System.Collections.Generic;
using System.Reflection;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Coverage for #71: CPU baseline initialization (menu-boot catalog load) must be context-free -
/// no GameLevelManager, no MatchRuntime, no Level mutation - while runtime Hardcore/contest
/// initialization (SpawnCoordinator -> CharacterProfile.Start) must remain exactly equivalent to the
/// old <c>intializeCpuShooterStats</c> behavior and must not compound on a retry.
/// </summary>
public class Level5CpuBaselineInitializationTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private bool previousHardcore;

    [SetUp]
    public void SetUp()
    {
        previousHardcore = GameOptions.hardcoreModeEnabled;
    }

    [TearDown]
    public void TearDown()
    {
        GameOptions.hardcoreModeEnabled = previousHardcore;
        ActiveMatch.Clear();

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private CharacterProfile MakeCpuProfile(int level)
    {
        GameObject go = new GameObject("cpu_test_profile");
        spawned.Add(go);
        CharacterProfile profile = go.AddComponent<CharacterProfile>();
        profile.isCpu = true;
        profile.Level = level;
        return profile;
    }

    // ---- A: baseline is context-free -----------------------------------------------------------

    [Test]
    public void BaselineDoesNotThrowUnderHardcoreWithNoGameLevelManager()
    {
        GameOptions.hardcoreModeEnabled = true;
        Assert.That(GameLevelManager.instance, Is.Null, "this characterizes the menu-boot case: no gameplay scene has run yet");

        CharacterProfile cpu = MakeCpuProfile(40);

        Assert.DoesNotThrow(() => cpu.InitializeCpuBaselineStats());
        Assert.That(cpu.Level, Is.EqualTo(40), "baseline initialization must never change Level");
    }

    // ---- B: baseline is idempotent --------------------------------------------------------------

    [Test]
    public void BaselineInitializationIsIdempotent()
    {
        CharacterProfile cpu = MakeCpuProfile(55);

        cpu.InitializeCpuBaselineStats();
        (int level, float a3, float a4, float a7, float release, int range, int luck, int clutch) first = Snapshot(cpu);

        cpu.InitializeCpuBaselineStats();
        (int level, float a3, float a4, float a7, float release, int range, int luck, int clutch) second = Snapshot(cpu);

        Assert.That(second, Is.EqualTo(first));
    }

    private static (int, float, float, float, float, int, int, int) Snapshot(CharacterProfile profile)
    {
        return (profile.Level, profile.Accuracy3Pt, profile.Accuracy4Pt, profile.Accuracy7Pt,
            profile.Release, profile.Range, profile.Luck, profile.Clutch);
    }

    // ---- C: contest state cannot contaminate the menu baseline ----------------------------------

    [Test]
    public void ContestStateDoesNotContaminateMenuBaseline()
    {
        GameOptions.gameModeThreePointContest = true;
        try
        {
            CharacterProfile cpu = MakeCpuProfile(60);

            cpu.InitializeCpuBaselineStats();

            Assert.That(cpu.Luck, Is.Not.EqualTo(0), "menu baseline must produce ordinary Luck, not match-only zero");
            Assert.That(cpu.Clutch, Is.Not.EqualTo(0), "menu baseline must produce ordinary Clutch, not match-only zero");
        }
        finally
        {
            GameOptions.gameModeThreePointContest = false;
        }
    }

    // ---- D: Hardcore level policy -----------------------------------------------------------------

    [TestCase(40, 20, false, 40)]
    [TestCase(40, 20, true, 50)]
    [TestCase(20, 40, true, 50)]
    [TestCase(40, 40, true, 50)]
    public void HardcoreLevelPolicyMatchesExistingFormula(int authoredCpuLevel, int primaryLevel, bool hardcore, int expected)
    {
        Assert.That(CpuDifficultyLevelPolicy.Resolve(authoredCpuLevel, primaryLevel, hardcore), Is.EqualTo(expected));
    }

    [Test]
    public void HardcoreLevelPolicyIsDeterministicAcrossRepeatedCalls()
    {
        int first = CpuDifficultyLevelPolicy.Resolve(30, 50, true);
        int second = CpuDifficultyLevelPolicy.Resolve(30, 50, true);

        Assert.That(first, Is.EqualTo(60));
        Assert.That(second, Is.EqualTo(first));
    }

    // ---- E: prepared Hardcore context does not compound on retry --------------------------------

    [Test]
    public void PreparedHardcoreContextDoesNotCompoundOnRepeatedApplication()
    {
        CharacterProfile cpu = MakeCpuProfile(30);
        ResolvedMatchRules rules = new ResolvedMatchRules(hardcore: true);
        cpu.PrepareCpuMatchContext(primaryHumanLevel: 50, rules);

        cpu.ApplyPreparedCpuMatchInitialization();
        Assert.That(cpu.Level, Is.EqualTo(60));

        cpu.ApplyPreparedCpuMatchInitialization();
        Assert.That(cpu.Level, Is.EqualTo(60), "a second application must resolve from the same prepared base, never compound");
    }

    // ---- F: normal (non-Hardcore) prepared context leaves Level unchanged -----------------------

    [Test]
    public void PreparedNormalContextLeavesLevelUnchanged()
    {
        CharacterProfile cpu = MakeCpuProfile(30);
        ResolvedMatchRules rules = new ResolvedMatchRules(hardcore: false);
        cpu.PrepareCpuMatchContext(primaryHumanLevel: 999, rules);

        cpu.ApplyPreparedCpuMatchInitialization();

        Assert.That(cpu.Level, Is.EqualTo(30));
    }

    // ---- G: prepared contest context suppresses Luck/Clutch at runtime --------------------------

    [Test]
    public void PreparedContestContextSuppressesLuckAndClutch()
    {
        CharacterProfile cpu = MakeCpuProfile(60);
        ResolvedMatchRules rules = new ResolvedMatchRules(shotRule: ShotRule.ThreePoint);
        cpu.PrepareCpuMatchContext(primaryHumanLevel: 10, rules);

        cpu.ApplyPreparedCpuMatchInitialization();

        Assert.That(cpu.Luck, Is.EqualTo(0));
        Assert.That(cpu.Clutch, Is.EqualTo(0));
    }

    // ---- missing context: safe fallback, no NRE, no invented primary level ----------------------

    [Test]
    public void MissingPreparedContextFallsBackToSafeBaselineWithoutThrowing()
    {
        CharacterProfile cpu = MakeCpuProfile(45);

        Assert.DoesNotThrow(() => cpu.ApplyPreparedCpuMatchInitialization());

        Assert.That(cpu.Level, Is.EqualTo(45), "no prepared context means no Hardcore bump and no invented primary level");
    }

    // ---- H: Arcade/easy precedence is preserved after CPU/contest initialization ----------------

    [Test]
    public void ArcadeEasyOverrideStillWinsAfterCpuMatchInitialization()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.difficultySelected = 0; // Easy - triggers CharacterProfile.Start's override branch
            GameOptions.hardcoreModeEnabled = false;

            CharacterProfile cpu = MakeCpuProfile(40);
            // A contest rule that would otherwise zero Luck/Clutch, to prove the later Arcade
            // override still wins over it (precedence pinned, not changed, by #71).
            ResolvedMatchRules rules = new ResolvedMatchRules(shotRule: ShotRule.ThreePoint);
            cpu.PrepareCpuMatchContext(primaryHumanLevel: 10, rules);

            InvokeStart(cpu);

            Assert.That(cpu.Accuracy3Pt, Is.EqualTo(100));
            Assert.That(cpu.Luck, Is.EqualTo(10));
            Assert.That(cpu.Clutch, Is.EqualTo(100));
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    private static void InvokeStart(CharacterProfile profile)
    {
        MethodInfo start = typeof(CharacterProfile).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(start, Is.Not.Null, "CharacterProfile.Start not found by reflection");
        start.Invoke(profile, null);
    }

    // ---- I: SpawnCoordinator prepares CPU context from registry slot 0, without a full scene -----

    [Test]
    public void SpawnCoordinatorPreparesCpuContextFromPrimarySlotZero()
    {
        PlayerRegistry registry = new PlayerRegistry();

        GameObject primaryGo = new GameObject("primary_test");
        spawned.Add(primaryGo);
        PlayerIdentifier primaryIdentifier = primaryGo.AddComponent<PlayerIdentifier>();
        CharacterProfile primaryProfile = primaryGo.AddComponent<CharacterProfile>();
        primaryProfile.Level = 77;
        primaryIdentifier.characterProfile = primaryProfile;
        registry.Add(primaryIdentifier);

        GameObject cpuGo = new GameObject("cpu_test");
        spawned.Add(cpuGo);
        PlayerIdentifier cpuIdentifier = cpuGo.AddComponent<PlayerIdentifier>();
        CharacterProfile cpuProfile = cpuGo.AddComponent<CharacterProfile>();
        cpuProfile.isCpu = true;
        cpuProfile.Level = 30;
        cpuIdentifier.characterProfile = cpuProfile;

        ResolvedMatchRules rules = new ResolvedMatchRules(hardcore: true);
        SpawnCoordinator.SpawnLocations locations = new SpawnCoordinator.SpawnLocations();
        PlayerRoster roster = PlayerRoster.Build(new List<PlayerRosterEntry>());
        SpawnCoordinator coordinator = new SpawnCoordinator(locations, registry, rules, roster, GameModeId.None);

        MethodInfo prepare = typeof(SpawnCoordinator).GetMethod(
            "PrepareCpuMatchContext", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(prepare, Is.Not.Null, "SpawnCoordinator.PrepareCpuMatchContext not found by reflection");
        prepare.Invoke(coordinator, new object[] { cpuIdentifier });

        cpuProfile.ApplyPreparedCpuMatchInitialization();

        Assert.That(cpuProfile.Level, Is.EqualTo(87), "max(cpuBase 30, primary 77) + 10 - the primary Level must come from registry slot 0");
    }
}
