using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: <c>BasketBall.Launch()</c>'s and <c>BasketBallAuto.Launch()</c>'s direct
/// <c>BehaviorNpcCritical.instance.playAnimationCriticalSuccesful()</c> calls are replaced by a
/// bind-once <c>Action</c> callback (<see cref="BasketBall.BindCriticalSuccessPresentation"/>,
/// <see cref="BasketBallAuto.BindCriticalSuccessPresentation"/>), bound once by composition
/// (<c>SpawnCoordinator.GiveBall</c>) to a shared late-resolving adapter
/// (<c>SpawnCoordinator.PlayCriticalSuccessPresentation</c>) - mirroring the bind/rebind/null-guard
/// shape <see cref="Level5BasketBallShotTelemetryTests"/> already established for the same seam. This
/// file covers <c>BindCriticalSuccessPresentation</c> itself on both concrete types, the exact
/// human/CPU launch invocation conditions (including the human-only <c>!isCpu</c> gate), an unbound
/// callback's no-op safety, the coordinator's composition-time wiring to both basketball types, and
/// that the bound callback is a bare static method reference rather than a closure that could have
/// captured a composition-time <c>BehaviorNpcCritical.instance</c> snapshot.
/// </summary>
public class Level5BasketBallCriticalSuccessPresentationTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        BasketBall.instance = null;
        BasketBallAuto.instance = null;
        BehaviorNpcCritical.instance = null;

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
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
        public ShooterAttributes ShooterAttributes { get; }
        public int Clutch => 0;
        public float ShotMeterSliderValue { get; set; }
        public bool ShotMeterEnded => true;

        /// <summary>
        /// <paramref name="luck"/> and <paramref name="range"/> deterministically steer
        /// <c>BasketballShotPipeline.ComputeLaunch</c>'s <c>IsSwish</c> result without depending on
        /// engine RNG: luck 100/0 makes <c>RollForCriticalShotChance</c> always/never succeed
        /// (<see cref="PercentChance.Succeeds"/>'s clamped endpoints), and a positive range with the
        /// default (zero) <c>lastShotDistance</c> this file always launches with makes
        /// <c>ShotModifiers.ReachesRim</c> divide-by-zero to true, zeroing the range modifier without
        /// consuming a roll either way. A swish additionally needs the critical branch's zeroed
        /// X/Y modifiers; a non-swish needs <paramref name="sliderValue"/> below 100 so the slider term
        /// alone makes the accuracy modifier non-zero regardless of the random direction/release rolls.
        /// </summary>
        public FakeShooterActor(int luck, int range, float sliderValue)
        {
            ShooterAttributes = new ShooterAttributes(
                displayName: "fake-shooter", accuracyTwoPoint: 80, accuracyThreePoint: 70, accuracyFourPoint: 60,
                accuracySevenPoint: 50, shootAngle: 45, range: range, release: 50, luck: luck, jumpForce: 0, runSpeed: 0);
            ShotMeterSliderValue = sliderValue;
        }

        public void SetAnimBool(string name, bool value) { }
        public void SetAnimTrigger(string name) { }
        public void LockCallBallToPlayer(bool locked) { }
        public void DisplayShotMeterMessage(string message) { }
        public void EndShootCycle() { }
    }

    private sealed class FakeGroundHeightProvider : IGroundHeightProvider
    {
        public float GroundHeight { get; set; }
    }

    // ==================== BasketBall (human) launch fixtures ====================

    private BasketBall BuildLaunchableHumanBall(int luck, int range, float sliderValue, bool isCpu, out FakeShooterActor actor)
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
        actor = new FakeShooterActor(luck, range, sliderValue);
        ball.BindOwner(0, isCpu, true, playerGo, actor);
        ball.BindGroundHeightProvider(new FakeGroundHeightProvider());
        ball.BindMatchRules(new ResolvedMatchRules(enemiesOnly: false));

        MethodInfo start = typeof(BasketBall).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        start.Invoke(ball, null);

        return ball;
    }

    private static void InvokeLaunch(MonoBehaviour ball, GameObject ballPositionAtLaunch)
    {
        MethodInfo launch = ball.GetType().GetMethod("Launch", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(launch, $"{ball.GetType().Name} must declare Launch(GameObject)");
        launch.Invoke(ball, new object[] { ballPositionAtLaunch });
    }

    // ==================== BasketBallAuto (CPU) launch fixtures ====================

    private BasketBallAuto BuildLaunchableCpuBall(int luck, int range, float sliderValue, out FakeShooterActor actor)
    {
        GameObject autoPlayerGo = Spawn("cpu-actor");
        GameObject basketballPositionGo = Spawn("basketBall_position");
        basketballPositionGo.transform.parent = autoPlayerGo.transform;

        GameObject ballGo = Spawn("cpu-ball");
        BasketBallState state = ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        GameObject dropShadowGo = Spawn("drop shadow");
        dropShadowGo.transform.parent = ballGo.transform;

        state.TwoPoints = true;
        state.BasketBallTarget = Spawn("target");
        state.BasketBallTarget.transform.position = new Vector3(0f, 0f, 20f);

        BasketBallAuto ball = ballGo.AddComponent<BasketBallAuto>();
        actor = new FakeShooterActor(luck, range, sliderValue);
        ball.BindOwner(0, true, false, autoPlayerGo, actor);
        ball.BindMatchRules(new ResolvedMatchRules(enemiesOnly: false));

        MethodInfo start = typeof(BasketBallAuto).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        start.Invoke(ball, null);

        return ball;
    }

    // ==================== BasketBall.BindCriticalSuccessPresentation bind semantics ====================

    [Test]
    public void HumanBindCriticalSuccessPresentationRejectsNullFirstBind()
    {
        BasketBall ball = Spawn("human-ball-null-critical").AddComponent<BasketBall>();

        LogAssert.Expect(LogType.Error, new Regex("null critical-success presentation callback"));
        ball.BindCriticalSuccessPresentation(null);

        Assert.IsNull(GetPrivateField(ball, "criticalSuccessPresentationCallback"), "a rejected null bind must leave the ball unbound");
    }

    [Test]
    public void HumanBindCriticalSuccessPresentationAcceptsFirstValidBind()
    {
        BasketBall ball = Spawn("human-ball-first-critical").AddComponent<BasketBall>();
        Action callback = () => { };

        ball.BindCriticalSuccessPresentation(callback);

        Assert.AreSame(callback, GetPrivateField(ball, "criticalSuccessPresentationCallback"));
    }

    [Test]
    public void HumanBindCriticalSuccessPresentationRejectsSecondValidBindWithoutReplacingTheOriginal()
    {
        BasketBall ball = Spawn("human-ball-second-critical").AddComponent<BasketBall>();
        Action first = () => { };
        Action second = () => { };
        ball.BindCriticalSuccessPresentation(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound critical-success presentation callback"));
        ball.BindCriticalSuccessPresentation(second);

        Assert.AreSame(first, GetPrivateField(ball, "criticalSuccessPresentationCallback"),
            "a second BindCriticalSuccessPresentation call must not overwrite the original callback");
    }

    [Test]
    public void HumanBindCriticalSuccessPresentationRejectsNullSecondBindWithAnAlreadyBoundMessage()
    {
        BasketBall ball = Spawn("human-ball-null-second-critical").AddComponent<BasketBall>();
        Action first = () => { };
        ball.BindCriticalSuccessPresentation(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound critical-success presentation callback"));
        ball.BindCriticalSuccessPresentation(null);

        Assert.AreSame(first, GetPrivateField(ball, "criticalSuccessPresentationCallback"), "a null second bind must not clear the original callback");
    }

    // ==================== BasketBallAuto.BindCriticalSuccessPresentation bind semantics ====================

    [Test]
    public void CpuBindCriticalSuccessPresentationRejectsNullFirstBind()
    {
        BasketBallAuto ball = Spawn("cpu-ball-null-critical").AddComponent<BasketBallAuto>();

        LogAssert.Expect(LogType.Error, new Regex("null critical-success presentation callback"));
        ball.BindCriticalSuccessPresentation(null);

        Assert.IsNull(GetPrivateField(ball, "criticalSuccessPresentationCallback"), "a rejected null bind must leave the ball unbound");
    }

    [Test]
    public void CpuBindCriticalSuccessPresentationAcceptsFirstValidBind()
    {
        BasketBallAuto ball = Spawn("cpu-ball-first-critical").AddComponent<BasketBallAuto>();
        Action callback = () => { };

        ball.BindCriticalSuccessPresentation(callback);

        Assert.AreSame(callback, GetPrivateField(ball, "criticalSuccessPresentationCallback"));
    }

    [Test]
    public void CpuBindCriticalSuccessPresentationRejectsSecondValidBindWithoutReplacingTheOriginal()
    {
        BasketBallAuto ball = Spawn("cpu-ball-second-critical").AddComponent<BasketBallAuto>();
        Action first = () => { };
        Action second = () => { };
        ball.BindCriticalSuccessPresentation(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound critical-success presentation callback"));
        ball.BindCriticalSuccessPresentation(second);

        Assert.AreSame(first, GetPrivateField(ball, "criticalSuccessPresentationCallback"),
            "a second BindCriticalSuccessPresentation call must not overwrite the original callback");
    }

    [Test]
    public void CpuBindCriticalSuccessPresentationRejectsNullSecondBindWithAnAlreadyBoundMessage()
    {
        BasketBallAuto ball = Spawn("cpu-ball-null-second-critical").AddComponent<BasketBallAuto>();
        Action first = () => { };
        ball.BindCriticalSuccessPresentation(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound critical-success presentation callback"));
        ball.BindCriticalSuccessPresentation(null);

        Assert.AreSame(first, GetPrivateField(ball, "criticalSuccessPresentationCallback"), "a null second bind must not clear the original callback");
    }

    // ==================== BasketBall.Launch() invocation ====================

    [Test]
    public void HumanSwishInvokesTheBoundCallbackExactlyOnce()
    {
        BasketBall ball = BuildLaunchableHumanBall(luck: 100, range: 10000, sliderValue: 80, isCpu: false, out _);
        int callCount = 0;
        ball.BindCriticalSuccessPresentation(() => callCount++);

        InvokeLaunch(ball, ball.gameObject);

        Assert.That(callCount, Is.EqualTo(1), "a human swish must invoke the bound critical-success presentation callback exactly once");
    }

    [Test]
    public void HumanNonSwishDoesNotInvokeTheCallback()
    {
        BasketBall ball = BuildLaunchableHumanBall(luck: 0, range: 10000, sliderValue: 50, isCpu: false, out _);
        int callCount = 0;
        ball.BindCriticalSuccessPresentation(() => callCount++);

        InvokeLaunch(ball, ball.gameObject);

        Assert.That(callCount, Is.EqualTo(0), "a human non-swish must not invoke the critical-success presentation callback");
    }

    /// <summary>Pins the exact preserved condition: <c>computation.IsSwish &amp;&amp; !isCpu</c>.</summary>
    [Test]
    public void HumanSwishWithIsCpuTrueDoesNotInvokeTheCallback()
    {
        BasketBall ball = BuildLaunchableHumanBall(luck: 100, range: 10000, sliderValue: 80, isCpu: true, out _);
        int callCount = 0;
        ball.BindCriticalSuccessPresentation(() => callCount++);

        InvokeLaunch(ball, ball.gameObject);

        Assert.That(callCount, Is.EqualTo(0), "BasketBall.Launch must preserve its existing !isCpu gate on critical-success presentation");
    }

    [Test]
    public void UnboundCriticalSuccessPresentationDoesNotPreventOrAlterHumanLaunchBehavior()
    {
        BasketBall ball = BuildLaunchableHumanBall(luck: 100, range: 10000, sliderValue: 80, isCpu: false, out FakeShooterActor actor);
        actor.HasBasketball = true;

        Assert.DoesNotThrow(() => InvokeLaunch(ball, ball.gameObject));

        Assert.IsFalse(actor.HasBasketball, "launch must still clear HasBasketball with no critical-success presentation bound");
    }

    // ==================== BasketBallAuto.Launch() invocation ====================

    [Test]
    public void CpuSwishInvokesTheBoundCallbackExactlyOnce()
    {
        BasketBallAuto ball = BuildLaunchableCpuBall(luck: 100, range: 10000, sliderValue: 80, out _);
        int callCount = 0;
        ball.BindCriticalSuccessPresentation(() => callCount++);

        InvokeLaunch(ball, ball.gameObject);

        Assert.That(callCount, Is.EqualTo(1), "a CPU swish must invoke the bound critical-success presentation callback exactly once");
    }

    [Test]
    public void CpuNonSwishDoesNotInvokeTheCallback()
    {
        BasketBallAuto ball = BuildLaunchableCpuBall(luck: 0, range: 10000, sliderValue: 50, out _);
        int callCount = 0;
        ball.BindCriticalSuccessPresentation(() => callCount++);

        InvokeLaunch(ball, ball.gameObject);

        Assert.That(callCount, Is.EqualTo(0), "a CPU non-swish must not invoke the critical-success presentation callback");
    }

    [Test]
    public void UnboundCriticalSuccessPresentationDoesNotPreventOrAlterCpuLaunchBehavior()
    {
        BasketBallAuto ball = BuildLaunchableCpuBall(luck: 100, range: 10000, sliderValue: 80, out FakeShooterActor actor);
        actor.HasBasketball = true;

        Assert.DoesNotThrow(() => InvokeLaunch(ball, ball.gameObject));

        Assert.IsFalse(actor.HasBasketball, "launch must still clear HasBasketball with no critical-success presentation bound");
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

    private PlayerIdentifier RegisterCpuParticipant(int pid, PlayerRegistry registry)
    {
        GameObject actorGo = Spawn($"cpu-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<AutoPlayerController>();
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, true);
        identifier.autoPlayer = actorGo;
        identifier.setAutoPlayer(identifier.autoPlayer);
        registry.Add(identifier);
        return identifier;
    }

    private static Delegate GetBoundCriticalSuccessDelegate(object runtime)
    {
        return (Delegate)GetPrivateField(runtime, "criticalSuccessPresentationCallback");
    }

    /// <summary>
    /// Proves the coordinator hands the human ball the same shared late-resolving adapter as the CPU
    /// ball, and that the binding is a bare static method reference (<c>Target == null</c>) rather
    /// than a closure - a closure would be the only way this callback could have captured a
    /// composition-time <c>BehaviorNpcCritical.instance</c> snapshot, which the required late-resolution
    /// behavior forbids.
    /// </summary>
    [Test]
    public void PrimaryHumanBallReceivesTheSharedLateResolvingPresentationAdapter()
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
        Delegate bound = GetBoundCriticalSuccessDelegate(runtime);
        Assert.IsNotNull(bound, "GiveBall must bind a critical-success presentation callback to the human ball");
        Assert.That(bound.Method.DeclaringType, Is.EqualTo(typeof(SpawnCoordinator)));
        Assert.That(bound.Method.Name, Is.EqualTo("PlayCriticalSuccessPresentation"));
        Assert.IsNull(bound.Target,
            "the bound callback must be a static method reference, not a closure capable of capturing a composition-time BehaviorNpcCritical.instance value");
    }

    /// <summary>
    /// CPU swishes receive this presentation exactly like human ones (unlike the human-only shot
    /// telemetry binding), so <c>BasketBallAuto</c> must receive the same adapter here.
    /// </summary>
    [Test]
    public void CpuBallAlsoReceivesTheSharedLateResolvingPresentationAdapter()
    {
        PlayerRegistry registry = new PlayerRegistry();
        RegisterCpuParticipant(0, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);
        Assert.IsNotNull(cpuPrefab, "CPU basketball prefab failed to load");

        GiveBallVia(coordinator, 0, cpuPrefab, forCpu: true);

        GameObject ball = registry.GetBySlot(0).autoBasketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's autoBasketball reference");
        spawned.Add(ball);

        BasketBallAuto runtime = ball.GetComponent<BasketBallAuto>();
        Delegate bound = GetBoundCriticalSuccessDelegate(runtime);
        Assert.IsNotNull(bound, "GiveBall must bind a critical-success presentation callback to the CPU ball");
        Assert.That(bound.Method.DeclaringType, Is.EqualTo(typeof(SpawnCoordinator)));
        Assert.That(bound.Method.Name, Is.EqualTo("PlayCriticalSuccessPresentation"));
        Assert.IsNull(bound.Target,
            "the bound callback must be a static method reference, not a closure capable of capturing a composition-time BehaviorNpcCritical.instance value");
    }

    /// <summary>
    /// Direct coverage of the adapter itself: a swish reached before the cheerleader's own
    /// <c>Start()</c> has assigned <see cref="BehaviorNpcCritical.instance"/> (the exact ordering
    /// <c>SpawnCoordinator.SpawnBasketballs</c> then <c>SpawnCheerleader</c> guarantees) must not throw
    /// or otherwise fail the shot.
    /// </summary>
    [Test]
    public void PlayCriticalSuccessPresentationDoesNotThrowWhenBehaviorNpcCriticalInstanceIsAbsent()
    {
        BehaviorNpcCritical.instance = null;
        MethodInfo method = typeof(SpawnCoordinator).GetMethod("PlayCriticalSuccessPresentation", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "SpawnCoordinator must declare a PlayCriticalSuccessPresentation adapter");

        Assert.DoesNotThrow(() => method.Invoke(null, null));
    }
}
