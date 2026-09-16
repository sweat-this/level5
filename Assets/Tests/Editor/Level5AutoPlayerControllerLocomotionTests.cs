using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 4 Slice 72: <c>AutoPlayerController</c>'s ordinary navigation moved from
/// <c>Rigidbody.MovePosition</c> to <see cref="RigidbodyLocomotionMotor"/>. These tests drive the real
/// public <c>moveToPosition</c> entry point and the real (extracted, private) arrival transition against
/// a minimally-composed controller, <c>Start()</c> never invoked, rather than the full scene/match-runtime
/// composition <c>Start()</c> requires, which this slice's locomotion change does not touch. Composition
/// varies by test: <see cref="BuildController"/> sets only <c>rigidBody</c>/<c>movementSpeed</c> for the
/// pure velocity/arrival tests; the two tests driving the real <c>FixedUpdate()</c> additionally compose
/// an <c>Animator</c>/<c>PlayerIdentifier</c> (and, for the jump test, <c>CharacterProfile</c>/
/// <c>ShotMeter</c>) - the minimum <c>FixedUpdate</c> needs to reach the branch under test without
/// composing the unrelated shoot-cycle dependencies <c>Start()</c> would otherwise wire up. Mirrors the
/// reflection-driven, no-Start() shape <c>Level5PlayerDunkSeamTests</c> and
/// <c>Level5BasketBallCriticalSuccessPresentationTests</c> already use for this controller's siblings.
/// </summary>
public class Level5AutoPlayerControllerLocomotionTests
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

    private AutoPlayerController BuildController(out Rigidbody rigidBody, float movementSpeed)
    {
        GameObject go = Spawn("cpu-locomotion-test-actor");
        rigidBody = go.AddComponent<Rigidbody>();
        AutoPlayerController controller = go.AddComponent<AutoPlayerController>();

        SetPrivateField(controller, "rigidBody", rigidBody);
        SetPrivateField(controller, "movementSpeed", movementSpeed);

        return controller;
    }

    [Test]
    public void MoveToPosition_CommandsPlanarVelocityScaledByMovementSpeedAlone()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);

        controller.moveToPosition(new Vector3(10f, 0f, 0f));

        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(6f).Within(0.0001f),
            "the commanded velocity must be direction * movementSpeed, in world units per second - "
            + "not scaled again by deltaTime/fixedDeltaTime on top of the physics step");
        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    /// <summary>
    /// Code review finding on this slice: normalizing the full 3D direction (including any Y delta)
    /// before scaling by <c>movementSpeed</c> would silently throttle horizontal speed below
    /// <c>movementSpeed</c> whenever the actor and target differ in Y - harmless under the old
    /// <c>MovePosition</c> path (the discarded Y portion was still physically applied there) but a real
    /// speed loss once only X/Z ever reaches the Rigidbody. The target here is offset in both X and Y so
    /// a 3D-normalized-then-truncated direction would produce a diluted (and provably wrong) horizontal
    /// speed if the flattening fix regressed.
    /// </summary>
    [Test]
    public void MoveToPosition_TargetWithYDelta_StillCommandsFullMovementSpeedHorizontally()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);

        controller.moveToPosition(new Vector3(10f, 5f, 0f));

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.EqualTo(6f).Within(0.0001f),
            "horizontal speed must be exactly movementSpeed regardless of the target's Y delta - only "
            + "X/Z ever reaches the Rigidbody now, so a 3D-normalized direction would silently throttle "
            + "it below movementSpeed");
    }

    [Test]
    public void MoveToPosition_PreservesExistingYVelocity()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);
        rigidBody.linearVelocity = new Vector3(0f, 8f, 0f);

        controller.moveToPosition(new Vector3(0f, 0f, 10f));

        Assert.That(rigidBody.linearVelocity.y, Is.EqualTo(8f),
            "ordinary navigation must never touch Y - gravity/jump own it");
    }

    [Test]
    public void MoveToPosition_KeepsTheMovementFieldsExistingDisplacementMeaning()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);
        Time.timeScale = 1f;

        controller.moveToPosition(new Vector3(10f, 0f, 0f));

        Vector3 movement = (Vector3)GetPrivateField(controller, "movement");
        Vector3 expected = new Vector3(1f, 0f, 0f) * (6f * Time.deltaTime);
        Assert.That(movement.x, Is.EqualTo(expected.x).Within(0.0001f),
            "the animation-facing `movement` field must keep its existing per-step displacement "
            + "meaning (direction * speed * Time.deltaTime) - only the Rigidbody command changed units");
    }

    [Test]
    public void ArrivalTransition_ClearsPlanarVelocityAndPreservesY()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);

        InvokePrivate(controller, "ApplyArrivalTransition");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f), "arrival must release the last commanded X velocity");
        Assert.That(velocity.z, Is.EqualTo(0f), "arrival must release the last commanded Z velocity");
        Assert.That(velocity.y, Is.EqualTo(-2f), "arrival must not touch Y - that's gravity's own state");
        Assert.That(controller.arrivedAtTarget, Is.True);
        Assert.That(controller.stateWalk, Is.False);
        Assert.That(controller.stateIdle, Is.True);
    }

    /// <summary>
    /// Code review finding on this slice: <c>FixedUpdate</c> has its own arrival-detection branch,
    /// independent of <c>Update</c>'s (<c>Grounded</c> can differ between the two calls in the same
    /// frame pair). Before the fix it set <c>arrivedAtTarget = true</c> directly, which was harmless
    /// under the old <c>MovePosition</c> path (no persistent velocity to leak) but - now that
    /// <c>moveToPosition</c> commands a persistent Rigidbody velocity - left the CPU sliding forever at
    /// its last commanded speed, since <c>arrivedAtTarget</c> already true also blocks <c>Update</c>'s
    /// own arrival check from ever running to correct it. This drives the real <c>FixedUpdate()</c>
    /// through exactly that branch. <c>currentState</c> is set to a sentinel distinct from every
    /// Animator-state-hash field (all default to 0 with <c>Start()</c> never invoked) so the method's
    /// unrelated guard clauses evaluate the same way they would with real hashes; <c>hasBasketball</c>
    /// is set true purely to short-circuit the unrelated "call ball to player" block before it
    /// dereferences the basketball/call-ball fields this test does not compose.
    /// </summary>
    [Test]
    public void FixedUpdate_ArrivalBranch_AlsoClearsResidualPlanarVelocity()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);
        GameObject go = controller.gameObject;
        Animator anim = go.AddComponent<Animator>();
        PlayerIdentifier identifier = go.AddComponent<PlayerIdentifier>();

        SetPrivateField(controller, "anim", anim);
        SetPrivateField(controller, "playerIdentifier", identifier);
        SetPrivateField(controller, "distanceToTarget", 0.02f);
        controller.currentState = 42;
        controller.hasBasketball = true;
        controller.Grounded = true;
        controller.InAir = false;
        controller.arrivedAtTarget = false;
        rigidBody.linearVelocity = new Vector3(5f, -3f, 5f);

        InvokePrivate(controller, "FixedUpdate");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "FixedUpdate's own arrival branch must release the last commanded X velocity too");
        Assert.That(velocity.z, Is.EqualTo(0f),
            "FixedUpdate's own arrival branch must release the last commanded Z velocity too");
        Assert.That(velocity.y, Is.EqualTo(-3f), "arrival must not touch Y");
        Assert.That(controller.arrivedAtTarget, Is.True);
        Assert.That(controller.stateWalk, Is.False);
        Assert.That(controller.stateIdle, Is.True);
    }

    /// <summary>
    /// Code review finding on this slice: <c>AutoPlayerJump()</c> used to overwrite the whole
    /// <c>linearVelocity</c> vector (<c>Vector3.up * jumpForce</c>), which only avoided discarding a
    /// same-tick navigation command because of <c>FixedUpdate</c>'s call order - an ordering dependency
    /// rather than a write that is correct regardless of order. It now writes only <c>velocity.y</c>,
    /// matching <see cref="RigidbodyLocomotionMotor"/>'s own "preserve the axis you don't own" contract.
    /// Drives the real (private) <c>AutoPlayerJump()</c> directly, independent of ordering or
    /// <c>FixedUpdate</c>'s other branches.
    /// </summary>
    [Test]
    public void AutoPlayerJump_PreservesExistingPlanarVelocity()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);
        CharacterProfile characterProfile = controller.gameObject.AddComponent<CharacterProfile>();
        ShotMeter shotMeter = controller.gameObject.AddComponent<ShotMeter>();
        characterProfile.JumpForce = 12f;
        SetPrivateField(controller, "characterProfile", characterProfile);
        controller.Shotmeter = shotMeter;
        controller.currentState = 42;
        rigidBody.linearVelocity = new Vector3(3f, 0f, -4f);

        InvokePrivate(controller, "AutoPlayerJump");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.y, Is.EqualTo(12f), "the jump's vertical velocity must land");
        Assert.That(velocity.x, Is.EqualTo(3f), "the jump must not discard existing X velocity");
        Assert.That(velocity.z, Is.EqualTo(-4f), "the jump must not discard existing Z velocity");
    }

    /// <summary>
    /// Code review finding on this slice: <c>FixedUpdate</c> used to call <c>AutoPlayerJump()</c>
    /// (triggered by <c>jumpTrigger</c>) *after* the navigation block, and <c>AutoPlayerJump()</c> did a
    /// full-vector <c>rigidBody.linearVelocity = Vector3.up * jumpForce</c> write, which would have
    /// silently discarded whatever X/Z <c>moveToPosition</c> had just commanded earlier in the same tick.
    /// Fixed two ways: <c>AutoPlayerJump()</c> now writes only <c>velocity.y</c>, matching
    /// <c>RigidbodyLocomotionMotor</c>'s own "preserve the axis you don't own" contract (see
    /// <see cref="AutoPlayerJump_PreservesExistingPlanarVelocity"/> for that in isolation), so the two
    /// writes commute regardless of order; the jump check was also kept ahead of the navigation block,
    /// matching <c>PlayerController</c>'s own ordering (<c>PlayerJump()</c> before
    /// <c>ApplyHorizontalMovement()</c>), for readability rather than as a correctness requirement now.
    /// This drives the real <c>FixedUpdate()</c> with both a pending jump and an unmet navigation target
    /// in the same tick and proves both survive.
    /// </summary>
    [Test]
    public void FixedUpdate_JumpAndNavigationInTheSameTick_BothSurviveInCorrectOrder()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);
        GameObject go = controller.gameObject;
        Animator anim = go.AddComponent<Animator>();
        PlayerIdentifier identifier = go.AddComponent<PlayerIdentifier>();
        CharacterProfile characterProfile = go.AddComponent<CharacterProfile>();
        ShotMeter shotMeter = go.AddComponent<ShotMeter>();
        characterProfile.JumpForce = 9f;

        SetPrivateField(controller, "anim", anim);
        SetPrivateField(controller, "playerIdentifier", identifier);
        SetPrivateField(controller, "characterProfile", characterProfile);
        SetPrivateField(controller, "targetPosition", new Vector3(10f, 0f, 0f));
        SetPrivateField(controller, "distanceToTarget", 10f);
        SetPrivateField(controller, "jumpTrigger", true);
        controller.Shotmeter = shotMeter;
        controller.currentState = 42;
        controller.hasBasketball = true;
        controller.Grounded = true;
        controller.InAir = false;
        controller.arrivedAtTarget = false;
        rigidBody.linearVelocity = Vector3.zero;

        InvokePrivate(controller, "FixedUpdate");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.y, Is.EqualTo(9f), "the same-tick jump's vertical velocity must land");
        Assert.That(velocity.x, Is.EqualTo(6f).Within(0.0001f),
            "the same-tick navigation command must not be discarded by the jump's full-vector write");
        Assert.That(velocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void AfterArrivalTransition_ANewMoveToPositionCallResumesNavigation()
    {
        AutoPlayerController controller = BuildController(out Rigidbody rigidBody, movementSpeed: 6f);
        rigidBody.linearVelocity = new Vector3(5f, 0f, 5f);
        InvokePrivate(controller, "ApplyArrivalTransition");
        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(0f),
            "sanity: arrival must have actually cleared planar velocity before this test proves recovery");
        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(0f));

        controller.moveToPosition(new Vector3(0f, 0f, 10f));

        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(6f).Within(0.0001f),
            "the CPU must be able to resume navigation once a new target is selected after arrival - "
            + "the arrival stop must not be a permanent lock on planar velocity");
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

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"{target.GetType().Name} must declare {methodName}()");
        method.Invoke(target, null);
    }
}
