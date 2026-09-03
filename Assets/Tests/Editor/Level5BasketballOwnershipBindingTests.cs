using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-013 regression coverage: a spawned basketball's runtime ownership (participant id, CPU role,
/// primary status, owner actor, IShooterActor, BasketBallState) is now bound explicitly by
/// <see cref="SpawnCoordinator.GiveBall"/> through <see cref="IBasketballRuntime"/>, instead of being
/// copied out of a second, hand-synced <c>PlayerIdentifier</c> placed on the basketball object itself.
///
/// These tests drive the real <see cref="SpawnCoordinator"/> (via its private <c>GiveBall</c>, the
/// sole production basketball-creation path) against the real production basketball prefabs, rather
/// than a stand-in - a regression in the actual composition wiring fails here, not just in a mock.
/// </summary>
public class Level5BasketballOwnershipBindingTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private PlayerRegistry registry;
    private SpawnCoordinator coordinator;
    private MethodInfo giveBall;
    private ResolvedMatchRules rules;

    [SetUp]
    public void SetUp()
    {
        registry = new PlayerRegistry();
        // hardcore: true is deliberately not ResolvedMatchRules' own default (false) - the rules
        // binding tests below assert on it precisely so a GiveBall that bound some other,
        // default-constructed ResolvedMatchRules instead of this coordinator's own `rules` field
        // would read back false and fail, rather than coincidentally matching the default.
        rules = new ResolvedMatchRules(combatMode: CombatMode.Standard, enemiesEnabled: false, hardcore: true, enemiesOnly: false);
        coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(),
            registry,
            rules,
            new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None,
            new FakeGroundHeightProvider());

        giveBall = typeof(SpawnCoordinator).GetMethod("GiveBall", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(giveBall, "SpawnCoordinator.GiveBall must exist - the sole basketball creation path this migration targets");
    }

    [TearDown]
    public void TearDown()
    {
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

    private PlayerIdentifier RegisterHumanParticipant(int pid)
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

    private PlayerIdentifier RegisterCpuParticipant(int pid)
    {
        GameObject actorGo = Spawn($"cpu-actor-{pid}");
        actorGo.AddComponent<CharacterProfile>();
        actorGo.AddComponent<AutoPlayerController>();
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, true);
        identifier.autoPlayer = actorGo;
        identifier.setAutoPlayer(actorGo);
        registry.Add(identifier);
        return identifier;
    }

    private void GiveBall(int slotId, GameObject prefab, bool forCpu)
    {
        giveBall.Invoke(coordinator, new object[] { slotId, prefab, Vector3.zero, forCpu });
    }

    private static void InvokeStart(MonoBehaviour behaviour)
    {
        MethodInfo start = behaviour.GetType().GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(start, $"{behaviour.GetType().Name} must declare Start()");
        start.Invoke(behaviour, null);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    [Test]
    public void HumanBallBindsCorrectParticipantIdAndCpuRole()
    {
        PlayerIdentifier owner = RegisterHumanParticipant(pid: 0);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        Assert.IsNotNull(humanPrefab, "human basketball prefab failed to load");

        GiveBall(0, humanPrefab, forCpu: false);

        GameObject ball = owner.basketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's basketball reference");
        spawned.Add(ball);

        IBasketballRuntime runtime = ball.GetComponent<IBasketballRuntime>();
        Assert.IsNotNull(runtime, "the human basketball prefab must implement IBasketballRuntime");
        Assert.That(runtime.ParticipantId, Is.EqualTo(0));
        Assert.IsFalse(runtime.IsCpu);
        Assert.IsTrue(runtime.IsPrimary, "slot 0 is always the primary ball");
        Assert.AreSame(owner.player, runtime.OwnerActor);
        Assert.AreSame(owner.Actor, runtime.Actor);
    }

    [Test]
    public void CpuBallBindsCorrectParticipantIdAndCpuRole()
    {
        RegisterHumanParticipant(pid: 0);
        PlayerIdentifier cpuOwner = RegisterCpuParticipant(pid: 1);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);
        Assert.IsNotNull(cpuPrefab, "CPU basketball prefab failed to load");

        GiveBall(1, cpuPrefab, forCpu: true);

        GameObject ball = cpuOwner.autoBasketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's autoBasketball reference");
        spawned.Add(ball);

        IBasketballRuntime runtime = ball.GetComponent<IBasketballRuntime>();
        Assert.IsNotNull(runtime, "the CPU basketball prefab must implement IBasketballRuntime");
        Assert.That(runtime.ParticipantId, Is.EqualTo(1));
        Assert.IsTrue(runtime.IsCpu);
        Assert.IsFalse(runtime.IsPrimary, "only slot 0 is ever primary");
        Assert.AreSame(cpuOwner.autoPlayer, runtime.OwnerActor);
        Assert.AreSame(cpuOwner.Actor, runtime.Actor);
    }

    [Test]
    public void SecondaryHumanBallIsNeverPrimary()
    {
        // The regression this pins: a second local human's ball must not be able to steal
        // BasketBall.instance from slot 0's ball. IsPrimary is now composition data (slotId == 0),
        // not a runtime GameLevelManager lookup a second human's ball could independently satisfy.
        RegisterHumanParticipant(pid: 0);
        PlayerIdentifier secondHuman = RegisterHumanParticipant(pid: 1);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBall(1, humanPrefab, forCpu: false);

        GameObject ball = secondHuman.basketball;
        spawned.Add(ball);

        IBasketballRuntime runtime = ball.GetComponent<IBasketballRuntime>();
        Assert.IsFalse(runtime.IsPrimary, "only slot 0 is primary, even for a second human");
    }

    [Test]
    public void BoundBasketballStateReceivesOwnerAndRole()
    {
        PlayerIdentifier cpuOwner = RegisterCpuParticipant(pid: 0);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);

        GiveBall(0, cpuPrefab, forCpu: true);

        GameObject ball = cpuOwner.autoBasketball;
        spawned.Add(ball);

        BasketBallState state = ball.GetComponent<BasketBallState>();
        Assert.IsTrue(state.Bound, "BindOwner must mark BasketBallState bound");
        Assert.IsTrue(state.isCpu);
        Assert.AreSame(cpuOwner.autoPlayer, state.Player);
    }

    [Test]
    public void ActorPlayerIdentifierStillReceivesBallStateAndStatsReferences()
    {
        // AUD-013 removes the ball's own duplicate PlayerIdentifier, but must not disturb the
        // authoritative actor-side PlayerIdentifier's references to the ball it now owns.
        PlayerIdentifier owner = RegisterHumanParticipant(pid: 0);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBall(0, humanPrefab, forCpu: false);
        spawned.Add(owner.basketball);

        Assert.IsNotNull(owner.basketBallController);
        Assert.IsNotNull(owner.basketBallState);
        Assert.IsNotNull(owner.gameStats);
        Assert.AreSame(owner.basketball.GetComponent<BasketBall>(), owner.basketBallController);
        Assert.AreSame(owner.basketball.GetComponent<BasketBallState>(), owner.basketBallState);
        Assert.AreSame(owner.basketball.GetComponent<GameStats>(), owner.gameStats);
    }

    /// <summary>
    /// AUD-010 Phase 2b0: GiveBall now also binds the coordinator's ResolvedMatchRules to the ball's
    /// GameStats, immediately after IBasketballRuntime.BindOwner - the seam GameStats.BuildExperienceInput
    /// depends on now that it no longer reads MatchRuntime directly.
    /// </summary>
    [Test]
    public void HumanBallReceivesTheCoordinatorsMatchRules()
    {
        PlayerIdentifier owner = RegisterHumanParticipant(pid: 0);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBall(0, humanPrefab, forCpu: false);
        spawned.Add(owner.basketball);

        GameStats stats = owner.gameStats;
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.HasBoundMatchRules, "GiveBall must bind the match rules to the human ball's GameStats");
        // rules.Hardcore is true here specifically because it is not ResolvedMatchRules' own default
        // - this fails if GiveBall ever bound a different (e.g. freshly-constructed default) rules
        // instance instead of this coordinator's own `rules` field.
        Assert.That(stats.BuildExperienceInput().HardcoreEnabled, Is.EqualTo(rules.Hardcore),
            "the bound rules must be the coordinator's own rules, not some other default-valued instance");
    }

    [Test]
    public void CpuBallReceivesTheCoordinatorsMatchRules()
    {
        RegisterHumanParticipant(pid: 0);
        PlayerIdentifier cpuOwner = RegisterCpuParticipant(pid: 1);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);

        GiveBall(1, cpuPrefab, forCpu: true);
        spawned.Add(cpuOwner.autoBasketball);

        GameStats stats = cpuOwner.gameStats;
        Assert.IsNotNull(stats);
        Assert.IsTrue(stats.HasBoundMatchRules, "GiveBall must bind the match rules to the CPU ball's GameStats");
        Assert.That(stats.BuildExperienceInput().HardcoreEnabled, Is.EqualTo(rules.Hardcore),
            "the bound rules must be the coordinator's own rules, not some other default-valued instance");
    }

    [Test]
    public void SecondaryHumanBallAlsoReceivesTheCoordinatorsMatchRules()
    {
        RegisterHumanParticipant(pid: 0);
        PlayerIdentifier secondHuman = RegisterHumanParticipant(pid: 1);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBall(1, humanPrefab, forCpu: false);
        spawned.Add(secondHuman.basketball);

        Assert.That(secondHuman.gameStats.BuildExperienceInput().HardcoreEnabled, Is.EqualTo(rules.Hardcore),
            "a secondary human's ball must be bound to the coordinator's own rules, not some other default-valued instance");
        Assert.IsTrue(secondHuman.gameStats.HasBoundMatchRules,
            "a secondary human's ball must not be skipped by rules composition");
    }

    /// <summary>
    /// AUD-010 Phase 2b0: GiveBall now also binds the coordinator's ResolvedMatchRules to the ball's
    /// BasketBallState, immediately after IBasketballRuntime.BindOwner - the seam BasketBallState.Update
    /// depends on now that it no longer reads MatchRuntime directly.
    /// </summary>
    [Test]
    public void HumanBallStateReceivesTheCoordinatorsMatchRules()
    {
        PlayerIdentifier owner = RegisterHumanParticipant(pid: 0);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBall(0, humanPrefab, forCpu: false);
        spawned.Add(owner.basketball);

        BasketBallState state = owner.basketBallState;
        Assert.IsNotNull(state);
        Assert.AreSame(rules, GetPrivateField(state, "matchRules"),
            "GiveBall must bind the coordinator's own rules to the human ball's BasketBallState, not some other default-valued instance");
    }

    [Test]
    public void CpuBallStateReceivesTheCoordinatorsMatchRules()
    {
        RegisterHumanParticipant(pid: 0);
        PlayerIdentifier cpuOwner = RegisterCpuParticipant(pid: 1);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);

        GiveBall(1, cpuPrefab, forCpu: true);
        spawned.Add(cpuOwner.autoBasketball);

        BasketBallState state = cpuOwner.basketBallState;
        Assert.IsNotNull(state);
        Assert.AreSame(rules, GetPrivateField(state, "matchRules"),
            "GiveBall must bind the coordinator's own rules to the CPU ball's BasketBallState, not some other default-valued instance");
    }

    [Test]
    public void SecondaryHumanBallStateAlsoReceivesTheCoordinatorsMatchRules()
    {
        RegisterHumanParticipant(pid: 0);
        PlayerIdentifier secondHuman = RegisterHumanParticipant(pid: 1);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBall(1, humanPrefab, forCpu: false);
        spawned.Add(secondHuman.basketball);

        BasketBallState state = secondHuman.basketBallState;
        Assert.AreSame(rules, GetPrivateField(state, "matchRules"),
            "a secondary human's ball must be bound to the coordinator's own rules, not left unbound");
    }

    // ======================= BasketBallState.BindMatchRules (AUD-010 Phase 2b0) =======================

    [Test]
    public void BasketBallStateRejectsNullMatchRulesBind()
    {
        GameObject go = Spawn("basketball-state-null-rules");
        BasketBallState state = go.AddComponent<BasketBallState>();

        LogAssert.Expect(LogType.Error, new Regex("null match rules"));
        state.BindMatchRules(null);

        Assert.IsNull(GetPrivateField(state, "matchRules"), "a rejected null bind must leave the state unbound");
    }

    [Test]
    public void BasketBallStateAcceptsFirstMatchRulesBind()
    {
        GameObject go = Spawn("basketball-state-first-rules");
        BasketBallState state = go.AddComponent<BasketBallState>();
        ResolvedMatchRules boundRules = new ResolvedMatchRules(requiresBasketball: false);

        state.BindMatchRules(boundRules);

        Assert.AreSame(boundRules, GetPrivateField(state, "matchRules"));
    }

    [Test]
    public void BasketBallStateRejectsSecondMatchRulesBind()
    {
        GameObject go = Spawn("basketball-state-second-rules");
        BasketBallState state = go.AddComponent<BasketBallState>();
        ResolvedMatchRules first = new ResolvedMatchRules(requiresBasketball: false);
        ResolvedMatchRules second = new ResolvedMatchRules(requiresBasketball: true);
        state.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already has bound match rules"));
        state.BindMatchRules(second);

        Assert.AreSame(first, GetPrivateField(state, "matchRules"), "a second BindMatchRules call must not overwrite the original rules");
    }

    /// <summary>
    /// Code review: a null second call after a real bind already succeeded must report "already
    /// bound", not "remaining unbound" - the state is not unbound, it still holds the original valid
    /// rules. Pins the check ordering (already-bound checked before null-argument) that makes that true.
    /// </summary>
    [Test]
    public void BasketBallStateRejectsNullSecondBindWithAnAlreadyBoundMessage()
    {
        GameObject go = Spawn("basketball-state-null-second-rules");
        BasketBallState state = go.AddComponent<BasketBallState>();
        ResolvedMatchRules first = new ResolvedMatchRules(requiresBasketball: false);
        state.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already has bound match rules"));
        state.BindMatchRules(null);

        Assert.AreSame(first, GetPrivateField(state, "matchRules"), "a null second bind must not clear the original rules");
    }

    [Test]
    public void MissingMatchRulesIsDetectedClearlyOnBasketBallStateStart()
    {
        GameObject go = Spawn("basketball-state-missing-rules");
        BasketBallState state = go.AddComponent<BasketBallState>();
        GameObject owner = Spawn("basketball-state-missing-rules-owner");
        state.BindOwner(false, owner);

        LogAssert.Expect(LogType.Error, new Regex("no bound match rules"));
        InvokeStart(state);

        Assert.IsFalse(go.activeSelf, "BasketBallState with a bound owner but no bound match rules must deactivate its GameObject rather than run Update() against a null rules reference");
    }

    [Test]
    public void MissingRuntimeBindingIsDetectedClearlyOnHumanBallStart()
    {
        GameObject go = Spawn("unbound-human-ball");
        BasketBall ball = go.AddComponent<BasketBall>();

        LogAssert.Expect(LogType.Error, new Regex("no bound owner"));
        InvokeStart(ball);

        // GameObject.SetActive, not enabled = false: Unity still dispatches
        // OnCollisionEnter/OnTriggerEnter to a disabled-but-active component, so deactivating the
        // whole object is what actually quarantines it (see BasketBall.Start()).
        Assert.IsFalse(go.activeSelf, "an unbound human ball must deactivate its GameObject rather than run against a null owner");
    }

    [Test]
    public void MissingRuntimeBindingIsDetectedClearlyOnCpuBallStart()
    {
        GameObject go = Spawn("unbound-cpu-ball");
        BasketBallAuto ball = go.AddComponent<BasketBallAuto>();

        LogAssert.Expect(LogType.Error, new Regex("no bound owner"));
        InvokeStart(ball);

        Assert.IsFalse(go.activeSelf, "an unbound CPU ball must deactivate its GameObject rather than run against a null owner");
    }

    [Test]
    public void MissingRuntimeBindingIsDetectedClearlyOnBasketBallStateStart()
    {
        GameObject go = Spawn("unbound-basketball-state");
        BasketBallState state = go.AddComponent<BasketBallState>();

        LogAssert.Expect(LogType.Error, new Regex("no bound owner"));
        InvokeStart(state);

        Assert.IsFalse(go.activeSelf, "unbound BasketBallState must deactivate its GameObject rather than run against a null owner");
    }

    [Test]
    public void SecondBindOwnerCallOnAnAlreadyBoundHumanBallIsRejected()
    {
        PlayerIdentifier owner = RegisterHumanParticipant(pid: 0);
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        GiveBall(0, humanPrefab, forCpu: false);
        GameObject ball = owner.basketball;
        spawned.Add(ball);

        IBasketballRuntime runtime = ball.GetComponent<IBasketballRuntime>();

        LogAssert.Expect(LogType.Error, new Regex("already bound"));
        runtime.BindOwner(participantId: 99, isCpu: true, isPrimary: false, ownerActor: ball, actor: null);

        Assert.That(runtime.ParticipantId, Is.EqualTo(0), "a second BindOwner call must not overwrite the original binding");
        Assert.IsFalse(runtime.IsCpu, "a second BindOwner call must not overwrite the original role");
    }

    [Test]
    public void SecondBindOwnerCallOnAnAlreadyBoundCpuBallIsRejected()
    {
        RegisterHumanParticipant(pid: 0);
        PlayerIdentifier cpuOwner = RegisterCpuParticipant(pid: 1);
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);
        GiveBall(1, cpuPrefab, forCpu: true);
        GameObject ball = cpuOwner.autoBasketball;
        spawned.Add(ball);

        IBasketballRuntime runtime = ball.GetComponent<IBasketballRuntime>();

        LogAssert.Expect(LogType.Error, new Regex("already bound"));
        runtime.BindOwner(participantId: 99, isCpu: false, isPrimary: true, ownerActor: ball, actor: null);

        Assert.That(runtime.ParticipantId, Is.EqualTo(1), "a second BindOwner call must not overwrite the original binding");
        Assert.IsTrue(runtime.IsCpu, "a second BindOwner call must not overwrite the original role");
    }

    [Test]
    public void SecondBindOwnerCallOnAnAlreadyBoundBasketBallStateIsRejected()
    {
        // Exercised directly rather than through the ball: BasketBall/BasketBallAuto's own rebind
        // guard returns before ever reaching BasketBallState.BindOwner a second time, so this is the
        // only way to prove BasketBallState's own guard (not just the ball's) actually works.
        GameObject go = Spawn("basketball-state-rebind");
        GameObject firstOwner = Spawn("first-owner");
        GameObject secondOwner = Spawn("second-owner");
        BasketBallState state = go.AddComponent<BasketBallState>();

        state.BindOwner(false, firstOwner);

        LogAssert.Expect(LogType.Error, new Regex("already bound"));
        state.BindOwner(true, secondOwner);

        Assert.IsFalse(state.isCpu, "a second BindOwner call must not overwrite the original role");
        Assert.AreSame(firstOwner, state.Player, "a second BindOwner call must not overwrite the original owner");
    }

    // GroundCheck (Assets/Scripts/player/groundcheck.cs) is authored as a child both of the player
    // actor and of the basketball itself. Its ball-attached branch previously read the ball's own
    // duplicate PlayerIdentifier and NRE'd in PlayMode once that component was removed by this
    // migration - these tests cover that branch directly instead of relying only on the broad
    // PlayMode suite to notice a regression here again.

    [Test]
    public void GroundCheckUnderACpuBallResolvesOwnerAndStateFromTheRuntimeBinding()
    {
        GameObject cpuActor = Spawn("cpu-actor-groundcheck");
        cpuActor.AddComponent<CharacterProfile>();
        AutoPlayerController controller = cpuActor.AddComponent<AutoPlayerController>();

        GameObject ballRoot = Spawn("cpu-ball-groundcheck");
        BasketBallAuto ball = ballRoot.AddComponent<BasketBallAuto>();
        BasketBallState state = ballRoot.AddComponent<BasketBallState>();
        ball.BindOwner(0, true, false, cpuActor, controller);

        GameObject groundCheckGo = Spawn("groundCheck");
        groundCheckGo.transform.parent = ballRoot.transform;
        GroundCheck groundCheck = groundCheckGo.AddComponent<GroundCheck>();

        InvokeStart(groundCheck);

        Assert.AreSame(state, GetPrivateField(groundCheck, "basketBallState"));
        Assert.AreSame(controller, GetPrivateField(groundCheck, "autoPlayerController"));
    }

    [Test]
    public void GroundCheckUnderAHumanBallResolvesOwnerAndStateFromTheRuntimeBinding()
    {
        GameObject humanActor = Spawn("human-actor-groundcheck");
        humanActor.AddComponent<CharacterProfile>();
        PlayerController controller = humanActor.AddComponent<PlayerController>();

        GameObject ballRoot = Spawn("human-ball-groundcheck");
        BasketBall ball = ballRoot.AddComponent<BasketBall>();
        BasketBallState state = ballRoot.AddComponent<BasketBallState>();
        ball.BindOwner(0, false, true, humanActor, controller);

        GameObject groundCheckGo = Spawn("groundCheck");
        groundCheckGo.transform.parent = ballRoot.transform;
        GroundCheck groundCheck = groundCheckGo.AddComponent<GroundCheck>();

        InvokeStart(groundCheck);

        Assert.AreSame(state, GetPrivateField(groundCheck, "basketBallState"));
        Assert.AreSame(controller, GetPrivateField(groundCheck, "playerController"));
    }

    [Test]
    public void GroundCheckWithNeitherIdentitySourceInItsParentHierarchyFailsCleanly()
    {
        GameObject groundCheckGo = Spawn("orphan-groundcheck");
        GroundCheck groundCheck = groundCheckGo.AddComponent<GroundCheck>();

        LogAssert.Expect(LogType.Error, new Regex("neither a PlayerIdentifier nor an IBasketballRuntime"));
        InvokeStart(groundCheck);

        Assert.IsFalse(groundCheckGo.activeSelf, "GroundCheck must deactivate itself rather than run its trigger handlers against null state");
    }

    // AUD-010 Phase 1c: GiveBall now also binds a human ball's IGroundHeightProvider - unrelated to
    // this file's IBasketballRuntime ownership focus, but a coordinator built without one makes every
    // human-ball GiveBall call here log an unexpected error (see
    // Level5BasketballGroundHeightProviderTests for that binding's own coverage). Supplying a stub
    // keeps this file's tests exercising only what they are named for.
    private sealed class FakeGroundHeightProvider : IGroundHeightProvider
    {
        public float GroundHeight => 0f;
    }
}
