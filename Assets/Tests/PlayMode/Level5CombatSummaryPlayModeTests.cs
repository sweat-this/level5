#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
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
using UnityEngine.UI;

/// <summary>Authored combat arenas, real health/death and GameRules.Update, disposable persistence.
/// Kills and the start timestamp are seeded; this does not certify combat input or enemy AI.</summary>
public class Level5CombatSummaryPlayModeTests
{
    private readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
    private LocalProfileTestDatabase database;
    private BackendV2Session previousSession;
    private int previousUserId;
    private string previousUserName;
    private string accountId;
    private bool queueBlocked;
    private object gameRules;
    private Text summary;
    private GameStats stats;
    private readonly List<string> enemyStartupErrors = new List<string>();
    private string QueuePath => Path.Combine(Application.persistentDataPath, "pending-match-persistence.json");

    [SetUp]
    public void SetUp()
    {
        previousSession = BackendV2SessionStore.Current;
        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
        enemyStartupErrors.Clear();
        foreach (string name in new[] { "backendv2_pending_match_results.json", "pending-match-persistence.json", "backendv2_session.json" })
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string path = Path.Combine(Application.persistentDataPath, name + suffix);
                Assert.That(Directory.Exists(path), Is.False, "refusing to replace an existing directory");
                files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
        try
        {
            foreach (string path in files.Keys) File.Delete(path);
            BackendV2SessionStore.Clear();
            LocalAccountIdentity.UserId = 0;
            accountId = "combat-summary-test-" + Guid.NewGuid().ToString("N");
            LocalAccountIdentity.UserName = accountId;
            RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        }
        catch { RestoreFiles(); throw; }
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            ActiveMatch.Clear();
            yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("combat-summary-cleanup");
        }
        finally
        {
            try
            {
                database?.Dispose();
                if (accountId != null)
                    Assembly.Load("Assembly-CSharp").GetType("PendingProgressionStore", true)
                        .GetMethod("DeleteAccountFiles").Invoke(null, new object[] { accountId });
                BackendV2SessionStore.Clear();
                if (previousSession != null) BackendV2SessionStore.Set(previousSession);
                LocalAccountIdentity.UserId = previousUserId;
                LocalAccountIdentity.UserName = previousUserName;
            }
            finally
            {
                Application.logMessageReceived -= CaptureEnemyStartupErrors;
                RestoreFiles();
            }
        }
    }

    private void CaptureEnemyStartupErrors(string message, string stackTrace, LogType type)
    {
        if (message.StartsWith("EnemyHealth on ", StringComparison.Ordinal)
            || message.Contains("Can't call Animator.Update on inactive object"))
            enemyStartupErrors.Add(message);
    }

    private void RestoreFiles()
    {
        if (queueBlocked)
        {
            Directory.Delete(QueuePath);
            queueBlocked = false;
        }
        foreach (var file in files)
            if (file.Value == null) File.Delete(file.Key);
            else File.WriteAllBytes(file.Key, file.Value);
        files.Clear();
    }

    private IEnumerator Launch(GameModeId mode, int arena)
    {
        Application.logMessageReceived += CaptureEnemyStartupErrors;
        // Unity gives the test coroutine a new log scope after SetUp. Match the existing
        // real-scene fixtures here too; arena startup noise is outside the summary assertions.
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        yield return null;
        yield return null;
        float deadline = Time.realtimeSinceStartup + 90f;
        while (!MatchCatalogs.IsReady || GameObject.Find("press_start") == null)
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "production menu/catalogs did not load");
            yield return null;
        }
        CharacterProfile profile = Resources.LoadAll<GameObject>("Prefabs/menu_start/default_shooter_profiles")
            .Select(prefab => prefab.GetComponent<CharacterProfile>()).First(value => value != null && value.IsFighter);
        var roster = PlayerRoster.Build(new[] { PlayerRosterEntry.LocalHuman(new CharacterSelection(
            profile.PlayerId, profile.PlayerObjectName, profile.PlayerDisplayName, profile.IsShooter, profile.IsFighter)) });
        MatchBuildResult result = MatchCatalogs.Builder.Build(new MatchRequest(mode, arena, roster,
            MatchModifiers.Default, source: "GP-001 F6 combat summary regression"));
        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
        ActiveMatch.Begin(result.Configuration);
        Assembly.Load("Assembly-CSharp").GetType("LegacyGameOptionsBridge", true)
            .GetMethod("Apply").Invoke(null, new object[] { result.Configuration });
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(result.Configuration.SceneName, _ => { });
        yield return null;
        Assert.That(MatchController.instance.IsPlaying, Is.True);
        gameRules = RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance();
        stats = RealScenePlayModeTestSupport.GetField<GameStats>(gameRules, "gameStats1");
        Assert.That(stats, Is.Not.Null);
        summary = RealScenePlayModeTestSupport.GetField<Text>(
            RealScenePlayModeTestSupport.GetField(gameRules, "hud"), "displayScoreText");
        Assert.That(summary, Is.Not.Null);
        LocalAccountIdentity.UserId = 0;
        LocalAccountIdentity.UserName = accountId;
        Assert.That(database.EnsureCharacterProfilesForAccount(accountId), Is.True);
        if (Convert.ToInt32(Sql("SELECT COUNT(*) FROM CharacterProfile WHERE accountId = '" + accountId
            + "' AND charid = " + profile.PlayerId)) == 0)
            database.SeedCharacterProfile(accountId, profile.PlayerId);
        stats.Stats.EnemiesKilled = 3;
        stats.Stats.MinionsKilled = 3;
        stats.Stats.TimePlayed = -999f;
        RealScenePlayModeTestSupport.SetField(gameRules, "timePlayedStart", Time.time - 65.125f);
    }

    private void Kill(bool idle = false)
    {
        RealScenePlayModeTestSupport.SetField(gameRules, "killedOnIdle", idle);
        // This is the production damage/death event, not an invocation of the GameRules callback.
        PlayerHealth health = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
        Assert.That(health, Is.Not.Null);
        Assert.That(health.TakeDamage(float.MaxValue), Is.True);
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.PlayerDied));
    }

    private IEnumerator WaitForCompletion()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (MatchController.instance.Phase != MatchPhase.Completed && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Completed));
    }

    private void AssertSummary(bool survival)
    {
        Assert.That(stats.Stats.TimePlayed, Is.GreaterThanOrEqualTo(65.125f));
        Assert.That(summary.text, Does.StartWith("You Bashed up 3 nerds"));
        Assert.That(summary.text, Does.Contain("experience gained : " + stats.getExperienceGainedFromSession()));
        if (survival)
        {
            int minutes = Mathf.FloorToInt(stats.Stats.TimePlayed / 60f);
            string elapsed = minutes.ToString("0") + ":" + (stats.Stats.TimePlayed - minutes * 60f).ToString("00.000");
            Assert.That(summary.text, Does.Contain("You survived for  : " + elapsed));
        }
        else Assert.That(summary.text, Does.Not.Contain("survived"));
        Assert.That(summary.text, Does.Not.Contain("wins"));
    }

    private void AssertPersistence(GameModeId mode)
    {
        Assert.That(enemyStartupErrors, Is.Empty, "real pooled enemy startup must initialize health and animate only while active");
        Assert.That(database.CountRows("HighScores", "modeid", (int)mode), Is.EqualTo(1));
        Assert.That(Convert.ToSingle(Sql("SELECT time FROM HighScores WHERE modeid = " + (int)mode)),
            Is.EqualTo(stats.Stats.TimePlayed).Within(0.001f), "saved duration must match the summary's statistics");
        Assert.That(database.CountRows("MatchPersistenceLedger"), Is.EqualTo(1));
        Assert.That(database.CountRows("ProgressionResultLedger"), Is.EqualTo(1));
        Assert.That((bool)RealScenePlayModeTestSupport.GetField(gameRules, "progressionPersistenceFailed"), Is.False);
    }

    private object Sql(string sql)
    {
        var connection = (IDbConnection)typeof(LocalProfileTestDatabase)
            .GetProperty("Connection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(database);
        using (IDbCommand command = connection.CreateCommand()) { command.CommandText = sql; return command.ExecuteScalar(); }
    }

    [UnityTest]
    public IEnumerator CageDeathShowsFinalSummaryAndRetriesWithoutExtendingTimeOrDuplicatingAwards()
    {
        yield return Launch(GameModeId.CageMatch, 17);
        Assert.That(ActiveMatch.Configuration.Rules.Objective, Is.EqualTo(MatchObjective.Survival));
        Assert.That(ActiveMatch.Configuration.Rules.IsCageMatch, Is.True);
        Assert.That(ActiveMatch.Configuration.Rules.IsBattleRoyal, Is.True);
        Sql("CREATE TEMP TRIGGER refuse_cage_score BEFORE INSERT ON HighScores BEGIN SELECT RAISE(ABORT, 'summary retry test'); END");
        Directory.CreateDirectory(QueuePath); // Prevent the failed score's durable fallback too.
        queueBlocked = true;
        Kill();
        yield return null;
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Ending));
        Assert.That((bool)RealScenePlayModeTestSupport.GetField(gameRules, "matchScoreSaveCompleted"), Is.False);
        AssertSummary(true);
        float endedAt = stats.Stats.TimePlayed;
        string firstSummary = summary.text;
        float firstRetry = (float)RealScenePlayModeTestSupport.GetField(gameRules, "nextMatchEndRetryTime");
        Time.timeScale = 1f; // Exercise a running clock while the real Update retry loop waits.
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.That(Time.time - (float)RealScenePlayModeTestSupport.GetField(gameRules, "timePlayedStart"),
            Is.GreaterThan(endedAt));
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(endedAt));
        Assert.That(summary.text, Is.EqualTo(firstSummary));
        Assert.That((float)RealScenePlayModeTestSupport.GetField(gameRules, "nextMatchEndRetryTime"),
            Is.GreaterThan(firstRetry), "the real Update loop must have made another failed save attempt");
        Assert.That(database.CountRows("HighScores"), Is.Zero);
        Assert.That(database.CountRows("ProgressionResultLedger"), Is.EqualTo(1));
        Sql("DROP TRIGGER refuse_cage_score");
        Directory.Delete(QueuePath);
        queueBlocked = false;
        yield return WaitForCompletion();
        AssertSummary(true);
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(endedAt));
        AssertPersistence(GameModeId.CageMatch);
        RealScenePlayModeTestSupport.Invoke(gameRules, "HandleMatchEnded");
        RealScenePlayModeTestSupport.Invoke(gameRules, "setTimePlayed");
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(endedAt));
        AssertPersistence(GameModeId.CageMatch);
    }

    [UnityTest]
    public IEnumerator CageIdleDeathKeepsSpecialMessage()
    {
        yield return Launch(GameModeId.CageMatch, 17);
        Kill(idle: true);
        yield return WaitForCompletion();
        Assert.That(summary.text, Is.EqualTo("You dead bruh"));
        AssertPersistence(GameModeId.CageMatch);
    }

    [UnityTest]
    public IEnumerator CageWithoutSummaryTextStillPersistsResults()
    {
        yield return Launch(GameModeId.CageMatch, 17);
        RealScenePlayModeTestSupport.SetField(RealScenePlayModeTestSupport.GetField(gameRules, "hud"),
            "displayScoreText", null);
        Kill();
        yield return WaitForCompletion();
        AssertPersistence(GameModeId.CageMatch);
    }

    [UnityTest]
    public IEnumerator BattleRoyalDeathRetainsSurvivalSummary()
    {
        yield return Launch(GameModeId.BattleRoyal, 17);
        Assert.That(ActiveMatch.Configuration.Rules.Objective, Is.EqualTo(MatchObjective.Survival));
        Kill();
        yield return WaitForCompletion();
        AssertSummary(true);
        AssertPersistence(GameModeId.BattleRoyal);
    }

    [UnityTest]
    public IEnumerator BattleRoyalSaveExitsRecordALongLiveRunOnceAndPreserveTheAcceptedCause()
    {
        yield return Launch(GameModeId.BattleRoyal, 17);
        RealScenePlayModeTestSupport.SetField(gameRules, "timePlayedStart", Time.time - 14400f);
        Pause pause = Pause.instance;
        Assert.That(pause, Is.Not.Null);
        pause.TogglePause();
        Assert.That(RealScenePlayModeTestSupport.GetField<Text>(pause, "loadStartScreenText").text,
            Is.EqualTo(Pause.BattleRoyalSaveMenuLabel));
        Assert.That(RealScenePlayModeTestSupport.GetField<Text>(pause, "quitGameText").text,
            Is.EqualTo(Pause.BattleRoyalSaveQuitLabel));
        PlayerHealth health = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
        Assert.That(health.IsDead, Is.False);

        // Execute each real exit's durability step, stopping before process quit or scene navigation.
        IEnumerator quit = pause.Quit();
        Assert.That(quit.MoveNext(), Is.True);
        (quit as IDisposable)?.Dispose();
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Completed));
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.PlayerStopped));
        Assert.That(health.IsDead, Is.False);
        Assert.That(stats.Stats.TimePlayed, Is.GreaterThanOrEqualTo(14400f));
        Assert.That(Convert.ToInt32(Sql("SELECT enemiesKilled FROM HighScores WHERE modeid = 21")), Is.EqualTo(3));
        AssertSummary(true);
        AssertPersistence(GameModeId.BattleRoyal);
        float stoppedAt = stats.Stats.TimePlayed;

        IEnumerator menu = pause.loadstartScreen();
        Assert.That(menu.MoveNext(), Is.True);
        (menu as IDisposable)?.Dispose();
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(stoppedAt));
        AssertPersistence(GameModeId.BattleRoyal);
        health.TakeDamage(float.MaxValue); // A later death must not replace or save the accepted run again.
        yield return null;
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.PlayerStopped));
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(stoppedAt));
        AssertPersistence(GameModeId.BattleRoyal);
    }

    [UnityTest]
    public IEnumerator BattleRoyalFailedSaveBlocksQuitMenuAndRestartUntilTheSameRunIsDurable()
    {
        yield return Launch(GameModeId.BattleRoyal, 17);
        Pause pause = Pause.instance;
        // The gameplay harness suppresses Pause and unregisters its buttons. Restore the real
        // component before exercising onClick, rather than bypassing the production UI handlers.
        pause.enabled = true;
        Assert.That(pause.StartOnPause, Is.False);
        Assert.That(pause.TogglePause(), Is.True);
        Assert.That(pause.TogglePause(), Is.False, "a live run must still allow ordinary resume");
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(pause.TogglePause(), Is.True);
        string resultId = MatchSession.EnsureCurrentMatch();
        Sql("CREATE TEMP TRIGGER refuse_battle_score BEFORE INSERT ON HighScores BEGIN SELECT RAISE(ABORT, 'survival exit retry test'); END");
        // Startup repair may have written an empty queue. This fixture snapshots its whole file
        // family in SetUp; verify no pending results exist before replacing the primary with a blocker.
        if (File.Exists(QueuePath))
        {
            string queue = File.ReadAllText(QueuePath);
            Assert.That(queue, Does.Contain("\"scores\": []"));
            Assert.That(queue, Does.Contain("\"allTime\": []"));
            File.Delete(QueuePath);
        }
        Directory.CreateDirectory(QueuePath);
        queueBlocked = true;
        RealScenePlayModeTestSupport.GetField<Button>(pause, "quitGameButton").onClick.Invoke();
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Ending));
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.PlayerStopped));
        Assert.That(RealScenePlayModeTestSupport.GetField<Text>(pause, "quitGameText").text,
            Is.EqualTo(Pause.ExitNotSavedMessage));
        Assert.That(database.CountRows("HighScores"), Is.Zero);
        float stoppedAt = stats.Stats.TimePlayed;
        int stoppedKills = stats.Stats.EnemiesKilled;
        // Exercise the exact shared API used by mobile double-tap Cancel, which bypasses onClick.
        Assert.That(pause.TogglePause(), Is.True);
        Assert.That(pause.Paused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Ending));
        Assert.That(pause.Paused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(stoppedAt));
        Assert.That(stats.Stats.EnemiesKilled, Is.EqualTo(stoppedKills));
        Assert.That(database.CountRows("HighScores"), Is.Zero);
        RealScenePlayModeTestSupport.GetField<Button>(pause, "loadStartScreenButton").onClick.Invoke();
        Assert.That(RealScenePlayModeTestSupport.GetField<Text>(pause, "loadStartScreenText").text,
            Is.EqualTo(Pause.ExitNotSavedMessage));
        pause.reloadScene();
        Assert.That(MatchSession.EnsureCurrentMatch(), Is.EqualTo(resultId));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(ActiveMatch.Configuration.SceneName));
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(stoppedAt));

        Sql("DROP TRIGGER refuse_battle_score");
        Directory.Delete(QueuePath);
        queueBlocked = false;
        yield return WaitForCompletion(); // Existing unscaled-time GameRules.Update retry, even while paused.
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(stoppedAt));
        Assert.That(stats.Stats.EnemiesKilled, Is.EqualTo(stoppedKills));
        Assert.That(pause.TogglePause(), Is.True, "a completed stopped run must not resume either");
        Assert.That(Time.timeScale, Is.Zero);
        AssertPersistence(GameModeId.BattleRoyal);
        IEnumerator quit = pause.Quit();
        Assert.That(quit.MoveNext(), Is.True, "durability must release the exit gate");
        (quit as IDisposable)?.Dispose();
        AssertPersistence(GameModeId.BattleRoyal);
    }

    [UnityTest]
    public IEnumerator BattleRoyalDeathBeforeSaveExitKeepsPlayerDiedAndOneResult()
    {
        yield return Launch(GameModeId.BattleRoyal, 17);
        Kill();
        IEnumerator quit = Pause.instance.Quit();
        Assert.That(quit.MoveNext(), Is.True);
        (quit as IDisposable)?.Dispose();
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.PlayerDied));
        AssertPersistence(GameModeId.BattleRoyal);
    }

    [UnityTest]
    public IEnumerator BattleRoyalSaveMenuAcceptsDurableFallbackAndRepairsOneScore()
    {
        yield return Launch(GameModeId.BattleRoyal, 17);
        Pause.instance.TogglePause();
        Sql("CREATE TEMP TRIGGER refuse_battle_score BEFORE INSERT ON HighScores BEGIN SELECT RAISE(ABORT, 'survival exit fallback test'); END");
        IEnumerator menu = Pause.instance.loadstartScreen();
        Assert.That(menu.MoveNext(), Is.True);
        (menu as IDisposable)?.Dispose();
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Completed));
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.PlayerStopped));
        Assert.That(database.CountRows("HighScores"), Is.Zero);
        Assert.That(File.Exists(QueuePath), Is.True, "the score must be durable before leaving");
        Sql("DROP TRIGGER refuse_battle_score");
        Assembly.Load("Assembly-CSharp").GetType("PendingMatchPersistenceStore", true)
            .GetMethod("Repair").Invoke(null, null);
        AssertPersistence(GameModeId.BattleRoyal);
        Assert.That(Convert.ToInt32(Sql("SELECT enemiesKilled FROM HighScores WHERE modeid = 21")), Is.EqualTo(3));
        Assert.That((bool)gameRules.GetType().GetMethod("TrySaveBattleRoyalForExit").Invoke(gameRules, null), Is.True);
        AssertPersistence(GameModeId.BattleRoyal);
    }

    [UnityTest]
    public IEnumerator BashDeathRetainsKillsAndExperienceSummary()
    {
        yield return Launch(GameModeId.BashUpSomeNerds, 1);
        Kill();
        yield return WaitForCompletion();
        AssertSummary(false);
        AssertPersistence(GameModeId.BashUpSomeNerds);
    }
}
#endif
