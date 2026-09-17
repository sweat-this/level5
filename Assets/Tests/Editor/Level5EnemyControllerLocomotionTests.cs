using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 4 Slice 74: <c>EnemyController</c>'s ordinary pursue/patrol locomotion moved from
/// <c>Rigidbody.MovePosition</c> to <see cref="RigidbodyLocomotionMotor"/>, mirroring
/// <c>AutoPlayerDefense</c>'s Slice 73 migration (<c>Level5AutoPlayerDefenseLocomotionTests</c>).
/// <c>pursueTarget</c>/<c>moveToTarget</c>/<c>returnToPatrol</c> are all public, so most tests call them
/// directly; reflection is used for the private <c>rigidBody</c>/<c>enemyDetection</c>/<c>movementSpeed</c>/
/// <c>targetPosition</c>/<c>movement</c>/<c>currentBodyguardTarget</c>/<c>currentState</c> fields this
/// class's own composition (<c>Awake</c>) or state machine (<c>UpdateDistanceFromPlayer</c>/
/// <c>RefreshBodyguardTarget</c>/<c>Update</c>) otherwise own, and for the private <c>FixedUpdate()</c>
/// method itself in the tests that exercise its own release branch. <c>Awake()</c>/<c>OnEnable()</c> run
/// automatically on <c>AddComponent</c>, but <c>Awake()</c> is not guaranteed to have completed by the
/// time a synchronous EditMode <c>[Test]</c> continues past that call (no active player-loop tick drives
/// it inline the way Play Mode does) - confirmed empirically, not assumed - so <c>rigidBody</c>/
/// <c>enemyDetection</c> are set explicitly via reflection rather than relied on from <c>Awake()</c>.
/// <c>Update()</c> itself is never invoked - its own state-gating logic (how <c>stateWalk</c>/
/// <c>stateAttack</c>/<c>stateIdle</c> get computed) is out of scope for this slice; only the resulting
/// field values this class's actual <c>FixedUpdate()</c> reads are set directly.
/// </summary>
public class Level5EnemyControllerLocomotionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

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

    private EnemyController BuildEnemy(out Rigidbody rigidBody, out EnemyDetection detection, float movementSpeed)
    {
        GameObject go = Spawn("enemy-locomotion-test-actor");
        rigidBody = go.AddComponent<Rigidbody>();
        detection = go.AddComponent<EnemyDetection>();
        EnemyController enemy = go.AddComponent<EnemyController>();

        // Awake() (which resolves rigidBody/enemyDetection via GetComponent) is not guaranteed to have
        // run yet by the time a synchronous EditMode [Test] continues past AddComponent - there is no
        // active player-loop tick to invoke it inline the way Play Mode does. Set explicitly rather
        // than depend on Awake() having completed.
        SetPrivateField(enemy, "rigidBody", rigidBody);
        SetPrivateField(enemy, "enemyDetection", detection);
        SetPrivateField(enemy, "movementSpeed", movementSpeed);

        return enemy;
    }

    /// <summary>
    /// pursueTarget's very first check is `TargetQueue == null` - true on both the bodyguard and
    /// reserved-attack-position paths, unchanged by this slice. Bodyguard-path tests below need a
    /// resolvable queue purely to clear that gate; its contents are irrelevant to the bodyguard branch.
    /// </summary>
    private PlayerAttackQueue AssignBareQueue(EnemyController enemy)
    {
        GameObject queueGo = Spawn("attack-queue");
        PlayerAttackQueue queue = queueGo.AddComponent<PlayerAttackQueue>();
        enemy.AssignTargetQueue(queue);
        return queue;
    }

    private class StubCombatAgent : ICombatAgent
    {
        private readonly GameObject combatObject;

        public StubCombatAgent(GameObject combatObject)
        {
            this.combatObject = combatObject;
        }

        public GameObject CombatObject => combatObject;
        public Transform CombatTransform => combatObject.transform;
        public bool CanAct => true;
    }

    [Test]
    public void PursueTarget_BodyguardTarget_CommandsPlanarVelocityEqualToMovementSpeed()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        AssignBareQueue(enemy);
        GameObject bodyguardGo = Spawn("bodyguard-target");
        bodyguardGo.transform.position = new Vector3(10f, 0f, 0f);
        SetPrivateField(enemy, "currentBodyguardTarget", new StubCombatAgent(bodyguardGo));

        enemy.pursueTarget();

        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(10f).Within(0.0001f),
            "a bodyguard target far beyond one physics step's travel must command exactly movementSpeed, "
            + "in world units per second - not a per-step displacement scaled again by fixedDeltaTime");
        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    /// <summary>
    /// The old <c>MovePosition</c> path used the full 3D <c>(target - position)</c> direction, so a
    /// bodyguard's Y offset was still physically written to Y every step. Under velocity-only
    /// navigation only X/Z ever reaches the Rigidbody, so a 3D-normalized direction would silently
    /// throttle horizontal speed below <c>movementSpeed</c> whenever the target sits above or below
    /// this enemy. Offset in both X and Y so a regression (normalize-then-truncate instead of
    /// flatten-then-normalize) would produce a provably diluted horizontal speed.
    /// </summary>
    [Test]
    public void PursueTarget_BodyguardTargetWithYDelta_StillCommandsFullMovementSpeedHorizontally()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        AssignBareQueue(enemy);
        GameObject bodyguardGo = Spawn("bodyguard-target");
        bodyguardGo.transform.position = new Vector3(10f, 5f, 0f);
        SetPrivateField(enemy, "currentBodyguardTarget", new StubCombatAgent(bodyguardGo));

        enemy.pursueTarget();

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.EqualTo(10f).Within(0.0001f),
            "horizontal speed must be exactly movementSpeed regardless of the target's Y component - "
            + "only X/Z ever reaches the Rigidbody now");
    }

    [Test]
    public void PursueTarget_BodyguardTarget_PreservesExistingYVelocity()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        AssignBareQueue(enemy);
        GameObject bodyguardGo = Spawn("bodyguard-target");
        bodyguardGo.transform.position = new Vector3(0f, 0f, 10f);
        SetPrivateField(enemy, "currentBodyguardTarget", new StubCombatAgent(bodyguardGo));
        rigidBody.linearVelocity = new Vector3(0f, 8f, 0f);

        enemy.pursueTarget();

        Assert.That(rigidBody.linearVelocity.y, Is.EqualTo(8f),
            "ordinary pursuit must never touch Y - gravity/knockback own it");
    }

    [Test]
    public void PursueTarget_NoBodyguard_UsesReservedAttackPositionAtMovementSpeed()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out EnemyDetection detection, movementSpeed: 10f);
        GameObject queueGo = Spawn("attack-queue");
        PlayerAttackQueue queue = queueGo.AddComponent<PlayerAttackQueue>();
        GameObject attackPositionGo = Spawn("attack-position-0");
        attackPositionGo.transform.position = new Vector3(0f, 0f, 10f);
        SetPrivateField(queue, "attackPositions", new[] { attackPositionGo });
        enemy.AssignTargetQueue(queue);
        detection.SetAttackReservation(true, 0);

        enemy.pursueTarget();

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(velocity.z, Is.EqualTo(10f).Within(0.0001f),
            "with no bodyguard target, pursuit must advance on the reserved attack position at "
            + "movementSpeed");
    }

    [Test]
    public void PursueTarget_NoQueue_ReleasesPlanarVelocityAndPreservesY()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);

        enemy.pursueTarget();

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "an unresolvable target queue must release the last commanded X velocity - the old "
            + "MovePosition path simply stopped producing displacement, which a persisted velocity "
            + "command must now replicate explicitly");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-2f), "release must not touch Y");
    }

    [Test]
    public void PursueTarget_NoBodyguardAndNoAttackReservation_ReleasesPlanarVelocityAndPreservesY()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out EnemyDetection detection, movementSpeed: 10f);
        GameObject queueGo = Spawn("attack-queue");
        PlayerAttackQueue queue = queueGo.AddComponent<PlayerAttackQueue>();
        SetPrivateField(queue, "attackPositions", new GameObject[0]);
        enemy.AssignTargetQueue(queue);
        // AttackPositionId defaults to -1 (SetAttackReservation never called) - GetAttackPositionTransform(-1)
        // resolves to null via its own negative-id guard, exercising the "cannot continue" release path.
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);

        enemy.pursueTarget();

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "no attack reservation yet must release the last commanded X velocity");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-2f), "release must not touch Y");
    }

    /// <summary>
    /// Distinct from <see cref="PursueTarget_NoBodyguardAndNoAttackReservation_ReleasesPlanarVelocityAndPreservesY"/>:
    /// that test relies on GetAttackPositionTransform's negative-id guard (id defaults to -1 when no
    /// reservation was ever granted). This one holds a genuine reservation with a non-negative id that
    /// is nonetheless out of range for the queue's current attackPositions array - a queue-side reason
    /// GetAttackPositionTransform can return null distinct from "never reserved", both of which must
    /// hit the same release path.
    /// </summary>
    [Test]
    public void PursueTarget_NoBodyguardAndOutOfRangeAttackPosition_ReleasesPlanarVelocityAndPreservesY()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out EnemyDetection detection, movementSpeed: 10f);
        GameObject queueGo = Spawn("attack-queue");
        PlayerAttackQueue queue = queueGo.AddComponent<PlayerAttackQueue>();
        SetPrivateField(queue, "attackPositions", new GameObject[0]);
        enemy.AssignTargetQueue(queue);
        detection.SetAttackReservation(true, 5);
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);

        enemy.pursueTarget();

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "a reserved attack position id outside the queue's current array must release the last "
            + "commanded X velocity");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-2f), "release must not touch Y");
    }

    [Test]
    public void PursueTarget_KeepsTheMovementFieldsExistingDisplacementMeaning()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        AssignBareQueue(enemy);
        GameObject bodyguardGo = Spawn("bodyguard-target");
        bodyguardGo.transform.position = new Vector3(10f, 0f, 0f);
        SetPrivateField(enemy, "currentBodyguardTarget", new StubCombatAgent(bodyguardGo));

        enemy.pursueTarget();

        Vector3 movement = (Vector3)GetPrivateField(enemy, "movement");
        Vector3 expected = new Vector3(1f, 0f, 0f) * (10f * Time.fixedDeltaTime);
        Assert.That(movement.x, Is.EqualTo(expected.x).Within(0.0001f),
            "the movement field must keep its existing per-step displacement meaning (direction * "
            + "speed * fixedDeltaTime) - only the Rigidbody command's units changed");
        Assert.That(movement.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void MoveToTarget_CommandsPlanarVelocityFromExistingTargetPositionDirection()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        SetPrivateField(enemy, "targetPosition", new Vector3(1f, 0f, 0f));

        enemy.moveToTarget(new List<GameObject>());

        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(10f).Within(0.0001f),
            "moveToTarget must command movementSpeed along the existing targetPosition direction, "
            + "unchanged in signature and contract - only its Rigidbody write moved to the shared motor");
        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ReturnToPatrol_OutsideThreshold_CommandsPlanarVelocityTowardOriginalPosition()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        enemy.OriginalPosition = new Vector3(10f, 0f, 0f);
        // transform.position defaults to the origin, further than the 1-unit completion threshold away.

        enemy.returnToPatrol();

        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(10f).Within(0.0001f),
            "outside the completion threshold, patrol must command exactly movementSpeed toward "
            + "OriginalPosition");
        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ReturnToPatrol_WithinThreshold_ReleasesPlanarVelocityPreservesYAndClearsStatePatrol()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        enemy.OriginalPosition = enemy.transform.position;
        enemy.statePatrol = true;
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);

        enemy.returnToPatrol();

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "reaching OriginalPosition must release the last commanded X velocity, or the enemy would "
            + "keep sliding past its patrol destination");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-2f), "patrol completion must not touch Y");
        Assert.That(enemy.statePatrol, Is.False, "patrol completion must still clear statePatrol");
    }

    /// <summary>
    /// Code review finding on this slice: the internal releases above cover pursueTarget()'s own early
    /// returns and returnToPatrol()'s own completion branch, but FixedUpdate's external gates
    /// (stateWalk/currentState/statePatrol) can also flip false for reasons neither method ever sees -
    /// Update() flipping stateWalk when stateAttack starts, or EnemyDetection.CheckReturnToPatrolStatus
    /// writing statePatrol directly on its own 3-second cadence, bypassing returnToPatrol() entirely.
    /// These tests drive the real (private) FixedUpdate() to prove its own else-branch release, plus two
    /// positive controls proving that branch does not suppress legitimate ongoing pursuit/patrol.
    /// </summary>
    [Test]
    public void FixedUpdate_NeitherWalkingNorPatrolling_ReleasesResidualPlanarVelocityAndPreservesY()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);
        enemy.stateWalk = false;
        enemy.statePatrol = false;

        InvokeFixedUpdate(enemy);

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "when neither pursuing nor patrolling, FixedUpdate must release residual X velocity itself - "
            + "e.g. when CheckReturnToPatrolStatus clears statePatrol directly without returnToPatrol() "
            + "ever running again to release it internally");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-2f), "release must not touch Y");
    }

    [Test]
    public void FixedUpdate_KnockedDownSuppressesPursuit_ReleasesResidualPlanarVelocityAndPreservesY()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out EnemyDetection detection, movementSpeed: 10f);
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);
        enemy.stateWalk = true;
        enemy.statePatrol = false;
        detection.SetAttackReservation(true, 0);
        int knockdownHash = (int)GetPrivateStaticField(typeof(EnemyController), "AnimatorState_Knockdown");
        SetPrivateField(enemy, "currentState", knockdownHash);

        InvokeFixedUpdate(enemy);

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "stateWalk alone is not enough to pursue while knocked down - the currentState guard must "
            + "suppress pursuit and this else-branch must release the residual velocity regardless of "
            + "whether Update()'s unrelated stateIdle branch has run yet this frame");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-2f), "release must not touch Y");
    }

    [Test]
    public void FixedUpdate_StillPursuing_DoesNotReleaseCommandedVelocity()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out EnemyDetection detection, movementSpeed: 10f);
        AssignBareQueue(enemy);
        GameObject bodyguardGo = Spawn("bodyguard-target");
        bodyguardGo.transform.position = new Vector3(10f, 0f, 0f);
        SetPrivateField(enemy, "currentBodyguardTarget", new StubCombatAgent(bodyguardGo));
        enemy.stateWalk = true;
        enemy.statePatrol = false;
        detection.SetAttackReservation(true, 0);

        InvokeFixedUpdate(enemy);

        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(10f).Within(0.0001f),
            "the new else-branch must not suppress legitimate ongoing pursuit");
    }

    [Test]
    public void FixedUpdate_StillPatrolling_DoesNotReleaseCommandedVelocity()
    {
        EnemyController enemy = BuildEnemy(out Rigidbody rigidBody, out _, movementSpeed: 10f);
        enemy.OriginalPosition = new Vector3(10f, 0f, 0f);
        enemy.stateWalk = false;
        enemy.statePatrol = true;

        InvokeFixedUpdate(enemy);

        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(10f).Within(0.0001f),
            "the new else-branch is skipped whenever statePatrol is about to run - patrol must still "
            + "command its own velocity uninterrupted");
    }

    private static void InvokeFixedUpdate(EnemyController enemy)
    {
        MethodInfo method = typeof(EnemyController).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "EnemyController must declare FixedUpdate()");
        method.Invoke(enemy, null);
    }

    private static object GetPrivateStaticField(System.Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, $"{type.Name} must declare a static field named '{fieldName}'");
        return field.GetValue(null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }
}
