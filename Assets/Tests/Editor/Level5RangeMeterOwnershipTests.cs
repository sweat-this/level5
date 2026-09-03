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
/// AUD-010 Phase 2b0: <see cref="RangeMeter"/> also reads its match rules and configuration-presence
/// through a separately, and independently, bound match context
/// (<see cref="RangeMeter.BindMatchContext"/>), bound by <see cref="SpawnCoordinator"/> alongside
/// actor ownership, instead of reaching <c>MatchRuntime.Rules</c>/<c>MatchRuntime.HasConfiguration</c>
/// itself. Both bindings (owner, match context) are mandatory for a valid <see cref="RangeMeter.Start"/>.
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
    private ResolvedMatchRules coordinatorRules;
    private MethodInfo registerHuman;
    private MethodInfo registerCpu;

    [SetUp]
    public void SetUp()
    {
        // RangeMeter no longer reads MatchRuntime.HasConfiguration itself, but clearing this keeps
        // every other MatchRuntime-reading system deterministic regardless of what ran before this
        // test, and is the baseline the SpawnCoordinator wiring tests toggle explicitly.
        ActiveMatch.Clear();

        registry = new PlayerRegistry();
        // AUD-010 Phase 2b0: a non-default rules object (CombatMode.Standard, not the default None) so
        // a RangeMeter that accidentally binds a fresh default ResolvedMatchRules instead of this exact
        // reference cannot pass the AreSame assertions below.
        coordinatorRules = new ResolvedMatchRules(combatMode: CombatMode.Standard, enemiesEnabled: false, hardcore: false, enemiesOnly: false, allowsCpuShooters: false);
        coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            registry,
            coordinatorRules,
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
        InvokeRegisterHuman(coordinator, participant, pid, slot);
    }

    private void InvokeRegisterHuman(SpawnCoordinator target, GameObject participant, int pid, PlayerSlot slot)
    {
        registerHuman.Invoke(target, new object[] { participant, pid, slot });
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
    /// A configured, non-hostile ruleset with configuration present - the baseline "meter stays
    /// visible" case for a human. <c>allowsCpuShooters: false</c> is required: the legacy predicate
    /// (preserved exactly) hides a human meter whenever CPU shooters are allowed.
    /// </summary>
    private static ResolvedMatchRules NormalRules()
    {
        return new ResolvedMatchRules(combatMode: CombatMode.Standard, enemiesEnabled: false, hardcore: false, enemiesOnly: false, allowsCpuShooters: false);
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

    // ==================== BindMatchContext ====================

    [Test]
    public void BindMatchContext_ValidRules_Binds()
    {
        RangeMeter meter = Spawn("meter").AddComponent<RangeMeter>();
        ResolvedMatchRules rules = NormalRules();

        meter.BindMatchContext(rules, hasActiveMatchConfiguration: true);

        Assert.AreSame(rules, GetPrivateField(meter, "matchRules"));
        Assert.IsTrue((bool)GetPrivateField(meter, "hasActiveMatchConfiguration"));
    }

    [Test]
    public void BindMatchContext_NullRules_LogsAndLeavesUnbound()
    {
        RangeMeter meter = Spawn("meter").AddComponent<RangeMeter>();

        LogAssert.Expect(LogType.Error, new Regex("null match rules"));
        meter.BindMatchContext(null, hasActiveMatchConfiguration: true);

        Assert.IsNull(GetPrivateField(meter, "matchRules"));
    }

    [Test]
    public void BindMatchContext_SecondCall_IsRejectedAndOriginalContextRetained()
    {
        RangeMeter meter = Spawn("meter").AddComponent<RangeMeter>();
        ResolvedMatchRules first = NormalRules();
        ResolvedMatchRules second = new ResolvedMatchRules(hardcore: true);
        meter.BindMatchContext(first, hasActiveMatchConfiguration: true);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound match context"));
        meter.BindMatchContext(second, hasActiveMatchConfiguration: false);

        Assert.AreSame(first, GetPrivateField(meter, "matchRules"), "a second BindMatchContext call must not overwrite the original rules");
        Assert.IsTrue((bool)GetPrivateField(meter, "hasActiveMatchConfiguration"), "a second BindMatchContext call must not overwrite the original configuration-presence value");
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
    public void Start_WithoutBoundMatchContext_DisablesOnlyTheComponent()
    {
        RangeMeter meter = Spawn("no-context-meter").AddComponent<RangeMeter>();
        meter.BindOwner(new FakeShooterActor(), isCpu: false);

        LogAssert.Expect(LogType.Error, new Regex("no bound match context"));
        InvokeStart(meter);

        Assert.IsFalse(meter.enabled, "a RangeMeter with no bound match context must disable itself");
        Assert.IsTrue(meter.gameObject.activeSelf, "a RangeMeter with no bound match context must not deactivate its whole GameObject");
    }

    // ==================== Human visibility truth table ====================

    [Test]
    public void Start_ConfiguredNormalHuman_RemainsVisible()
    {
        RangeMeter meter = MakePresentableMeter("human-normal");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchContext(NormalRules(), hasActiveMatchConfiguration: true);

        InvokeStart(meter);

        Assert.IsTrue(meter.gameObject.activeInHierarchy, "a configured, non-hostile human RangeMeter must remain visible");
    }

    [Test]
    public void Start_DirectEntryNormalHuman_Hides()
    {
        // Mirrors the legacy condition this migration preserves: a human meter hides when
        // !hasActiveMatchConfiguration, among other rules - the direct-entry case where a
        // ResolvedMatchRules exists but no MatchConfiguration validated it.
        RangeMeter meter = MakePresentableMeter("human-direct-entry");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchContext(NormalRules(), hasActiveMatchConfiguration: false);

        InvokeStart(meter);

        Assert.IsFalse(meter.gameObject.activeInHierarchy, "a human RangeMeter must hide when there is no active match configuration");
    }

    [Test]
    public void Start_HardcoreHuman_Hides()
    {
        RangeMeter meter = MakePresentableMeter("human-hardcore");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchContext(
            new ResolvedMatchRules(hardcore: true, enemiesOnly: false, combatMode: CombatMode.None, allowsCpuShooters: false),
            hasActiveMatchConfiguration: true);

        InvokeStart(meter);

        Assert.IsFalse(meter.gameObject.activeInHierarchy, "a Hardcore human RangeMeter must hide");
    }

    [Test]
    public void Start_EnemiesOnlyHuman_Hides()
    {
        RangeMeter meter = MakePresentableMeter("human-enemies-only");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchContext(
            new ResolvedMatchRules(hardcore: false, enemiesOnly: true, combatMode: CombatMode.None, allowsCpuShooters: false),
            hasActiveMatchConfiguration: true);

        InvokeStart(meter);

        Assert.IsFalse(meter.gameObject.activeInHierarchy, "an EnemiesOnly human RangeMeter must hide");
    }

    [Test]
    public void Start_BattleRoyalHuman_Hides()
    {
        RangeMeter meter = MakePresentableMeter("human-battle-royal");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchContext(
            new ResolvedMatchRules(hardcore: false, enemiesOnly: false, combatMode: CombatMode.BattleRoyal, allowsCpuShooters: false),
            hasActiveMatchConfiguration: true);

        InvokeStart(meter);

        Assert.IsFalse(meter.gameObject.activeInHierarchy, "a Battle Royal human RangeMeter must hide");
    }

    [Test]
    public void Start_AllowsCpuShootersHuman_Hides()
    {
        // Preserved exactly, even though it reads as surprising in isolation: the legacy condition
        // hides a human meter whenever the mode allows CPU shooters at all.
        RangeMeter meter = MakePresentableMeter("human-allows-cpu");
        meter.BindOwner(new FakeShooterActor(), isCpu: false);
        meter.BindMatchContext(
            new ResolvedMatchRules(hardcore: false, enemiesOnly: false, combatMode: CombatMode.None, allowsCpuShooters: true),
            hasActiveMatchConfiguration: true);

        InvokeStart(meter);

        Assert.IsFalse(meter.gameObject.activeInHierarchy, "a human RangeMeter must hide when the mode allows CPU shooters");
    }

    // ==================== CPU visibility ====================

    [Test]
    public void Start_CpuActor_RemainsVisibleRegardlessOfConfiguration()
    {
        // The legacy condition is gated on "!playerIdentifier.isCpu" - a CPU meter never hides on
        // this branch. Preserved as "!isCpu" against the bound role.
        RangeMeter meter = MakePresentableMeter("cpu-meter");
        meter.BindOwner(new FakeShooterActor(), isCpu: true);
        meter.BindMatchContext(NormalRules(), hasActiveMatchConfiguration: true);

        InvokeStart(meter);

        Assert.IsTrue(meter.gameObject.activeInHierarchy, "a CPU RangeMeter's visibility must be unaffected by the human-only hide conditions");
    }

    [Test]
    public void Start_CpuActor_RemainsVisibleWithoutConfiguration()
    {
        RangeMeter meter = MakePresentableMeter("cpu-no-config");
        meter.BindOwner(new FakeShooterActor(), isCpu: true);
        meter.BindMatchContext(NormalRules(), hasActiveMatchConfiguration: false);

        InvokeStart(meter);

        Assert.IsTrue(meter.gameObject.activeInHierarchy, "a CPU RangeMeter must remain visible without an active match configuration");
    }

    [Test]
    public void Start_CpuActor_RemainsVisibleUnderHostileRules()
    {
        RangeMeter meter = MakePresentableMeter("cpu-hostile");
        meter.BindOwner(new FakeShooterActor(), isCpu: true);
        meter.BindMatchContext(
            new ResolvedMatchRules(hardcore: true, enemiesOnly: true, combatMode: CombatMode.BattleRoyal, allowsCpuShooters: true),
            hasActiveMatchConfiguration: true);

        InvokeStart(meter);

        Assert.IsTrue(meter.gameObject.activeInHierarchy, "a CPU RangeMeter must remain visible under every human-only hide condition combined");
    }

    // ==================== Range/slider behavior ====================

    [Test]
    public void SetSliderValue_ReadsRangeAndDistanceFromTheBoundActor()
    {
        RangeMeter meter = MakePresentableMeter("range-meter");
        FakeShooterActor actor = new FakeShooterActor { Range = 42, DistanceFromRim = 7f };
        meter.BindOwner(actor, isCpu: true); // isCpu keeps it active so Start() completes its setup
        meter.BindMatchContext(NormalRules(), hasActiveMatchConfiguration: true);
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
        meterA.BindMatchContext(NormalRules(), hasActiveMatchConfiguration: true);
        meterB.BindMatchContext(NormalRules(), hasActiveMatchConfiguration: true);
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
    public void SpawnCoordinator_RegisterHuman_BindsChildRangeMeterToThatParticipantsOwnActorAndMatchContext()
    {
        GameObject actorGo = SpawnHumanParticipantWithRangeMeter(pid: 0);
        RangeMeter meter = actorGo.GetComponentInChildren<RangeMeter>(true);

        InvokeRegisterHuman(actorGo, 0, null);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actorGo.GetComponent<PlayerController>(), GetPrivateField(meter, "actor"));
        Assert.IsFalse((bool)GetPrivateField(meter, "isCpu"));
        Assert.AreSame(coordinatorRules, GetPrivateField(meter, "matchRules"));
        Assert.IsFalse((bool)GetPrivateField(meter, "hasActiveMatchConfiguration"), "ActiveMatch.Clear() in SetUp means no configuration is active");
    }

    [Test]
    public void SpawnCoordinator_RegisterCpu_BindsChildRangeMeterToThatParticipantsOwnActorAndMatchContext()
    {
        GameObject actorGo = SpawnCpuParticipantWithRangeMeter(pid: 1);
        RangeMeter meter = actorGo.GetComponentInChildren<RangeMeter>(true);

        InvokeRegisterCpu(actorGo, 1);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(actorGo.GetComponent<AutoPlayerController>(), GetPrivateField(meter, "actor"));
        Assert.IsTrue((bool)GetPrivateField(meter, "isCpu"));
        Assert.AreSame(coordinatorRules, GetPrivateField(meter, "matchRules"));
        Assert.IsFalse((bool)GetPrivateField(meter, "hasActiveMatchConfiguration"));
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
        Assert.AreSame(coordinatorRules, GetPrivateField(secondaryMeter, "matchRules"),
            "a secondary participant's RangeMeter must still receive this match's own resolved rules");
    }

    [Test]
    public void SpawnCoordinator_InactiveChildRangeMeter_ReceivesBothBindings()
    {
        // GetComponentsInChildren(true) reaches inactive/disabled authored copies - binding itself has
        // no presentation side effects, so an inactive meter must be composed correctly in case it is
        // activated later.
        GameObject actorGo = SpawnHumanParticipantWithRangeMeter(pid: 0);
        RangeMeter meter = actorGo.GetComponentInChildren<RangeMeter>(true);
        meter.gameObject.SetActive(false);

        InvokeRegisterHuman(actorGo, 0, null);

        Assert.IsTrue(meter.Bound);
        Assert.AreSame(coordinatorRules, GetPrivateField(meter, "matchRules"));
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

    /// <summary>
    /// A throwaway configuration, built in code so it does not depend on the authored catalogs -
    /// mirrors <c>Level5VersusIntegrationTests.BuildAnyMatch</c>. Only its presence matters here:
    /// <c>ActiveMatch.Begin</c> is what makes <c>MatchRuntime.HasConfiguration</c> observe true.
    /// </summary>
    private static MatchConfiguration BuildAnyMatch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        PlayerRoster roster = TestDefinitions.SoloRoster();

        return new MatchConfiguration(
            mode,
            level,
            roster,
            MatchModifiers.Default,
            MatchConfigurationBuilder.Resolve(mode, level, roster, MatchModifiers.Default),
            CheerleaderSelection.None,
            "range meter ownership test");
    }

    /// <summary>
    /// AUD-010 Phase 2b0: <c>SpawnCoordinator</c> now captures configuration presence once in its own
    /// constructor (mirroring how it already owns <c>rules</c>), rather than re-reading
    /// <c>MatchRuntime.HasConfiguration</c> on every <c>BindRangeMeters</c> call - so, unlike every
    /// other test in this fixture, this one needs a coordinator constructed after <c>ActiveMatch</c> is
    /// set, not the shared one built in <c>SetUp</c> (which always runs after <c>ActiveMatch.Clear()</c>).
    /// </summary>
    private SpawnCoordinator BuildCoordinator()
    {
        return new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            registry,
            coordinatorRules,
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None);
    }

    [Test]
    public void SpawnCoordinator_RegisterHuman_WithActiveMatch_CapturesConfigurationPresent()
    {
        ActiveMatch.Begin(BuildAnyMatch());
        SpawnCoordinator configuredCoordinator = BuildCoordinator();
        GameObject actorGo = SpawnHumanParticipantWithRangeMeter(pid: 0);
        RangeMeter meter = actorGo.GetComponentInChildren<RangeMeter>(true);

        // With ActiveMatch configured, RegisterHuman also drives InitializeHumanProfile against this
        // synthetic participant's bare CharacterProfile, which cannot resolve a real saved profile for
        // BuildAnyMatch's character id - an unrelated, expected error in this rig, not what this test
        // is about (RangeMeter's captured configuration-presence value).
        LogAssert.ignoreFailingMessages = true;
        InvokeRegisterHuman(configuredCoordinator, actorGo, 0, null);
        LogAssert.ignoreFailingMessages = false;

        Assert.IsTrue((bool)GetPrivateField(meter, "hasActiveMatchConfiguration"),
            "a RangeMeter composed by a coordinator constructed while ActiveMatch is configured must capture configuration presence");
    }

    [Test]
    public void SpawnCoordinator_RegisterHuman_WithoutActiveMatch_CapturesConfigurationAbsent()
    {
        ActiveMatch.Clear();
        SpawnCoordinator unconfiguredCoordinator = BuildCoordinator();
        GameObject actorGo = SpawnHumanParticipantWithRangeMeter(pid: 0);
        RangeMeter meter = actorGo.GetComponentInChildren<RangeMeter>(true);

        InvokeRegisterHuman(unconfiguredCoordinator, actorGo, 0, null);

        Assert.IsFalse((bool)GetPrivateField(meter, "hasActiveMatchConfiguration"),
            "a RangeMeter composed by a coordinator constructed with no ActiveMatch (direct scene entry) must capture configuration absent");
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
