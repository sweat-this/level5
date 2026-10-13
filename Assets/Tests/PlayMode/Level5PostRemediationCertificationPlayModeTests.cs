#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.BackendV2;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Bounded certification probes. Natural observations are printed separately from controlled
/// callbacks; a passing probe does not certify autonomous participation, combat input or ball flight.
/// Run only in the disposable certification project/company namespace documented in the audit.
/// </summary>
public class Level5PostRemediationCertificationPlayModeTests
{
    private CampaignBoundaryPersistenceIsolation persistence;
    private LocalProfileTestDatabase database;
    private BackendV2Session previousSession;
    private int previousUserId;
    private string previousUserName;
    private bool previousIgnoreLogs;
    private bool enteredIsolation;
    private Scene runnerScene;
    private readonly Dictionary<FieldInfo, object> options = new Dictionary<FieldInfo, object>();
    private readonly Dictionary<string, int> errors = new Dictionary<string, int>();
    private object gameRules;
    private PlayerController human;
    private GameStats stats;
    private Text summary;
    private static Type Production(string name) => Assembly.Load("Assembly-CSharp").GetType(name, true);
    private static Component[] ActiveEnemies() => ((IEnumerable)Production("EnemyController")
        .GetProperty("ActiveEnemies").GetValue(null)).Cast<Component>().Where(e => e != null).ToArray();

    [SetUp]
    public void SetUp()
    {
        // The existing older scene fixtures do not all isolate account queues. This certification
        // invocation additionally uses a fresh company directory, never the normal player directory.
        if (Application.companyName != "level5-audit-20261012-14b2cdc")
            Assert.Ignore("Requires the disposable certification company namespace; see the dated audit.");
        runnerScene = SceneManager.GetActiveScene();
        enteredIsolation = true;
        previousSession = BackendV2SessionStore.Current;
        previousUserId = LocalAccountIdentity.UserId;
        previousUserName = LocalAccountIdentity.UserName;
        previousIgnoreLogs = LogAssert.ignoreFailingMessages;
        foreach (FieldInfo field in Production("GameOptions").GetFields(BindingFlags.Public | BindingFlags.Static))
            if (!field.IsLiteral && !field.IsInitOnly) options[field] = field.GetValue(null);
        persistence = new CampaignBoundaryPersistenceIsolation(Application.persistentDataPath);
        BackendV2SessionStore.Clear();
        LocalAccountIdentity.UserId = 74;
        LocalAccountIdentity.UserName = "guest";
        errors.Clear();
        Application.logMessageReceived += CaptureError;
    }

    private void CaptureError(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        string key = message.Split('\n')[0] + " | " + stack.Split('\n').FirstOrDefault();
        errors[key] = errors.TryGetValue(key, out int count) ? count + 1 : 1;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (!enteredIsolation) yield break;
        try
        {
            ActiveMatch.Clear();
            // An initialization failure can leave the coroutine in the original runner scene.
            // Keep that scene alive so NUnit can report the failure and finish cleanup.
            if (SceneManager.GetActiveScene() != runnerScene)
                yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("post-remediation-cleanup");
        }
        finally
        {
            try
            {
                database?.Dispose();
                BackendV2SessionStore.Clear();
                if (previousSession != null) BackendV2SessionStore.Set(previousSession);
                foreach (var option in options) option.Key.SetValue(null, option.Value);
                LocalAccountIdentity.UserId = previousUserId;
                LocalAccountIdentity.UserName = previousUserName;
            }
            finally
            {
                Application.logMessageReceived -= CaptureError;
                foreach (var error in errors) TestContext.WriteLine($"OBSERVED ERROR {error.Value}x: {error.Key}");
                persistence?.Dispose();
                options.Clear();
                enteredIsolation = false;
                LogAssert.ignoreFailingMessages = previousIgnoreLogs;
            }
        }
    }

    private IEnumerator Launch(int modeId, int arenaId, bool withCpu = false, bool checkCpuStartup = false)
    {
        if (checkCpuStartup)
        {
            LogAssert.ignoreFailingMessages = false;
            // D3 is an existing, separately scoped role diagnostic. No exception is accepted.
            if (withCpu)
                LogAssert.Expect(LogType.Error, new Regex("^PlayerAnimationEvents on .* found no PlayerController on its hierarchy; attack boxes and lunge events will not run\\.$"));
        }
        else RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        float deadline = Time.realtimeSinceStartup + 90f;
        do
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "production loading/menu did not become ready");
            yield return null;
        }
        while (!MatchCatalogs.IsReady || GameObject.Find("press_start") == null);
        CharacterSelection Select(CharacterProfile p) => new CharacterSelection(
            p.PlayerId, p.PlayerObjectName, p.PlayerDisplayName, p.IsShooter, p.IsFighter);
        CharacterProfile profile = Resources.Load<GameObject>(
            "Prefabs/menu_start/default_shooter_profiles/player_selected_johnnyb").GetComponent<CharacterProfile>();
        if (modeId == 21)
            profile = Resources.LoadAll<GameObject>("Prefabs/menu_start/default_shooter_profiles")
                .Select(p => p.GetComponent<CharacterProfile>()).First(p => p != null && p.IsFighter);
        var entries = new List<PlayerRosterEntry> { PlayerRosterEntry.LocalHuman(Select(profile)) };
        if (withCpu)
        {
            CharacterProfile cpu = Resources.LoadAll<GameObject>("Prefabs/menu_start/cpu_players_selected_objects")
                .Select(p => p.GetComponent<CharacterProfile>()).First(p => p != null && p.PlayerObjectName == "ian");
            entries.Add(PlayerRosterEntry.Cpu(Select(cpu)));
        }
        MatchBuildResult build = MatchCatalogs.Builder.Build(new MatchRequest(
            (GameModeId)modeId, arenaId, PlayerRoster.Build(entries), MatchModifiers.Default,
            source: "post-GP-001 bounded certification probe"));
        Assert.That(build.Succeeded, Is.True, build.Validation.ToString());
        ActiveMatch.Begin(build.Configuration);
        Production("LegacyGameOptionsBridge").GetMethod("Apply").Invoke(null, new object[] { build.Configuration });
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(build.Configuration.SceneName, p => human = p);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.That(MatchController.instance.IsPlaying, Is.True);
        gameRules = RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance();
        stats = RealScenePlayModeTestSupport.GetField<GameStats>(gameRules, "gameStats1");
        summary = RealScenePlayModeTestSupport.GetField<Text>(
            RealScenePlayModeTestSupport.GetField(gameRules, "hud"), "displayScoreText");
        Assert.That(stats, Is.Not.Null);
        Assert.That(summary, Is.Not.Null);
        TestContext.WriteLine($"LAUNCH mode={modeId} scene={build.Configuration.SceneName} roster={build.Configuration.Roster.Count}");
    }

    private static T Overlay<T>(string name) where T : Component
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(c => c.name == name);

    private void CheckNoCpuStartupException()
    {
        Assert.That(errors.Keys.Any(e => e.Contains("BasketBallAuto.Start")), Is.False,
            "CPU startup must never throw, including the original inactive textBackground lookup");
    }

    private void ToggleHumanOverlay()
    {
        BasketBall ball = BasketBall.instance;
        GameObject background = Overlay<Transform>("textBackground").gameObject;
        Text score = Overlay<Text>("shootStatsTextObject");
        Text profile = Overlay<Text>("shooterProfileTextObject");
        Assert.That(ball.UiStatsEnabled, Is.False);
        Assert.That(background.activeSelf, Is.False);
        Assert.That(score.text, Is.Empty);
        Assert.That(profile.text, Is.Empty);
        ball.toggleUiStats();
        Assert.That(ball.displayUiStats(), Is.True);
        Assert.That(background.activeSelf, Is.True);
        Assert.That(score.text, Does.Contain("points : " + stats.TotalPoints));
        Assert.That(profile.text, Is.Not.Empty);
        ball.toggleUiStats();
        Assert.That(ball.displayUiStats(), Is.False);
        Assert.That(background.activeSelf, Is.False);
        Assert.That(score.text, Is.Empty);
        Assert.That(profile.text, Is.Empty);
    }

    [UnityTest]
    public IEnumerator VersusCpuFallbackScoresRenderOnMatchHudAndDoNotSurviveSceneReload()
    {
        yield return Launch(23, 1, withCpu: true, checkCpuStartup: true);
        MatchHudPresenter Hud() => (MatchHudPresenter)RealScenePlayModeTestSupport.GetField(gameRules, "hud");
        Text[] Panels() => Object.FindObjectsByType<Text>(FindObjectsInactive.Include)
            .Where(t => Regex.IsMatch(t.name, "^display_p[1-4]_score$")).OrderBy(t => t.name).ToArray();
        void CheckPanels(Text[] panels)
        {
            Assert.That(Hud().IsComplete, Is.True);
            Assert.That(panels, Has.Length.EqualTo(4));
            Canvas canvas = summary.GetComponentInParent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            foreach (Text panel in panels)
            {
                Assert.That(panel.transform.parent, Is.SameAs(canvas.transform));
                Assert.That(panel.font, Is.Not.Null);
                Assert.That(panel.raycastTarget, Is.False);
            }
            foreach (Text panel in panels.Take(2))
            {
                Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                Assert.That(panel.canvas, Is.SameAs(canvas));
                Assert.That(panel.cachedTextGenerator.vertexCount, Is.GreaterThan(0));
            }
            Assert.That(panels[2].gameObject.activeSelf, Is.False);
            Assert.That(panels[3].gameObject.activeSelf, Is.False);
        }

        // Inject values at the presentation boundary; this does not simulate made shots.
        stats.Stats.TotalPoints = 9;
        Object.FindAnyObjectByType<BasketBallAuto>().GameStats.Stats.TotalPoints = 3;
        yield return null;
        Canvas.ForceUpdateCanvases();
        Text[] original = Panels();
        CheckPanels(original);
        Assert.That(original[0].text, Does.StartWith("Player 1\nJohnny B\npoints : 9"));
        Assert.That(original[1].text, Does.StartWith("CPU 2\nIan\npoints : 3"));
        Hud().Initialize();
        Assert.That(Panels(), Is.EqualTo(original));

        LogAssert.Expect(LogType.Error, new Regex("^PlayerAnimationEvents on .* found no PlayerController on its hierarchy; attack boxes and lunge events will not run\\.$"));
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(ActiveMatch.Configuration.SceneName, p => human = p);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.That(original.All(t => t == null), Is.True);
        gameRules = RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance();
        stats = RealScenePlayModeTestSupport.GetField<GameStats>(gameRules, "gameStats1");
        summary = RealScenePlayModeTestSupport.GetField<Text>(Hud(), "displayScoreText");
        Canvas.ForceUpdateCanvases();
        CheckPanels(Panels());
        yield return ExpireCountdown();
        Assert.That(summary.text, Does.Contain("wins!"));
        Assert.That(errors.Keys.All(e => e.StartsWith("PlayerAnimationEvents on ")), Is.True,
            string.Join("\n", errors.Keys));
    }

    [UnityTest]
    public IEnumerator Mode16CpuStartsWithHiddenHumanOverlayAndStillMovesShootsAndOwnsItsStats()
    {
        yield return Launch(16, 1, withCpu: true, checkCpuStartup: true);
        CheckNoCpuStartupException();
        ToggleHumanOverlay();
        BasketBallAuto cpu = Object.FindAnyObjectByType<BasketBallAuto>();
        AutoPlayerController actor = Object.FindAnyObjectByType<AutoPlayerController>();
        Assert.That(cpu, Is.Not.Null);
        Assert.That(actor, Is.Not.Null);
        Assert.That(cpu.ParticipantId, Is.EqualTo(1));
        Assert.That(cpu.OwnerActor, Is.SameAs(actor.gameObject));
        Assert.That(cpu.GameStats, Is.Not.SameAs(stats));
        Assert.That(cpu.GameStats.Stats, Is.Not.SameAs(stats.Stats));
        Vector3 initial = actor.transform.position;
        int attempts = cpu.GameStats.Stats.ShotAttempt;
        int humanAttempts = stats.Stats.ShotAttempt;
        int humanPoints = stats.TotalPoints;
        yield return new WaitForSeconds(12f);
        Assert.That(Vector3.Distance(initial, actor.transform.position), Is.GreaterThan(0.01f));
        Assert.That(cpu.GameStats.Stats.ShotAttempt, Is.GreaterThan(attempts));
        Assert.That(stats.Stats.ShotAttempt, Is.EqualTo(humanAttempts));
        Assert.That(stats.TotalPoints, Is.EqualTo(humanPoints));
        TestContext.WriteLine($"D1 NATURAL CPU displacement={Vector3.Distance(initial, actor.transform.position)} "
            + $"attempts={cpu.GameStats.Stats.ShotAttempt - attempts} points={cpu.GameStats.TotalPoints}");
        ToggleHumanOverlay();
        CheckNoCpuStartupException();
    }

    [UnityTest]
    public IEnumerator VersusCpuStartupPreservesHumanOverlayAndSeparateStatistics()
    {
        yield return Launch((int)GameModeId.VersusCpu, 1, withCpu: true, checkCpuStartup: true);
        BasketBallAuto cpu = Object.FindAnyObjectByType<BasketBallAuto>();
        Assert.That(cpu, Is.Not.Null);
        Assert.That(cpu.GameStats, Is.Not.SameAs(stats));
        ToggleHumanOverlay();
        int before = cpu.GameStats.Stats.ShotAttempt;
        yield return new WaitForSeconds(12f);
        Assert.That(cpu.GameStats.Stats.ShotAttempt, Is.GreaterThan(before));
        Assert.That(stats.Stats.ShotAttempt, Is.EqualTo(0));
        CheckNoCpuStartupException();
    }

    [UnityTest]
    public IEnumerator SoloHumanRetainsProductionOverlayToggleBehavior()
    {
        yield return Launch(16, 1, checkCpuStartup: true);
        Assert.That(Object.FindAnyObjectByType<BasketBallAuto>(), Is.Null);
        ToggleHumanOverlay();
        CheckNoCpuStartupException();
    }

    private IEnumerator ExpireCountdown()
    {
        human.Grounded = true;
        human.InAir = false;
        BasketBall.instance.BasketBallState.Thrown = false;
        RealScenePlayModeTestSupport.SetField(Timer.instance, "currentTime", Timer.instance.TimeStart);
        yield return Complete(MatchEndCause.TimeExpired);
    }

    private IEnumerator Complete(MatchEndCause cause)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (MatchController.instance.Phase != MatchPhase.Completed && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Completed));
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(cause));
        TestContext.WriteLine($"TERMINAL cause={cause} time={stats.Stats.TimePlayed} summary={summary.text}");
        Assert.That(database.CountRows("HighScores", "modeid", MatchRuntime.RawModeId), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Mode16ObservesCpuThenChecksControlledMarkerOwnershipAndTimeout()
    {
        yield return Launch(16, 1, withCpu: true);
        BasketBallAuto cpu = Object.FindAnyObjectByType<BasketBallAuto>();
        Assert.That(cpu, Is.Not.Null);
        BasketBallShotMarker[] markers = Object.FindObjectsByType<BasketBallShotMarker>(FindObjectsInactive.Exclude)
            .Where(m => m.isActiveAndEnabled && m.MarkerEnabled).ToArray();
        Assert.That(markers, Is.Not.Empty);
        int before = cpu.GameStats.Stats.ShotAttempt;
        AutoPlayerController cpuActor = Object.FindAnyObjectByType<AutoPlayerController>();
        Assert.That(cpuActor, Is.Not.Null);
        Vector3 initialPosition = cpuActor.transform.position;
        yield return new WaitForSeconds(12f);
        TestContext.WriteLine($"NATURAL CPU attempts={cpu.GameStats.Stats.ShotAttempt - before} points={cpu.GameStats.Stats.TotalPoints} "
            + $"markerAttempts={markers.Sum(m => m.ShotAttempt)} currentMarker={cpu.BasketBallState.CurrentShotMarker?.name} "
            + $"actorDisplacement={Vector3.Distance(initialPosition, cpuActor.transform.position)}");
        BasketBallShotMarker marker = markers.First(m => m.MarkerEnabled);
        Collider hitbox = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude)
            .First(c => c.CompareTag("autoPlayerHitbox") && c.GetComponentInParent<IBasketballParticipantStateProvider>() != null);
        marker.SendMessage("OnTriggerEnter", hitbox); // Explicit simulated occupancy, separate from natural observations.
        Assert.That(cpu.BasketBallState.CurrentShotMarker, Is.SameAs(marker));
        int cpuPoints = cpu.GameStats.Stats.TotalPoints;
        int humanPoints = stats.Stats.TotalPoints;
        int markerAttempts = marker.ShotAttempt;
        BasketballShotPipeline.ApplyShotAttempt(cpu, false, true, false, false);
        BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(cpu, (IMoneyBallState)gameRules, ActiveMatch.Configuration.Rules);
        Assert.That(marker.ShotAttempt, Is.EqualTo(markerAttempts + 1));
        BasketBallShotMade.instance.SendMessage("OnTriggerEnter", cpu.GetComponent<Collider>());
        BasketBallShotMade.instance.shotMade(cpu); // Explicit simulated hoop make, not physical flight.
        int expectedPoints = markerAttempts + 1 == marker.MaxShotAttempt ? 6 : 3;
        Assert.That(cpu.GameStats.Stats.TotalPoints, Is.EqualTo(cpuPoints + expectedPoints));
        Assert.That(stats.Stats.TotalPoints, Is.EqualTo(humanPoints));
        TestContext.WriteLine($"CONTROLLED CPU marker attempt accepted; owned points delta={cpu.GameStats.Stats.TotalPoints - cpuPoints}");
        yield return ExpireCountdown();
    }

    [UnityTest]
    public IEnumerator Mode27TracksHumanThenChecksControlledBlockAccountingAndTimeout()
    {
        yield return Launch(27, 1);
        Assert.That(ActiveMatch.Configuration.Roster.Count, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<BasketBall>(FindObjectsInactive.Exclude), Has.Length.EqualTo(1));
        Assert.That(Object.FindObjectsByType<BasketBallAuto>(FindObjectsInactive.Exclude), Is.Empty);
        AutoPlayerDefense defense = Object.FindAnyObjectByType<AutoPlayerDefense>();
        Assert.That(defense, Is.Not.Null);
        Assert.That(defense.GuardedPlayer.playerController, Is.SameAs(human));
        Vector3 initial = defense.transform.position;
        human.transform.position += Vector3.right * 2f; // Controlled target displacement; real FixedUpdate/Rigidbody tracking.
        yield return new WaitForSeconds(2f);
        TestContext.WriteLine($"DEFENDER tracking displacement={Vector3.Distance(initial, defense.transform.position)} enabled={defense.enabled}");
        Assert.That(Vector3.Distance(initial, defense.transform.position), Is.GreaterThan(0.01f));
        CollisionCheckDefense block = defense.GetComponentInChildren<CollisionCheckDefense>();
        Assert.That(block, Is.Not.Null);
        int before = stats.Stats.BlockedShots;
        human.InAir = true;
        block.OnTriggerEnter(BasketBall.instance.GetComponent<Collider>()); // Controlled collision callback, not an AI-earned block.
        block.OnTriggerEnter(BasketBall.instance.GetComponent<Collider>());
        Assert.That(stats.Stats.BlockedShots, Is.EqualTo(before + 1));
        Assert.That(defense.blockedShots, Is.EqualTo(1));
        yield return ExpireCountdown();
        Assert.That(summary.text, Does.Contain("You were blocked " + stats.Stats.BlockedShots + " times"));
    }

    [UnityTest]
    public IEnumerator Mode21ObservesStagedPopulationAndChecksCreditedKillDeathAndSummary()
    {
        yield return Launch(21, 17);
        Assert.That(ActiveMatch.Configuration.Rules.Objective, Is.EqualTo(MatchObjective.Survival));
        Assert.That(ActiveMatch.Configuration.Rules.CombatMode, Is.EqualTo(CombatMode.BattleRoyal));
        // Combat retains the primary stats-owning ball, frozen above the arena by BasketBall.Start.
        BasketBall retainedBall = Object.FindAnyObjectByType<BasketBall>();
        Assert.That(retainedBall, Is.Not.Null);
        Assert.That(retainedBall.GetComponent<Rigidbody>().constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
        Assert.That(Object.FindAnyObjectByType<BasketBallAuto>(), Is.Null);
        TestContext.WriteLine($"COMBAT retained stats ball constraints={retainedBall.GetComponent<Rigidbody>().constraints}");
        Component[] initial = ActiveEnemies();
        Assert.That(initial, Is.Not.Empty);
        yield return new WaitForSeconds(11f); // Let the existing ten-second InvokeRepeating stage run.
        Component[] staged = ActiveEnemies();
        TestContext.WriteLine($"POPULATION initial={initial.Length} staged={staged.Length} "
            + $"bosses={staged.Count(e => (bool)e.GetType().GetProperty("IsBoss").GetValue(e))}");
        Assert.That(staged.Length, Is.GreaterThan(initial.Length));
        Assert.That(staged.Length, Is.LessThanOrEqualTo(EnemyPopulationRules.BattleRoyalCap));
        int before = stats.Stats.EnemiesKilled;
        // Controlled disable, not an elimination: an empty registry must not end this survival run.
        foreach (Component enemy in staged) enemy.gameObject.SetActive(false);
        for (int frame = 0; frame < 3; frame++)
        {
            yield return null;
            Assert.That(ActiveEnemies(), Is.Empty);
            Assert.That(MatchController.instance.IsPlaying, Is.True);
            Assert.That(database.CountRows("HighScores", "modeid", 21), Is.Zero);
        }
        Assert.That(stats.Stats.EnemiesKilled, Is.EqualTo(before));
        float deadline = Time.realtimeSinceStartup + 11f;
        while (ActiveEnemies().Length == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Component[] replenished = ActiveEnemies();
        Assert.That(replenished, Is.Not.Empty, "the existing admission schedule must continue after an empty population");
        Assert.That(MatchController.instance.IsPlaying, Is.True);
        TestContext.WriteLine($"EMPTY POPULATION remained Playing; scheduled admissions resumed={replenished.Length}");
        replenished[0].GetType().GetMethod("HandleDeath").Invoke(replenished[0], new object[] { human.gameObject, true });
        // Controlled production death/credit seam, not a player attack.
        Assert.That(stats.Stats.EnemiesKilled, Is.EqualTo(before + 1));
        PlayerHealth health = human.GetComponentInChildren<PlayerHealth>();
        Assert.That(health.TakeDamage(float.MaxValue), Is.True); // Real health event to production match end.
        yield return Complete(MatchEndCause.PlayerDied);
        IDbConnection connection = (IDbConnection)typeof(LocalProfileTestDatabase)
            .GetProperty("Connection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(database);
        using (IDbCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT enemiesKilled, time FROM HighScores WHERE modeid = 21";
            using (IDataReader row = command.ExecuteReader())
            {
                Assert.That(row.Read(), Is.True);
                Assert.That(Convert.ToInt32(row[0]), Is.EqualTo(stats.Stats.EnemiesKilled));
                Assert.That(Convert.ToSingle(row[1]), Is.EqualTo(stats.Stats.TimePlayed).Within(0.001f));
                TestContext.WriteLine($"SQLITE kills={row[0]} survival={row[1]}");
            }
        }
        Assert.That(summary.text, Does.Contain("You Bashed up " + stats.Stats.EnemiesKilled + " nerds"));
        Assert.That(summary.text, Does.Contain("You survived for"));
    }
}
#endif
