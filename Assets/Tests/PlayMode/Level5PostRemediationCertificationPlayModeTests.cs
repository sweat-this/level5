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

    private BasketBallShotMarker[] FreezeContestForBoundary()
    {
        human.StopAllCoroutines();
        human.enabled = false;
        human.Grounded = true;
        human.InAir = false;
        ((IShooterActor)human).HasBasketball = false;
        human.GetComponent<Rigidbody>().isKinematic = true;
        foreach (IBasketballRuntime ball in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
            .OfType<IBasketballRuntime>())
        {
            ((MonoBehaviour)ball).StopAllCoroutines();
            ((MonoBehaviour)ball).enabled = false;
            ((Component)ball).GetComponent<Rigidbody>().isKinematic = true;
            ball.State.Thrown = false;
            ball.State.InAir = false;
            if (ball.IsCpu)
            {
                var actor = (AutoPlayerController)ball.Actor;
                actor.StopAllCoroutines();
                actor.enabled = false;
                actor.Grounded = true;
                actor.InAir = false;
                ball.Actor.HasBasketball = false;
                actor.GetComponent<Rigidbody>().isKinematic = true;
            }
        }
        Timer.instance.enabled = false;
        BasketBallShotMarker[] markers = Object.FindObjectsByType<BasketBallShotMarker>(FindObjectsInactive.Exclude)
            .Where(m => m.isActiveAndEnabled && m.MarkerEnabled).ToArray();
        foreach (var marker in markers) marker.enabled = false;
        return markers;
    }

    private static void RecordControlledRelease(IBasketballRuntime ball, BasketBallShotMarker marker)
    {
        ball.State.EnterShotMarker(marker);
        BasketballShotPipeline.ApplyShotAttempt(ball, false, true, false, false);
        BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(ball, (IMoneyBallState)RealScenePlayModeTestSupport.ResolveRuntimeGameRulesInstance(), ActiveMatch.Configuration.Rules);
        typeof(BasketBallState).GetMethod("RecordShotRelease", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(ball.State, null);
        ball.State.Thrown = true;
    }

    private static void TickContestAtZero()
    {
        RealScenePlayModeTestSupport.SetField(Timer.instance, "currentTime", Timer.instance.TimeStart - Time.deltaTime);
        Timer.instance.SendMessage("Update");
        TestContext.WriteLine($"BOUNDARY frame={Time.frameCount} phase={MatchController.instance.Phase} cause={MatchController.instance.EndReason.Cause}");
    }

    private IEnumerator VerifyContestPersistence(int expectedPoints, MatchEndCause cause)
    {
        yield return Complete(cause);
        string resultId = database.ReadOnlyScoreId(16);
        Assert.That(resultId, Is.Not.Empty);
        float frozenDuration = stats.Stats.TimePlayed;
        using (IDbCommand command = ((IDbConnection)typeof(LocalProfileTestDatabase)
            .GetProperty("Connection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(database)).CreateCommand())
        {
            command.CommandText = "SELECT totalpoints,time FROM HighScores WHERE modeid=16";
            using (IDataReader reader = command.ExecuteReader())
            {
                Assert.That(reader.Read(), Is.True);
                Assert.That(Convert.ToInt32(reader.GetValue(0)), Is.EqualTo(expectedPoints));
                Assert.That(Convert.ToSingle(reader.GetValue(1)), Is.EqualTo(frozenDuration).Within(0.0001f));
            }
        }
        Assert.That(MatchController.instance.RequestEnd(MatchEndReason.PlayerDied), Is.False);
        yield return null;
        gameRules.GetType().GetMethod("setTimePlayed").Invoke(gameRules, null);
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(frozenDuration));
        Assert.That(database.ReadOnlyScoreId(16), Is.EqualTo(resultId));
        TestContext.WriteLine($"PERSISTED result={resultId} match={MatchSession.EnsureCurrentMatch()} points={expectedPoints} duration={frozenDuration} markers={((IShotMarkerSession)gameRules).MarkersRemaining}");
    }

    [UnityTest]
    public IEnumerator Mode16PreZeroHumanAndCpuFlightsScoreThroughAuthoredHoopAndEndOnce()
    {
        yield return Launch(16, 1, withCpu: true);
        BasketBallShotMarker[] markers = FreezeContestForBoundary();
        Assert.That(markers, Has.Length.EqualTo(5));
        BasketBallAuto cpu = Object.FindAnyObjectByType<BasketBallAuto>();
        IBasketballRuntime[] balls = { BasketBall.instance, cpu };
        int endings = 0, completions = 0;
        MatchController.instance.Ending += _ => endings++;
        MatchController.instance.Completed += _ => completions++;
        for (int i = 0; i < balls.Length; i++) RecordControlledRelease(balls[i], markers[i]);
        yield return null; // Controlled release is timestamped before the controlled zero frame.
        TickContestAtZero();
        Assert.That(MatchController.instance.IsPlaying, Is.True);

        var upper = BasketBallShotMade.instance;
        var lower = Object.FindAnyObjectByType<BasketBallShotMadeCollision>();
        SimulationMode previous = Physics.simulationMode;
        var floor = new GameObject("mode16-boundary-ground");
        floor.tag = "ground";
        floor.transform.position = new Vector3(200f, 0f, 200f);
        floor.AddComponent<BoxCollider>().size = new Vector3(10f, 1f, 10f);
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            foreach (IBasketballRuntime ball in balls)
            {
                Rigidbody body = ((Component)ball).GetComponent<Rigidbody>();
                body.isKinematic = false;
                body.useGravity = false;
                body.constraints = RigidbodyConstraints.FreezeAll;
                Vector3 offset = body.transform.TransformVector(body.GetComponent<SphereCollider>().center);
                void Step(Vector3 point)
                {
                    body.position = point - offset;
                    Physics.SyncTransforms();
                    Physics.Simulate(Time.fixedDeltaTime);
                }
                Step(upper.GetComponent<Collider>().bounds.center + Vector3.up * 2f);
                Step(upper.GetComponent<Collider>().bounds.center);
                Step(lower.GetComponent<Collider>().bounds.center);
                Assert.That(ball.Stats.Stats.TotalPoints, Is.EqualTo(3));
                // Repeat the same physical crossings: the same attempt scores once.
                Step(upper.GetComponent<Collider>().bounds.center + Vector3.up * 2f);
                Step(upper.GetComponent<Collider>().bounds.center);
                Step(lower.GetComponent<Collider>().bounds.center);
                Assert.That(ball.Stats.Stats.TotalPoints, Is.EqualTo(3));
                Step(floor.transform.position + Vector3.up * 0.5f);
                Assert.That(ball.State.ShotReleasedAt, Is.EqualTo(double.PositiveInfinity), "actual ground collision ends grace");
                Timer.instance.SendMessage("Update");
                Assert.That(MatchController.instance.IsOver, Is.EqualTo(ball.IsCpu), "CPU flight must survive the human's resolution");
            }
        }
        finally
        {
            Physics.simulationMode = previous;
            Object.Destroy(floor);
        }
        yield return VerifyContestPersistence(3, MatchEndCause.TimeExpired);
        Assert.That(cpu.GameStats.Stats.TotalPoints, Is.EqualTo(3));
        Assert.That(endings, Is.EqualTo(1));
        Assert.That(completions, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Mode16IdleSoloExpiresAndAReleaseAtZeroCannotRenewGrace()
    {
        yield return Launch(16, 1);
        BasketBallShotMarker[] markers = FreezeContestForBoundary();
        RecordControlledRelease(BasketBall.instance, markers[0]);
        TickContestAtZero(); // Release and zero have the same frame timestamp.
        Assert.That(MatchController.instance.IsOver, Is.True);
        Assert.That(MatchController.instance.EndReason.Cause, Is.EqualTo(MatchEndCause.TimeExpired));
        yield return VerifyContestPersistence(0, MatchEndCause.TimeExpired);
    }

    private static void CrossUpper(IBasketballRuntime ball)
        => BasketBallShotMade.instance.SendMessage("OnTriggerEnter", ((Component)ball).GetComponent<Collider>());

    private static void CrossLower(IBasketballRuntime ball)
        => Object.FindAnyObjectByType<BasketBallShotMadeCollision>()
            .SendMessage("OnTriggerEnter", ((Component)ball).GetComponent<Collider>());

    [UnityTest]
    public IEnumerator Mode24AuthoredFourAttemptMoneyBallsKeepCompletionAndSavedStatistics()
    {
        yield return Launch(24, 1);
        Assert.That(ActiveMatch.Configuration.SceneName, Is.EqualTo("level_01_scrapyard"));
        Assert.That(ActiveMatch.Configuration.Rules.IsSevenPointContest, Is.True);
        Assert.That(Timer.instance.TimeStart, Is.EqualTo(90f));
        var all = Object.FindObjectsByType<BasketBallShotMarker>(FindObjectsInactive.Include);
        var enabled = all.Where(m => m.isActiveAndEnabled).ToArray();
        Assert.That(enabled, Has.Length.EqualTo(6));
        foreach (var marker in all)
        {
            TestContext.WriteLine($"MODE24 composition name={marker.name} active={marker.isActiveAndEnabled} seven={marker.ShotTypeSeven} quota={marker.MaxShotAttempt} madeQuota={marker.MaxShotMade}");
            Assert.That(marker.isActiveAndEnabled, Is.EqualTo(marker.ShotTypeSeven));
        }
        foreach (var marker in enabled) Assert.That(marker.MaxShotAttempt, Is.EqualTo(4));
        var markers = FreezeContestForBoundary();
        IBasketballRuntime ball = BasketBall.instance;
        void Register(BasketBallShotMarker marker)
        {
            ball.State.EnterShotMarker(marker);
            BasketballShotPipeline.ApplyShotAttempt(ball, false, false, false, true);
            BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(ball, (IMoneyBallState)gameRules, ActiveMatch.Configuration.Rules);
            typeof(BasketBallState).GetMethod("RecordShotRelease", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ball.State, null);
            ball.State.Thrown = true;
        }
        void Make()
        {
            CrossUpper(ball);
            CrossLower(ball);
            CrossUpper(ball);
            CrossLower(ball);
        }
        var made = markers[0];
        for (int i = 0; i < 3; i++) Register(made);
        int beforeFinal = stats.Stats.MoneyBallAttempts;
        Register(made);
        bool capturedFinal = ball.State.IsFinalMarkerAttempt;
        int finalAttempts = stats.Stats.MoneyBallAttempts;
        Make();
        TestContext.WriteLine($"MODE24 final make: points={stats.TotalPoints} money={stats.Stats.MoneyBallAttempts}/{stats.Stats.MoneyBallMade} marker={made.ShotAttempt}/{made.ShotMade} snapshotBeforeMake={capturedFinal}");
        Assert.That(stats.TotalPoints, Is.EqualTo(14));
        Assert.That(stats.Stats.MoneyBallMade, Is.EqualTo(1));
        Assert.That(made.ShotMade, Is.EqualTo(1));
        Assert.That(ball.State.IsFinalMarkerAttempt, Is.False);
        made.SendMessage("Update");
        Assert.That(made.MarkerEnabled, Is.False);
        var missed = markers[1];
        for (int i = 0; i < 4; i++) Register(missed);
        int afterMiss = stats.Stats.MoneyBallAttempts;
        missed.SendMessage("Update");
        Assert.That(missed.MarkerEnabled, Is.False);
        Assert.That(missed.ShotMade, Is.Zero);
        Register(missed); // Completed marker's fifth registration must not create an attempt credit.
        Make();
        Register(missed); // Nor may a sixth inherit the final bonus.
        Make();
        int afterExtra = stats.Stats.MoneyBallAttempts;
        Assert.That(ball.State.IsFinalMarkerAttempt, Is.False);
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.EqualTo(4));
        TestContext.WriteLine($"MODE24 missed/extra: afterMiss={afterMiss} afterExtra={afterExtra} points={stats.TotalPoints} moneyMade={stats.Stats.MoneyBallMade} remaining={((IShotMarkerSession)gameRules).MarkersRemaining}");
        Production("GameRules").GetMethod("RequestEnd", new[] { typeof(MatchEndReason) })
            .Invoke(gameRules, new object[] { MatchEndReason.TimeExpired });
        yield return Complete(MatchEndCause.TimeExpired);
        string savedId = database.ReadOnlyScoreId(24);
        Assert.That(summary.text, Does.Contain("money ball : 1 / 2"));
        Assert.That(summary.text, Does.Contain("50.0%"));
        using (IDbCommand command = ((IDbConnection)typeof(LocalProfileTestDatabase)
            .GetProperty("Connection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(database)).CreateCommand())
        {
            command.CommandText = "SELECT totalpoints,moneyBallAtt,moneyBallMade FROM HighScores WHERE modeid=24";
            using (IDataReader reader = command.ExecuteReader())
            {
                Assert.That(reader.Read(), Is.True);
                TestContext.WriteLine($"MODE24 saved: id={savedId} points={reader.GetValue(0)} money={reader.GetValue(1)}/{reader.GetValue(2)}");
                Assert.That(Convert.ToInt32(reader.GetValue(0)), Is.EqualTo(14));
                Assert.That(Convert.ToInt32(reader.GetValue(1)), Is.EqualTo(afterExtra));
                Assert.That(Convert.ToInt32(reader.GetValue(2)), Is.EqualTo(1));
            }
        }
        CrossUpper(ball);
        CrossLower(ball);
        Assert.That(database.ReadOnlyScoreId(24), Is.EqualTo(savedId));
        Assert.That(stats.TotalPoints, Is.EqualTo(14));
        Assert.That(beforeFinal, Is.Zero);
        Assert.That(capturedFinal, Is.True);
        Assert.That(finalAttempts, Is.EqualTo(1));
        Assert.That(afterMiss, Is.EqualTo(2));
        Assert.That(afterExtra, Is.EqualTo(afterMiss));
    }

    [UnityTest]
    public IEnumerator Mode16OverlappingFinalMarkerBonusesKeepOwnershipCompletionAndSavedScore()
    {
        yield return Launch(16, 1, withCpu: true);
        var markers = FreezeContestForBoundary();
        IBasketballRuntime humanBall = BasketBall.instance;
        IBasketballRuntime cpuBall = Object.FindAnyObjectByType<BasketBallAuto>();
        int events = 0;
        BasketBallShotMade.instance.ShotResolved += _ => events++;
        for (int scenario = 0; scenario < 3; scenario++)
        {
            var marker = markers[scenario];
            Assert.That(marker.MaxShotAttempt, Is.EqualTo(5));
            for (int i = 0; i < 3; i++) RecordControlledRelease(humanBall, marker); // Misses.
            RecordControlledRelease(humanBall, marker); // Fourth.
            RecordControlledRelease(cpuBall, marker); // Fifth.
            cpuBall.State.InAir = true;
            marker.SendMessage("Update");
            Assert.That(marker.MarkerEnabled, Is.True, "Completion waits for captured CPU flight");
            int humanBefore = humanBall.Stats.TotalPoints;
            int cpuBefore = cpuBall.Stats.TotalPoints;
            void Make(IBasketballRuntime ball)
            {
                CrossUpper(ball);
                CrossLower(ball);
                CrossUpper(ball);
                CrossLower(ball);
            }
            if (scenario == 1) Make(cpuBall); // Fifth before fourth.
            Make(humanBall);
            if (scenario == 2)
            {
                RecordControlledRelease(humanBall, marker); // Sixth before fifth resolves.
                Make(humanBall);
            }
            if (scenario != 1) Make(cpuBall);
            TestContext.WriteLine($"CONTROLLED scenario={scenario} humanDelta={humanBall.Stats.TotalPoints-humanBefore} cpuDelta={cpuBall.Stats.TotalPoints-cpuBefore} attempts={marker.ShotAttempt} makes={marker.ShotMade} events={events} enabled={marker.MarkerEnabled} humanMoney={humanBall.Stats.Stats.MoneyBallAttempts}/{humanBall.Stats.Stats.MoneyBallMade} cpuMoney={cpuBall.Stats.Stats.MoneyBallAttempts}/{cpuBall.Stats.Stats.MoneyBallMade}");
            Assert.That(humanBall.Stats.TotalPoints - humanBefore, Is.EqualTo(scenario == 2 ? 6 : 3));
            Assert.That(cpuBall.Stats.TotalPoints - cpuBefore, Is.EqualTo(6));
            Assert.That(marker.ShotAttempt, Is.EqualTo(scenario == 2 ? 6 : 5));
            Assert.That(marker.ShotMade, Is.EqualTo(scenario == 2 ? 3 : 2));
            marker.SendMessage("Update");
            Assert.That(marker.MarkerEnabled, Is.True);
            cpuBall.State.InAir = false;
            marker.SendMessage("Update");
            marker.SendMessage("Update");
            Assert.That(marker.MarkerEnabled, Is.False);
            Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.EqualTo(4 - scenario));
        }
        Assert.That(events, Is.EqualTo(7));
        Assert.That(humanBall.Stats.TotalPoints, Is.EqualTo(12));
        Assert.That(cpuBall.Stats.TotalPoints, Is.EqualTo(18));
        Assert.That(humanBall.Stats.Stats.MoneyBallAttempts, Is.Zero);
        Assert.That(humanBall.Stats.Stats.MoneyBallMade, Is.Zero);
        Assert.That(cpuBall.Stats.Stats.MoneyBallAttempts, Is.EqualTo(3));
        Assert.That(cpuBall.Stats.Stats.MoneyBallMade, Is.EqualTo(3));
        for (int i = 0; i < 5; i++) RecordControlledRelease(cpuBall, markers[3]);
        CrossUpper(cpuBall); // Pending bonus-bearing flight at accepted ending.
        gameRules.GetType().GetMethod("RequestEnd").Invoke(gameRules, new object[] { MatchEndReason.TimeExpired });
        CrossLower(cpuBall);
        Assert.That(cpuBall.Stats.TotalPoints, Is.EqualTo(18));
        Assert.That(markers[3].ShotMade, Is.Zero);
        Assert.That(events, Is.EqualTo(7));
        yield return VerifyContestPersistence(12, MatchEndCause.TimeExpired);
        CrossUpper(cpuBall);
        CrossLower(cpuBall);
        Assert.That(cpuBall.Stats.TotalPoints, Is.EqualTo(18));
        Assert.That(events, Is.EqualTo(7));
        yield return VerifyContestPersistence(12, MatchEndCause.TimeExpired);
    }

    [UnityTest]
    public IEnumerator Mode16LateMakesCannotChangeAcceptedScoresMarkersOrSavedResult()
    {
        yield return Launch(16, 1, withCpu: true);
        var markers = FreezeContestForBoundary();
        IBasketballRuntime[] balls = { BasketBall.instance, Object.FindAnyObjectByType<BasketBallAuto>() };
        int events = 0;
        BasketBallShotMade.instance.ShotResolved += _ => events++;
        foreach (var ball in balls)
        {
            RecordControlledRelease(ball, markers[0]);
            CrossUpper(ball);
            CrossLower(ball);
            CrossLower(ball);
            Assert.That(ball.Stats.TotalPoints, Is.EqualTo(3));
            RecordControlledRelease(ball, markers[1]);
            CrossUpper(ball); // Upper is accepted during Playing, lower arrives during Ending.
        }
        int markerMakes = markers[1].ShotMade;
        int remaining = ((IShotMarkerSession)gameRules).MarkersRemaining;
        string matchId = MatchSession.EnsureCurrentMatch();
        Assert.That((bool)gameRules.GetType().GetMethod("RequestEnd")
            .Invoke(gameRules, new object[] { MatchEndReason.TimeExpired }), Is.True);
        Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Ending));
        Assert.That(database.CountRows("HighScores", "modeid", 16), Is.Zero);
        foreach (var ball in balls)
        {
            CrossLower(ball);
            CrossUpper(ball);
            CrossLower(ball);
            Assert.That(ball.Stats.TotalPoints, Is.EqualTo(3));
            Assert.That(ball.Stats.Stats.ShotMade, Is.EqualTo(1));
        }
        Assert.That(events, Is.EqualTo(2));
        Assert.That(markers[1].ShotMade, Is.EqualTo(markerMakes));
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.EqualTo(remaining));
        yield return VerifyContestPersistence(3, MatchEndCause.TimeExpired);
        string savedId = database.ReadOnlyScoreId(16);
        float frozenTime = stats.Stats.TimePlayed;
        BasketBallShotMade.instance.enabled = false;
        BasketBallShotMade.instance.enabled = true;
        foreach (var ball in balls)
        {
            CrossUpper(ball);
            CrossLower(ball);
            RecordControlledRelease(ball, markers[2]); // A new generation must also remain ineligible.
            CrossUpper(ball);
            CrossLower(ball);
            CrossLower(ball);
            Assert.That(ball.Stats.TotalPoints, Is.EqualTo(3));
            Assert.That(ball.Stats.Stats.ShotMade, Is.EqualTo(1));
        }
        Assert.That(events, Is.EqualTo(2));
        Assert.That(markers[2].ShotMade, Is.Zero);
        Assert.That(markers[1].ShotMade, Is.EqualTo(markerMakes));
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.EqualTo(remaining));
        yield return VerifyContestPersistence(3, MatchEndCause.TimeExpired);
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(frozenTime));
        Assert.That(database.ReadOnlyScoreId(16), Is.EqualTo(savedId));
        Assert.That(MatchSession.EnsureCurrentMatch(), Is.EqualTo(matchId));
    }

    [UnityTest]
    public IEnumerator Mode16FailedPersistenceRetryKeepsAcceptedScoreTimeAndMatchIdentity()
    {
        yield return Launch(16, 1);
        var markers = FreezeContestForBoundary();
        IBasketballRuntime ball = BasketBall.instance;
        RecordControlledRelease(ball, markers[0]);
        CrossUpper(ball);
        CrossLower(ball);
        Assert.That(stats.TotalPoints, Is.EqualTo(3));
        string matchId = MatchSession.EnsureCurrentMatch();
        var connectorField = Production("DBConnector").GetField("instance");
        object connector = connectorField.GetValue(null);
        string pendingPath = System.IO.Path.Combine(Application.persistentDataPath, "pending-match-persistence.json");
        // Startup repair may write an empty queue after SetUp's isolation has cleared it.
        if (System.IO.File.Exists(pendingPath))
        {
            Type pendingType = Production("PendingMatchPersistenceData");
            object pending = JsonUtility.FromJson(System.IO.File.ReadAllText(pendingPath), pendingType);
            Assert.That(((IList)pendingType.GetField("scores").GetValue(pending)).Count, Is.Zero);
            Assert.That(((IList)pendingType.GetField("allTime").GetValue(pending)).Count, Is.Zero);
        }
        foreach (string suffix in new[] { "", ".bak", ".tmp" })
            if (System.IO.File.Exists(pendingPath + suffix)) System.IO.File.Delete(pendingPath + suffix);
        System.IO.Directory.CreateDirectory(pendingPath); // Deliberately block the isolated fallback write.
        bool blockingFallback = true;
        float frozenTime = 0;
        string durableResultId = null;
        try
        {
            connectorField.SetValue(null, null); // SQLite unavailable AND fallback unavailable.
            gameRules.GetType().GetMethod("RequestEnd").Invoke(gameRules, new object[] { MatchEndReason.TimeExpired });
            for (int retry = 0; retry < 2; retry++)
            {
                gameRules.GetType().GetMethod("HandleMatchEnded", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(gameRules, null);
                Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Ending));
                if (retry == 0) frozenTime = stats.Stats.TimePlayed;
                Assert.That(stats.Stats.TimePlayed, Is.EqualTo(frozenTime));
                RecordControlledRelease(ball, markers[1]);
                CrossUpper(ball);
                CrossLower(ball);
                Assert.That(stats.TotalPoints, Is.EqualTo(3));
                Assert.That(MatchSession.EnsureCurrentMatch(), Is.EqualTo(matchId));
                Assert.That(database.CountRows("HighScores", "modeid", 16), Is.Zero);
                yield return null;
            }
            System.IO.Directory.Delete(pendingPath);
            blockingFallback = false;
            // Recover local fallback first, while SQLite is still unavailable.
            gameRules.GetType().GetMethod("HandleMatchEnded", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(gameRules, null);
            Assert.That(MatchController.instance.Phase, Is.EqualTo(MatchPhase.Completed));
            Type pendingType = Production("PendingMatchPersistenceData");
            object pending = JsonUtility.FromJson(System.IO.File.ReadAllText(pendingPath), pendingType);
            var scores = (IList)pendingType.GetField("scores").GetValue(pending);
            Assert.That(scores.Count, Is.EqualTo(1));
            object queued = scores[0];
            durableResultId = (string)queued.GetType().GetField("Scoreid").GetValue(queued);
            Assert.That(durableResultId, Is.Not.Empty);
            Assert.That(queued.GetType().GetField("TotalPoints").GetValue(queued), Is.EqualTo(3));
            Assert.That((float)queued.GetType().GetField("Time").GetValue(queued), Is.EqualTo(frozenTime));
            RecordControlledRelease(ball, markers[1]);
            CrossUpper(ball);
            CrossLower(ball);
            Assert.That(stats.TotalPoints, Is.EqualTo(3));
        }
        finally
        {
            connectorField.SetValue(null, connector);
            if (blockingFallback) System.IO.Directory.Delete(pendingPath); // Only the empty directory created above.
        }
        for (int replay = 0; replay < 2; replay++)
            Assert.That((bool)Production("PendingMatchPersistenceStore").GetMethod("Repair").Invoke(null, null), Is.True);
        yield return VerifyContestPersistence(3, MatchEndCause.TimeExpired);
        string savedId = database.ReadOnlyScoreId(16);
        Assert.That(savedId, Is.EqualTo(durableResultId), "repair preserves the first locally durable result identity");
        gameRules.GetType().GetMethod("HandleMatchEnded", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameRules, null);
        Assert.That(database.ReadOnlyScoreId(16), Is.EqualTo(savedId));
        Assert.That(MatchSession.EnsureCurrentMatch(), Is.EqualTo(matchId));
        Assert.That(stats.Stats.TimePlayed, Is.EqualTo(frozenTime));
        Assert.That(markers[1].ShotMade, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Mode16SharedCourseStillWaitsForCapturedCpuFinalAttemptAndCompletesOnce()
    {
        yield return Launch(16, 1, withCpu: true);
        BasketBallShotMarker[] markers = FreezeContestForBoundary();
        BasketBallAuto cpu = Object.FindAnyObjectByType<BasketBallAuto>();
        foreach (var marker in markers.Take(markers.Length - 1))
        {
            while (marker.ShotAttempt < marker.MaxShotAttempt) marker.RegisterAttempt(BasketBall.instance);
            marker.SendMessage("Update");
        }
        BasketBallShotMarker finalMarker = markers.Last();
        while (finalMarker.ShotAttempt < finalMarker.MaxShotAttempt - 1) finalMarker.RegisterAttempt(cpu);
        RecordControlledRelease(cpu, finalMarker);
        Assert.That(finalMarker.ShotAttempt, Is.EqualTo(finalMarker.MaxShotAttempt));
        cpu.BasketBallState.InAir = true;
        finalMarker.SendMessage("Update");
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.EqualTo(1));
        yield return null;
        TickContestAtZero();
        Assert.That(MatchController.instance.IsPlaying, Is.True);
        // Controlled make callback at the actual configured countdown boundary; flight is tested separately.
        BasketBallShotMade.instance.SendMessage("OnTriggerEnter", cpu.GetComponent<Collider>());
        BasketBallShotMade.instance.shotMade(cpu);
        Assert.That(cpu.GameStats.Stats.TotalPoints, Is.EqualTo(6));
        Assert.That(stats.Stats.TotalPoints, Is.Zero);
        finalMarker.SendMessage("Update");
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.EqualTo(1));
        BasketBall.instance.BasketBallState.InAir = true; // Completion reads the captured CPU, not this human.
        cpu.BasketBallState.InAir = false;
        finalMarker.SendMessage("Update");
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.Zero);
        finalMarker.SendMessage("Update");
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.Zero);
        TickContestAtZero();
        yield return VerifyContestPersistence(0, MatchEndCause.Unknown);
        int finalMakes = finalMarker.ShotMade;
        RecordControlledRelease(BasketBall.instance, finalMarker);
        CrossUpper(BasketBall.instance);
        CrossLower(BasketBall.instance);
        Assert.That(finalMarker.ShotMade, Is.EqualTo(finalMakes));
        Assert.That(finalMarker.MarkerEnabled, Is.False);
        Assert.That(((IShotMarkerSession)gameRules).MarkersRemaining, Is.Zero);
        Assert.That(stats.TotalPoints, Is.Zero);
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
