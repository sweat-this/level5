#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using Level5.BackendV2;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Authored range modes in the real arena HUD, backed only by disposable SQLite.</summary>
public class Level5RangeShotRecordPlayModeTests
{
    private LocalProfileTestDatabase database;
    private GameObject playerData;
    private object previousPlayerData;
    private int previousUserId;
    private string previousUserName;
    private BackendV2Session previousSession;
    private CampaignBoundaryPersistenceIsolation persistence;
    private readonly Dictionary<FieldInfo, object> options = new Dictionary<FieldInfo, object>();
    private readonly List<GameModeDefinition> modes = new List<GameModeDefinition>();
    private LevelDefinition level;
    private static Type Production(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);

    [SetUp]
    public void SetUp()
    {
        previousPlayerData = Production("PlayerData").GetField("instance").GetValue(null);
        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
        previousSession = BackendV2SessionStore.Current;
        Type gameOptions = Production("GameOptions");
        foreach (FieldInfo field in gameOptions.GetFields(BindingFlags.Public | BindingFlags.Static))
            if (!field.IsLiteral && !field.IsInitOnly) options[field] = field.GetValue(null);
        // Protect queues/session mirrors even if an unexpected scene failure ends the match.
        persistence = new CampaignBoundaryPersistenceIsolation(Application.persistentDataPath);
        LocalAccountIdentity.UserId = 74;
        LocalAccountIdentity.UserName = "range-record-test";
        BackendV2SessionStore.Clear();
        gameOptions.GetField("hardcoreModeEnabled").SetValue(null, false);
    }

    [UnityTest]
    public IEnumerator AuthoredRangeModesShowQualifyingPersonalBestsAndCurrentCountsInTheArena()
    {
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        database = new LocalProfileTestDatabase();
        Debug.Log("Range records: opening disposable SQLite");
        yield return database.Open();
        Debug.Log("Range records: disposable SQLite ready");
        // Never enter LoadManager or an end-match save path: those can repair real JSON queues.
        IDbConnection connection = (IDbConnection)typeof(LocalProfileTestDatabase)
            .GetProperty("Connection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(database);
        Debug.Log("Range records: disposable connection resolved");
        using (IDbCommand command = connection.CreateCommand())
        {
            foreach (string values in new[] { "2,100,1,0,0,0", "2,8,8,0,0,0", "3,100,0,1,0,0",
                "3,9,0,9,0,0", "4,100,0,0,1,0", "4,10,0,0,10,0" })
            {
                command.CommandText = "INSERT INTO HighScores (modeid, maxShotMade, threeMade, fourMade, sevenMade, hardcoreEnabled) "
                    + "VALUES (" + values + ")";
                command.ExecuteNonQuery();
            }
        }
        Debug.Log("Range records: disposable scores seeded");
        FieldInfo instance = Production("PlayerData").GetField("instance");
        instance.SetValue(null, null);
        playerData = Object.Instantiate(Resources.Load<GameObject>("Prefabs/critical/player_data"));
        yield return null; // Production PlayerData.Start loads personal bests from our database.
        Debug.Log("Range records: personal bests loaded");

        GameObject levelPrefab = Resources.Load<GameObject>(
            "Prefabs/menu_start/level_selected_objects/level_selected_01_scrapyard");
        Assert.That(levelPrefab, Is.Not.Null);
        level = (LevelDefinition)Production("LevelDefinitionFactory").GetMethod("Create", new[] { Production("LevelSelected") })
            .Invoke(null, new object[] { levelPrefab.GetComponent("LevelSelected") });
        CharacterProfile shooter = Resources.Load<GameObject>(
            "Prefabs/menu_start/default_shooter_profiles/player_selected_johnnyb").GetComponent<CharacterProfile>();
        Assert.That(shooter.IsShooter, Is.True);
        Debug.Log("Range records: selected arena and shooter loaded");
        PlayerRoster roster = PlayerRoster.Build(new[] { PlayerRosterEntry.LocalHuman(new CharacterSelection(
            shooter.PlayerId, shooter.PlayerObjectName, shooter.PlayerDisplayName, shooter.IsShooter, shooter.IsFighter)) });

        foreach (int id in new[] { 2, 3, 4 })
        {
            // Unity coroutine log scopes reset the harness's scene-noise setting.
            RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
            Component source = Resources.Load<GameObject>(
                $"Prefabs/menu_start/mode_selected_objects/mode_selected_{id:D2}").GetComponent("StartScreenModeSelected");
            GameModeDefinition mode = (GameModeDefinition)Production("GameModeDefinitionFactory").GetMethod("Create")
                .Invoke(null, new object[] { source });
            modes.Add(mode);
            MatchBuildResult built = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }),
                new LevelDefinitionCatalog(new[] { level })).Build(new MatchRequest(mode.Id, 1, roster));
            Assert.That(built.Succeeded, Is.True, built.Validation.ToString());
            ActiveMatch.Begin(built.Configuration);
            Debug.Log("Range records: launching authored mode " + id);
            Production("LegacyGameOptionsBridge").GetMethod("Apply").Invoke(null, new object[] { built.Configuration });
            PlayerController player = null;
            yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(built.Configuration.SceneName, value => player = value);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(MatchController.instance.IsPlaying, Is.True);
            Assert.That(MatchRuntime.RawModeId, Is.EqualTo(id));
            GameStats stats = player.GetComponent<PlayerIdentifier>().gameStats;
            stats.Stats.ShotMade = 500;
            stats.Stats.ThreePointerMade = 2;
            stats.Stats.FourPointerMade = 3;
            stats.Stats.SevenPointerMade = 4;
            MonoBehaviour hud = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
                SceneManager.GetActiveScene(), "MatchHudPresenter");
            Assert.That(hud, Is.Not.Null);
            hud.GetType().GetMethod("SetScoreDisplayText").Invoke(hud, null);
            Assert.That(GameObject.Find("display_high_score").GetComponent<Text>().text,
                Is.EqualTo("high score : " + (id + 6)));
            Assert.That(GameObject.Find("display_current_score").GetComponent<Text>().text,
                Does.StartWith((id == 2 ? "3" : id == 3 ? "4" : "7") + "s made : " + id));
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            ActiveMatch.Clear();
            yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("range-record-cleanup");
        }
        finally
        {
            try
            {
                if (playerData != null) Object.DestroyImmediate(playerData);
                Production("PlayerData").GetField("instance").SetValue(null, previousPlayerData);
                database?.Dispose();
                foreach (var option in options) option.Key.SetValue(null, option.Value);
                LocalAccountIdentity.UserId = previousUserId;
                LocalAccountIdentity.UserName = previousUserName;
                BackendV2SessionStore.Clear();
                if (previousSession != null) BackendV2SessionStore.Set(previousSession);
                foreach (GameModeDefinition mode in modes) Object.DestroyImmediate(mode);
                if (level != null) Object.DestroyImmediate(level);
            }
            finally { persistence?.Dispose(); }
        }
    }
}
#endif
