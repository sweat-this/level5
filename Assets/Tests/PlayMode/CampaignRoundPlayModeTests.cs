using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Level5.BackendV2;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CampaignRoundPlayModeTests
{
    [UnityTest]
    public IEnumerator FinalLossRetriesWhileAContinueRemains()
    {
        yield return null;

        Assert.That(
            CampaignRoundDecision.Decide(true, true, false, 1),
            Is.EqualTo(CampaignNextAction.Retry));
    }
}

#if UNITY_INCLUDE_TESTS
/// <summary>Real authored arenas and end-round UI; results are injected, not played by physical input.</summary>
public class CampaignBoundaryPlayModeTests
{
    private CampaignBoundaryPersistenceIsolation persistence;
    private readonly Dictionary<FieldInfo, object> options = new Dictionary<FieldInfo, object>();
    private LocalProfileTestDatabase database;
    private BackendV2Session previousSession;
    private Guid owner;
    private DurableResultTransport transport;
    private int previousLocalUserId;
    private string previousLocalUserName;
    private Scene runnerScene;
    private bool previousIgnoreLogs;
    private object previousPrimarySelection;
    private readonly object[] previousCpuSelections = new object[3];
    private static Type Production(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static void SetStatic(string type, string field, object value)
        => Production(type).GetField(field).SetValue(null, value);
    private static object Static(string type, string field) => Production(type).GetField(field).GetValue(null);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void SetProperty(object target, string name, object value)
        => target.GetType().GetProperty(name).SetValue(target, value);

    private static void RememberMenuLevel(int index)
    {
        object selection = Activator.CreateInstance(Production("StartMenuSelectionState"));
        SetProperty(selection, "LevelIndex", index);
        RealScenePlayModeTestSupport.Invoke(selection, "SavePersistedPreferences");
    }

    private static object RememberedMenuLevel()
    {
        object selection = Activator.CreateInstance(Production("StartMenuSelectionState"));
        RealScenePlayModeTestSupport.Invoke(selection, "LoadPersistedPreferences");
        return Property(selection, "LevelIndex");
    }

    [SetUp]
    public void SetUp()
    {
        runnerScene = SceneManager.GetActiveScene();
        previousIgnoreLogs = LogAssert.ignoreFailingMessages;
        previousSession = BackendV2SessionStore.Current;
        previousLocalUserId = LocalAccountIdentity.UserId;
        previousLocalUserName = LocalAccountIdentity.UserName;
        Type selectionSession = Production("PlayerSelectionSession");
        previousPrimarySelection = selectionSession.GetProperty("PrimaryCharacterId").GetValue(null);
        for (int slot = 0; slot < previousCpuSelections.Length; slot++)
            previousCpuSelections[slot] = selectionSession.GetMethod("GetCpu").Invoke(null, new object[] { slot });
        foreach (string type in new[] { "GameOptions", "EndRoundData", "StartMenuPreferences" })
            foreach (FieldInfo field in Production(type).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                if (!field.IsLiteral && !field.IsInitOnly) options[field] = field.GetValue(null);
        try
        {
            // LoadManager repairs guest XP into our disposable SQLite database on startup.
            // Isolate its account-scoped queue before any production scene can consume it.
            persistence = new CampaignBoundaryPersistenceIsolation(Application.persistentDataPath);
            owner = Guid.NewGuid();
            transport = new DurableResultTransport(() => database.CountRows("HighScores", "modeid", 26));
            BackendV2Runtime.Override(transport);
            BackendV2SessionStore.Clear();
            ActiveMatch.Clear();
            ActiveRemoteAttempt.Clear();
            ActiveVersusAttempt.Clear();
            // Existing scene fixtures suppress unrelated scene noise; assertions below inspect the actual boundary.
            RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        }
        catch
        {
            RestoreFiles();
            throw;
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            ActiveMatch.Clear();
            // Setup can fail before any gameplay scene loads. Keep NUnit's runner alive to report it.
            if (SceneManager.GetActiveScene() != runnerScene)
                yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("campaign-boundary-cleanup");
        }
        finally
        {
            try
            {
                database?.Dispose();
                BackendV2SessionStore.Clear();
                if (previousSession != null) BackendV2SessionStore.Set(previousSession);
                BackendV2Runtime.Reset();
                foreach (var field in options) field.Key.SetValue(null, field.Value);
                LocalAccountIdentity.UserId = previousLocalUserId;
                LocalAccountIdentity.UserName = previousLocalUserName;
                Type selectionSession = Production("PlayerSelectionSession");
                selectionSession.GetMethod("RememberPrimary").Invoke(null, new[] { previousPrimarySelection });
                for (int slot = 0; slot < previousCpuSelections.Length; slot++)
                    selectionSession.GetMethod("RememberCpu").Invoke(null, new object[] { slot, previousCpuSelections[slot] });
                options.Clear();
            }
            finally
            {
                RestoreFiles();
                LogAssert.ignoreFailingMessages = previousIgnoreLogs;
            }
        }
    }

    private void RestoreFiles()
    {
        persistence?.Dispose();
        persistence = null;
    }

    private IEnumerator OpenCampaignMenu()
    {
        // Unity creates a separate log scope for the test coroutine after NUnit SetUp.
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        Debug.Log("Campaign boundary: opening isolated database");
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        Debug.Log("Campaign boundary: loading authored menu");
        LocalAccountIdentity.UserId = 74; // Production's reserved local guest identity.
        LocalAccountIdentity.UserName = "guest";
        Debug.Log("Campaign boundary: requesting production loading scene");
        // StartManager expects LoadedData to exist before its Awake; enter through production loading.
        AsyncOperation loading = SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_loading);
        float loadDeadline = Time.realtimeSinceStartup + 90f;
        while (!loading.isDone)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(loadDeadline), "production loading scene did not load");
            yield return null;
        }
        yield return null;
        yield return null;
        float deadline = Time.realtimeSinceStartup + 90f;
        while (!MatchCatalogs.IsReady || GameObject.Find("press_start") == null)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "authored menu/catalogs did not load");
            yield return null;
        }
    }

    private IEnumerator BeginAtRound(int index, int continues = 2)
    {
        yield return OpenCampaignMenu();
        Debug.Log("Campaign boundary: composing round " + index);
        object playerData = Static("PlayerData", "instance");
        Assert.That(playerData, Is.Not.Null, "production menu must create PlayerData");
        IList route = (IList)Property(playerData, "LevelsList");
        Assert.That(route.Count, Is.EqualTo(21));
        RememberMenuLevel(0); // Menu preference stays at the last launch.
        SetStatic("EndRoundData", "currentLevelIndex", index);
        SetStatic("EndRoundData", "numberOfContinues", continues);
        object level = route[index];
        CharacterProfile human = Resources.LoadAll<GameObject>("Prefabs/menu_start/default_shooter_profiles")
            .Select(prefab => prefab.GetComponent<CharacterProfile>()).First(profile => profile != null && profile.IsShooter);
        CharacterProfile cpu = ((GameObject)Property(level, "CpuPlayer")).GetComponent<CharacterProfile>();
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(Selection(human)), PlayerRosterEntry.Cpu(Selection(cpu))
        });
        // Continuation deliberately permits hidden campaign-only arenas, as the production menu does.
        GameModeDefinition mode = MatchCatalogs.Modes.Find(26);
        // The ordinary level catalog excludes Dev; use the same authored-reference conversion as pressNext.
        LevelDefinition arena = (LevelDefinition)Production("LevelDefinitionFactory").GetMethod("Create", new[] { Production("LevelSelected") })
            .Invoke(null, new[] { level });
        Assert.That(mode, Is.Not.Null, "production catalog must include Beat tha Computahs");
        Assert.That(arena, Is.Not.Null, "authored campaign arena must resolve independently of ordinary selectability");
        Assert.That(arena.SceneName, Is.EqualTo(Property(level, "LevelObjectName") + "_" + Property(level, "LevelDescription")));
        MatchConfiguration configuration = new MatchConfiguration(mode, arena, roster, MatchModifiers.Default,
            MatchConfigurationBuilder.Resolve(mode, arena, roster, MatchModifiers.Default), CheerleaderSelection.None,
            "authored campaign boundary regression");
        ActiveMatch.Begin(configuration);
        Production("LegacyGameOptionsBridge").GetMethod("Apply").Invoke(null, new object[] { configuration });
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(configuration.SceneName, _ => { });
        Debug.Log("Campaign boundary: loaded " + configuration.SceneName);
        BackendV2SessionStore.Set(new BackendV2Session("test-access", DateTimeOffset.UtcNow.AddHours(1),
            owner, "test-refresh", DateTimeOffset.UtcNow.AddDays(1)));
        AssertSubmissionSession("initial round");
    }

    private void AssertSubmissionSession(string stage)
    {
        Assert.That(BackendV2SessionStore.Current, Is.Not.Null, stage);
        Assert.That(BackendV2SessionStore.Current.PlayerId, Is.EqualTo(owner), stage);
        Assert.That(BackendV2SessionStore.IsAccessTokenExpiringSoon(TimeSpan.FromSeconds(30)), Is.False, stage);
        Assert.That(ActiveRemoteAttempt.IsActive || ActiveVersusAttempt.IsActive, Is.False, stage);
        Assert.That(BackendV2Runtime.Transport, Is.SameAs(transport), stage);
        Debug.Log("Campaign submission session: stage=" + stage + " owner=" + owner + " valid=True");
    }

    private static IEnumerator AwaitBackendDrain()
    {
        // Enqueue is synchronous; delivery traverses nested production coroutines. Await its actual
        // completion rather than assuming a fixed number of frames is enough on every platform.
        FieldInfo draining = typeof(MatchResultSubmissionCoordinator).GetField(
            "draining", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(draining, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 20f;
        while ((bool)draining.GetValue(null))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Backend drain did not complete");
            yield return null;
        }
    }

    private static CharacterSelection Selection(CharacterProfile profile) => new CharacterSelection(
        profile.PlayerId, profile.PlayerObjectName, profile.PlayerDisplayName, profile.IsShooter, profile.IsFighter);

    private static IEnumerator FinishRound(int humanPoints, int cpuPoints)
    {
        object manager = Static("GameLevelManager", "instance");
        IList players = (IList)RealScenePlayModeTestSupport.Invoke(manager, "getSortedGameStatsList");
        Debug.Log("Campaign boundary: finishing round with " + players.Count + " participants");
        Assert.That(players.Count, Is.EqualTo(2));
        foreach (object player in players)
        {
            bool cpu = (bool)player.GetType().GetField("isCpu").GetValue(player);
            object stats = player.GetType().GetField("gameStats").GetValue(player);
            SetProperty(stats, "TotalPoints", cpu ? cpuPoints : humanPoints);
        }
        RealScenePlayModeTestSupport.Invoke(RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance(), "RequestGameOver");
        yield return WaitForScene(Constants.SCENE_NAME_level_00_end_round_screen);
        yield return null;
    }

    private static IEnumerator WaitForScene(string name)
    {
        float deadline = Time.realtimeSinceStartup + 40f;
        while (SceneManager.GetActiveScene().name != name)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "did not reach " + name);
            yield return null;
        }
        yield return null;
    }

    private static object EndMenu => Static("EndRoundMenuManager", "instance");
    private static CampaignNextAction Action => (CampaignNextAction)RealScenePlayModeTestSupport.GetField(EndMenu, "nextAction");

    [UnityTest]
    public IEnumerator FreshNormalCampaignStartsAtAuthoredFirstArenaAndResetsCursor()
    {
        yield return LaunchFreshCampaign(false);
    }

    [UnityTest]
    public IEnumerator FreshHardcoreCampaignStartsAtAuthoredFirstArenaWithoutContinues()
    {
        yield return LaunchFreshCampaign(true);
    }

    private IEnumerator LaunchFreshCampaign(bool hardcore)
    {
        yield return OpenCampaignMenu();
        object start = Static("StartManager", "instance");
        float deadline = Time.realtimeSinceStartup + 90f;
        while (!(bool)RealScenePlayModeTestSupport.Invoke(start, "HasLoadedGameSetup"))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "menu did not become launchable");
            yield return null;
        }
        object selection = RealScenePlayModeTestSupport.GetField(start, "selection");
        SeedRecord(8, false);
        SeedRecord(7, true);
        SetProperty(selection, "ModeIndex", MatchCatalogs.Modes.Definitions.ToList()
            .FindIndex(mode => mode.Id == GameModeId.BeatThaComputahs));
        SetProperty(selection, "LevelIndex", MatchCatalogs.Levels.Count - 1);
        SetProperty(selection, "HardcoreEnabled", hardcore);
        object playerSelect = Property(start, "PlayerSelect");
        playerSelect.GetType().GetMethod("SetMatchContext").Invoke(playerSelect, new object[]
            { MatchCatalogs.Modes.Find(26), new MatchModifiers(hardcoreRequested: hardcore) });
        // Changing context does not silently replace the highlighted character. Select capable
        // human/CPU options through the real menu coordinator before asking the builder to launch.
        for (int attempt = 0; ; attempt++)
        {
            Assert.That(attempt, Is.LessThan(100), "menu has no selectable campaign shooter pair");
            object[] args = { null, null };
            bool ready = (bool)playerSelect.GetType().GetMethod("TryBuildRoster").Invoke(playerSelect, args);
            Assert.That(ready, Is.True, args[1] as string);
            var roster = (PlayerRoster)args[0];
            bool humanReady = roster.GetBySlotId(0).Character.IsShooter;
            bool cpuReady = roster.GetBySlotId(1).Character.IsShooter;
            if (humanReady && cpuReady) break;
            if (!humanReady) playerSelect.GetType().GetMethod("SelectNextPrimary").Invoke(playerSelect, null);
            if (!cpuReady) playerSelect.GetType().GetMethod("SelectNextCpu").Invoke(playerSelect, new object[] { 0 });
        }
        SetStatic("EndRoundData", "currentLevelIndex", 20);
        SetStatic("EndRoundData", "nextLevelIndex", 20);
        SetStatic("EndRoundData", "numberOfContinues", -1);

        RealScenePlayModeTestSupport.Invoke(start, "loadGame");
        Assert.That(ActiveMatch.IsActive, Is.True, "production launch was rejected; inspect its validation reason");
        yield return WaitForScene(Constants.SCENE_NAME_level_01_scrapyard);
        yield return null;

        Assert.That(ActiveMatch.Configuration.ModeId, Is.EqualTo(GameModeId.BeatThaComputahs));
        Assert.That(ActiveMatch.Configuration.LevelId, Is.EqualTo(1));
        Assert.That(ActiveMatch.Configuration.Rules.Hardcore, Is.EqualTo(hardcore));
        Assert.That(Static("EndRoundData", "currentLevelIndex"), Is.Zero);
        Assert.That(Static("EndRoundData", "nextLevelIndex"), Is.Zero);
        Assert.That(Static("EndRoundData", "numberOfContinues"), Is.EqualTo(hardcore ? 0 : 2));
        object manager = Static("GameLevelManager", "instance");
        IList players = (IList)RealScenePlayModeTestSupport.Invoke(manager, "getSortedGameStatsList");
        Assert.That(players.Count, Is.EqualTo(2));
        object data = Static("PlayerData", "instance");
        Assert.That(Property(data, "ThreePointerMade"), Is.EqualTo(hardcore ? 7f : 8f), "launch record filter");
        SetStatic("GameOptions", "hardcoreModeEnabled", !hardcore);
        Type pauseType = Assembly.Load("Level5.Match").GetType("Pause", true);
        object pause = pauseType.GetField("instance").GetValue(null);
        Assert.That(pause, Is.Not.Null);
        var refresh = (Action)RealScenePlayModeTestSupport.GetField(pause, "reloadPlayerData");
        refresh(); // The actual production delegate bound by this scene's GameLevelManager.Start.
        Assert.That(Property(data, "ThreePointerMade"), Is.EqualTo(hardcore ? 7f : 8f),
            "pause refresh must retain the bound match filter after the global preference changes");
    }

    private static void SeedRecord(int makes, bool hardcore)
    {
        object model = Activator.CreateInstance(Production("Assets.Scripts.database.HighScoreModel"));
        model.GetType().GetField("Scoreid").SetValue(model, Guid.NewGuid().ToString("N"));
        model.GetType().GetField("Modeid").SetValue(model, 2);
        model.GetType().GetField("ThreeMade").SetValue(model, makes);
        model.GetType().GetField("HardcoreEnabled").SetValue(model, hardcore ? 1 : 0);
        Assert.That(Production("DBHelper").GetMethod("InsertGameScore")
            .Invoke(Static("DBHelper", "instance"), new[] { model }), Is.True);
    }

    [UnityTest]
    public IEnumerator HiddenBoneyardRetainsItsAuthoredOpponentAndAdvancesByRoutePosition()
    {
        yield return BeginAtRound(14);
        Assert.That(ActiveMatch.Configuration.LevelId, Is.EqualTo(24));
        object manager = Static("GameLevelManager", "instance");
        IList players = (IList)RealScenePlayModeTestSupport.Invoke(manager, "getSortedGameStatsList");
        object cpu = players.Cast<object>().Single(player => (bool)player.GetType().GetField("isCpu").GetValue(player));
        var profile = (CharacterProfile)cpu.GetType().GetField("characterProfile").GetValue(cpu);
        Assert.That(profile.PlayerObjectName, Is.EqualTo("johnny_dracula"));
        yield return FinishRound(100, 0);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.Advance));
        RealScenePlayModeTestSupport.Invoke(EndMenu, "pressNext");
        yield return WaitForScene("level_03_snow");
        Assert.That(Static("EndRoundData", "currentLevelIndex"), Is.EqualTo(15));
        Assert.That(ActiveMatch.Configuration.LevelId, Is.EqualTo(4));
        Assert.That(RememberedMenuLevel(), Is.Zero);
    }

    [UnityTest]
    public IEnumerator CocaineIslandVictoryLoadsDevAndFinalVictorySavesOnceBeforeBackendAndReturnsToMenu()
    {
        yield return BeginAtRound(19);
        yield return FinishRound(100, 0);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.Advance));
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.Zero);
        Assert.That(transport.Requests, Is.Zero, "intermediate rounds must not submit an aggregate");
        RealScenePlayModeTestSupport.Invoke(EndMenu, "pressNext");
        yield return WaitForScene("level_23_dev");
        AssertSubmissionSession("Dev loaded");
        Assert.That(ActiveMatch.Configuration.LevelId, Is.EqualTo(23));
        Assert.That(ActiveMatch.Configuration.Roster.CpuCount, Is.EqualTo(1));
        Assert.That(Static("EndRoundData", "currentLevelIndex"), Is.EqualTo(20));
        Assert.That(Static("GameOptions", "levelSelectedIndex"), Is.Zero, "campaign advance must not overwrite the menu preference");
        AssertSubmissionSession("before final victory");
        yield return FinishRound(100, 0);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.Complete));
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.EqualTo(1));
        RealScenePlayModeTestSupport.Invoke(EndMenu, "saveGame");
        RealScenePlayModeTestSupport.Invoke(EndMenu, "saveGame");
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.EqualTo(1));
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1));
        var pending = PendingMatchResultStore.GetRetryable(owner)[0];
        Assert.That(pending.OwnerPlayerId, Is.EqualTo(owner));
        Assert.That(pending.DefinitiveFailure, Is.False);
        Assert.That(pending.Request.ClientResultId, Is.EqualTo(Guid.Parse(database.ReadOnlyScoreId(26))));
        Assert.That(pending.Request.ModeId, Is.EqualTo(26));
        Assert.That(pending.Request.Metrics["TotalPoints"], Is.EqualTo(200));
        AssertSubmissionSession("after final save");
        yield return AwaitBackendDrain();
        Assert.That(transport.Requests, Is.EqualTo(1));
        Assert.That(transport.LastRequest.RequiresAuth, Is.True);
        Assert.That(transport.LastRequest.Method, Is.EqualTo(ApiHttpMethod.Post));
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1), "503 remains retryable after drain completion");
        Assert.That(transport.RowsAtSubmission, Is.EqualTo(1), "Backend submission follows committed local aggregate");
        yield return AssertDurableAndReturnToMenu();
    }

    [UnityTest]
    public IEnumerator QueuedCampaignPayloadRoundTripsBeforeDeliveryInAStrippedPlayer()
    {
        // Use only the production parameterized constructor: directly calling the default one here
        // would accidentally keep it in the player and conceal the serialization regression.
        var request = new SubmitMatchResultDto(Guid.NewGuid(), 26, 23, "45", "4.0.0", "Desktop",
            new Dictionary<string, double> { [MatchResultMetric.TotalPoints.ToString()] = 200 },
            new MatchResultModifiersDto());
        Assert.That(PendingMatchResultStore.Enqueue(owner, request), Is.EqualTo(PendingMatchResultEnqueueResult.Added));
        Assert.That(File.Exists(Path.Combine(Application.persistentDataPath, "backendv2_pending_match_results.json")), Is.True);
        var restored = PendingMatchResultStore.GetRetryable(owner);
        Assert.That(restored, Has.Count.EqualTo(1));
        Assert.That(restored[0].OwnerPlayerId, Is.EqualTo(owner));
        Assert.That(restored[0].Request.IsEquivalentTo(request), Is.True);
        Assert.That(PendingMatchResultStore.Enqueue(owner, request), Is.EqualTo(PendingMatchResultEnqueueResult.AlreadyQueued));
        Assert.That(PendingMatchResultStore.GetRetryable(Guid.NewGuid()), Is.Empty);
        Assert.That(transport.Requests, Is.Zero, "synchronous persistence needs no authenticated drain");
        yield break;
    }

    [UnityTest]
    public IEnumerator FinalTieRetriesWithoutSpendingAContinue()
    {
        yield return BeginAtRound(20, 1);
        yield return FinishRound(10, 10);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.Retry));
        RealScenePlayModeTestSupport.Invoke(EndMenu, "pressNext");
        yield return WaitForScene("level_23_dev");
        Assert.That(Static("EndRoundData", "numberOfContinues"), Is.EqualTo(1));
        Assert.That(ActiveMatch.Configuration.LevelId, Is.EqualTo(23));
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.Zero);
    }

    [UnityTest]
    public IEnumerator FinalCpuVictorySpendsOneContinueThenEndsRunWhenExhausted()
    {
        yield return BeginAtRound(20, 1);
        yield return FinishRound(0, 100);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.Retry));
        RealScenePlayModeTestSupport.Invoke(EndMenu, "pressNext");
        yield return WaitForScene("level_23_dev");
        Assert.That(Static("EndRoundData", "numberOfContinues"), Is.EqualTo(0));
        yield return FinishRound(0, 100);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.EndRun));
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.EqualTo(1));
        yield return AssertDurableAndReturnToMenu();
    }

    private IEnumerator AssertDurableAndReturnToMenu()
    {
        // Reopen a fresh SQLite connection before leaving the actual end-round UI.
        database.Close();
        yield return database.Open();
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.EqualTo(1));
        RealScenePlayModeTestSupport.Invoke(EndMenu, "pressStartMenu");
        yield return WaitForScene(Constants.SCENE_NAME_level_00_start);
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.EqualTo(1));
        Assert.That(Static("GameOptions", "levelSelectedIndex"), Is.Zero,
            "return to menu retains its last launch preference independently of the campaign cursor");
    }

    private sealed class DurableResultTransport : IApiTransport
    {
        private readonly Func<int> countRows;
        internal DurableResultTransport(Func<int> countRows) { this.countRows = countRows; }
        internal int Requests { get; private set; }
        internal int RowsAtSubmission { get; private set; }
        internal ApiRequest LastRequest { get; private set; }
        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            if (request.RelativePath == "api/v2/match-results")
            {
                LastRequest = request;
                Requests++;
                RowsAtSubmission = countRows();
            }
            completed(RawApiResponse.Completed(503, "{}"));
            yield break;
        }
    }
}

/// <summary>Protects the exact file families touched by the guest campaign fixture.</summary>
internal sealed class CampaignBoundaryPersistenceIsolation : IDisposable
{
    private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

    internal CampaignBoundaryPersistenceIsolation(string root)
    {
        foreach (string name in new[]
        {
            "backendv2_pending_match_results.json", "pending-match-persistence.json", "backendv2_session.json",
            Path.Combine("accounts", "74-pending-progression.json")
        })
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.Combine(root, name + suffix);
                files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }

        // Read every snapshot before mutating any file; a failed read leaves all originals intact.
        try
        {
            foreach (string path in files.Keys)
                if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var file in files)
            if (file.Value == null)
            {
                if (File.Exists(file.Key)) File.Delete(file.Key);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file.Key));
                File.WriteAllBytes(file.Key, file.Value);
            }
        files.Clear();
    }
}

public class CampaignBoundaryPersistenceIsolationTests
{
    [Test]
    public void MissingPersistentDirectoriesAreSupportedWithoutCreatingFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "level5-campaign-absent-" + Guid.NewGuid().ToString("N"));
        using (new CampaignBoundaryPersistenceIsolation(root))
            Assert.That(Directory.Exists(root), Is.False);
        Assert.That(Directory.Exists(root), Is.False);
    }

    [TestCase(true, false)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    [TestCase(false, true)]
    public void GuestProgressionFileFamilySurvivesCleanup(bool originalExists, bool failDuringExecution)
    {
        string root = Path.Combine(Path.GetTempPath(), "level5-campaign-isolation-" + Guid.NewGuid().ToString("N"));
        string guest = Path.Combine(root, "accounts", "74-pending-progression.json");
        string otherAccount = Path.Combine(root, "accounts", "75-pending-progression.json");
        string sessionTemp = Path.Combine(root, "backendv2_session.json.tmp");
        string[] suffixes = { "", ".bak", ".tmp" };
        byte[][] originals = suffixes.Select((_, index) => System.Text.Encoding.UTF8.GetBytes(
            "{\"results\":[{\"resultId\":\"saved-" + index + "\",\"characterId\":1,\"experienceGained\":12.5}]}")).ToArray();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(guest));
            File.WriteAllText(otherAccount, "untouched account");
            File.WriteAllText(sessionTemp, "original temporary session");
            if (originalExists)
                for (int index = 0; index < suffixes.Length; index++)
                    File.WriteAllBytes(guest + suffixes[index], originals[index]);

            bool failureObserved = false;
            try
            {
                using (new CampaignBoundaryPersistenceIsolation(root))
                {
                    foreach (string suffix in suffixes) Assert.That(File.Exists(guest + suffix), Is.False);
                    Assert.That(File.Exists(sessionTemp), Is.False);
                    // Simulate repair consuming a queue, then test gameplay creating its own file family.
                    File.WriteAllBytes(guest, originals[0]);
                    File.Delete(guest);
                    foreach (string suffix in suffixes) File.WriteAllText(guest + suffix, "test-owned progression");
                    File.WriteAllText(sessionTemp, "test-owned session");
                    if (failDuringExecution) throw new InvalidOperationException("simulated boundary failure");
                }
            }
            catch (InvalidOperationException exception) when (exception.Message == "simulated boundary failure")
            {
                failureObserved = true;
            }

            Assert.That(failureObserved, Is.EqualTo(failDuringExecution));
            for (int index = 0; index < suffixes.Length; index++)
                if (originalExists) Assert.That(File.ReadAllBytes(guest + suffixes[index]), Is.EqualTo(originals[index]));
                else Assert.That(File.Exists(guest + suffixes[index]), Is.False);
            Assert.That(File.ReadAllText(otherAccount), Is.EqualTo("untouched account"));
            Assert.That(File.ReadAllText(sessionTemp), Is.EqualTo("original temporary session"));
        }
        finally { Directory.Delete(root, true); }
    }
}
#endif
