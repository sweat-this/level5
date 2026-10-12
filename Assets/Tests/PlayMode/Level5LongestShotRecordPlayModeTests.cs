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

/// <summary>Authored Arcade/Free Play HUD and save paths with injected stats and disposable SQLite; not flight certification.</summary>
public class Level5LongestShotRecordPlayModeTests
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
    private Scene runnerScene;
    private static Type Production(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);

    [SetUp]
    public void SetUp()
    {
        runnerScene = SceneManager.GetActiveScene();
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
        LocalAccountIdentity.UserName = "longest-shot-test";
        BackendV2SessionStore.Clear();
        gameOptions.GetField("hardcoreModeEnabled").SetValue(null, false);
    }

    [UnityTest]
    public IEnumerator AuthoredArcadeAndFreePlayKeepIndependentHistoryAndExistingSavePolicies()
    {
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        IDbConnection connection = (IDbConnection)typeof(LocalProfileTestDatabase)
            .GetProperty("Connection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(database);
        Execute(connection, "INSERT INTO HighScores (modeid, longestShot, hardcoreEnabled) VALUES (98,35,0)");
        Execute(connection, "INSERT INTO HighScores (modeid, longestShot, hardcoreEnabled) VALUES (99,12,0)");
        Execute(connection, "DELETE FROM AllTimeStats");
        Execute(connection, "INSERT INTO AllTimeStats (longestShot) VALUES (5)");
        FieldInfo instance = Production("PlayerData").GetField("instance");
        instance.SetValue(null, null);
        playerData = Object.Instantiate(Resources.Load<GameObject>("Prefabs/critical/player_data"));
        yield return null;
        object data = instance.GetValue(null);
        GameObject levelPrefab = Resources.Load<GameObject>(
            "Prefabs/menu_start/level_selected_objects/level_selected_01_scrapyard");
        level = (LevelDefinition)Production("LevelDefinitionFactory").GetMethod("Create", new[] { Production("LevelSelected") })
            .Invoke(null, new object[] { levelPrefab.GetComponent("LevelSelected") });
        CharacterProfile shooter = Resources.Load<GameObject>(
            "Prefabs/menu_start/default_shooter_profiles/player_selected_johnnyb").GetComponent<CharacterProfile>();
        PlayerRoster roster = PlayerRoster.Build(new[] { PlayerRosterEntry.LocalHuman(new CharacterSelection(
            shooter.PlayerId, shooter.PlayerObjectName, shooter.PlayerDisplayName, shooter.IsShooter, shooter.IsFighter)) });

        foreach (int id in new[] { 98, 99 })
        {
            RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
            Component source = Resources.Load<GameObject>(
                $"Prefabs/menu_start/mode_selected_objects/mode_selected_{id}").GetComponent("StartScreenModeSelected");
            GameModeDefinition mode = (GameModeDefinition)Production("GameModeDefinitionFactory").GetMethod("Create")
                .Invoke(null, new object[] { source });
            modes.Add(mode);
            MatchBuildResult built = new MatchConfigurationBuilder(new GameModeCatalog(new[] { mode }),
                new LevelDefinitionCatalog(new[] { level })).Build(new MatchRequest(mode.Id, 1, roster));
            Assert.That(built.Succeeded, Is.True, built.Validation.ToString());
            ActiveMatch.Begin(built.Configuration);
            Production("LegacyGameOptionsBridge").GetMethod("Apply").Invoke(null, new object[] { built.Configuration });
            PlayerController player = null;
            yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(built.Configuration.SceneName, value => player = value);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(MatchController.instance.IsPlaying, Is.True);
            Assert.That(MatchRuntime.RawModeId, Is.EqualTo(id));
            GameStats stats = player.GetComponent<PlayerIdentifier>().gameStats;
            stats.Stats.LongestShotMade = 20f; // Production stats -> HUD seam; no physical shot claim.
            stats.Stats.ShotAttempt = 1;
            MonoBehaviour hud = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
                SceneManager.GetActiveScene(), "MatchHudPresenter");
            hud.GetType().GetMethod("SetScoreDisplayText").Invoke(hud, null);
            Assert.That(GameObject.Find("display_high_score").GetComponent<Text>().text,
                Does.StartWith("high score : " + (id == 98 ? "35.00" : "12.00")));
            Assert.That(GameObject.Find("display_current_score").GetComponent<Text>().text,
                Does.StartWith("longest shot : 20.00"));
            Assert.That((float)Production("PlayerData").GetProperty("LongestShotMadeFreePlay").GetValue(data),
                Is.EqualTo(id == 98 ? 12f : 20f));
            Assert.That((float)Production("PlayerData").GetProperty("LongestShotMadeArcade").GetValue(data), Is.EqualTo(35f));
            Assert.That(Convert.ToSingle(Execute(connection, "SELECT longestShot FROM AllTimeStats")), Is.EqualTo(20f));
            Assert.That(Convert.ToInt32(Execute(connection, "SELECT COUNT(*) FROM HighScores")), Is.EqualTo(id == 98 ? 2 : 3));
            Assert.That(stats.getExperienceGainedFromSession(), id == 98 ? Is.EqualTo(0) : Is.GreaterThan(0));

            object rules = RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance();
            MonoBehaviour pause = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
                SceneManager.GetActiveScene(), "Pause");
            // The first iterator step runs the real quit policy and its save adapter, then yields
            // the database wait. Stop there so the runner never calls Application.Quit.
            IEnumerator quit = (IEnumerator)pause.GetType().GetMethod("Quit").Invoke(pause, null);
            Assert.That(quit.MoveNext(), Is.True);
            if (id == 98)
            {
                Assert.That(database.CountRows("HighScores", "modeid", 98), Is.EqualTo(1),
                    "manual Arcade exit retains its existing no-history-save policy");
                Assert.That(Convert.ToInt32(Execute(connection,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'MatchPersistenceLedger'")),
                    Is.Zero, "manual Arcade exit must not create the lazy all-time result ledger");
                RealScenePlayModeTestSupport.Invoke(rules, "OnPlayerDied");
                float deadline = Time.realtimeSinceStartup + 20f;
                while (MatchController.instance.Phase != MatchPhase.Completed)
                {
                    Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Arcade end persistence did not complete");
                    yield return null;
                }
                RealScenePlayModeTestSupport.Invoke(rules, "HandleMatchEnded");
                Assert.That(database.CountRows("HighScores", "modeid", 98), Is.EqualTo(2), "ordinary Arcade result saved once");
                Assert.That(database.CountRows("MatchPersistenceLedger"), Is.EqualTo(1));
            }
            else
            {
                Assert.That(database.CountRows("HighScores", "modeid", 99), Is.EqualTo(2));
                Assert.That(database.CountRows("MatchPersistenceLedger"), Is.EqualTo(2));
                Production("PlayerData").GetMethod("loadStatsFromDatabase").Invoke(data, null);
                Assert.That((float)Production("PlayerData").GetProperty("LongestShotMadeFreePlay").GetValue(data), Is.EqualTo(20f));
                Assert.That((float)Production("PlayerData").GetProperty("LongestShotMadeArcade").GetValue(data), Is.EqualTo(35f));
            }
        }
    }

    private static object Execute(IDbConnection connection, string sql)
    {
        using (IDbCommand command = connection.CreateCommand())
        {
            command.CommandText = sql;
            return command.ExecuteScalar();
        }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            ActiveMatch.Clear();
            // Setup/asset failures can leave the runner in its original scene. Unloading that
            // scene stops its coroutine before NUnit can report the failure.
            if (SceneManager.GetActiveScene() != runnerScene)
                yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("longest-shot-cleanup");
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
