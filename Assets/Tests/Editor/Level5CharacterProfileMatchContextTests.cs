using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 2b Slice 27: <c>CharacterProfile</c> no longer discovers <c>MatchRuntime</c> or
/// <c>LoadedData</c>. The saved-profile lookup, the cheerleader selection and the resolved match
/// rules are handed to it by <c>SpawnCoordinator</c> instead.
///
/// This covers the behaviour that inversion made load-bearing, not every property assignment in the
/// copy: that the requested character id reaches the resolver and the copy is unchanged, that the
/// cheerleader bonuses and the contest Luck/Clutch suppression still decide the same thing off the
/// prepared context, that <c>Start</c>'s Arcade/easy override reads the prepared rules, that a
/// missing context reports itself instead of falling back to a global, and that composition prepares
/// every human - including a directly entered scene's, which still skips the saved-profile rebuild.
///
/// CPU preparation is unchanged and stays covered by
/// <see cref="Level5CpuBaselineInitializationTests"/>; the source-level boundary is
/// <see cref="Level5CharacterProfileDependencyGuardTests"/>.
/// </summary>
public class Level5CharacterProfileMatchContextTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private LoadedData previousLoadedData;

    [SetUp]
    public void SetUp()
    {
        previousLoadedData = LoadedData.instance;
        ActiveMatch.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        LoadedData.instance = previousLoadedData;
        ActiveMatch.Clear();

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    // ---- fixtures ------------------------------------------------------------------------------

    private CharacterProfile MakeProfile(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go.AddComponent<CharacterProfile>();
    }

    /// <summary>
    /// A stand-in for a saved profile, populated through the same public properties production writes
    /// them through, with a distinct value per field so a transposed assignment cannot pass.
    /// </summary>
    private CharacterProfile MakeSavedProfile(int characterId, int experience = 24000)
    {
        CharacterProfile source = MakeProfile("saved_profile_" + characterId);
        source.PlayerId = characterId;
        source.PlayerObjectName = "objectname" + characterId;
        source.PlayerDisplayName = "Display " + characterId;
        source.Experience = experience;
        source.Speed = 3.25f;
        source.RunSpeed = 4.75f;
        source.RunSpeedHasBall = 4.25f;
        source.JumpForce = 5.5f;
        source.ShootAngle = 63;
        source.Accuracy2Pt = 71;
        source.Accuracy3Pt = 72;
        source.Accuracy4Pt = 73;
        source.Accuracy7Pt = 74;
        source.Range = 75;
        source.Release = 76;
        source.Clutch = 77;
        source.Luck = 7;
        source.PointsAvailable = 11;
        source.PointsUsed = 12;
        return source;
    }

    private static CheerleaderSelection Cheerleader(
        int three = 0,
        int four = 0,
        int seven = 0,
        int range = 0,
        int release = 0,
        int luck = 0,
        int clutch = 0)
    {
        return new CheerleaderSelection(
            1,
            "cheer",
            "Cheer",
            bonusThreeAccuracy: three,
            bonusFourAccuracy: four,
            bonusSevenAccuracy: seven,
            bonusRelease: release,
            bonusRange: range,
            bonusLuck: luck,
            bonusClutch: clutch);
    }

    private static void InvokeStart(CharacterProfile profile)
    {
        MethodInfo start = typeof(CharacterProfile).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(start, Is.Not.Null, "CharacterProfile.Start not found by reflection");
        start.Invoke(profile, null);
    }

    private static object GetPrivateField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name} not found by reflection");
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name} not found by reflection");
        field.SetValue(target, value);
    }

    /// <summary>
    /// Runs <paramref name="action"/> and returns every warning logged while it ran.
    ///
    /// <c>LogAssert.Expect</c> can assert that a warning happened, but not that it was the only one -
    /// Unity fails a test on unexpected errors, not on unexpected warnings - and "exactly one report
    /// per participant" is the contract being checked here, so the messages are counted directly.
    /// </summary>
    private static List<string> CaptureWarnings(Action action)
    {
        List<string> warnings = new List<string>();
        Application.LogCallback handler = (condition, stackTrace, type) =>
        {
            if (type == LogType.Warning)
            {
                warnings.Add(condition);
            }
        };

        Application.logMessageReceived += handler;
        try
        {
            action();
        }
        finally
        {
            Application.logMessageReceived -= handler;
        }

        return warnings;
    }

    // ---- A: human source resolution and copy ---------------------------------------------------

    [Test]
    public void PreparedResolverReceivesTheRequestedCharacterId()
    {
        CharacterProfile target = MakeProfile("target");
        CharacterProfile source = MakeSavedProfile(7);
        List<int> requested = new List<int>();

        target.PrepareHumanMatchContext(
            id => { requested.Add(id); return source; },
            CheerleaderSelection.None,
            new ResolvedMatchRules());

        target.intializeShooterStatsFromProfile(7);

        Assert.That(requested, Is.EqualTo(new[] { 7 }),
            "the character id the caller asked for must reach the resolver, not the primary slot's id");
    }

    [Test]
    public void HumanRebuildCopiesTheSavedProfileContractFields()
    {
        CharacterProfile target = MakeProfile("target");
        CharacterProfile source = MakeSavedProfile(4, experience: 24000);

        target.PrepareHumanMatchContext(id => source, CheerleaderSelection.None, new ResolvedMatchRules());
        target.intializeShooterStatsFromProfile(4);

        Assert.That(target.Experience, Is.EqualTo(24000), "experience");
        Assert.That(target.Level, Is.EqualTo(CharacterLevel.FromExperience(24000)), "derived level");
        Assert.That(target.PlayerId, Is.EqualTo(4), "identity");
        Assert.That(target.PlayerObjectName, Is.EqualTo("objectname4"), "object name");
        Assert.That(target.PlayerDisplayName, Is.EqualTo("Display 4"), "display name");
        Assert.That(target.RunSpeed, Is.EqualTo(4.75f), "movement stat");
        Assert.That(target.Accuracy2Pt, Is.EqualTo(71), "shooting stat");
        Assert.That(target.ShootAngle, Is.EqualTo(63), "shoot angle");
        Assert.That(target.PointsAvailable, Is.EqualTo(11), "progression - points available");
        Assert.That(target.PointsUsed, Is.EqualTo(12), "progression - points used");
    }

    [Test]
    public void UnresolvedSavedProfileReportsAndLeavesTheTargetUntouched()
    {
        CharacterProfile target = MakeProfile("target");
        target.Accuracy2Pt = 13;
        target.Experience = 999;

        target.PrepareHumanMatchContext(id => null, CheerleaderSelection.None, new ResolvedMatchRules());

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the selected player profile"));
        target.intializeShooterStatsFromProfile(4);

        Assert.That(target.Accuracy2Pt, Is.EqualTo(13), "nothing may be mutated before the source resolves");
        Assert.That(target.Experience, Is.EqualTo(999));
    }

    // ---- B: cheerleader policy ------------------------------------------------------------------

    [Test]
    public void CheerleaderBonusesApplyToEveryCategoryTheyAlwaysHave()
    {
        CharacterProfile target = MakeProfile("target");
        CharacterProfile source = MakeSavedProfile(1);

        target.PrepareHumanMatchContext(
            id => source,
            Cheerleader(three: 1, four: 2, seven: 3, range: 4, release: 5, luck: 6, clutch: 8),
            new ResolvedMatchRules());

        target.intializeShooterStatsFromProfile(1);

        Assert.That(target.Accuracy3Pt, Is.EqualTo(72 + 1), "3pt accuracy bonus");
        Assert.That(target.Accuracy4Pt, Is.EqualTo(73 + 2), "4pt accuracy bonus");
        Assert.That(target.Accuracy7Pt, Is.EqualTo(74 + 3), "7pt accuracy bonus");
        Assert.That(target.Range, Is.EqualTo(75 + 4), "range bonus");
        Assert.That(target.Release, Is.EqualTo(76 + 5), "release bonus");
        Assert.That(target.Clutch, Is.EqualTo(77 + 8), "clutch bonus");
        Assert.That(target.Luck, Is.EqualTo(7 + 6), "luck bonus");
        Assert.That(target.Accuracy2Pt, Is.EqualTo(71), "2pt accuracy has never carried a bonus");
    }

    // ---- C: contest policy ----------------------------------------------------------------------

    [TestCase(ShotRule.ThreePoint)]
    [TestCase(ShotRule.FourPoint)]
    [TestCase(ShotRule.SevenPoint)]
    [TestCase(ShotRule.AllRanges)]
    public void PointContestSuppressesLuckAndClutch(ShotRule contest)
    {
        CharacterProfile target = MakeProfile("target");
        CharacterProfile source = MakeSavedProfile(1);

        target.PrepareHumanMatchContext(
            id => source,
            Cheerleader(luck: 6, clutch: 8),
            new ResolvedMatchRules(shotRule: contest));

        target.intializeShooterStatsFromProfile(1);

        Assert.That(target.Luck, Is.EqualTo(0));
        Assert.That(target.Clutch, Is.EqualTo(0));
    }

    [Test]
    public void OrdinaryRulesKeepLuckAndClutchWithTheirBonuses()
    {
        CharacterProfile target = MakeProfile("target");
        CharacterProfile source = MakeSavedProfile(1);

        target.PrepareHumanMatchContext(
            id => source,
            Cheerleader(luck: 6, clutch: 8),
            new ResolvedMatchRules(shotRule: ShotRule.Any));

        target.intializeShooterStatsFromProfile(1);

        Assert.That(target.Luck, Is.EqualTo(7 + 6));
        Assert.That(target.Clutch, Is.EqualTo(77 + 8));
    }

    // ---- D: Start policy ------------------------------------------------------------------------

    private CharacterProfile PreparedHumanForStart(ResolvedMatchRules rules)
    {
        CharacterProfile profile = MakeProfile("start_target");
        profile.Level = 30;
        profile.Accuracy2Pt = 40;
        profile.Luck = 1;
        profile.PrepareHumanMatchContext(id => null, CheerleaderSelection.None, rules);
        return profile;
    }

    private static void AssertMaxStatOverrideApplied(CharacterProfile profile)
    {
        Assert.That(profile.Accuracy2Pt, Is.EqualTo(100));
        Assert.That(profile.Accuracy3Pt, Is.EqualTo(100));
        Assert.That(profile.Accuracy4Pt, Is.EqualTo(100));
        Assert.That(profile.Accuracy7Pt, Is.EqualTo(100));
        Assert.That(profile.Release, Is.EqualTo(100));
        Assert.That(profile.Range, Is.EqualTo(150));
        Assert.That(profile.Clutch, Is.EqualTo(100));
        Assert.That(profile.Luck, Is.EqualTo(10));
    }

    [Test]
    public void ArcadeModeAppliesTheMaximumStatOverride()
    {
        CharacterProfile profile = PreparedHumanForStart(new ResolvedMatchRules(arcadeMode: true));

        InvokeStart(profile);

        AssertMaxStatOverrideApplied(profile);
    }

    [Test]
    public void EasyDifficultyAppliesTheMaximumStatOverride()
    {
        CharacterProfile profile = PreparedHumanForStart(new ResolvedMatchRules(difficulty: MatchDifficulty.Easy));

        InvokeStart(profile);

        AssertMaxStatOverrideApplied(profile);
    }

    [Test]
    public void OrdinaryRulesDoNotApplyTheMaximumStatOverride()
    {
        CharacterProfile profile = PreparedHumanForStart(
            new ResolvedMatchRules(difficulty: MatchDifficulty.Normal, arcadeMode: false));

        InvokeStart(profile);

        Assert.That(profile.Accuracy2Pt, Is.EqualTo(40));
        Assert.That(profile.Luck, Is.EqualTo(1));
    }

    [Test]
    public void StartWithoutPreparedRulesWarnsOnceAndLeavesTheProfileUsable()
    {
        CharacterProfile profile = MakeProfile("unprepared");
        profile.Level = 30;
        profile.Accuracy2Pt = 40;

        List<string> warnings = CaptureWarnings(() => InvokeStart(profile));

        Assert.That(warnings.Count, Is.EqualTo(1), "exactly one report per participant: " + string.Join(" | ", warnings));
        Assert.That(warnings[0], Does.Contain("no match rules prepared"));
        Assert.That(profile.isActiveAndEnabled, Is.True, "a missing context must not disable the component");
        Assert.That(profile.gameObject.activeSelf, Is.True, "a missing context must not disable the participant");
        Assert.That(profile.InAirSpeed, Is.EqualTo(0.5f),
            "context-free initialization still runs: fadeaway floors at 50, InAirSpeed = fadeaway / 100");
        Assert.That(profile.Accuracy2Pt, Is.EqualTo(40), "only the match-derived override is skipped");
    }

    /// <summary>
    /// The diagnostic-suppression branch in <c>Start</c>: an unprepared shooting CPU has already been
    /// reported by <c>ApplyPreparedCpuMatchInitialization</c>, so the missing-rules warning must stay
    /// silent for it - while a defensive CPU, which never runs that method, must still be reported.
    /// One report per participant either way, and never zero.
    /// </summary>
    [TestCase(false, "initialized as a CPU with no match context",
        TestName = "UnpreparedShootingCpuIsReportedOnceByTheCpuPath")]
    [TestCase(true, "no match rules prepared",
        TestName = "UnpreparedDefensiveCpuIsReportedOnceByStart")]
    public void UnpreparedCpuReachingStartReportsExactlyOneDiagnostic(bool defensive, string expectedMessage)
    {
        CharacterProfile cpu = MakeProfile("unprepared_cpu");
        cpu.isCpu = true;
        cpu.Level = 45;
        SetPrivateField(cpu, "isDefensiveCpuPlayer", defensive);

        List<string> warnings = CaptureWarnings(() => InvokeStart(cpu));

        Assert.That(warnings.Count, Is.EqualTo(1), "exactly one report per participant: " + string.Join(" | ", warnings));
        Assert.That(warnings[0], Does.Contain(expectedMessage));
        Assert.That(cpu.Level, Is.EqualTo(45),
            "the safe baseline stands: no Hardcore bump and no invented primary human level");
    }

    // ---- E: missing human context ---------------------------------------------------------------

    [Test]
    public void HumanInitializerWithoutPreparedContextReportsAndChangesNothing()
    {
        CharacterProfile target = MakeProfile("target");
        target.Accuracy2Pt = 13;
        target.Experience = 999;
        LoadedData.instance = null;

        LogAssert.Expect(LogType.Error, new Regex("no human match context prepared"));
        target.intializeShooterStatsFromProfile(4);

        Assert.That(target.Accuracy2Pt, Is.EqualTo(13));
        Assert.That(target.Experience, Is.EqualTo(999));
    }

    [Test]
    public void HumanInitializerWithoutPreparedRulesReportsRatherThanGuessing()
    {
        CharacterProfile target = MakeProfile("target");
        CharacterProfile source = MakeSavedProfile(4);
        target.Experience = 999;

        // A resolver and a cheerleader, but no rules: the contest branch has nothing to decide from,
        // so this is a composition defect rather than something to invent a default for.
        target.PrepareHumanMatchContext(id => source, CheerleaderSelection.None, null);

        LogAssert.Expect(LogType.Error, new Regex("no human match context prepared"));
        target.intializeShooterStatsFromProfile(4);

        Assert.That(target.Experience, Is.EqualTo(999));
    }

    // ---- F: composition ------------------------------------------------------------------------

    private LoadedData InstallLoadedData(params CharacterProfile[] savedProfiles)
    {
        GameObject go = new GameObject("loaded_data");
        spawned.Add(go);
        LoadedData data = go.AddComponent<LoadedData>();
        FieldInfo field = typeof(LoadedData).GetField(
            "playerSelectedData", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null, "LoadedData.playerSelectedData not found by reflection");
        field.SetValue(data, new List<CharacterProfile>(savedProfiles));
        LoadedData.instance = data;
        return data;
    }

    private PlayerIdentifier MakeHumanParticipant(string name, CharacterProfile profile)
    {
        PlayerIdentifier identifier = profile.gameObject.AddComponent<PlayerIdentifier>();
        identifier.name = name;
        identifier.characterProfile = profile;
        return identifier;
    }

    private static PlayerSlot HumanSlot(int slotId, int characterId)
    {
        return new PlayerSlot(
            slotId,
            PlayerControlType.LocalHuman,
            TestDefinitions.Character("objectname" + characterId, characterId: characterId),
            localInputSlot: slotId);
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 57: <c>SpawnCoordinator</c> no longer implements the saved-profile lookup
    /// itself - it only forwards whatever <c>loadedCharacterProfileResolver</c> it was constructed
    /// with. This file's whole point is proving composition reaches the real <see cref="LoadedData"/>
    /// singleton this fixture installs (<see cref="InstallLoadedData"/>), so <see cref="BuildCoordinator"/>
    /// supplies the exact production adapter (<c>GameLevelManager.ResolveLoadedCharacterProfile</c>),
    /// resolved via reflection (it is <c>private static</c>) - mirroring production composition
    /// (<c>GameLevelManager.Awake</c>), not a fixture-local stand-in.
    /// </summary>
    private static Func<int, CharacterProfile> ProductionLoadedCharacterProfileResolver()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod(
            "ResolveLoadedCharacterProfile", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "GameLevelManager.ResolveLoadedCharacterProfile not found by reflection");
        return (Func<int, CharacterProfile>)Delegate.CreateDelegate(typeof(Func<int, CharacterProfile>), method);
    }

    private static SpawnCoordinator BuildCoordinator(ResolvedMatchRules rules)
    {
        return new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            new PlayerRegistry(),
            rules,
            PlayerRoster.Build(new List<PlayerRosterEntry>()),
            GameModeId.None,
            loadedCharacterProfileResolver: ProductionLoadedCharacterProfileResolver());
    }

    private static void InvokeInitializeHumanProfile(
        SpawnCoordinator coordinator, PlayerIdentifier identifier, PlayerSlot slot)
    {
        MethodInfo method = typeof(SpawnCoordinator).GetMethod(
            "InitializeHumanProfile", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, "SpawnCoordinator.InitializeHumanProfile not found by reflection");
        method.Invoke(coordinator, new object[] { identifier, slot });
    }

    /// <summary>
    /// A throwaway configuration so <c>MatchRuntime.HasConfiguration</c> observes true - the same
    /// shape <c>Level5RangeMeterOwnershipTests.BuildAnyMatch</c> uses.
    ///
    /// Unlike that one, this carries a cheerleader with real bonuses rather than
    /// <c>CheerleaderSelection.None</c>: composition reading the *match's* cheerleader is the wiring
    /// this slice created, and with a zero-bonus cheerleader every assertion would still pass if
    /// <c>InitializeHumanProfile</c> passed the wrong one.
    /// </summary>
    private static MatchConfiguration BuildAnyMatch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        PlayerRoster roster = TestDefinitions.SoloRoster();

        return new MatchConfiguration(
            mode,
            level,
            roster,
            MatchModifiers.Default,
            MatchConfigurationBuilder.Resolve(mode, level, roster, MatchModifiers.Default),
            Cheerleader(three: 5, luck: 2),
            "character profile match context test");
    }

    [Test]
    public void ConfiguredHumanIsPreparedBeforeTheSavedProfileRebuildRuns()
    {
        // No LoadedData at all: the resolver therefore answers null. If preparation happened after
        // the configured-match gate, the initializer would report a *missing context* instead - so
        // which error arrives is exactly the ordering assertion.
        LoadedData.instance = null;
        ActiveMatch.Begin(BuildAnyMatch());

        ResolvedMatchRules rules = new ResolvedMatchRules(difficulty: MatchDifficulty.Easy);
        SpawnCoordinator coordinator = BuildCoordinator(rules);
        CharacterProfile profile = MakeProfile("human_configured");
        PlayerIdentifier identifier = MakeHumanParticipant("human_configured", profile);

        LogAssert.Expect(LogType.Error, new Regex("could not resolve the selected player profile"));
        InvokeInitializeHumanProfile(coordinator, identifier, HumanSlot(0, 3));

        Assert.That(GetPrivateField(profile, "preparedRules"), Is.SameAs(rules),
            "the profile must hold the coordinator's already-resolved rules, not one of its own");
    }

    [Test]
    public void ConfiguredHumansRebuildFromTheirOwnSlotCharacterAndTheMatchCheerleader()
    {
        CharacterProfile firstSaved = MakeSavedProfile(3, experience: 9000);
        CharacterProfile secondSaved = MakeSavedProfile(8, experience: 27000);
        InstallLoadedData(firstSaved, secondSaved);
        ActiveMatch.Begin(BuildAnyMatch());

        SpawnCoordinator coordinator = BuildCoordinator(new ResolvedMatchRules());

        CharacterProfile first = MakeProfile("human_slot0");
        CharacterProfile second = MakeProfile("human_slot1");
        InvokeInitializeHumanProfile(coordinator, MakeHumanParticipant("human_slot0", first), HumanSlot(0, 3));
        InvokeInitializeHumanProfile(coordinator, MakeHumanParticipant("human_slot1", second), HumanSlot(1, 8));

        Assert.That(first.PlayerId, Is.EqualTo(3), "slot 0 rebuilds from its own character");
        Assert.That(first.Experience, Is.EqualTo(9000));
        Assert.That(second.PlayerId, Is.EqualTo(8), "slot 1 must not be rebuilt as slot 0");
        Assert.That(second.Experience, Is.EqualTo(27000));

        // The match's own cheerleader has to reach both humans: composition supplies it, and passing
        // CheerleaderSelection.None instead would be invisible without these two assertions.
        Assert.That(first.Accuracy3Pt, Is.EqualTo(72 + 5), "the configured match's cheerleader bonus must reach slot 0");
        Assert.That(second.Luck, Is.EqualTo(7 + 2), "...and slot 1");
    }

    [Test]
    public void DirectSceneHumanGetsRulesContextButNoSavedProfileRebuild()
    {
        CharacterProfile saved = MakeSavedProfile(3, experience: 9000);
        InstallLoadedData(saved);
        ActiveMatch.Clear();

        ResolvedMatchRules rules = new ResolvedMatchRules(arcadeMode: true);
        SpawnCoordinator coordinator = BuildCoordinator(rules);
        CharacterProfile profile = MakeProfile("human_direct");
        profile.Experience = 500;
        profile.PlayerId = 99;

        InvokeInitializeHumanProfile(coordinator, MakeHumanParticipant("human_direct", profile), HumanSlot(0, 3));

        Assert.That(profile.Experience, Is.EqualTo(500), "a directly entered scene must not load saved profile data");
        Assert.That(profile.PlayerId, Is.EqualTo(99));
        Assert.That(GetPrivateField(profile, "preparedRules"), Is.SameAs(rules),
            "it must still hold this match's rules, which is what Start's Arcade/easy override reads");

        InvokeStart(profile);
        AssertMaxStatOverrideApplied(profile);
    }

    [Test]
    public void HumanWithoutACharacterProfileIsReportedRatherThanCrashing()
    {
        ActiveMatch.Clear();
        SpawnCoordinator coordinator = BuildCoordinator(new ResolvedMatchRules());

        GameObject go = new GameObject("human_no_profile");
        spawned.Add(go);
        PlayerIdentifier identifier = go.AddComponent<PlayerIdentifier>();

        LogAssert.Expect(LogType.Error, new Regex("has no CharacterProfile to initialize"));
        Assert.DoesNotThrow(() => InvokeInitializeHumanProfile(coordinator, identifier, HumanSlot(0, 3)));
    }
}
