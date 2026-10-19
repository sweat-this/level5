#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Level5.BackendV2;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The approved bare Scrapyard entry, including its deliberately empty selected identities.
/// An independently supplied reader proves the existing seam, not production input ownership.
/// </summary>
public class Level5BareDirectEntryPlayModeTests
{
    private const string SceneName = "level_01_scrapyard";
    private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly Dictionary<FieldInfo, object> previousOptions = new Dictionary<FieldInfo, object>();
    private Func<LegacyMatchRuntimeSnapshot> previousReader;
    private LocalProfileTestDatabase database;
    private CampaignBoundaryPersistenceIsolation persistence;
    private BackendV2Session previousSession;
    private int previousUserId;
    private string previousUserName;
    private float previousTimeScale;
    private Scene runnerScene;
    private LegacyMatchRuntimeSnapshot firstSnapshot;
    private string firstReadStack;
    private int readCount;

    private static Type Production(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);

    [SetUp]
    public void SetUp()
    {
        runnerScene = SceneManager.GetActiveScene();
        previousTimeScale = Time.timeScale;
        previousReader = (Func<LegacyMatchRuntimeSnapshot>)typeof(MatchRuntime)
            .GetField("legacyFallbackReader", StaticFields).GetValue(null);
        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
        previousSession = BackendV2SessionStore.Current;
        foreach (FieldInfo field in Production("GameOptions").GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsLiteral && !field.IsInitOnly) previousOptions.Add(field, field.GetValue(null));
        }

        persistence = new CampaignBoundaryPersistenceIsolation(Application.persistentDataPath);
        LocalAccountIdentity.UserId = 74;
        LocalAccountIdentity.UserName = "bare-entry-test";
        BackendV2SessionStore.Clear();
        ActiveMatch.Clear();
        firstSnapshot = null;
        firstReadStack = null;
        readCount = 0;
    }

    [UnityTest]
    public IEnumerator CleanProductionFallbackPreservesBareScrapyardEntry()
    {
        foreach (FieldInfo field in previousOptions.Keys)
        {
            field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
        }
        SetOption("numPlayers", 1);
        SetOption("difficultySelected", 1);
        SetOption("levelRequiresTimeOfDay", true);
        MethodInfo capture = Production("GameOptions").GetMethod("CaptureMatchRuntimeSnapshot", StaticFields);
        var productionReader = (Func<LegacyMatchRuntimeSnapshot>)Delegate.CreateDelegate(
            typeof(Func<LegacyMatchRuntimeSnapshot>), capture);
        yield return VerifyScene(productionReader);
    }

    [UnityTest]
    public IEnumerator IndependentBareAnswersReachManagerAwakeWithoutReadingLegacyInputs()
    {
        // Contradict the independent answers, including values that would change spawning.
        SetOption("numPlayers", 4);
        SetOption("player2IsCpu", true);
        SetOption("gameModeSelectedId", 27);
        SetOption("characterId", 999);
        SetOption("characterDisplayName", "not the selected character");
        SetOption("characterObjectName", "missing-prefab");
        SetOption("characterObjectNames", new List<string> { "missing-prefab" });
        SetOption("levelId", 999);
        SetOption("levelDisplayName", "not Scrapyard");
        SetOption("levelRequiresTimeOfDay", false);
        SetOption("levelRequiresWeather", true);
        SetOption("levelHasSevenPointers", true);
        SetOption("gameModeRequiresCountDown", true);
        SetOption("gameModeRequiresBasketball", true);
        SetOption("trafficEnabled", true);
        yield return VerifyScene(CaptureIndependentBareAnswers);
    }

    private IEnumerator VerifyScene(Func<LegacyMatchRuntimeSnapshot> source)
    {
        // Existing real-scene scaffolding suppresses unrelated authored scene log noise.
        // Assertions below inspect the manager's actual initialization, not just a live accessor.
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        MatchRuntime.InstallLegacyFallbackReader(() =>
        {
            LegacyMatchRuntimeSnapshot snapshot = source();
            if (readCount++ == 0)
            {
                firstSnapshot = snapshot;
                firstReadStack = string.Join(" > ", new StackTrace().GetFrames()
                    .Select(frame => frame.GetMethod())
                    .Where(method => method.DeclaringType == typeof(MatchRuntime)
                        || method.DeclaringType == typeof(LevelRuntimeContext)
                        || method.DeclaringType?.Name == "GameLevelManager")
                    .Select(method => method.DeclaringType.Name + "." + method.Name));
            }
            return snapshot;
        });

        SceneManager.LoadScene(SceneName);
        yield return null;
        yield return null;

        Scene scene = SceneManager.GetActiveScene();
        Assert.That(scene.name, Is.EqualTo(SceneName));
        MonoBehaviour manager = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(scene, "GameLevelManager");
        Assert.That(manager, Is.Not.Null);
        Assert.That(manager.enabled, Is.True, "spawn/device validation must not leave a disabled manager");
        Assert.That(readCount, Is.GreaterThan(0));
        Assert.That(firstReadStack, Does.Contain("GameLevelManager.Awake"));
        Assert.That(firstReadStack, Does.Not.Contain("LevelRuntimeContext.Awake"),
            "the manager consumes rules before it adds the context");
        ResolvedMatchRules rules = RealScenePlayModeTestSupport.GetField<ResolvedMatchRules>(manager, "_rules");
        PlayerRoster roster = RealScenePlayModeTestSupport.GetField<PlayerRoster>(manager, "_roster");
        Assert.That(rules, Is.SameAs(firstSnapshot.Rules));
        AssertBareRules(rules);
        AssertBareRules(MatchRuntime.Rules);
        Assert.That(roster.Count, Is.EqualTo(1));
        PlayerSlot primary = roster.GetBySlotId(0);
        Assert.That(primary.IsLocalHuman, Is.True);
        Assert.That(primary.LocalInputSlot, Is.EqualTo(0));
        Assert.That(primary.Character.CharacterId, Is.Zero);
        Assert.That(primary.Character.ObjectName, Is.Empty);
        Assert.That(primary.Character.DisplayName, Is.Empty);
        Assert.That(primary.Character.IsShooter && primary.Character.IsFighter, Is.True);
        Assert.That(MatchRuntime.HasConfiguration, Is.False);
        Assert.That(MatchRuntime.RawModeId, Is.Zero);
        Assert.That(MatchRuntime.ModeId, Is.EqualTo(GameModeId.None));
        Assert.That(MatchRuntime.ModeDisplayName, Is.Empty);
        Assert.That(MatchRuntime.Level, Is.Null);
        Assert.That(MatchRuntime.LevelId, Is.Zero);
        Assert.That(MatchRuntime.LevelDisplayName, Is.Empty);
        Assert.That(MatchRuntime.LevelRequiresTimeOfDay, Is.True);
        Assert.That(MatchRuntime.LevelHasWeather || MatchRuntime.LevelHasSevenPointers || MatchRuntime.CustomCamera, Is.False);
        Assert.That(MatchRuntime.PrimaryCharacterId, Is.Zero);
        Assert.That(MatchRuntime.PrimaryCharacterObjectName, Is.Empty);
        Assert.That(MatchRuntime.PrimaryCharacterDisplayName, Is.Empty);
        Assert.That(MatchRuntime.Cheerleader.ObjectName, Is.Empty);
        Assert.That(MatchRuntime.Cheerleader.CheerleaderId, Is.Zero);
        for (int slot = 0; slot < 4; slot++) Assert.That(MatchRuntime.LocalInputSlotFor(slot), Is.EqualTo(slot));

        LevelRuntimeContext context = LevelRuntimeContext.instance;
        Assert.That(context, Is.Not.Null);
        Assert.That(context.gameObject.scene, Is.EqualTo(scene));
        Assert.That(context.Players, Is.SameAs(RealScenePlayModeTestSupport.GetField<PlayerRegistry>(manager, "registry")));
        Assert.That(context.Players.Count, Is.EqualTo(1));
        PlayerIdentifier spawned = context.Players.GetBySlot(0);
        Assert.That(spawned.isCpu, Is.False);
        Assert.That(spawned.GetComponent<PlayerController>(), Is.Not.Null);
        Assert.That(spawned.GetComponent<CharacterProfile>().PlayerDisplayName, Is.EqualTo("Doctor Blood"));
        UnityEngine.Debug.Log("Bare Scrapyard entry: first consumer " + firstReadStack
            + "; one human spawned; selected identity remains empty; reader calls " + readCount);
    }

    private static void AssertBareRules(ResolvedMatchRules rules)
    {
        Assert.That(rules.Objective, Is.EqualTo(MatchObjective.Score));
        Assert.That(rules.ClockMode, Is.EqualTo(MatchClockMode.None));
        Assert.That(rules.CustomTimerSeconds, Is.Zero);
        Assert.That(rules.MatchLengthSeconds, Is.EqualTo(180f));
        Assert.That(rules.CombatMode, Is.EqualTo(CombatMode.None));
        Assert.That(rules.ShotRule, Is.EqualTo(ShotRule.Any));
        Assert.That(rules.ShotMarkers, Is.EqualTo(ShotMarkerRequirement.None));
        Assert.That(rules.BasketballCount, Is.EqualTo(1));
        Assert.That(rules.Difficulty, Is.EqualTo(MatchDifficulty.Normal));
        Assert.That(rules.Sniper, Is.EqualTo(SniperMode.None));
        Assert.That(rules.RequiresBasketball || rules.AllowsCpuShooters || rules.EnemiesEnabled
            || rules.TrafficEnabled || rules.ObstaclesEnabled || rules.Hardcore || rules.ArcadeMode
            || rules.AddsImplicitDefender || rules.EnemiesOnly || rules.RequiresMoneyBall
            || rules.RequiresConsecutiveShots || rules.RequiresPlayerSurvive, Is.False);
    }

    private static LegacyMatchRuntimeSnapshot CaptureIndependentBareAnswers()
    {
        return new LegacyMatchRuntimeSnapshot(
            rules: () => new ResolvedMatchRules(clockMode: MatchClockMode.None,
                requiresBasketball: false, allowsCpuShooters: false),
            roster: () => PlayerRoster.SingleLocalHuman(new CharacterSelection(0, "", "", true, true)),
            modeId: GameModeId.None, rawModeId: 0, modeDisplayName: "", levelDisplayName: "", levelId: 0,
            levelRequiresTimeOfDay: true, levelHasWeather: false, levelHasSevenPointers: false,
            primaryCharacterDisplayName: "", primaryCharacterObjectName: "", primaryCharacterId: 0,
            cheerleader: CheerleaderSelection.None, getHumanPlayerInputSlot: slot => Math.Max(0, slot));
    }

    private static void SetOption(string name, object value) => Production("GameOptions").GetField(name).SetValue(null, value);

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            if (SceneManager.GetActiveScene() != runnerScene)
                yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("bare-entry-cleanup");
        }
        finally
        {
            try
            {
                ActiveMatch.Clear();
                MatchRuntime.ResetLegacyFallbackReader();
                if (previousReader != null) MatchRuntime.InstallLegacyFallbackReader(previousReader);
                foreach (var option in previousOptions) option.Key.SetValue(null, option.Value);
                previousOptions.Clear();
                database?.Dispose();
                LocalAccountIdentity.UserId = previousUserId;
                LocalAccountIdentity.UserName = previousUserName;
                BackendV2SessionStore.Clear();
                if (previousSession != null) BackendV2SessionStore.Set(previousSession);
                Time.timeScale = previousTimeScale;
            }
            finally { persistence?.Dispose(); }
        }
    }
}
#endif
