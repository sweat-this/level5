using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: <c>BasketBall.Launch()</c>'s direct <c>AnaylticsManager.PlayerShoot</c> call is
/// replaced by a bind-once <c>Action&lt;float&gt;</c> callback (<see cref="BasketBall.BindShotTelemetry"/>),
/// bound once by composition (<c>SpawnCoordinator.GiveBall</c>) - mirroring the bind/rebind/null-guard
/// shape <see cref="BasketBall.BindMatchRules"/> already established for the same seam
/// (<see cref="Level5BasketBallMatchRulesTests"/>). This file covers <c>BindShotTelemetry</c> itself,
/// the exact human-launch invocation (once, with the exact slider value, after
/// <c>actor.EndShootCycle()</c>), an unbound callback's no-op safety, the coordinator's
/// composition-time wiring to <c>AnaylticsManager.PlayerShoot</c>, and CPU composition's exclusion
/// from telemetry.
/// </summary>
public class Level5BasketBallShotTelemetryTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        BasketBall.instance = null;

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    private sealed class FakeShooterActor : IShooterActor
    {
        public bool HasBasketball { get; set; } = true;
        public bool FacingFront => true;
        public bool Grounded => true;
        public bool InAir => false;
        public bool InDunkState => false;
        public float DistanceFromRim => 0f;
        // A zeroed (default) ShooterAttributes has ShootAngle=0, which degenerates
        // BasketballShotPipeline.ComputeLaunch's H/R*tanAlpha term to 0/0 (NaN) for a same-height
        // ball/target pair - not a telemetry concern, just a non-degenerate shot geometry needed to
        // exercise Launch() at all.
        public ShooterAttributes ShooterAttributes => new ShooterAttributes(
            displayName: "fake-shooter", accuracyTwoPoint: 80, accuracyThreePoint: 70, accuracyFourPoint: 60,
            accuracySevenPoint: 50, shootAngle: 45, range: 10000, release: 50, luck: 0, jumpForce: 0, runSpeed: 0);
        public int Clutch => 0;
        public float ShotMeterSliderValue { get; set; }
        public bool ShotMeterEnded => true;
        public int EndShootCycleCallCount { get; private set; }

        public void SetAnimBool(string name, bool value) { }
        public void SetAnimTrigger(string name) { }
        public void LockCallBallToPlayer(bool locked) { }
        public void DisplayShotMeterMessage(string message) { }
        public void EndShootCycle() => EndShootCycleCallCount++;
    }

    /// <summary>
    /// Builds a fully-composed, primary human ball able to run its real <c>Launch(GameObject)</c> -
    /// the same scene-hierarchy pieces <see cref="Level5BasketBallMatchRulesTests"/>' own
    /// <c>BuildBoundBall</c> needs for <c>Start()</c>, plus a <c>BasketBallTarget</c>
    /// (<see cref="BasketballShotPipeline.ComputeLaunch"/>'s own target-position dereference, never
    /// touched by <c>Start()</c>/<c>Update()</c>).
    /// </summary>
    private BasketBall BuildLaunchableBall(out FakeShooterActor actor)
    {
        GameObject playerGo = Spawn("human-actor");
        GameObject basketballPositionGo = Spawn("basketBall_position");
        basketballPositionGo.transform.parent = playerGo.transform;

        GameObject ballGo = Spawn("human-ball");
        BasketBallState state = ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        GameObject dropShadowGo = Spawn("drop shadow");
        dropShadowGo.transform.parent = ballGo.transform;

        state.TwoPoints = true;
        state.BasketBallTarget = Spawn("target");
        state.BasketBallTarget.transform.position = new Vector3(0f, 0f, 20f);

        BasketBall ball = ballGo.AddComponent<BasketBall>();
        actor = new FakeShooterActor();
        ball.BindOwner(0, false, true, playerGo, actor);
        ball.BindGroundHeightProvider(new FakeGroundHeightProvider());
        ball.BindMatchRules(new ResolvedMatchRules(enemiesOnly: false));

        MethodInfo start = typeof(BasketBall).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        start.Invoke(ball, null);

        return ball;
    }

    private sealed class FakeGroundHeightProvider : IGroundHeightProvider
    {
        public float GroundHeight { get; set; }
    }

    private static void InvokeLaunch(BasketBall ball, GameObject ballPositionAtLaunch)
    {
        MethodInfo launch = typeof(BasketBall).GetMethod("Launch", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(launch, "BasketBall must declare Launch(GameObject)");
        launch.Invoke(ball, new object[] { ballPositionAtLaunch });
    }

    // ==================== BindShotTelemetry bind semantics ====================

    [Test]
    public void BindShotTelemetryRejectsNullFirstBind()
    {
        BasketBall ball = Spawn("human-ball-null-telemetry").AddComponent<BasketBall>();

        LogAssert.Expect(LogType.Error, new Regex("null shot-telemetry callback"));
        ball.BindShotTelemetry(null);

        Assert.IsNull(GetPrivateField(ball, "shotTelemetryCallback"), "a rejected null bind must leave the ball unbound");
    }

    [Test]
    public void BindShotTelemetryAcceptsFirstValidBind()
    {
        BasketBall ball = Spawn("human-ball-first-telemetry").AddComponent<BasketBall>();
        System.Action<float> callback = _ => { };

        ball.BindShotTelemetry(callback);

        Assert.AreSame(callback, GetPrivateField(ball, "shotTelemetryCallback"));
    }

    [Test]
    public void BindShotTelemetryRejectsSecondValidBindWithoutReplacingTheOriginal()
    {
        BasketBall ball = Spawn("human-ball-second-telemetry").AddComponent<BasketBall>();
        System.Action<float> first = _ => { };
        System.Action<float> second = _ => { };
        ball.BindShotTelemetry(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound shot-telemetry callback"));
        ball.BindShotTelemetry(second);

        Assert.AreSame(first, GetPrivateField(ball, "shotTelemetryCallback"),
            "a second BindShotTelemetry call must not overwrite the original callback");
    }

    [Test]
    public void BindShotTelemetryRejectsNullSecondBindWithAnAlreadyBoundMessage()
    {
        BasketBall ball = Spawn("human-ball-null-second-telemetry").AddComponent<BasketBall>();
        System.Action<float> first = _ => { };
        ball.BindShotTelemetry(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound shot-telemetry callback"));
        ball.BindShotTelemetry(null);

        Assert.AreSame(first, GetPrivateField(ball, "shotTelemetryCallback"), "a null second bind must not clear the original callback");
    }

    // ==================== Launch() invocation ====================

    [Test]
    public void HumanLaunchInvokesTheBoundCallbackExactlyOnceWithTheExactSliderValue()
    {
        BasketBall ball = BuildLaunchableBall(out FakeShooterActor actor);
        actor.ShotMeterSliderValue = 63.5f;
        List<float> received = new List<float>();
        ball.BindShotTelemetry(v => received.Add(v));

        InvokeLaunch(ball, ball.gameObject);

        Assert.That(received, Has.Count.EqualTo(1), "a human launch must invoke the bound telemetry callback exactly once");
        Assert.That(received[0], Is.EqualTo(63.5f), "the callback must receive the exact ShotMeterSliderValue used for this launch");
    }

    [Test]
    public void HumanLaunchInvokesTheCallbackAfterEndShootCycle()
    {
        BasketBall ball = BuildLaunchableBall(out FakeShooterActor actor);
        int endShootCycleCountAtCallback = -1;
        ball.BindShotTelemetry(_ => endShootCycleCountAtCallback = actor.EndShootCycleCallCount);

        InvokeLaunch(ball, ball.gameObject);

        Assert.That(endShootCycleCountAtCallback, Is.EqualTo(1),
            "the telemetry callback must fire after actor.EndShootCycle() has already run");
    }

    [Test]
    public void UnboundTelemetryDoesNotPreventOrAlterLaunchBehavior()
    {
        BasketBall ball = BuildLaunchableBall(out FakeShooterActor actor);
        actor.HasBasketball = true;

        Assert.DoesNotThrow(() => InvokeLaunch(ball, ball.gameObject));

        Assert.IsFalse(actor.HasBasketball, "launch must still clear HasBasketball with no telemetry bound");
        Assert.That(actor.EndShootCycleCallCount, Is.EqualTo(1), "launch must still call EndShootCycle() with no telemetry bound");
    }

    // ==================== SpawnCoordinator.GiveBall composition ====================

    private MethodInfo giveBall;

    private void GiveBallVia(SpawnCoordinator coordinator, int slotId, GameObject prefab, bool forCpu)
    {
        giveBall ??= typeof(SpawnCoordinator).GetMethod("GiveBall", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(giveBall, "SpawnCoordinator.GiveBall must exist - the sole basketball creation path this migration targets");
        giveBall.Invoke(coordinator, new object[] { slotId, prefab, Vector3.zero, forCpu });
    }

    private PlayerIdentifier RegisterHumanParticipant(int pid, PlayerRegistry registry)
    {
        GameObject actorGo = Spawn($"human-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, false);
        identifier.player = actorGo;
        identifier.setPlayer(actorGo);
        registry.Add(identifier);
        return identifier;
    }

    private static System.Delegate GetBoundTelemetryDelegate(BasketBall ball)
    {
        return (System.Delegate)GetPrivateField(ball, "shotTelemetryCallback");
    }

    /// <summary>
    /// Proves the coordinator hands the human ball the exact <c>AnaylticsManager.PlayerShoot</c>
    /// method, without ever invoking it - invoking it would call into <c>UnityEngine.Analytics</c>,
    /// which this issue explicitly does not need to re-certify (see file header).
    /// </summary>
    [Test]
    public void PrimaryHumanBallReceivesAnaylticsManagerPlayerShootAsItsTelemetryCallback()
    {
        PlayerRegistry registry = new PlayerRegistry();
        RegisterHumanParticipant(0, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        Assert.IsNotNull(humanPrefab, "human basketball prefab failed to load");

        GiveBallVia(coordinator, 0, humanPrefab, forCpu: false);

        GameObject ball = registry.GetBySlot(0).basketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's basketball reference");
        spawned.Add(ball);

        BasketBall runtime = ball.GetComponent<BasketBall>();
        System.Delegate bound = GetBoundTelemetryDelegate(runtime);
        Assert.IsNotNull(bound, "GiveBall must bind a shot-telemetry callback to the primary human ball");
        Assert.That(bound.Method.DeclaringType, Is.EqualTo(typeof(AnaylticsManager)));
        Assert.That(bound.Method.Name, Is.EqualTo(nameof(AnaylticsManager.PlayerShoot)));
    }

    [Test]
    public void SecondaryHumanBallAlsoReceivesTheTelemetryBinding()
    {
        PlayerRegistry registry = new PlayerRegistry();
        RegisterHumanParticipant(0, registry);
        RegisterHumanParticipant(1, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBallVia(coordinator, 0, humanPrefab, forCpu: false);
        GiveBallVia(coordinator, 1, humanPrefab, forCpu: false);

        GameObject secondBall = registry.GetBySlot(1).basketball;
        spawned.Add(registry.GetBySlot(0).basketball);
        spawned.Add(secondBall);

        BasketBall secondRuntime = secondBall.GetComponent<BasketBall>();
        Assert.IsNotNull(GetBoundTelemetryDelegate(secondRuntime), "a second human ball must also receive a telemetry binding");
    }

    /// <summary>
    /// CPU composition gains no telemetry behavior: <c>BasketBallAuto</c> declares no
    /// <c>BindShotTelemetry</c> method at all (the required "do not extend BasketBallAuto"
    /// constraint), so <c>GiveBall</c>'s CPU branch has nothing to call even if it wanted to.
    /// </summary>
    [Test]
    public void BasketBallAutoDeclaresNoShotTelemetryBindingMethod()
    {
        MethodInfo method = typeof(BasketBallAuto).GetMethod("BindShotTelemetry", BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNull(method, "BasketBallAuto must not gain any shot-telemetry binding surface - CPU shots stay untelemetered");
    }
}
