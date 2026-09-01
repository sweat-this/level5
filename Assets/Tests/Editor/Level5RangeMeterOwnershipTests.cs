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
/// AUD-010 Phase 1c: <see cref="RangeMeter"/> now reads its shooter data through an explicitly bound
/// <see cref="IShooterActor"/> (<see cref="RangeMeter.BindOwner"/>), bound by
/// <see cref="SpawnCoordinator"/> during participant composition, instead of reaching
/// <c>GameLevelManager.instance.players[0]</c> itself.
///
/// The synthetic-actor tests exercise <see cref="RangeMeter"/> directly. The
/// <c>SpawnCoordinator_*</c> tests drive the real <c>RegisterHuman</c>/<c>RegisterCpu</c> private
/// methods (the sole production participant-composition path, mirroring
/// <see cref="Level5BasketballOwnershipBindingTests"/>'s use of <c>GiveBall</c>) to prove the actual
/// wiring, not just a stand-in.
/// </summary>
public class Level5RangeMeterOwnershipTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private PlayerRegistry registry;
    private SpawnCoordinator coordinator;
    private MethodInfo registerHuman;
    private MethodInfo registerCpu;

    [SetUp]
    public void SetUp()
    {
        // RangeMeter's visibility gate reads MatchRuntime.HasConfiguration (via ActiveMatch); clearing
        // it makes "no active match" deterministic regardless of what ran before this test.
        ActiveMatch.Clear();

        registry = new PlayerRegistry();
        coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            registry,
            new ResolvedMatchRules(combatMode: CombatMode.Standard, enemiesEnabled: false, hardcore: false, enemiesOnly: false),
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None);

        registerHuman = typeof(SpawnCoordinator).GetMethod("RegisterHuman", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerHuman, "SpawnCoordinator.RegisterHuman must exist");
        registerCpu = typeof(SpawnCoordinator).GetMethod("RegisterCpu", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(registerCpu, "SpawnCoordinator.RegisterCpu must exist");
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

    private static void InvokeStart(RangeMeter meter)
    {
        MethodInfo start = typeof(RangeMeter).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(start, "RangeMeter must declare Start()");
        start.Invoke(meter, null);
    }

    private static void InvokeSetSliderValue(RangeMeter meter)
    {
        MethodInfo method = typeof(RangeMeter).GetMethod("setSliderValue", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "RangeMeter must declare setSliderValue()");
        method.Invoke(meter, null);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    /// <summary>
    /// Builds a RangeMeter with the Slider/Text presentation objects Start() resolves by name
    /// (GameObject.Find("range_slider_value_text")/("range_slider_stats_text")), so Start() can run
    /// to completion exactly as it does against the authored prefab.
    /// </summary>
    private RangeMeter MakePresentableMeter(string name)
    {
        GameObject meterGo = Spawn(name);
        RangeMeter meter = meterGo.AddComponent<RangeMeter>();

        GameObject sliderGo = Spawn($"{name}-slider");
        sliderGo.transform.parent = meterGo.transform;
        Slider slider = sliderGo.AddComponent<Slider>();
        // Matches range_meter.prefab's authored Slider range (m_MinValue: 1, m_MaxValue: 100) - the
        // component's own default (0..1) would silently clamp every value this fixture sets.
        slider.minValue = 1;
        slider.maxValue = 100;

        // Parented under the meter root, matching range_meter.prefab's authored hierarchy, so
        // deactivating the meter deactivates these along with it exactly as it does in production.
        // GameObject.Find still resolves them by name regardless of parentage.
        GameObject valueTextGo = Spawn($"{name}-value-text");
        valueTextGo.name = "range_slider_value_text";
        valueTextGo.transform.parent = meterGo.transform;
        valueTextGo.AddComponent<Text>();

        GameObject statsTextGo = Spawn($"{name}-stats-text");
        statsTextGo.name = "range_slider_stats_text";
        statsTextGo.transform.parent = meterGo.transform;
        statsTextGo.AddComponent<Text>();

        return meter;
    }

    // ==================== BindOwner ====================

    [Test]
    public void BindOwner_HumanActor_Binds()
    {
        RangeMeter meter = Spawn("meter").AddComponent<RangeMeter>();
        FakeShooterActor actor = new FakeShooterActor();

        meter.BindOwner(actor, isCpu: false);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actor, GetPrivateField(meter, "actor"));
        Assert.IsFalse((bool)GetPrivateField(meter, "isCpu"));
    }

    [Test]
    public void BindOwner_CpuActor_Binds()
    {
        RangeMeter meter = Spawn("meter").AddComponent<RangeMeter>();
        FakeShooterActor actor = new FakeShooterActor();

        meter.BindOwner(actor, isCpu: true);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actor, GetPrivateField(meter, "actor"));
        Assert.IsTrue((bool)GetPrivateField(meter, "isCpu"));
    }

    [Test]
    public void BindOwner_NullActor_LogsAndLeavesUnbound()
    {
        RangeMeter meter = Spawn("meter").AddComponent<RangeMeter>();

        LogAssert.Expect(LogType.Error, new Regex("null actor"));
        meter.BindOwner(null, isCpu: false);

        Assert.IsFalse(meter.Bound);
    }

    [Test]
    public void BindOwner_SecondCall_IsRejectedAndOriginalOwnerRetained()
    {
        RangeMeter meter = Spawn("meter").AddComponent<RangeMeter>();
        FakeShooterActor first = new FakeShooterActor();
        FakeShooterActor second = new FakeShooterActor();
        meter.BindOwner(first, isCpu: false);

        LogAssert.Expect(LogType.Error, new Regex("already bound"));
        meter.BindOwner(second, isCpu: true);

        Assert.AreSame(first, GetPrivateField(meter, "actor"), "a second BindOwner call must not overwrite the original binding");
        Assert.IsFalse((bool)GetPrivateField(meter, "isCpu"), "a second BindOwner call must not overwrite the original role");
    }

    // ==================== Start() ====================

    [Test]
    public void Start_WithoutBoundOwner_DisablesOnlyTheComponent()
    {
        RangeMeter meter = Spawn("unbound-meter").AddComponent<RangeMeter>();

        LogAssert.Expect(LogType.Error, new Regex("no bound owner"));
        InvokeStart(meter);

        Assert.IsFalse(meter.enabled, "an unbound RangeMeter must disable itself");
        Assert.IsTrue(meter.gameObject.activeSelf, "an unbound RangeMeter must not deactivate its whole GameObject - it may host unrelated UI");
    }

    [Test]
    public void Start_HumanWithNoActiveMatch_HidesTheMeter()
    {
        // Mirrors the legacy condition this migration preserves: a human meter hides when
        // !MatchRuntime.HasConfiguration, among other rules. ActiveMatch.Clear() in SetUp makes
        // HasConfiguration false deterministically.
        RangeMeter meter = MakePresentableMeter("human-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);

        InvokeStart(meter);

        Assert.IsFalse(meter.gameObject.activeInHierarchy, "a human RangeMeter must hide when there is no active match configuration");
    }

    [Test]
    public void Start_CpuActor_RemainsVisibleRegardlessOfConfiguration()
    {
        // The legacy condition is gated on "!playerIdentifier.isCpu" - a CPU meter never hides on
        // this branch. Preserved as "!isCpu" against the bound role.
        RangeMeter meter = MakePresentableMeter("cpu-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: true);

        InvokeStart(meter);

        Assert.IsTrue(meter.gameObject.activeInHierarchy, "a CPU RangeMeter's visibility must be unaffected by the human-only hide conditions");
    }

    [Test]
    public void SetSliderValue_ReadsRangeAndDistanceFromTheBoundActor()
    {
        RangeMeter meter = MakePresentableMeter("range-meter");
        FakeShooterActor actor = new FakeShooterActor { Range = 42, DistanceFromRim = 7f };
        meter.BindOwner(actor, isCpu: true); // isCpu keeps it active so Start() completes its setup
        InvokeStart(meter);

        InvokeSetSliderValue(meter);

        Slider slider = (Slider)GetPrivateField(meter, "slider");
        Text statsText = (Text)GetPrivateField(meter, "sliderStatsText");
        // (42 / (7 * 6)) * 100 == 100
        Assert.That(slider.value, Is.EqualTo(100f).Within(0.01f));
        Assert.That(statsText.text, Is.EqualTo("Range : 42 feet"));
    }

    [Test]
    public void SetSliderValue_TwoMetersBoundToDifferentActorsRemainIndependent()
    {
        RangeMeter meterA = MakePresentableMeter("meter-a");
        RangeMeter meterB = MakePresentableMeter("meter-b");
        FakeShooterActor actorA = new FakeShooterActor { Range = 10, DistanceFromRim = 5f };
        FakeShooterActor actorB = new FakeShooterActor { Range = 20, DistanceFromRim = 5f };
        meterA.BindOwner(actorA, isCpu: true);
        meterB.BindOwner(actorB, isCpu: true);
        InvokeStart(meterA);
        InvokeStart(meterB);

        InvokeSetSliderValue(meterA);
        InvokeSetSliderValue(meterB);

        Slider sliderA = (Slider)GetPrivateField(meterA, "slider");
        Slider sliderB = (Slider)GetPrivateField(meterB, "slider");
        // secondary participant cannot read participant-zero state: each meter's value tracks only
        // its own bound actor, even though both were built and started back to back.
        Assert.That(sliderA.value, Is.EqualTo((10f / (5f * 6f)) * 100f).Within(0.01f));
        Assert.That(sliderB.value, Is.EqualTo((20f / (5f * 6f)) * 100f).Within(0.01f));
        Assert.AreNotEqual(sliderA.value, sliderB.value);
    }

    // ==================== SpawnCoordinator wiring ====================

    private GameObject SpawnHumanParticipantWithRangeMeter(int pid)
    {
        GameObject actorGo = Spawn($"human-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        GameObject meterGo = Spawn($"human-rangemeter-{pid}");
        meterGo.transform.parent = actorGo.transform;
        meterGo.AddComponent<RangeMeter>();

        return actorGo;
    }

    private GameObject SpawnCpuParticipantWithRangeMeter(int pid)
    {
        GameObject actorGo = Spawn($"cpu-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<AutoPlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        GameObject meterGo = Spawn($"cpu-rangemeter-{pid}");
        meterGo.transform.parent = actorGo.transform;
        meterGo.AddComponent<RangeMeter>();

        return actorGo;
    }

    [Test]
    public void SpawnCoordinator_RegisterHuman_BindsChildRangeMeterToThatParticipantsOwnActor()
    {
        GameObject actorGo = SpawnHumanParticipantWithRangeMeter(pid: 0);
        RangeMeter meter = actorGo.GetComponentInChildren<RangeMeter>(true);

        InvokeRegisterHuman(actorGo, 0, null);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actorGo.GetComponent<PlayerController>(), GetPrivateField(meter, "actor"));
        Assert.IsFalse((bool)GetPrivateField(meter, "isCpu"));
    }

    [Test]
    public void SpawnCoordinator_RegisterCpu_BindsChildRangeMeterToThatParticipantsOwnActor()
    {
        GameObject actorGo = SpawnCpuParticipantWithRangeMeter(pid: 1);
        RangeMeter meter = actorGo.GetComponentInChildren<RangeMeter>(true);

        InvokeRegisterCpu(actorGo, 1);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actorGo.GetComponent<AutoPlayerController>(), GetPrivateField(meter, "actor"));
        Assert.IsTrue((bool)GetPrivateField(meter, "isCpu"));
    }

    [Test]
    public void SpawnCoordinator_SecondaryHumanRangeMeterBindsToItsOwnActor_NotThePrimarys()
    {
        GameObject primary = SpawnHumanParticipantWithRangeMeter(pid: 0);
        GameObject secondary = SpawnHumanParticipantWithRangeMeter(pid: 1);

        InvokeRegisterHuman(primary, 0, null);
        InvokeRegisterHuman(secondary, 1, null);

        RangeMeter secondaryMeter = secondary.GetComponentInChildren<RangeMeter>(true);
        object boundActor = GetPrivateField(secondaryMeter, "actor");

        Assert.AreSame(secondary.GetComponent<PlayerController>(), boundActor);
        Assert.AreNotSame(primary.GetComponent<PlayerController>(), boundActor,
            "a secondary participant's RangeMeter must never collapse to the primary participant's actor");
    }

    [Test]
    public void SpawnCoordinator_PrefabsWithNoRangeMeterAreUnaffected()
    {
        // Most player prefabs carry no RangeMeter at all - RegisterHuman must not require one.
        GameObject actorGo = Spawn("human-actor-no-meter");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<PlayerController>();
        actorGo.AddComponent<PlayerIdentifier>();

        Assert.DoesNotThrow(() => InvokeRegisterHuman(actorGo, 0, null));
    }

    // ==================== test double ====================

    private sealed class FakeShooterActor : IShooterActor
    {
        public bool HasBasketball { get; set; }
        public bool FacingFront => true;
        public bool Grounded => true;
        public bool InAir { get; set; }
        public bool InDunkState => false;
        public float DistanceFromRim { get; set; }
        public int Range { get; set; }
        public ShooterAttributes ShooterAttributes => new ShooterAttributes(
            "fake", 0, 0, 0, 0, 0, Range, 0, 0, 0, 0);
        public int Clutch => 0;
        public float ShotMeterSliderValue => 0f;
        public bool ShotMeterEnded => true;
        public void SetAnimBool(string name, bool value) { }
        public void SetAnimTrigger(string name) { }
        public void LockCallBallToPlayer(bool locked) { }
        public void DisplayShotMeterMessage(string message) { }
        public void EndShootCycle() { }
    }
}
