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
/// (<c>SpawnCoordinator.GiveBall</c>) to a shared late-resolving adapter - mirroring the
/// bind/rebind/null-guard shape <see cref="Level5BasketBallShotTelemetryTests"/> already established for
/// the same seam.
///
/// AUD-012 Phase 2b Slice 56: that adapter moved from <c>SpawnCoordinator</c> (a bound-in-code static
/// method it implemented itself) to <c>GameLevelManager.PlayCriticalSuccessPresentation</c> - the
/// Assembly-CSharp composition side. <c>SpawnCoordinator</c> now only forwards whatever
/// <c>criticalSuccessPresentation</c> callback it was constructed with, unexamined. This file covers
/// <c>BindCriticalSuccessPresentation</c> itself on both concrete types, the exact human/CPU launch
/// invocation conditions (including the human-only <c>!isCpu</c> gate), an unbound callback's no-op
/// safety, the coordinator's composition-time forwarding of a supplied callback to both basketball
/// types, that human and CPU receive the exact same supplied delegate instance, the production adapter's
/// ownership/identity (<c>GameLevelManager</c>, not <c>SpawnCoordinator</c>) and its late-resolution
/// lifecycle (absent, late-assigned, replaced, cleared).
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
    /// The production <c>GameLevelManager.PlayCriticalSuccessPresentation</c> adapter, resolved via
    /// reflection (it is <c>private static</c>) and wrapped as a bare-static <see cref="Action"/> - the
    /// same method group shape <c>GameLevelManager.Awake</c> hands the real constructor.
    /// </summary>
    private static Action ProductionCriticalSuccessAdapter()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod("PlayCriticalSuccessPresentation", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager must declare a PlayCriticalSuccessPresentation adapter");
        return (Action)Delegate.CreateDelegate(typeof(Action), method);
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 56: <c>SpawnCoordinator</c> no longer implements or names this adapter -
    /// it only forwards whatever critical-success callback it was constructed with. This supplies the
    /// exact production adapter explicitly (mirroring production composition, see
    /// <c>GameLevelManager.Awake</c>) and proves the coordinator hands the human ball that exact
    /// callback, and that the binding is a bare static method reference (<c>Target == null</c>) rather
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
            GameModeId.None, new FakeGroundHeightProvider(),
            criticalSuccessPresentation: ProductionCriticalSuccessAdapter());
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        Assert.IsNotNull(humanPrefab, "human basketball prefab failed to load");

        GiveBallVia(coordinator, 0, humanPrefab, forCpu: false);

        GameObject ball = registry.GetBySlot(0).basketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's basketball reference");
        spawned.Add(ball);

        BasketBall runtime = ball.GetComponent<BasketBall>();
        Delegate bound = GetBoundCriticalSuccessDelegate(runtime);
        Assert.IsNotNull(bound, "GiveBall must bind the supplied critical-success presentation callback to the human ball");
        Assert.That(bound.Method.DeclaringType, Is.EqualTo(typeof(GameLevelManager)));
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
            GameModeId.None, new FakeGroundHeightProvider(),
            criticalSuccessPresentation: ProductionCriticalSuccessAdapter());
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);
        Assert.IsNotNull(cpuPrefab, "CPU basketball prefab failed to load");

        GiveBallVia(coordinator, 0, cpuPrefab, forCpu: true);

        GameObject ball = registry.GetBySlot(0).autoBasketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's autoBasketball reference");
        spawned.Add(ball);

        BasketBallAuto runtime = ball.GetComponent<BasketBallAuto>();
        Delegate bound = GetBoundCriticalSuccessDelegate(runtime);
        Assert.IsNotNull(bound, "GiveBall must bind the supplied critical-success presentation callback to the CPU ball");
        Assert.That(bound.Method.DeclaringType, Is.EqualTo(typeof(GameLevelManager)));
        Assert.That(bound.Method.Name, Is.EqualTo("PlayCriticalSuccessPresentation"));
        Assert.IsNull(bound.Target,
            "the bound callback must be a static method reference, not a closure capable of capturing a composition-time BehaviorNpcCritical.instance value");
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 56: proves human and CPU balls given the same coordinator instance receive
    /// the exact same supplied delegate - not two separately-created method groups that merely happen to
    /// share a declaring type/method name.
    /// </summary>
    [Test]
    public void HumanAndCpuBallsReceiveTheExactSameSuppliedCriticalSuccessCallback()
    {
        PlayerRegistry registry = new PlayerRegistry();
        RegisterHumanParticipant(0, registry);
        RegisterCpuParticipant(1, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        Action callback = () => { };
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider(),
            criticalSuccessPresentation: callback);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);

        GiveBallVia(coordinator, 0, humanPrefab, forCpu: false);
        GiveBallVia(coordinator, 1, cpuPrefab, forCpu: true);

        GameObject humanBall = registry.GetBySlot(0).basketball;
        GameObject cpuBall = registry.GetBySlot(1).autoBasketball;
        spawned.Add(humanBall);
        spawned.Add(cpuBall);

        Delegate humanBound = GetBoundCriticalSuccessDelegate(humanBall.GetComponent<BasketBall>());
        Delegate cpuBound = GetBoundCriticalSuccessDelegate(cpuBall.GetComponent<BasketBallAuto>());
        Assert.AreSame(callback, humanBound, "the human ball must receive the exact supplied delegate instance");
        Assert.AreSame(callback, cpuBound, "the CPU ball must receive the exact same supplied delegate instance as the human ball");
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 56: a coordinator built without a critical-success callback (every
    /// existing direct/test construction site that omits the new optional parameter) must leave both
    /// balls unbound rather than calling <c>BindCriticalSuccessPresentation(null)</c>, which both
    /// concrete types reject as a composition error. No <see cref="LogAssert"/> expectation is set up:
    /// an unexpected error log from a regressed null bind would already fail this test.
    /// </summary>
    [Test]
    public void NoCriticalSuccessCallbackLeavesBothBallsUnboundWithoutError()
    {
        PlayerRegistry registry = new PlayerRegistry();
        RegisterHumanParticipant(0, registry);
        RegisterCpuParticipant(1, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);

        GiveBallVia(coordinator, 0, humanPrefab, forCpu: false);
        GiveBallVia(coordinator, 1, cpuPrefab, forCpu: true);

        GameObject humanBall = registry.GetBySlot(0).basketball;
        GameObject cpuBall = registry.GetBySlot(1).autoBasketball;
        spawned.Add(humanBall);
        spawned.Add(cpuBall);

        Assert.IsNull(GetBoundCriticalSuccessDelegate(humanBall.GetComponent<BasketBall>()), "the human ball must remain unbound");
        Assert.IsNull(GetBoundCriticalSuccessDelegate(cpuBall.GetComponent<BasketBallAuto>()), "the CPU ball must remain unbound");
    }

    /// <summary>
    /// Direct coverage of the adapter itself, now owned by <c>GameLevelManager</c>: a swish reached
    /// before the cheerleader's own <c>Start()</c> has assigned
    /// <see cref="BehaviorNpcCritical.instance"/> (the exact ordering
    /// <c>SpawnCoordinator.SpawnBasketballs</c> then <c>SpawnCheerleader</c> guarantees) must not throw
    /// or otherwise fail the shot.
    /// </summary>
    [Test]
    public void PlayCriticalSuccessPresentationDoesNotThrowWhenBehaviorNpcCriticalInstanceIsAbsent()
    {
        BehaviorNpcCritical.instance = null;
        MethodInfo method = typeof(GameLevelManager).GetMethod("PlayCriticalSuccessPresentation", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager must declare a PlayCriticalSuccessPresentation adapter");

        Assert.DoesNotThrow(() => method.Invoke(null, null));
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 56: a <see cref="BehaviorNpcCritical"/> whose own <c>Start()</c> never ran
    /// - as in an EditMode test, adding the component does not run Unity lifecycle methods - leaves its
    /// private <c>anim</c> field at its default, unset value. This is deliberate, not an oversight: it
    /// turns "was this instance actually reached" into a positive, checkable signal. A
    /// <c>DoesNotThrow</c> assertion on a no-op path proves nothing about whether the call underneath it
    /// happened at all (a completely gutted adapter body would also not throw); routing the call into an
    /// instance whose <c>anim</c> is null makes reaching
    /// <c>playAnimationCriticalSuccesful() -&gt; playCriticalSuccessfulAnim() -&gt; anim.Play(...)</c>
    /// produce a specific, predictable <see cref="NullReferenceException"/> (wrapped in a
    /// <see cref="TargetInvocationException"/> by the reflection <c>Invoke</c> below) - evidence the
    /// call chain actually executed, not just evidence that nothing crashed.
    /// </summary>
    private BehaviorNpcCritical BuildUnstartedCheerleader(string name)
    {
        return Spawn(name).AddComponent<BehaviorNpcCritical>();
    }

    /// <summary>
    /// AUD-012 Phase 2b: proves the relocated adapter still resolves <c>BehaviorNpcCritical.instance</c>
    /// fresh at invocation time rather than at composition time - an instance assigned only after the
    /// "no instance yet" case above is still reached by the same callback. This is the ownership move's
    /// whole point: only where the lookup lives changed, not when it happens. Reachability is proven by
    /// the <see cref="NullReferenceException"/> the late-assigned instance's own unset <c>anim</c> field
    /// produces - see <see cref="BuildUnstartedCheerleader"/>.
    /// </summary>
    [Test]
    public void PlayCriticalSuccessPresentationReachesAnInstanceAssignedAfterComposition()
    {
        BehaviorNpcCritical.instance = null;
        MethodInfo method = typeof(GameLevelManager).GetMethod("PlayCriticalSuccessPresentation", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager must declare a PlayCriticalSuccessPresentation adapter");

        Assert.DoesNotThrow(() => method.Invoke(null, null), "no instance yet must still no-op safely");

        BehaviorNpcCritical.instance = BuildUnstartedCheerleader("late-cheerleader");

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, null),
            "a late-assigned instance must actually be reached, proven by the NullReferenceException its own unset anim field produces");
        Assert.That(thrown.InnerException, Is.InstanceOf<NullReferenceException>());
    }

    /// <summary>
    /// Proves swapping <c>BehaviorNpcCritical.instance</c> for a second instance is observed rather than
    /// erroring or silently continuing to reference the first - both instances are deliberately
    /// unstarted (see <see cref="BuildUnstartedCheerleader"/>), so each invocation reaching a live,
    /// currently-assigned instance is proven the same way: a <see cref="NullReferenceException"/> from
    /// that instance's own unset <c>anim</c>, not a crash from touching a stale reference.
    /// </summary>
    [Test]
    public void PlayCriticalSuccessPresentationReachesAReplacementInstance()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod("PlayCriticalSuccessPresentation", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager must declare a PlayCriticalSuccessPresentation adapter");

        BehaviorNpcCritical.instance = BuildUnstartedCheerleader("first-cheerleader");
        TargetInvocationException first = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, null),
            "precondition: the original instance must be reached");
        Assert.That(first.InnerException, Is.InstanceOf<NullReferenceException>());

        BehaviorNpcCritical.instance = BuildUnstartedCheerleader("replacement-cheerleader");
        TargetInvocationException replacement = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, null),
            "the replacement instance must be reached too, not silently skipped after the swap");
        Assert.That(replacement.InnerException, Is.InstanceOf<NullReferenceException>());
    }

    /// <summary>
    /// Proves clearing <c>BehaviorNpcCritical.instance</c> back to null returns the adapter to its
    /// original no-op behavior, rather than continuing to reach for (and throw on) whatever it last
    /// resolved - ruling out any cached "have I already found an instance" state.
    /// </summary>
    [Test]
    public void PlayCriticalSuccessPresentationNoOpsAfterInstanceIsCleared()
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod("PlayCriticalSuccessPresentation", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "GameLevelManager must declare a PlayCriticalSuccessPresentation adapter");

        BehaviorNpcCritical.instance = BuildUnstartedCheerleader("cleared-cheerleader");
        Assert.Throws<TargetInvocationException>(() => method.Invoke(null, null),
            "precondition: the instance must be reached while assigned");

        // Simulates BehaviorNpcCritical.OnDestroy clearing the static, without actually destroying the
        // component - this fixture's own TearDown already does a real DestroyImmediate pass separately.
        BehaviorNpcCritical.instance = null;

        Assert.DoesNotThrow(() => method.Invoke(null, null), "clearing the instance must return to no-op behavior");
    }
}
