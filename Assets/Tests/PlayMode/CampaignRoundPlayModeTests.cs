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
    private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
    private readonly Dictionary<FieldInfo, object> options = new Dictionary<FieldInfo, object>();
    private LocalProfileTestDatabase database;
    private BackendV2Session previousSession;
    private Guid owner;
    private DurableResultTransport transport;
    private int previousLocalUserId;
    private string previousLocalUserName;
    private static Type Production(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static void SetStatic(string type, string field, object value)
        => Production(type).GetField(field).SetValue(null, value);
    private static object Static(string type, string field) => Production(type).GetField(field).GetValue(null);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void SetProperty(object target, string name, object value)
        => target.GetType().GetProperty(name).SetValue(target, value);

    [SetUp]
    public void SetUp()
    {
        previousSession = BackendV2SessionStore.Current;
        previousLocalUserId = LocalAccountIdentity.UserId;
        previousLocalUserName = LocalAccountIdentity.UserName;
        foreach (string type in new[] { "GameOptions", "EndRoundData" })
            foreach (FieldInfo field in Production(type).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (!field.IsLiteral && !field.IsInitOnly) options[field] = field.GetValue(null);
        foreach (string name in new[] { "backendv2_pending_match_results.json", "pending-match-persistence.json", "backendv2_session.json" })
            foreach (string suffix in new[] { "", ".bak" })
            {
                string path = Path.Combine(Application.persistentDataPath, name + suffix);
                files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
        try
        {
            foreach (string path in files.Keys) File.Delete(path);
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
                options.Clear();
            }
            finally { RestoreFiles(); }
        }
    }

    private void RestoreFiles()
    {
        foreach (var file in files)
            if (file.Value == null) File.Delete(file.Key);
            else File.WriteAllBytes(file.Key, file.Value);
        files.Clear();
    }

    private IEnumerator BeginAtRound(int index, int continues = 2)
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
        Debug.Log("Campaign boundary: composing round " + index);
        object playerData = Static("PlayerData", "instance");
        Assert.That(playerData, Is.Not.Null, "production menu must create PlayerData");
        IList route = (IList)Property(playerData, "LevelsList");
        Assert.That(route.Count, Is.EqualTo(21));
        SetStatic("GameOptions", "levelsList", route);
        SetStatic("GameOptions", "levelSelectedIndex", index);
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
    public IEnumerator CocaineIslandVictoryLoadsDevAndFinalVictorySavesOnceBeforeBackendAndReturnsToMenu()
    {
        yield return BeginAtRound(19);
        yield return FinishRound(100, 0);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.Advance));
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.Zero);
        Assert.That(transport.Requests, Is.Zero, "intermediate rounds must not submit an aggregate");
        RealScenePlayModeTestSupport.Invoke(EndMenu, "pressNext");
        yield return WaitForScene("level_23_dev");
        Assert.That(ActiveMatch.Configuration.LevelId, Is.EqualTo(23));
        Assert.That(ActiveMatch.Configuration.Roster.CpuCount, Is.EqualTo(1));
        Assert.That(Static("GameOptions", "levelSelectedIndex"), Is.EqualTo(20));
        yield return FinishRound(100, 0);
        Assert.That(Action, Is.EqualTo(CampaignNextAction.Complete));
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.EqualTo(1));
        RealScenePlayModeTestSupport.Invoke(EndMenu, "saveGame");
        RealScenePlayModeTestSupport.Invoke(EndMenu, "saveGame");
        Assert.That(database.CountRows("HighScores", "modeid", 26), Is.EqualTo(1));
        Assert.That(PendingMatchResultStore.GetRetryable(owner), Has.Count.EqualTo(1));
        Assert.That(transport.Requests, Is.EqualTo(1));
        Assert.That(transport.RowsAtSubmission, Is.EqualTo(1), "Backend submission follows committed local aggregate");
        yield return AssertDurableAndReturnToMenu();
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
    }

    private sealed class DurableResultTransport : IApiTransport
    {
        private readonly Func<int> countRows;
        internal DurableResultTransport(Func<int> countRows) { this.countRows = countRows; }
        internal int Requests { get; private set; }
        internal int RowsAtSubmission { get; private set; }
        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            if (request.RelativePath == "api/v2/match-results")
            {
                Requests++;
                RowsAtSubmission = countRows();
            }
            completed(RawApiResponse.Completed(503, "{}"));
            yield break;
        }
    }
}
#endif
