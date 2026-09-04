using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: <c>BasketBall</c>'s last two live <c>MatchRuntime</c> reads
/// (<c>Start()</c>'s <c>MatchRuntime.Rules.EnemiesOnly</c>/<c>IsBattleRoyal</c>, <c>Update()</c>'s
/// <c>MatchRuntime.Rules.EnemiesOnly</c>) are replaced by a bind-once <see cref="ResolvedMatchRules"/>
/// reference, bound once by composition (<see cref="SpawnCoordinator.GiveBall"/>) - mirroring the
/// bind/rebind/null-guard shape <see cref="BasketBallState.BindMatchRules"/> and
/// <see cref="BasketBallAuto.BindMatchRules"/> already established for the same seam. This file
/// covers <c>BindMatchRules</c> itself, the Start()-time validation guard (and its position after the
/// existing owner/ground-height-provider guards), the preserved
/// <c>EnemiesOnly</c>/<c>IsBattleRoyal</c> Start()/Update() asymmetry, and the coordinator's
/// composition-time wiring for both a primary and a secondary human ball.
/// </summary>
public class Level5BasketBallMatchRulesTests
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

    private static void InvokeStart(MonoBehaviour behaviour)
    {
        MethodInfo start = behaviour.GetType().GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(start, $"{behaviour.GetType().Name} must declare Start()");
        start.Invoke(behaviour, null);
    }

    private static void InvokeUpdate(MonoBehaviour behaviour)
    {
        MethodInfo update = behaviour.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(update, $"{behaviour.GetType().Name} must declare Update()");
        update.Invoke(behaviour, null);
    }

    private sealed class FakeShooterActor : IShooterActor
    {
        public bool HasBasketball { get; set; }
        public bool FacingFront => true;
        public bool Grounded => true;
        public bool InAir => false;
        public bool InDunkState => false;
        public float DistanceFromRim => 0f;
        public ShooterAttributes ShooterAttributes => default;
        public int Clutch => 0;
        public float ShotMeterSliderValue => 0f;
        public bool ShotMeterEnded => true;
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

    /// <summary>
    /// Builds just enough of a bound, primary human ball to run its real Start()/Update() - the
    /// "basketBall_position" child under the owner actor and the "drop shadow" child under the ball's
    /// own root are the two scene-hierarchy pieces Start() dereferences unconditionally once past the
    /// owner/provider/rules guards.
    /// </summary>
    private BasketBall BuildBoundBall(
        ResolvedMatchRules rules, out GameObject ballGo, out FakeShooterActor actor,
        bool bindGroundHeightProvider = true)
    {
        GameObject playerGo = Spawn("human-actor");
        GameObject basketballPositionGo = Spawn("basketBall_position");
        basketballPositionGo.transform.parent = playerGo.transform;

        ballGo = Spawn("human-ball");
        ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        GameObject dropShadowGo = Spawn("drop shadow");
        dropShadowGo.transform.parent = ballGo.transform;

        BasketBall ball = ballGo.AddComponent<BasketBall>();
        actor = new FakeShooterActor();
        ball.BindOwner(0, false, true, playerGo, actor);
        if (bindGroundHeightProvider)
        {
            ball.BindGroundHeightProvider(new FakeGroundHeightProvider());
        }

        if (rules != null)
        {
            ball.BindMatchRules(rules);
        }

        return ball;
    }

    // ==================== BindMatchRules bind semantics ====================

    [Test]
    public void BindMatchRulesRejectsNullFirstBind()
    {
        BasketBall ball = Spawn("human-ball-null-rules").AddComponent<BasketBall>();

        LogAssert.Expect(LogType.Error, new Regex("null match rules"));
        ball.BindMatchRules(null);

        Assert.IsNull(GetPrivateField(ball, "matchRules"), "a rejected null bind must leave the ball unbound");
    }

    [Test]
    public void BindMatchRulesAcceptsFirstValidBind()
    {
        BasketBall ball = Spawn("human-ball-first-rules").AddComponent<BasketBall>();
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);

        ball.BindMatchRules(rules);

        Assert.AreSame(rules, GetPrivateField(ball, "matchRules"));
    }

    [Test]
    public void BindMatchRulesRejectsSecondValidBind()
    {
        BasketBall ball = Spawn("human-ball-second-rules").AddComponent<BasketBall>();
        ResolvedMatchRules first = new ResolvedMatchRules(enemiesOnly: true);
        ResolvedMatchRules second = new ResolvedMatchRules(enemiesOnly: false);
        ball.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already has bound match rules"));
        ball.BindMatchRules(second);

        Assert.AreSame(first, GetPrivateField(ball, "matchRules"),
            "a second BindMatchRules call must not overwrite the original rules");
    }

    /// <summary>
    /// Code review shape (mirroring BasketBallState/BasketBallAuto's BindMatchRules): a null second
    /// call after a real bind already succeeded must report "already bound", not "remaining unbound" -
    /// the ball is not unbound, it still holds the original valid rules. Pins the check ordering
    /// (already-bound checked before null-argument) that makes that true.
    /// </summary>
    [Test]
    public void BindMatchRulesRejectsNullSecondBindWithAnAlreadyBoundMessage()
    {
        BasketBall ball = Spawn("human-ball-null-second-rules").AddComponent<BasketBall>();
        ResolvedMatchRules first = new ResolvedMatchRules(enemiesOnly: true);
        ball.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already has bound match rules"));
        ball.BindMatchRules(null);

        Assert.AreSame(first, GetPrivateField(ball, "matchRules"), "a null second bind must not clear the original rules");
    }

    // ==================== Start() rules guard ====================

    [Test]
    public void StartWithBoundOwnerAndProviderButNoMatchRulesDeactivatesAndDoesNotClaimTheStatic()
    {
        BasketBall ball = BuildBoundBall(null, out GameObject ballGo, out _);

        LogAssert.Expect(LogType.Error, new Regex("no bound match rules"));
        InvokeStart(ball);

        Assert.IsFalse(ballGo.activeSelf,
            "a BasketBall with a bound owner and ground-height provider but no bound match rules must deactivate its GameObject");
        Assert.AreNotSame(ball, BasketBall.instance, "an invalid human basketball must not claim BasketBall.instance");
    }

    [Test]
    public void StartWithBoundOwnerProviderAndMatchRulesClaimsTheStaticAsBefore()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        BasketBall ball = BuildBoundBall(rules, out _, out _);

        InvokeStart(ball);

        Assert.AreSame(ball, BasketBall.instance, "a valid primary basketball must still claim the static exactly as before");
    }

    /// <summary>
    /// Preserves the existing ground-height-provider failure's precedence: even with the ball's own
    /// match rules also unbound, a missing ground-height provider must still be the reported failure,
    /// since it is checked first. The sibling BasketBallState is fully composed (owner - bound
    /// automatically through BasketBall.BindOwner - and its own rules) so it cannot mask this
    /// behavior by producing an error of its own; BasketBallState.Start() is never invoked here.
    /// </summary>
    [Test]
    public void StartWithMissingProviderAndMissingMatchRulesStillFailsOnTheProviderFirst()
    {
        GameObject playerGo = Spawn("human-actor-provider-precedence");
        GameObject basketballPositionGo = Spawn("basketBall_position-provider-precedence");
        basketballPositionGo.transform.parent = playerGo.transform;

        GameObject ballGo = Spawn("human-ball-provider-precedence");
        BasketBallState state = ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        GameObject dropShadowGo = Spawn("drop shadow-provider-precedence");
        dropShadowGo.transform.parent = ballGo.transform;

        BasketBall ball = ballGo.AddComponent<BasketBall>();
        FakeShooterActor actor = new FakeShooterActor();
        ball.BindOwner(0, false, true, playerGo, actor);
        // ground-height provider intentionally left unbound.
        // BasketBall's own match rules intentionally left unbound too.
        state.BindMatchRules(new ResolvedMatchRules());

        LogAssert.Expect(LogType.Error, new Regex("no bound ground-height provider"));
        InvokeStart(ball);

        Assert.IsFalse(ballGo.activeSelf,
            "a human ball with neither a ground-height provider nor match rules bound must still report "
            + "the ground-height-provider failure first, unchanged from before this migration");
    }

    /// <summary>
    /// Isolates BasketBall's own missing-rules failure: everything else - owner, ground-height
    /// provider, and the sibling BasketBallState's own owner and rules - is fully composed, so this
    /// test can only fail because BasketBall itself lacks a bound ResolvedMatchRules, not because a
    /// sibling component is incompletely composed.
    /// </summary>
    [Test]
    public void StartWithFullyComposedSiblingStateButBasketBallsOwnMatchRulesMissingFailsOnMatchRules()
    {
        GameObject playerGo = Spawn("human-actor-rules-isolated");
        GameObject basketballPositionGo = Spawn("basketBall_position-rules-isolated");
        basketballPositionGo.transform.parent = playerGo.transform;

        GameObject ballGo = Spawn("human-ball-rules-isolated");
        BasketBallState state = ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        GameObject dropShadowGo = Spawn("drop shadow-rules-isolated");
        dropShadowGo.transform.parent = ballGo.transform;

        BasketBall ball = ballGo.AddComponent<BasketBall>();
        FakeShooterActor actor = new FakeShooterActor();
        ball.BindOwner(0, false, true, playerGo, actor); // also binds BasketBallState's owner.
        ball.BindGroundHeightProvider(new FakeGroundHeightProvider());
        state.BindMatchRules(new ResolvedMatchRules()); // sibling fully composed.
        // BasketBall's own match rules intentionally left unbound.

        LogAssert.Expect(LogType.Error, new Regex("no bound match rules"));
        InvokeStart(ball);

        Assert.IsFalse(ballGo.activeSelf,
            "BasketBall with a fully composed sibling BasketBallState but no bound match rules of its "
            + "own must still deactivate its GameObject");
        Assert.AreNotSame(ball, BasketBall.instance, "an invalid basketball must not claim BasketBall.instance");
    }

    // ==================== Rule behavior (Start) ====================

    [Test]
    public void StartDoesNotApplySpecialStartupEffectsUnderNormalRules()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        BasketBall ball = BuildBoundBall(rules, out GameObject ballGo, out _);
        float originalY = ballGo.transform.position.y;
        GameObject dropShadowGo = ballGo.transform.Find("drop shadow").gameObject;

        InvokeStart(ball);

        Assert.That(ballGo.transform.position.y, Is.EqualTo(originalY).Within(0.001f),
            "normal rules (EnemiesOnly=false, IsBattleRoyal=false) must not apply the fighting-mode Y offset");
        Assert.That(ballGo.GetComponent<Rigidbody>().constraints, Is.Not.EqualTo(RigidbodyConstraints.FreezeAll),
            "normal rules must not freeze the ball's Rigidbody constraints");
        Assert.IsTrue(dropShadowGo.activeSelf, "normal rules must not deactivate the drop shadow at Start()");
    }

    [Test]
    public void StartAppliesEnemiesOnlyStartupEffectsWhenTrue()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        BasketBall ball = BuildBoundBall(rules, out GameObject ballGo, out _);
        float originalY = ballGo.transform.position.y;
        GameObject dropShadowGo = ballGo.transform.Find("drop shadow").gameObject;

        InvokeStart(ball);

        Assert.That(ballGo.transform.position.y, Is.EqualTo(originalY + 20).Within(0.001f),
            "EnemiesOnly=true must still raise the ball by 20 on Start(), exactly as before");
        Assert.That(ballGo.GetComponent<Rigidbody>().constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll),
            "EnemiesOnly=true must still freeze the ball's Rigidbody constraints on Start()");
        Assert.IsFalse(dropShadowGo.activeSelf, "EnemiesOnly=true must still deactivate the drop shadow on Start()");
    }

    /// <summary>
    /// The highest-value predicate-regression case: Battle Royal without EnemiesOnly must still apply
    /// Start()'s special effects (the Start predicate is <c>EnemiesOnly || IsBattleRoyal</c>), but must
    /// NOT skip Update()'s normal body (Update only gates on <c>!EnemiesOnly</c>) - the intentional
    /// asymmetry between the two predicates.
    /// </summary>
    [Test]
    public void StartAppliesBattleRoyalOnlyStartupEffectsButUpdateStillRunsTheNormalBody()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(combatMode: CombatMode.BattleRoyal, enemiesOnly: false);
        Assert.IsTrue(rules.IsBattleRoyal, "test setup sanity: rules must actually resolve IsBattleRoyal=true");
        Assert.IsFalse(rules.EnemiesOnly, "test setup sanity: rules must actually resolve EnemiesOnly=false");

        BasketBall ball = BuildBoundBall(rules, out GameObject ballGo, out FakeShooterActor actor);
        float originalY = ballGo.transform.position.y;
        GameObject dropShadowGo = ballGo.transform.Find("drop shadow").gameObject;

        InvokeStart(ball);

        Assert.That(ballGo.transform.position.y, Is.EqualTo(originalY + 20).Within(0.001f),
            "Battle-Royal-only must still raise the ball by 20 on Start(), same as EnemiesOnly");
        Assert.That(ballGo.GetComponent<Rigidbody>().constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll),
            "Battle-Royal-only must still freeze the ball's Rigidbody constraints on Start()");
        Assert.IsFalse(dropShadowGo.activeSelf, "Battle-Royal-only must still deactivate the drop shadow on Start()");

        actor.HasBasketball = true;
        Assert.DoesNotThrow(() => InvokeUpdate(ball));
        Assert.IsFalse(ballGo.GetComponent<BasketBallState>().CanPullBall,
            "Update()'s normal possession block must still run under Battle-Royal-only - Update only gates on EnemiesOnly, not IsBattleRoyal");
    }

    // ==================== Rule behavior (Update) ====================

    [Test]
    public void UpdateSkipsTheNormalBodyWhenEnemiesOnlyIsTrue()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        BasketBall ball = BuildBoundBall(rules, out GameObject ballGo, out FakeShooterActor actor);
        InvokeStart(ball);
        actor.HasBasketball = true;

        Assert.DoesNotThrow(() => InvokeUpdate(ball));

        Assert.IsTrue(ballGo.GetComponent<BasketBallState>().CanPullBall,
            "the normal possession/visibility block must stay skipped while EnemiesOnly is true");
    }

    [Test]
    public void UpdateRunsTheNormalBodyWhenEnemiesOnlyIsFalse()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        BasketBall ball = BuildBoundBall(rules, out GameObject ballGo, out FakeShooterActor actor);
        InvokeStart(ball);
        actor.HasBasketball = true;

        InvokeUpdate(ball);

        Assert.IsFalse(ballGo.GetComponent<BasketBallState>().CanPullBall,
            "the normal possession block must run and clear CanPullBall while the ball is held and EnemiesOnly is false");
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

    /// <summary>
    /// EnemiesOnly=true is deliberately not ResolvedMatchRules' own default (false) - an accidentally
    /// default-constructed rules instance would read back false and fail this, rather than
    /// coincidentally matching the coordinator's own rules.
    /// </summary>
    [Test]
    public void PrimaryHumanBallReceivesTheCoordinatorsExactMatchRulesReference()
    {
        PlayerRegistry registry = new PlayerRegistry();
        PlayerIdentifier owner = RegisterHumanParticipant(0, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);
        Assert.IsNotNull(humanPrefab, "human basketball prefab failed to load");

        GiveBallVia(coordinator, 0, humanPrefab, forCpu: false);

        GameObject ball = owner.basketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's basketball reference");
        spawned.Add(ball);

        BasketBall runtime = ball.GetComponent<BasketBall>();
        Assert.AreSame(rules, GetPrivateField(runtime, "matchRules"),
            "GiveBall must bind the coordinator's own rules to the primary human ball, not some other default-valued instance");
    }

    [Test]
    public void SecondaryHumanBallAlsoReceivesTheCoordinatorsExactMatchRulesReferenceWithItsOwnIdentity()
    {
        PlayerRegistry registry = new PlayerRegistry();
        RegisterHumanParticipant(0, registry);
        PlayerIdentifier secondHuman = RegisterHumanParticipant(1, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject humanPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_human);

        GiveBallVia(coordinator, 0, humanPrefab, forCpu: false);
        GiveBallVia(coordinator, 1, humanPrefab, forCpu: false);

        GameObject firstBall = registry.GetBySlot(0).basketball;
        GameObject secondBall = secondHuman.basketball;
        spawned.Add(firstBall);
        spawned.Add(secondBall);

        BasketBall firstRuntime = firstBall.GetComponent<BasketBall>();
        BasketBall secondRuntime = secondBall.GetComponent<BasketBall>();
        Assert.AreSame(rules, GetPrivateField(firstRuntime, "matchRules"));
        Assert.AreSame(rules, GetPrivateField(secondRuntime, "matchRules"),
            "a second human ball must also receive the coordinator's own rules");
        Assert.That(secondRuntime.ParticipantId, Is.EqualTo(1),
            "each human ball keeps its own participant identity while sharing the same match rules");
        Assert.IsFalse(secondRuntime.IsPrimary, "a secondary human ball must remain non-primary");
        Assert.That(firstRuntime.ParticipantId, Is.EqualTo(0));
        Assert.IsTrue(firstRuntime.IsPrimary, "slot 0 remains the primary ball");
    }
}
