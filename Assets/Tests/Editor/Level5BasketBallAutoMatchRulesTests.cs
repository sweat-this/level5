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
/// AUD-010 Phase 2b0: <c>BasketBallAuto</c>'s last two live <c>MatchRuntime</c> reads
/// (<c>Start()</c>/<c>Update()</c>'s <c>MatchRuntime.Rules.EnemiesOnly</c>) are replaced by a bind-once
/// <see cref="ResolvedMatchRules"/> reference, bound once by composition
/// (<see cref="SpawnCoordinator.GiveBall"/>) - mirroring the bind/rebind/null-guard shape
/// <see cref="BasketBallState.BindMatchRules"/> already established for the same seam. This file
/// covers <c>BindMatchRules</c> itself, the Start()-time validation guard, the preserved
/// <c>EnemiesOnly</c> startup/update behavior, and the coordinator's composition-time wiring.
/// </summary>
public class Level5BasketBallAutoMatchRulesTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        BasketBallAuto.instance = null;

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

    /// <summary>
    /// Builds just enough of a bound CPU ball to run its real Start()/Update() - the "basketBall_position"
    /// child under the owner actor and the "drop shadow" child under the ball's own root are the two
    /// scene-hierarchy pieces Start() dereferences unconditionally once past the owner/rules guards.
    /// </summary>
    private BasketBallAuto BuildBoundBall(
        ResolvedMatchRules rules, out GameObject ballGo, out FakeShooterActor actor)
    {
        GameObject autoPlayerGo = Spawn("cpu-actor");
        GameObject basketballPositionGo = Spawn("basketBall_position");
        basketballPositionGo.transform.parent = autoPlayerGo.transform;

        ballGo = Spawn("cpu-ball");
        ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<GameStats>();
        ballGo.AddComponent<Rigidbody>();
        ballGo.AddComponent<Animator>();
        GameObject dropShadowGo = Spawn("drop shadow");
        dropShadowGo.transform.parent = ballGo.transform;

        BasketBallAuto ball = ballGo.AddComponent<BasketBallAuto>();
        actor = new FakeShooterActor();
        ball.BindOwner(0, true, false, autoPlayerGo, actor);
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
        BasketBallAuto ball = Spawn("cpu-ball-null-rules").AddComponent<BasketBallAuto>();

        LogAssert.Expect(LogType.Error, new Regex("null match rules"));
        ball.BindMatchRules(null);

        Assert.IsNull(GetPrivateField(ball, "matchRules"), "a rejected null bind must leave the ball unbound");
    }

    [Test]
    public void BindMatchRulesAcceptsFirstValidBind()
    {
        BasketBallAuto ball = Spawn("cpu-ball-first-rules").AddComponent<BasketBallAuto>();
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);

        ball.BindMatchRules(rules);

        Assert.AreSame(rules, GetPrivateField(ball, "matchRules"));
    }

    [Test]
    public void BindMatchRulesRejectsSecondValidBind()
    {
        BasketBallAuto ball = Spawn("cpu-ball-second-rules").AddComponent<BasketBallAuto>();
        ResolvedMatchRules first = new ResolvedMatchRules(enemiesOnly: true);
        ResolvedMatchRules second = new ResolvedMatchRules(enemiesOnly: false);
        ball.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already has bound match rules"));
        ball.BindMatchRules(second);

        Assert.AreSame(first, GetPrivateField(ball, "matchRules"),
            "a second BindMatchRules call must not overwrite the original rules");
    }

    /// <summary>
    /// Code review shape (mirroring BasketBallState.BindMatchRules): a null second call after a real
    /// bind already succeeded must report "already bound", not "remaining unbound" - the ball is not
    /// unbound, it still holds the original valid rules. Pins the check ordering (already-bound checked
    /// before null-argument) that makes that true.
    /// </summary>
    [Test]
    public void BindMatchRulesRejectsNullSecondBindWithAnAlreadyBoundMessage()
    {
        BasketBallAuto ball = Spawn("cpu-ball-null-second-rules").AddComponent<BasketBallAuto>();
        ResolvedMatchRules first = new ResolvedMatchRules(enemiesOnly: true);
        ball.BindMatchRules(first);

        LogAssert.Expect(LogType.Error, new Regex("already has bound match rules"));
        ball.BindMatchRules(null);

        Assert.AreSame(first, GetPrivateField(ball, "matchRules"), "a null second bind must not clear the original rules");
    }

    // ==================== Start() rules guard ====================

    [Test]
    public void StartWithBoundOwnerButNoMatchRulesDeactivatesAndDoesNotClaimTheStatic()
    {
        BasketBallAuto ball = BuildBoundBall(null, out GameObject ballGo, out _);

        LogAssert.Expect(LogType.Error, new Regex("no bound match rules"));
        InvokeStart(ball);

        Assert.IsFalse(ballGo.activeSelf,
            "a BasketBallAuto with a bound owner but no bound match rules must deactivate its GameObject");
        Assert.AreNotSame(ball, BasketBallAuto.instance,
            "an invalid CPU basketball must not claim BasketBallAuto.instance");
    }

    [Test]
    public void StartWithBoundOwnerAndMatchRulesClaimsTheStaticAsBefore()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        BasketBallAuto ball = BuildBoundBall(rules, out _, out _);

        InvokeStart(ball);

        Assert.AreSame(ball, BasketBallAuto.instance, "a valid CPU basketball must still claim the static exactly as before");
    }

    // ==================== EnemiesOnly startup effects (unchanged behavior) ====================

    [Test]
    public void StartAppliesEnemiesOnlyStartupEffectsWhenTrue()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        BasketBallAuto ball = BuildBoundBall(rules, out GameObject ballGo, out _);
        float originalY = ballGo.transform.position.y;

        InvokeStart(ball);

        Assert.That(ballGo.transform.position.y, Is.EqualTo(originalY + 20).Within(0.001f),
            "EnemiesOnly=true must still raise the ball by 20 on Start(), exactly as before");
        Assert.That(ballGo.GetComponent<Rigidbody>().constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll),
            "EnemiesOnly=true must still freeze the ball's Rigidbody constraints on Start()");
    }

    [Test]
    public void StartDoesNotApplyEnemiesOnlyStartupEffectsWhenFalse()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        BasketBallAuto ball = BuildBoundBall(rules, out GameObject ballGo, out _);
        float originalY = ballGo.transform.position.y;

        InvokeStart(ball);

        Assert.That(ballGo.transform.position.y, Is.EqualTo(originalY).Within(0.001f),
            "EnemiesOnly=false must not apply the fighting-mode Y offset");
        Assert.That(ballGo.GetComponent<Rigidbody>().constraints, Is.Not.EqualTo(RigidbodyConstraints.FreezeAll),
            "EnemiesOnly=false must not freeze the ball's Rigidbody constraints");
    }

    // ==================== Update() ordering (unchanged behavior) ====================

    private static readonly string BasketBallAutoPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "BasketBallAuto.cs");

    /// <summary>
    /// Pins the required Update() ordering structurally: CheckIsBallFacingGoalAuto() must run
    /// unconditionally, before the EnemiesOnly gate - not moved inside the normal-ball branch. A
    /// runtime check of this specific ordering claim can't use the ball's own facing-flip as its
    /// observable: EnemiesOnly=true also freezes the Rigidbody's constraints in Start() (see
    /// <see cref="StartAppliesEnemiesOnlyStartupEffectsWhenTrue"/>), and Unity's physics engine
    /// suppresses velocity along a frozen axis - the same production behavior that makes the ball
    /// genuinely not move during a fighting-mode match. The source-order check below is what actually
    /// proves the requirement regardless of that interaction.
    /// </summary>
    [Test]
    public void UpdateCallsFacingCheckBeforeTheEnemiesOnlyGate()
    {
        string text = Level5TestSourceText.StripComments(File.ReadAllText(BasketBallAutoPath));
        int callIndex = text.IndexOf("CheckIsBallFacingGoalAuto();");
        int gateIndex = text.IndexOf("if (!matchRules.EnemiesOnly)");

        Assert.That(callIndex, Is.GreaterThan(-1), "Update() must call CheckIsBallFacingGoalAuto()");
        Assert.That(gateIndex, Is.GreaterThan(-1), "Update() must gate its normal body on matchRules.EnemiesOnly");
        Assert.That(callIndex, Is.LessThan(gateIndex),
            "CheckIsBallFacingGoalAuto() must run unconditionally, before the EnemiesOnly gate - not moved inside the normal-ball branch");
    }

    [Test]
    public void UpdateDoesNotThrowWhenEnemiesOnlyIsTrueAndSkipsTheNormalBody()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        BasketBallAuto ball = BuildBoundBall(rules, out GameObject ballGo, out FakeShooterActor actor);
        InvokeStart(ball);
        actor.HasBasketball = true;

        Assert.DoesNotThrow(() => InvokeUpdate(ball));

        Assert.IsTrue(ballGo.GetComponent<BasketBallState>().CanPullBall,
            "the normal possession/visibility block must stay skipped while EnemiesOnly is true");
    }

    [Test]
    public void UpdateRunsNormalBodyWhenEnemiesOnlyIsFalse()
    {
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: false);
        BasketBallAuto ball = BuildBoundBall(rules, out GameObject ballGo, out FakeShooterActor actor);
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

    private sealed class FakeGroundHeightProvider : IGroundHeightProvider
    {
        public float GroundHeight => 0f;
    }

    /// <summary>
    /// EnemiesOnly=true is deliberately not ResolvedMatchRules' own default (false) - an accidentally
    /// default-constructed rules instance would read back false and fail this, rather than
    /// coincidentally matching the coordinator's own rules.
    /// </summary>
    [Test]
    public void CpuBallReceivesTheCoordinatorsExactMatchRulesReference()
    {
        PlayerRegistry registry = new PlayerRegistry();
        PlayerIdentifier cpuOwner = RegisterCpuParticipant(0, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);
        Assert.IsNotNull(cpuPrefab, "CPU basketball prefab failed to load");

        GiveBallVia(coordinator, 0, cpuPrefab, forCpu: true);

        GameObject ball = cpuOwner.autoBasketball;
        Assert.IsNotNull(ball, "GiveBall must wire the owner's autoBasketball reference");
        spawned.Add(ball);

        BasketBallAuto runtime = ball.GetComponent<BasketBallAuto>();
        Assert.AreSame(rules, GetPrivateField(runtime, "matchRules"),
            "GiveBall must bind the coordinator's own rules to the CPU ball, not some other default-valued instance");
    }

    [Test]
    public void SecondCpuBallAlsoReceivesTheCoordinatorsExactMatchRulesReferenceWithItsOwnIdentity()
    {
        PlayerRegistry registry = new PlayerRegistry();
        RegisterCpuParticipant(0, registry);
        PlayerIdentifier secondCpu = RegisterCpuParticipant(1, registry);
        ResolvedMatchRules rules = new ResolvedMatchRules(enemiesOnly: true);
        SpawnCoordinator coordinator = new SpawnCoordinator(
            new SpawnCoordinator.SpawnLocations(), registry, rules, new PlayerRoster(new PlayerSlot[0]),
            GameModeId.None, new FakeGroundHeightProvider());
        GameObject cpuPrefab = Resources.Load<GameObject>(Constants.PREFAB_PATH_BASKETBALL_cpu);

        GiveBallVia(coordinator, 0, cpuPrefab, forCpu: true);
        GiveBallVia(coordinator, 1, cpuPrefab, forCpu: true);

        GameObject firstBall = registry.GetBySlot(0).autoBasketball;
        GameObject secondBall = secondCpu.autoBasketball;
        spawned.Add(firstBall);
        spawned.Add(secondBall);

        BasketBallAuto firstRuntime = firstBall.GetComponent<BasketBallAuto>();
        BasketBallAuto secondRuntime = secondBall.GetComponent<BasketBallAuto>();
        Assert.AreSame(rules, GetPrivateField(firstRuntime, "matchRules"));
        Assert.AreSame(rules, GetPrivateField(secondRuntime, "matchRules"),
            "a second CPU ball must also receive the coordinator's own rules");
        Assert.That(secondRuntime.ParticipantId, Is.EqualTo(1),
            "each CPU ball keeps its own participant identity while sharing the same match rules");
        Assert.That(firstRuntime.ParticipantId, Is.EqualTo(0));
    }
}
