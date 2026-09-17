using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 4 Slice 75: <c>RacingCinderBlock</c>'s dynamic 3D chase moved from
/// <c>Rigidbody.MovePosition</c> to a full-vector Rigidbody velocity command. Unlike the planar roles
/// Slices 72-74 migrated onto <see cref="RigidbodyLocomotionMotor"/>, this chase intentionally drives
/// X/Y/Z together (<c>pursuePlayer</c>'s target includes a facing-dependent X offset and a +2 vertical
/// offset), so it does not route through that motor.
///
/// <c>pursuePlayer()</c>, and therefore the real (private) <c>FixedUpdate()</c>, both dereference
/// <c>RacingGameManager.instance.Player</c>/<c>.PlayerController</c> unconditionally - there is no null
/// guard to lean on. Rather than let a real <c>RacingGameManager</c>'s own <c>Awake()</c> run (it touches
/// <c>PlayerControlsProvider.Controls</c>, unrelated input-system state this locomotion slice has no need
/// to compose), the manager GameObject is kept inactive while composing it and its static <c>instance</c>
/// is assigned directly - the same "do not rely on Awake completing synchronously in EditMode" caution
/// <see cref="Level5EnemyControllerLocomotionTests"/> already documents, applied one level higher here
/// because the dependency is a whole manager rather than a single field this class owns.
/// </summary>
public class Level5RacingCinderBlockLocomotionTests
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
        RacingGameManager.instance = null;
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private RacingCinderBlock BuildCinderBlock(
        out Rigidbody rigidBody, float movementSpeed, float maxSpeed = 999f, float acceleration = 0f)
    {
        GameObject go = Spawn("cinderblock-locomotion-test-actor");
        rigidBody = go.AddComponent<Rigidbody>();
        RacingCinderBlock cinderBlock = go.AddComponent<RacingCinderBlock>();

        SetPrivateField(cinderBlock, "rigidbody", rigidBody);
        SetPrivateField(cinderBlock, "movementSpeed", movementSpeed);
        SetPrivateField(cinderBlock, "maxSpeed", maxSpeed);
        SetPrivateField(cinderBlock, "acceleration", acceleration);

        return cinderBlock;
    }

    /// <summary>
    /// Composes a bare <c>RacingGameManager</c>/<c>RacingVehicleController</c>/<c>RacingVehicleProfile</c>
    /// set sufficient for <c>pursuePlayer()</c>/<c>FixedUpdate()</c>'s unconditional
    /// <c>RacingGameManager.instance.Player</c>/<c>.PlayerController</c> dereferences - and, since
    /// <c>RacingCinderBlock.Start()</c> also unconditionally dereferences
    /// <c>RacingGameManager.instance.CharacterProfile.MaxSpeed</c>, this composes a profile too so tests
    /// that drive the real <c>Start()</c> do not need their own separate setup. Neither the manager's own
    /// <c>Awake()</c>/<c>Start()</c> nor the profile's own <c>Start()</c> are allowed to run - the manager
    /// GameObject stays inactive throughout composition, and <c>instance</c> is assigned directly rather
    /// than left for <c>Awake()</c> to set.
    /// </summary>
    private RacingVehicleController ComposeRacingGameManager(out GameObject player)
    {
        GameObject managerGo = new GameObject("racing-game-manager-test-actor");
        managerGo.SetActive(false);
        spawned.Add(managerGo);
        RacingGameManager manager = managerGo.AddComponent<RacingGameManager>();

        player = Spawn("racing-player-test-actor");
        RacingVehicleController playerController = player.AddComponent<RacingVehicleController>();
        RacingVehicleProfile profile = player.AddComponent<RacingVehicleProfile>();

        SetPrivateField(manager, "_player", player);
        SetPrivateField(manager, "_playerController", playerController);
        SetPrivateField(manager, "_characterProfile", profile);
        RacingGameManager.instance = manager;

        return playerController;
    }

    /// <summary>
    /// Code review finding: <c>Start()</c> used to assign the player's raw world position to
    /// <c>target</c> directly, instead of the normalized direction every other write to this field
    /// produces (see <c>pursuePlayer()</c>). That fed straight into this slice's new
    /// <c>rigidbody.linearVelocity = target * movementSpeed</c> write, so for one physics step the
    /// commanded velocity would be <c>worldPosition * movementSpeed</c> rather than
    /// <c>direction * movementSpeed</c> - an arbitrarily large, effectively undefined one-tick velocity.
    /// Fixed by having <c>Start()</c> call <c>pursuePlayer()</c> instead of assigning the raw position.
    /// </summary>
    [Test]
    public void Start_InitializesTargetAsNormalizedDirectionTowardPlayer()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(out Rigidbody rigidBody, movementSpeed: 10f);
        RacingVehicleController playerController = ComposeRacingGameManager(out GameObject player);
        playerController.FacingFront = true;
        // Far enough from the origin that a regression back to the raw-position assignment would be
        // unmistakable (a magnitude far from 1), not coincidentally close to it.
        player.transform.position = new Vector3(30f, 10f, -5f);

        InvokePrivate(cinderBlock, "Start");

        Vector3 target = (Vector3)GetPrivateField(cinderBlock, "target");
        Assert.That(target.magnitude, Is.EqualTo(1f).Within(0.0001f),
            "Start() must seed target as a normalized direction toward the player, matching every other "
            + "write to this field - not the player's raw world position.");
    }

    /// <summary>
    /// FixedUpdate writes the Rigidbody command from whatever <c>target</c> the previous tick's
    /// <c>pursuePlayer()</c> left behind, then calls <c>pursuePlayer()</c> again only afterward to
    /// recompute <c>target</c> for the following tick - a pre-existing one-tick lag this migration must
    /// not disturb. These tests set <c>target</c> directly, isolating the Rigidbody-command change from
    /// that unrelated, pre-existing recompute timing (covered separately by the
    /// <c>PursuePlayer_*</c> tests below). A distant, arrival-safe player position is still composed so
    /// the unconditional <c>KnockedDown</c> read and the trailing <c>pursuePlayer()</c> call do not
    /// null-dereference or accidentally trip the arrival transition and overwrite the assertion.
    /// </summary>
    [Test]
    public void FixedUpdate_Chasing_CommandsFullVectorVelocityEqualToTargetTimesMovementSpeed()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(out Rigidbody rigidBody, movementSpeed: 10f);
        RacingVehicleController playerController = ComposeRacingGameManager(out GameObject player);
        playerController.FacingFront = true;
        playerController.KnockedDown = false;
        player.transform.position = new Vector3(20f, 8f, 5f);
        // Offset in X, Y, and Z so a regression that silently flattened this chase onto a plane (e.g.
        // routing it through RigidbodyLocomotionMotor) would produce a provably wrong velocity.
        Vector3 target = new Vector3(1f, 2f, 2f).normalized;
        SetPrivateField(cinderBlock, "target", target);

        InvokePrivate(cinderBlock, "FixedUpdate");

        Vector3 expectedVelocity = target * 10f;
        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(expectedVelocity.x).Within(0.0001f));
        Assert.That(rigidBody.linearVelocity.y, Is.EqualTo(expectedVelocity.y).Within(0.0001f),
            "the chase must intentionally command Y, not just X/Z - it is not a planar role");
        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(expectedVelocity.z).Within(0.0001f));
    }

    [Test]
    public void FixedUpdate_Chasing_VelocityMagnitudeEqualsMovementSpeed()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(out Rigidbody rigidBody, movementSpeed: 12f);
        RacingVehicleController playerController = ComposeRacingGameManager(out GameObject player);
        playerController.FacingFront = false;
        playerController.KnockedDown = false;
        // FacingFront false means xDirection = -1 in pursuePlayer's own target calculation; kept far
        // enough on the positive side that the trailing pursuePlayer() call this same FixedUpdate makes
        // does not also trip the arrival transition and overwrite the velocity this test asserts on.
        player.transform.position = new Vector3(40f, 40f, -12f);
        SetPrivateField(cinderBlock, "target", new Vector3(-1f, 3f, -2f).normalized);

        InvokePrivate(cinderBlock, "FixedUpdate");

        Assert.That(rigidBody.linearVelocity.magnitude, Is.EqualTo(12f).Within(0.0001f),
            "the full 3D chase direction is unit-normalized before scaling by movementSpeed, so the "
            + "commanded velocity's magnitude must be exactly movementSpeed regardless of direction");
    }

    [Test]
    public void FixedUpdate_Chasing_KeepsTheMovementFieldsExistingDisplacementMeaning()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(out Rigidbody rigidBody, movementSpeed: 10f);
        RacingVehicleController playerController = ComposeRacingGameManager(out GameObject player);
        playerController.FacingFront = true;
        playerController.KnockedDown = false;
        player.transform.position = new Vector3(20f, 0f, 0f);
        Vector3 target = new Vector3(1f, 2f, 2f).normalized;
        SetPrivateField(cinderBlock, "target", target);

        InvokePrivate(cinderBlock, "FixedUpdate");

        Vector3 movement = (Vector3)GetPrivateField(cinderBlock, "movement");
        Vector3 expected = target * (10f * Time.fixedDeltaTime);
        Assert.That(movement.x, Is.EqualTo(expected.x).Within(0.0001f),
            "the movement field must keep its existing per-step displacement meaning (direction * "
            + "speed * fixedDeltaTime) - only the Rigidbody command's units changed");
        Assert.That(movement.y, Is.EqualTo(expected.y).Within(0.0001f));
        Assert.That(movement.z, Is.EqualTo(expected.z).Within(0.0001f));
    }

    [Test]
    public void FixedUpdate_BelowMaxSpeed_AcceleratesByAccelerationOverOneHundredEachTick()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(
            out Rigidbody rigidBody, movementSpeed: 3f, maxSpeed: 100f, acceleration: 5f);
        RacingVehicleController playerController = ComposeRacingGameManager(out GameObject player);
        playerController.FacingFront = true;
        playerController.KnockedDown = false;
        player.transform.position = new Vector3(20f, 0f, 0f);

        InvokePrivate(cinderBlock, "FixedUpdate");

        float movementSpeed = (float)GetPrivateField(cinderBlock, "movementSpeed");
        Assert.That(movementSpeed, Is.EqualTo(3f + (5f / 100f)).Within(0.0001f),
            "the existing acceleration rule (movementSpeed += acceleration / 100 while below maxSpeed) "
            + "must be unchanged by this migration");
    }

    [Test]
    public void FixedUpdate_PlayerKnockedDown_StopsFullChaseVelocityAndPreservesNoResidualAxis()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(out Rigidbody rigidBody, movementSpeed: 10f);
        RacingVehicleController playerController = ComposeRacingGameManager(out GameObject player);
        playerController.FacingFront = true;
        playerController.KnockedDown = true;
        rigidBody.linearVelocity = new Vector3(5f, -3f, 4f);

        InvokePrivate(cinderBlock, "FixedUpdate");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity, Is.EqualTo(Vector3.zero),
            "player-knockdown suppression must release the full commanded chase velocity - X/Y/Z, not "
            + "just X/Z - because this chase intentionally owns all three axes, unlike a planar role "
            + "where Y is left for gravity/impulses to own");
    }

    [Test]
    public void PursuePlayer_ArrivalCondition_TransitionsToTargetReachedAndStopsChaseVelocity()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(out Rigidbody rigidBody, movementSpeed: 10f);
        RacingVehicleController playerController = ComposeRacingGameManager(out GameObject player);
        playerController.FacingFront = true;
        // newVector.x - transform.position.x < 1 with the actor at the origin and xDirection = 1:
        // (-0.5 + 1) - 0 = 0.5 < 1.
        player.transform.position = new Vector3(-0.5f, 0f, 0f);
        rigidBody.linearVelocity = new Vector3(5f, -3f, 4f);

        cinderBlock.pursuePlayer();

        Assert.That((bool)GetPrivateField(cinderBlock, "targetReached"), Is.True,
            "reaching the arrival threshold must set targetReached");
        Assert.That(rigidBody.linearVelocity, Is.EqualTo(Vector3.zero),
            "the transition into the impact phase must clear the full chase velocity, not just X/Z");
    }

    [Test]
    public void OnTriggerEnter_PlayerCollisionOnObstacle_TransitionsToTargetReachedAndStopsChaseVelocity()
    {
        GameObject go = Spawn("cinderblock-collision-test-actor");
        go.tag = "obstacle";
        Rigidbody rigidBody = go.AddComponent<Rigidbody>();
        RacingCinderBlock cinderBlock = go.AddComponent<RacingCinderBlock>();
        SetPrivateField(cinderBlock, "rigidbody", rigidBody);
        rigidBody.linearVelocity = new Vector3(5f, -3f, 4f);

        GameObject playerGo = Spawn("player-collider-test-actor");
        playerGo.tag = "Player";
        Collider playerCollider = playerGo.AddComponent<BoxCollider>();

        InvokePrivate(cinderBlock, "OnTriggerEnter", playerCollider);

        Assert.That((bool)GetPrivateField(cinderBlock, "targetReached"), Is.True,
            "a player collision on an obstacle-tagged cinder block must transition to targetReached");
        Assert.That(rigidBody.linearVelocity, Is.EqualTo(Vector3.zero),
            "the player-collision transition path must also clear the full chase velocity exactly once, "
            + "through the same authoritative transition pursuePlayer()'s arrival check uses");
    }

    /// <summary>
    /// Code review requirement: once already in the impact phase, repeated processing must not
    /// repeatedly clear the Rigidbody velocity - the accumulating impact force
    /// (<c>rigidbody.AddForce(..., ForceMode.VelocityChange)</c>) must be free to build across ticks.
    /// Drives the real FixedUpdate twice with isLocked already true (so the AddForce branch itself does
    /// not fire and reintroduce a confound) and asserts a velocity set between the two calls survives.
    /// </summary>
    [Test]
    public void FixedUpdate_AlreadyTargetReached_DoesNotRepeatedlyClearAccumulatedImpactVelocity()
    {
        RacingCinderBlock cinderBlock = BuildCinderBlock(out Rigidbody rigidBody, movementSpeed: 10f);
        SetPrivateField(cinderBlock, "targetReached", true);
        SetPrivateField(cinderBlock, "isLocked", true);

        InvokePrivate(cinderBlock, "FixedUpdate");
        rigidBody.linearVelocity = new Vector3(1f, -2f, 3f);
        InvokePrivate(cinderBlock, "FixedUpdate");

        Assert.That(rigidBody.linearVelocity, Is.EqualTo(new Vector3(1f, -2f, 3f)),
            "FixedUpdate must not clear velocity every frame while already in the impact phase - only "
            + "the one-time transition into it does that");
    }

    [Test]
    public void AuthoredCinderBlockPrefabRigidbodyRemainsDynamic()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/racing/cinderblock");
        Assert.IsNotNull(prefab, "expected Assets/Resources/Prefabs/racing/cinderblock.prefab to load");

        Rigidbody rigidBody = prefab.GetComponent<Rigidbody>();
        Assert.IsNotNull(rigidBody, "the authored cinder block prefab must carry a Rigidbody");
        Assert.That(rigidBody.isKinematic, Is.False,
            "AUD-012 Phase 4 Slice 75 migrates RacingCinderBlock on the assumption its authored "
            + "Rigidbody is dynamic (non-kinematic) - if prefab authoring ever makes it kinematic, this "
            + "full-vector velocity command needs re-auditing, the same way a future dynamic bodyguard "
            + "Rigidbody would force a re-audit of BodyGuardController's MovePosition exception");
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

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"{target.GetType().Name} must declare {methodName}()");
        method.Invoke(target, args);
    }
}
