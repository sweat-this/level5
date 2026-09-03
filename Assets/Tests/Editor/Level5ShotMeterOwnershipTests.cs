using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// AUD-010 Phase 1c: <see cref="ShotMeter"/> now reads its shooter data through an explicitly bound
/// <see cref="IShooterActor"/> (<see cref="ShotMeter.BindOwner"/>), bound by
/// <see cref="SpawnCoordinator"/> during participant composition, instead of reaching a parent
/// <c>PlayerIdentifier</c> itself. A CPU shooter's own <see cref="IBasketballRuntime"/> - needed only
/// to resolve its automatic meter value - is bound separately and optionally
/// (<see cref="ShotMeter.BindBasketballRuntime"/>), once that participant's basketball exists. A
/// defensive/no-ball CPU never receives one and remains a valid, bound ShotMeter.
///
/// AUD-010 Phase 2b0: <see cref="ShotMeter"/> also reads its match rules through a separately, and
/// independently, bound <see cref="ResolvedMatchRules"/> (<see cref="ShotMeter.BindMatchRules"/>),
/// bound by <see cref="SpawnCoordinator"/> alongside actor ownership, instead of reaching
/// <c>MatchRuntime.Rules</c> itself. Both bindings are mandatory for a valid <see cref="ShotMeter.Start"/>;
/// only the basketball runtime remains optional.
///
/// Mirrors <see cref="Level5RangeMeterOwnershipTests"/>'s shape: synthetic-actor tests exercise
/// ShotMeter directly, and the SpawnCoordinator_*/GiveBall_* tests drive the real
/// RegisterHuman/RegisterCpu/GiveBall private methods (the sole production composition path) to prove
/// the actual wiring, not just a stand-in.
/// </summary>
public class Level5ShotMeterOwnershipTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private PlayerRegistry registry;
    private SpawnCoordinator coordinator;
    private ResolvedMatchRules coordinatorRules;
    private MethodInfo registerHuman;
    private MethodInfo registerCpu;
    private MethodInfo giveBall;

    [SetUp]
    public void SetUp()
    {
        // ShotMeter's visibility gate used to read MatchRuntime.Rules (via ActiveMatch); it now reads
        // an explicitly bound ResolvedMatchRules instead, but clearing this still keeps every other
        // MatchRuntime-reading system deterministic regardless of what ran before this test.
        ActiveMatch.Clear();

        registry = new PlayerRegistry();
        // AUD-010 Phase 2b0: a non-default rules object (CombatMode.Standard, not the default None) so
        // a ShotMeter that accidentally binds a fresh default ResolvedMatchRules instead of this exact
        // reference cannot pass the AreSame assertions below.
        coordinatorRules = new ResolvedMatchRules(combatMode: CombatMode.Standard, enemiesEnabled: false, hardcore: false, enemiesOnly: false);
        coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            registry,
            coordinatorRules,
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None,
            new FakeGroundHeightProvider());

        registerHuman = typeof(SpawnCoordinator).GetMethod("RegisterHuman", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerHuman, "SpawnCoordinator.RegisterHuman must exist");
        registerCpu = typeof(SpawnCoordinator).GetMethod("RegisterCpu", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerCpu, "SpawnCoordinator.RegisterCpu must exist");
        giveBall = typeof(SpawnCoordinator).GetMethod("GiveBall", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(giveBall, "SpawnCoordinator.GiveBall must exist");
    }

    [TearDown]
    public void TearDown()
    {
        ActiveMatch.Clear();

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

    private void InvokeRegisterHuman(GameObject participant, int pid, PlayerSlot slot)
    {
        registerHuman.Invoke(coordinator, new object[] { participant, pid, slot });
    }

    private void InvokeRegisterCpu(GameObject participant, int pid)
    {
        registerCpu.Invoke(coordinator, new object[] { participant, pid });
    }

    private void InvokeGiveBall(int slotId, GameObject prefab, bool forCpu)
    {
        giveBall.Invoke(coordinator, new object[] { slotId, prefab, Vector3.zero, forCpu });
    }

    private static void InvokeStart(ShotMeter meter)
    {
        MethodInfo start = typeof(ShotMeter).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(start, "ShotMeter must declare Start()");
        start.Invoke(meter, null);
    }

    private static void InvokeUpdate(ShotMeter meter)
    {
        MethodInfo update = typeof(ShotMeter).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(update, "ShotMeter must declare Update()");
        update.Invoke(meter, null);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        field.SetValue(target, value);
    }

    /// <summary>
    /// Builds a ShotMeter with the Slider/Text/meter-graphic presentation objects Start() resolves
    /// (transform.Find("slider_value_text")/("slider_message_text"), plus the meterRed/Yellow/Green/
    /// Handle fields), so Start() can run to completion exactly as it does against the authored prefab.
    /// </summary>
    private ShotMeter MakePresentableMeter(string name)
    {
        GameObject meterGo = Spawn(name);
        ShotMeter meter = meterGo.AddComponent<ShotMeter>();

        GameObject sliderGo = Spawn($"{name}-slider");
        sliderGo.transform.parent = meterGo.transform;
        sliderGo.AddComponent<Slider>();

        GameObject valueTextGo = Spawn($"{name}-value-text");
        valueTextGo.name = "slider_value_text";
        valueTextGo.transform.parent = meterGo.transform;
        valueTextGo.AddComponent<Text>();

        GameObject messageTextGo = Spawn($"{name}-message-text");
        messageTextGo.name = "slider_message_text";
        messageTextGo.transform.parent = meterGo.transform;
        messageTextGo.AddComponent<Text>();

        meter.meterRed = Spawn($"{name}-red");
        meter.meterYellow = Spawn($"{name}-yellow");
        meter.meterGreen = Spawn($"{name}-green");
        meter.meterHandle = Spawn($"{name}-handle");

        return meter;
    }

    // ==================== BindOwner ====================

    [Test]
    public void BindOwner_HumanActor_Binds()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeShooterActor actor = new FakeShooterActor();

        meter.BindOwner(actor, isCpu: false);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actor, GetPrivateField(meter, "actor"));
        Assert.IsFalse((bool)GetPrivateField(meter, "isCpu"));
    }

    [Test]
    public void BindOwner_CpuActor_Binds()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeShooterActor actor = new FakeShooterActor();

        meter.BindOwner(actor, isCpu: true);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actor, GetPrivateField(meter, "actor"));
        Assert.IsTrue((bool)GetPrivateField(meter, "isCpu"));
    }

    [Test]
    public void BindOwner_NullActor_LogsAndLeavesUnbound()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();

        LogAssert.Expect(LogType.Error, new Regex("null actor"));
        meter.BindOwner(null, isCpu: false);

        Assert.IsFalse(meter.Bound);
    }

    [Test]
    public void BindOwner_SecondCall_IsRejectedAndOriginalOwnerRetained()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeShooterActor first = new FakeShooterActor();
        FakeShooterActor second = new FakeShooterActor();
        meter.BindOwner(first, isCpu: false);

        LogAssert.Expect(LogType.Error, new Regex("already bound"));
        meter.BindOwner(second, isCpu: true);

        Assert.AreSame(first, GetPrivateField(meter, "actor"), "a second BindOwner call must not overwrite the original binding");
        Assert.IsFalse((bool)GetPrivateField(meter, "isCpu"), "a second BindOwner call must not overwrite the original role");
    }

    // ==================== BindMatchRules ====================

    private static ResolvedMatchRules NormalRules()
    {
        return new ResolvedMatchRules(combatMode: CombatMode.Standard, enemiesEnabled: false, hardcore: false, enemiesOnly: false);
    }

    [Test]
    public void BindMatchRules_ValidRules_Binds()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        ResolvedMatchRules rules = NormalRules();

        meter.BindMatchRules(rules);

        Assert.AreSame(rules, GetPrivateField(meter, "matchRules"));
    }

    [Test]
    public void BindMatchRules_Null_LogsAndLeavesUnbound()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();

        LogAssert.Expect(LogType.Error, new Regex("null match rules"));
        meter.BindMatchRules(null);

        Assert.IsNull(GetPrivateField(meter, "matchRules"));
    }

    [Test]
    public void BindMatchRules_SecondCall_IsRejectedAndOriginalRulesRetained()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        ResolvedMatchRules first = NormalRules();
        ResolvedMatchRules second = new ResolvedMatchRules(hardcore: true);
        meter.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already has bound match rules"));
        meter.BindMatchRules(second);

        Assert.AreSame(first, GetPrivateField(meter, "matchRules"), "a second BindMatchRules call must not overwrite the original binding");
    }

    // ==================== Start() ====================

    [Test]
    public void Start_WithoutBoundOwner_DisablesOnlyTheComponent()
    {
        ShotMeter meter = Spawn("unbound-meter").AddComponent<ShotMeter>();

        LogAssert.Expect(LogType.Error, new Regex("no bound owner"));
        InvokeStart(meter);

        Assert.IsFalse(meter.enabled, "an unbound ShotMeter must disable itself");
        Assert.IsTrue(meter.gameObject.activeSelf, "an unbound ShotMeter must not deactivate its whole GameObject - it may host unrelated UI");
    }

    [Test]
    public void Start_OwnerBoundWithoutMatchRules_DisablesOnlyTheComponent()
    {
        ShotMeter meter = Spawn("rules-unbound-meter").AddComponent<ShotMeter>();
        meter.BindOwner(new FakeShooterActor(), isCpu: false);

        LogAssert.Expect(LogType.Error, new Regex("no bound match rules"));
        InvokeStart(meter);

        Assert.IsFalse(meter.enabled, "a ShotMeter with no bound match rules must disable itself");
        Assert.IsTrue(meter.gameObject.activeSelf, "a ShotMeter with no bound match rules must not deactivate its whole GameObject");
    }

    [Test]
    public void Start_ActorBoundWithNoBasketballRuntime_Succeeds()
    {
        // A defensive/no-ball CPU: actor ownership and match rules alone are sufficient. No runtime is
        // ever bound for it, and Start() must not require one - this is the no-ball composition this
        // migration must keep valid.
        ShotMeter meter = MakePresentableMeter("defender-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: true);
        meter.BindMatchRules(NormalRules());

        Assert.DoesNotThrow(() => InvokeStart(meter));
        Assert.IsTrue(meter.enabled);
    }

    [Test]
    public void Start_HumanUnderNormalRules_MeterGraphicsRemainVisible()
    {
        ShotMeter meter = MakePresentableMeter("human-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchRules(NormalRules());

        InvokeStart(meter);

        Assert.IsTrue(meter.meterRed.activeSelf);
        Assert.IsTrue(meter.meterHandle.activeSelf);
    }

    [Test]
    public void Start_HardcoreHuman_HidesMeterGraphics()
    {
        ShotMeter meter = MakePresentableMeter("hardcore-human-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchRules(new ResolvedMatchRules(hardcore: true, enemiesOnly: false, combatMode: CombatMode.None));

        InvokeStart(meter);

        Assert.IsFalse(meter.meterRed.activeSelf);
        Assert.IsFalse(meter.meterHandle.activeSelf);
    }

    [Test]
    public void Start_EnemiesOnlyHuman_HidesMeterGraphics()
    {
        ShotMeter meter = MakePresentableMeter("enemies-only-human-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchRules(new ResolvedMatchRules(hardcore: false, enemiesOnly: true, combatMode: CombatMode.None));

        InvokeStart(meter);

        Assert.IsFalse(meter.meterRed.activeSelf);
        Assert.IsFalse(meter.meterHandle.activeSelf);
    }

    [Test]
    public void Start_BattleRoyalHuman_HidesMeterGraphics()
    {
        ShotMeter meter = MakePresentableMeter("battle-royal-human-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchRules(new ResolvedMatchRules(hardcore: false, enemiesOnly: false, combatMode: CombatMode.BattleRoyal));

        InvokeStart(meter);

        Assert.IsFalse(meter.meterRed.activeSelf);
        Assert.IsFalse(meter.meterHandle.activeSelf);
    }

    [Test]
    public void Start_EnemiesEnabledOnlyHuman_MeterGraphicsRemainVisible()
    {
        // Regression guard: EnemiesEnabled (a shooting mode with the enemies modifier switched on) is
        // distinct from EnemiesOnly (a fighting mode) and must never, by itself, hide the meter - the
        // exact old MatchRuntime.Rules predicate never included it, and this migration must not widen
        // the check to include it either.
        ShotMeter meter = MakePresentableMeter("enemies-enabled-human-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchRules(new ResolvedMatchRules(
            hardcore: false, enemiesOnly: false, combatMode: CombatMode.None, enemiesEnabled: true));

        InvokeStart(meter);

        Assert.IsTrue(meter.meterRed.activeSelf);
        Assert.IsTrue(meter.meterHandle.activeSelf);
    }

    [Test]
    public void Start_CpuActor_HidesMeterGraphicsRegardlessOfRules()
    {
        // Legacy condition read "... || playerIdentifier.isCpu" - a CPU meter is always hidden (it
        // resolves automatically, never shown to a player). Preserved as "... || isCpu" against the
        // bound role.
        ShotMeter meter = MakePresentableMeter("cpu-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: true);
        meter.BindMatchRules(NormalRules());

        InvokeStart(meter);

        Assert.IsFalse(meter.meterRed.activeSelf);
        Assert.IsFalse(meter.meterHandle.activeSelf);
    }

    [Test]
    public void Start_FillTimeDerivesFromTheBoundActorsJumpForce()
    {
        ShotMeter meter = MakePresentableMeter("fill-time-meter");
        FakeShooterActor actor = new FakeShooterActor { JumpForce = 12.5f };
        meter.BindOwner(actor, isCpu: true);
        meter.BindMatchRules(NormalRules());

        InvokeStart(meter);

        float expected = Mathf.Abs(12.5f / Physics.gravity.y);
        Assert.That(meter.meterFillTime, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void HumanMeterResult_ElapsedRatioAtFillTime_AppliesThePeakPenaltyCorrection()
    {
        // example : 90 - ABS( 100 -115 [ 15 ]) --> 100 - 15 = 75. Preserved unchanged: this pins the
        // >=100 correction path at exactly the fill-time boundary (ratio == 1.0 -> value 100 -> 90).
        //
        // The production formula reads Time.time twice - once here to set meterStartTime, once inside
        // Update() - with real wall-clock time able to elapse between the two (reflection-call
        // overhead in this test, or engine overhead in general). A short fill time makes that gap a
        // large fraction of the window and can tip the ratio past the razor-thin >=100 boundary,
        // flipping which branch's arithmetic applies. A large JumpForce produces a multi-second fill
        // time, so the same few milliseconds of jitter become a negligible fraction of the ratio.
        ShotMeter meter = MakePresentableMeter("human-timing-meter");
        FakeShooterActor actor = new FakeShooterActor { JumpForce = 500f };
        meter.BindOwner(actor, isCpu: false);
        meter.BindMatchRules(NormalRules());
        InvokeStart(meter);

        float fillTime = meter.meterFillTime;
        float now = Time.time;
        SetPrivateField(meter, "meterStartTime", now - fillTime);
        SetPrivateField(meter, "meterEnded", true);

        InvokeUpdate(meter);

        Assert.That(meter.SliderValueOnButtonPress, Is.EqualTo(90f).Within(0.5f));
    }

    // ==================== BindBasketballRuntime ====================

    [Test]
    public void BindBasketballRuntime_ValidMatchingRuntime_Binds()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeShooterActor actor = new FakeShooterActor();
        meter.BindOwner(actor, isCpu: true);
        FakeBasketballRuntime runtime = new FakeBasketballRuntime { Actor = actor, IsCpu = true };

        meter.BindBasketballRuntime(runtime);

        Assert.AreSame(runtime, GetPrivateField(meter, "basketballRuntime"));
    }

    [Test]
    public void BindBasketballRuntime_NullRuntime_LogsAndLeavesUnbound()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        meter.BindOwner(new FakeShooterActor(), isCpu: true);

        LogAssert.Expect(LogType.Error, new Regex("null basketball runtime"));
        meter.BindBasketballRuntime(null);

        Assert.IsNull(GetPrivateField(meter, "basketballRuntime"));
    }

    [Test]
    public void BindBasketballRuntime_BeforeOwner_Rejected()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeBasketballRuntime runtime = new FakeBasketballRuntime { Actor = new FakeShooterActor(), IsCpu = true };

        LogAssert.Expect(LogType.Error, new Regex("before its actor owner"));
        meter.BindBasketballRuntime(runtime);

        Assert.IsNull(GetPrivateField(meter, "basketballRuntime"));
    }

    [Test]
    public void BindBasketballRuntime_ActorMismatch_Rejected()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeShooterActor ownerActor = new FakeShooterActor();
        meter.BindOwner(ownerActor, isCpu: true);
        FakeBasketballRuntime runtime = new FakeBasketballRuntime { Actor = new FakeShooterActor(), IsCpu = true };

        LogAssert.Expect(LogType.Error, new Regex("does not belong to its own owner"));
        meter.BindBasketballRuntime(runtime);

        Assert.IsNull(GetPrivateField(meter, "basketballRuntime"));
    }

    [Test]
    public void BindBasketballRuntime_CpuRoleMismatch_Rejected()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeShooterActor actor = new FakeShooterActor();
        meter.BindOwner(actor, isCpu: false);
        FakeBasketballRuntime runtime = new FakeBasketballRuntime { Actor = actor, IsCpu = true };

        LogAssert.Expect(LogType.Error, new Regex("does not belong to its own owner"));
        meter.BindBasketballRuntime(runtime);

        Assert.IsNull(GetPrivateField(meter, "basketballRuntime"));
    }

    [Test]
    public void BindBasketballRuntime_Rebind_Rejected()
    {
        ShotMeter meter = Spawn("meter").AddComponent<ShotMeter>();
        FakeShooterActor actor = new FakeShooterActor();
        meter.BindOwner(actor, isCpu: true);
        FakeBasketballRuntime first = new FakeBasketballRuntime { Actor = actor, IsCpu = true };
        FakeBasketballRuntime second = new FakeBasketballRuntime { Actor = actor, IsCpu = true };
        meter.BindBasketballRuntime(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound basketball runtime"));
        meter.BindBasketballRuntime(second);

        Assert.AreSame(first, GetPrivateField(meter, "basketballRuntime"), "a second BindBasketballRuntime call must not overwrite the original binding");
    }

    [Test]
    public void BindBasketballRuntime_AnotherParticipantsRuntimeCannotBindToThisMeter()
    {
        ShotMeter meterA = Spawn("meter-a").AddComponent<ShotMeter>();
        FakeShooterActor actorA = new FakeShooterActor();
        meterA.BindOwner(actorA, isCpu: true);

        FakeShooterActor actorB = new FakeShooterActor();
        FakeBasketballRuntime runtimeB = new FakeBasketballRuntime { Actor = actorB, IsCpu = true };

        LogAssert.Expect(LogType.Error, new Regex("does not belong to its own owner"));
        meterA.BindBasketballRuntime(runtimeB);

        Assert.IsNull(GetPrivateField(meterA, "basketballRuntime"));
    }

    // ==================== CPU meter resolution ====================

    [Test]
    public void CpuMeterResolution_MissingRuntime_LogsAndPreservesTheMeterCycle()
    {
        // A normal CPU shooter reaching automatic resolution with no bound runtime is invalid
        // composition - logged, not deadlocked: the meterStarted/meterEnded transition that unblocks
        // BasketBallAuto.LaunchBasketBall's WaitUntil must still run on this path.
        ShotMeter meter = MakePresentableMeter("cpu-no-runtime");
        meter.BindOwner(new FakeShooterActor(), isCpu: true);
        meter.BindMatchRules(NormalRules());
        InvokeStart(meter);
        SetPrivateField(meter, "meterEnded", true);

        LogAssert.Expect(LogType.Error, new Regex("no bound basketball runtime"));
        Assert.DoesNotThrow(() => InvokeUpdate(meter));

        Assert.IsFalse((bool)GetPrivateField(meter, "meterEnded"));
        Assert.IsFalse((bool)GetPrivateField(meter, "meterStarted"));
    }

    [Test]
    public void CpuMeterResolution_BoundRuntimeIsNotABasketBallAuto_LogsAndPreservesTheMeterCycle()
    {
        ShotMeter meter = MakePresentableMeter("cpu-wrong-runtime-type");
        FakeShooterActor actor = new FakeShooterActor();
        meter.BindOwner(actor, isCpu: true);
        meter.BindMatchRules(NormalRules());
        InvokeStart(meter);
        meter.BindBasketballRuntime(new FakeBasketballRuntime { Actor = actor, IsCpu = true });
        SetPrivateField(meter, "meterEnded", true);

        LogAssert.Expect(LogType.Error, new Regex("not a BasketBallAuto"));
        Assert.DoesNotThrow(() => InvokeUpdate(meter));

        Assert.IsFalse((bool)GetPrivateField(meter, "meterEnded"));
    }

    [Test]
    public void CpuMeterResolution_ValidBoundBasketBallAuto_DispatchesToItsOwnRollWithNoErrors()
    {
        // The happy path the two error-path tests above don't cover: a real BasketBallAuto, correctly
        // bound, must actually be reached and produce a value - not just correctly rejected when it
        // isn't there or isn't the right type. Builds just enough of BasketBallAuto's own state
        // (currentShooter/gameStats, normally set in its own Start()) for rollForAutoPlayerSliderValue
        // to run without exercising BasketBallAuto.Start() itself, which needs a full scene hierarchy
        // this test has no reason to construct.
        ShotMeter meter = MakePresentableMeter("cpu-real-runtime");
        FakeShooterActor actor = new FakeShooterActor();
        meter.BindOwner(actor, isCpu: true);
        meter.BindMatchRules(NormalRules());
        InvokeStart(meter);

        GameObject ballGo = Spawn("cpu-real-ball");
        ballGo.AddComponent<BasketBallState>();
        GameStats gameStats = ballGo.AddComponent<GameStats>();
        BasketBallAuto ball = ballGo.AddComponent<BasketBallAuto>();
        GameObject ownerActorGo = Spawn("cpu-real-ball-owner");
        ball.BindOwner(participantId: 1, isCpu: true, isPrimary: false, ownerActor: ownerActorGo, actor: actor);
        SetPrivateField(ball, "currentShooter", actor.ShooterAttributes);
        SetPrivateField(ball, "gameStats", gameStats);

        meter.BindBasketballRuntime(ball);
        SetPrivateField(meter, "meterEnded", true);

        // No LogAssert.Expect: an unexpected Debug.LogError fails an EditMode test by default, so a
        // regression that stops ResolveCpuMeterValue from actually reaching BasketBallAuto - or that
        // reintroduces one of its own guard errors along a supposedly-valid path - fails here.
        InvokeUpdate(meter);

        Assert.That(meter.SliderValueOnButtonPress, Is.InRange(0f, 100f));
        Assert.IsFalse((bool)GetPrivateField(meter, "meterEnded"));
    }

    // ==================== SpawnCoordinator wiring ====================

    private GameObject SpawnHumanParticipantWithShotMeter(int pid)
    {
        GameObject actorGo = Spawn($"human-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        GameObject meterGo = Spawn($"human-shotmeter-{pid}");
        meterGo.transform.parent = actorGo.transform;
        meterGo.AddComponent<ShotMeter>();

        return actorGo;
    }

    private GameObject SpawnCpuParticipantWithShotMeter(int pid)
    {
        GameObject actorGo = Spawn($"cpu-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<AutoPlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        GameObject meterGo = Spawn($"cpu-shotmeter-{pid}");
        meterGo.transform.parent = actorGo.transform;
        meterGo.AddComponent<ShotMeter>();

        return actorGo;
    }

    [Test]
    public void SpawnCoordinator_RegisterHuman_BindsChildShotMeterToThatParticipantsOwnActor()
    {
        GameObject actorGo = SpawnHumanParticipantWithShotMeter(pid: 0);
        ShotMeter meter = actorGo.GetComponentInChildren<ShotMeter>(true);

        InvokeRegisterHuman(actorGo, 0, null);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actorGo.GetComponent<PlayerController>(), GetPrivateField(meter, "actor"));
        Assert.IsFalse((bool)GetPrivateField(meter, "isCpu"));
        Assert.AreSame(coordinatorRules, GetPrivateField(meter, "matchRules"));
    }

    [Test]
    public void SpawnCoordinator_RegisterCpu_BindsChildShotMeterToThatParticipantsOwnActor()
    {
        GameObject actorGo = SpawnCpuParticipantWithShotMeter(pid: 1);
        ShotMeter meter = actorGo.GetComponentInChildren<ShotMeter>(true);

        InvokeRegisterCpu(actorGo, 1);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actorGo.GetComponent<AutoPlayerController>(), GetPrivateField(meter, "actor"));
        Assert.IsTrue((bool)GetPrivateField(meter, "isCpu"));
        Assert.AreSame(coordinatorRules, GetPrivateField(meter, "matchRules"));
    }

    [Test]
    public void SpawnCoordinator_SecondaryHumanShotMeterBindsToItsOwnActor_NotThePrimarys()
    {
        GameObject primary = SpawnHumanParticipantWithShotMeter(pid: 0);
        GameObject secondary = SpawnHumanParticipantWithShotMeter(pid: 1);

        InvokeRegisterHuman(primary, 0, null);
        InvokeRegisterHuman(secondary, 1, null);

        ShotMeter secondaryMeter = secondary.GetComponentInChildren<ShotMeter>(true);
        object boundActor = GetPrivateField(secondaryMeter, "actor");

        Assert.AreSame(secondary.GetComponent<PlayerController>(), boundActor);
        Assert.AreNotSame(primary.GetComponent<PlayerController>(), boundActor,
            "a secondary participant's ShotMeter must never collapse to the primary participant's actor");
        Assert.AreSame(coordinatorRules, GetPrivateField(secondaryMeter, "matchRules"),
            "a secondary participant's ShotMeter must still receive this match's own resolved rules");
    }

    [Test]
    public void SpawnCoordinator_InactiveChildShotMeter_ReceivesBothBindings()
    {
        // GetComponentsInChildren(true) reaches inactive/disabled authored copies - binding itself has
        // no presentation side effects, so an inactive meter must be composed correctly in case it is
        // activated later.
        GameObject actorGo = SpawnHumanParticipantWithShotMeter(pid: 0);
        ShotMeter meter = actorGo.GetComponentInChildren<ShotMeter>(true);
        meter.gameObject.SetActive(false);

        InvokeRegisterHuman(actorGo, 0, null);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(coordinatorRules, GetPrivateField(meter, "matchRules"));
    }

    [Test]
    public void SpawnCoordinator_PrefabsWithNoShotMeterAreUnaffected()
    {
        // Most CPU compositions (e.g. Lockdown's defender) carry no ShotMeter at all -
        // RegisterHuman/RegisterCpu must not require one.
        GameObject actorGo = Spawn("human-actor-no-meter");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        Assert.DoesNotThrow(() => InvokeRegisterHuman(actorGo, 0, null));
    }

    [Test]
    public void SpawnCoordinator_DefensiveCpuWithNoBallKeepsItsShotMeterBoundWithNoRuntime()
    {
        // GiveBall is only ever called for slots that receive a ball. A defensive/no-ball CPU's
        // ShotMeter must still be actor-bound after RegisterCpu, with no runtime - and stay that way,
        // since nothing subsequently calls BindBasketballRuntime for it.
        GameObject defender = SpawnCpuParticipantWithShotMeter(pid: 1);

        InvokeRegisterCpu(defender, 1);

        ShotMeter meter = defender.GetComponentInChildren<ShotMeter>(true);
        Assert.IsTrue(meter.Bound);
        Assert.IsNull(GetPrivateField(meter, "basketballRuntime"));
        Assert.AreSame(coordinatorRules, GetPrivateField(meter, "matchRules"),
            "a defensive/no-ball CPU's ShotMeter must still receive this match's resolved rules");
    }

    // ==================== GiveBall wiring ====================

    [Test]
    public void GiveBall_AssociatesTheOwningHumanParticipantsRuntimeWithItsOwnShotMeter()
    {
        GameObject humanActor = SpawnHumanParticipantWithShotMeter(pid: 0);
        InvokeRegisterHuman(humanActor, 0, null);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        Assert.IsNotNull(humanPrefab, "human basketball prefab failed to load");

        InvokeGiveBall(0, humanPrefab, forCpu: false);

        GameObject ball = registry.GetBySlot(0).basketball;
        Assert.IsNotNull(ball);
        spawned.Add(ball);

        ShotMeter meter = humanActor.GetComponentInChildren<ShotMeter>(true);
        IBasketballRuntime runtime = ball.GetComponent<IBasketballRuntime>();
        Assert.AreSame(runtime, GetPrivateField(meter, "basketballRuntime"));
    }

    [Test]
    public void GiveBall_TwoIndependentCpuParticipantsEachBindTheirOwnRuntimeToTheirOwnMeter()
    {
        GameObject humanActor = SpawnHumanParticipantWithShotMeter(pid: 0);
        InvokeRegisterHuman(humanActor, 0, null);
        GameObject cpu1 = SpawnCpuParticipantWithShotMeter(pid: 1);
        InvokeRegisterCpu(cpu1, 1);
        GameObject cpu2 = SpawnCpuParticipantWithShotMeter(pid: 2);
        InvokeRegisterCpu(cpu2, 2);

        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);
        Assert.IsNotNull(humanPrefab, "human basketball prefab failed to load");
        Assert.IsNotNull(cpuPrefab, "CPU basketball prefab failed to load");

        InvokeGiveBall(0, humanPrefab, forCpu: false);
        InvokeGiveBall(1, cpuPrefab, forCpu: true);
        InvokeGiveBall(2, cpuPrefab, forCpu: true);

        GameObject humanBall = registry.GetBySlot(0).basketball;
        GameObject ball1 = registry.GetBySlot(1).autoBasketball;
        GameObject ball2 = registry.GetBySlot(2).autoBasketball;
        spawned.Add(humanBall);
        spawned.Add(ball1);
        spawned.Add(ball2);

        ShotMeter meter1 = cpu1.GetComponentInChildren<ShotMeter>(true);
        ShotMeter meter2 = cpu2.GetComponentInChildren<ShotMeter>(true);

        object runtime1 = GetPrivateField(meter1, "basketballRuntime");
        object runtime2 = GetPrivateField(meter2, "basketballRuntime");

        Assert.AreSame(ball1.GetComponent<IBasketballRuntime>(), runtime1);
        Assert.AreSame(ball2.GetComponent<IBasketballRuntime>(), runtime2);
        Assert.AreNotSame(runtime1, runtime2,
            "each CPU's ShotMeter must bind its own basketball runtime, never another participant's");
    }

    // ==================== test doubles ====================

    private sealed class FakeShooterActor : IShooterActor
    {
        public bool HasBasketball { get; set; }
        public bool FacingFront => true;
        public bool Grounded => false;
        public bool InAir { get; set; }
        public bool InDunkState => false;
        public float DistanceFromRim { get; set; }
        public float JumpForce { get; set; } = 10f;
        public ShooterAttributes ShooterAttributes => new ShooterAttributes(
            "fake", 0, 0, 0, 0, 0, 0, 0, 0, JumpForce, 0);
        public int Clutch => 0;
        public float ShotMeterSliderValue => 0f;
        public bool ShotMeterEnded => true;
        public void SetAnimBool(string name, bool value) { }
        public void SetAnimTrigger(string name) { }
        public void LockCallBallToPlayer(bool locked) { }
        public void DisplayShotMeterMessage(string message) { }
        public void EndShootCycle() { }
    }

    private sealed class FakeBasketballRuntime : IBasketballRuntime
    {
        public int ParticipantId { get; set; }
        public bool IsCpu { get; set; }
        public bool IsPrimary { get; set; }
        public GameObject OwnerActor { get; set; }
        public IShooterActor Actor { get; set; }
        public BasketBallState State => null;
        public GameStats Stats => null;
        public float LastShotDistance => 0f;
        public void BindOwner(int participantId, bool isCpu, bool isPrimary, GameObject ownerActor, IShooterActor actor) { }
    }

    // AUD-010 Phase 1c: GiveBall now also binds a human ball's IGroundHeightProvider - unrelated to
    // this file's ShotMeter ownership focus, but a coordinator built without one makes every
    // human-ball GiveBall call here log an unexpected error (see
    // Level5BasketballGroundHeightProviderTests for that binding's own coverage). Supplying a stub
    // keeps this file's tests exercising only what they are named for.
    private sealed class FakeGroundHeightProvider : IGroundHeightProvider
    {
        public float GroundHeight => 0f;
    }
}
