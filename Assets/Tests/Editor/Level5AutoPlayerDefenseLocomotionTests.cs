using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 4 Slice 73: <c>AutoPlayerDefense</c>'s ordinary locomotion moved from
/// <c>Rigidbody.MovePosition</c> to <see cref="RigidbodyLocomotionMotor"/>, mirroring
/// <c>AutoPlayerController</c>'s Slice 72 migration (<c>Level5AutoPlayerControllerLocomotionTests</c>).
/// Unlike that controller, this defender's <c>moveToPosition</c> and <c>movementSpeed</c> are already
/// public, so most tests here call the real methods directly rather than through reflection; reflection
/// is only used for the private <c>rigidBody</c>/<c>movement</c>/<c>jumpForce</c> fields and the private
/// <c>FixedUpdate</c>/<c>AutoPlayerJump</c> methods. <c>Start()</c> is never invoked - full composition
/// (animator, guarded-player identity, arena context, participant registry) belongs to the guarding
/// behavior this slice's locomotion change does not touch, the same scoping
/// <c>Level5AutoPlayerControllerLocomotionTests</c> already established for its sibling.
///
/// This defender has no arrival-state gate - <c>FixedUpdate</c> calls <c>moveToPosition</c> every tick
/// it is not suppressed by crossover/knockdown/disintegration, tracking a continuously moving guard
/// point rather than stopping at a discrete destination. That is why <c>moveToPosition</c> itself (not
/// a separate arrival transition, which this class has none of) has to release residual velocity on
/// reaching its target, and why <c>FixedUpdate</c>'s three suppression gates each need the same release.
/// </summary>
public class Level5AutoPlayerDefenseLocomotionTests
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

    private AutoPlayerDefense BuildDefender(out Rigidbody rigidBody, float movementSpeed)
    {
        GameObject go = Spawn("defender-locomotion-test-actor");
        rigidBody = go.AddComponent<Rigidbody>();
        AutoPlayerDefense defender = go.AddComponent<AutoPlayerDefense>();

        SetPrivateField(defender, "rigidBody", rigidBody);
        defender.movementSpeed = movementSpeed;

        return defender;
    }

    [Test]
    public void MoveToPosition_DistantTarget_CommandsPlanarVelocityEqualToMovementSpeed()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);

        defender.moveToPosition(new Vector3(10f, 0f, 0f));

        Assert.That(rigidBody.linearVelocity.x, Is.EqualTo(10f).Within(0.0001f),
            "a guard point far beyond one physics step's travel must command exactly movementSpeed, in "
            + "world units per second - not a per-step displacement scaled again by fixedDeltaTime");
        Assert.That(rigidBody.linearVelocity.z, Is.EqualTo(0f).Within(0.0001f));
    }

    /// <summary>
    /// The old <c>MovePosition</c> path used the full 3D <c>(target - position)</c> direction, so
    /// <c>target</c>'s Y component - LerpByDistance's blend between the guarded player's height and the
    /// rim's - was still physically written to Y every step. Under velocity-only navigation only X/Z
    /// ever reaches the Rigidbody, so a 3D-normalized direction would silently throttle horizontal speed
    /// below <c>movementSpeed</c> whenever the guard point sits above or below the defender. The target
    /// here is offset in both X and Y so a regression (normalize-then-truncate instead of flatten-then-
    /// normalize) would produce a provably diluted horizontal speed.
    /// </summary>
    [Test]
    public void MoveToPosition_TargetWithYDelta_StillCommandsFullMovementSpeedHorizontally()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);

        defender.moveToPosition(new Vector3(10f, 5f, 0f));

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.EqualTo(10f).Within(0.0001f),
            "horizontal speed must be exactly movementSpeed regardless of the guard point's Y "
            + "component - only X/Z ever reaches the Rigidbody now");
    }

    [Test]
    public void MoveToPosition_PreservesExistingYVelocity()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);
        rigidBody.linearVelocity = new Vector3(0f, 8f, 0f);

        defender.moveToPosition(new Vector3(0f, 0f, 10f));

        Assert.That(rigidBody.linearVelocity.y, Is.EqualTo(8f),
            "ordinary guarding locomotion must never touch Y - gravity/jump own it");
    }

    [Test]
    public void MoveToPosition_TargetCloserThanOneStep_ClampsWithoutOvershoot()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);
        // Deliberately a quarter of one full physics step's travel, computed from the live
        // fixedDeltaTime rather than an assumed constant, so this holds under any project timestep.
        float distanceRemaining = 10f * Time.fixedDeltaTime * 0.25f;

        defender.moveToPosition(new Vector3(distanceRemaining, 0f, 0f));

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(distanceRemaining / Time.fixedDeltaTime).Within(0.0001f),
            "a target closer than one full step must be clamped to the remaining distance, not the "
            + "full movementSpeed - a full-speed step would overshoot it");
        Assert.That(velocity.magnitude, Is.LessThan(10f),
            "the clamped command must stay below movementSpeed");
    }

    [Test]
    public void MoveToPosition_KeepsTheMovementFieldsExistingDisplacementMeaning()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);

        defender.moveToPosition(new Vector3(10f, 0f, 0f));

        Vector3 movement = (Vector3)GetPrivateField(defender, "movement");
        Vector3 expected = new Vector3(1f, 0f, 0f) * (10f * Time.fixedDeltaTime);
        Assert.That(movement.x, Is.EqualTo(expected.x).Within(0.0001f),
            "the `movement` field must keep its existing per-step displacement meaning (direction * "
            + "speed * fixedDeltaTime) - only the Rigidbody command's units changed");
        Assert.That(movement.z, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void MoveToPosition_AtTarget_ReleasesPlanarVelocityAndPreservesY()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);
        rigidBody.linearVelocity = new Vector3(5f, -2f, 4f);

        defender.moveToPosition(rigidBody.transform.position);

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "reaching the guard point - this defender tracks continuously, with no arrival gate - must "
            + "release the last commanded X velocity, or it would keep sliding while sitting on target");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-2f), "reaching the target must not touch Y");
    }

    /// <summary>
    /// Composes the private guarding-position inputs (<c>playerPosition</c>/<c>bballRimVector</c>/
    /// <c>playerGuardingDistance</c>) so <c>moveCpuPlayer()</c> - and therefore <c>FixedUpdate</c>'s
    /// fallthrough call to <c>moveToPosition</c> when a suppression gate fails to trip - resolves to a
    /// target meaningfully away from the actor's own position (the origin), rather than the (0,0,0)
    /// every one of these fields otherwise shares with a freshly-spawned, uncomposed defender.
    ///
    /// Without this, a broken suppression gate (e.g. a removed <c>playerCrossover ||</c> clause) would
    /// fall through to <c>moveToPosition</c> with a target equal to the actor's own position, which hits
    /// that method's own zero-distance release branch and coincidentally zeroes velocity anyway -
    /// passing the test for the wrong reason and masking exactly the regression it exists to catch.
    /// Discovered via negative control: removing the <c>playerCrossover</c> clause did not fail the
    /// crossover test until this composition was added.
    /// </summary>
    private static void ComposeNonTrivialGuardTarget(AutoPlayerDefense defender)
    {
        SetPrivateField(defender, "playerPosition", Vector3.zero);
        SetPrivateField(defender, "bballRimVector", new Vector3(10f, 0f, 0f));
        SetPrivateField(defender, "playerGuardingDistance", 5f);
    }

    [Test]
    public void FixedUpdate_CrossoverSuppressed_ReleasesResidualPlanarVelocityAndPreservesY()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);
        ComposeNonTrivialGuardTarget(defender);
        rigidBody.linearVelocity = new Vector3(5f, -3f, 5f);
        // currentState/knockedDownState/disintegratedState all default to 0 with Start() never
        // invoked, so currentState == knockedDownState (0 == 0) would coincidentally also satisfy the
        // gate - giving mismatching non-zero sentinels isolates playerCrossover as the sole trigger,
        // matching how the knockdown/disintegration tests below already isolate their own branch.
        defender.knockedDownState = 1;
        defender.disintegratedState = 2;
        defender.currentState = 0;
        defender.playerCrossover = true;

        InvokePrivate(defender, "FixedUpdate");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f),
            "crossover suppression must release residual X velocity, not just skip commanding new "
            + "velocity - the last commanded velocity otherwise persists on a dynamic Rigidbody");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-3f), "crossover suppression must not touch Y");
    }

    [Test]
    public void FixedUpdate_KnockedDownSuppressed_ReleasesResidualPlanarVelocityAndPreservesY()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);
        ComposeNonTrivialGuardTarget(defender);
        rigidBody.linearVelocity = new Vector3(5f, -3f, 5f);
        defender.knockedDownState = 42;
        defender.currentState = 42;

        InvokePrivate(defender, "FixedUpdate");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f), "knockdown suppression must release residual X velocity");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-3f), "knockdown suppression must not touch Y");
    }

    [Test]
    public void FixedUpdate_DisintegratedSuppressed_ReleasesResidualPlanarVelocityAndPreservesY()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);
        ComposeNonTrivialGuardTarget(defender);
        rigidBody.linearVelocity = new Vector3(5f, -3f, 5f);
        defender.disintegratedState = 42;
        defender.currentState = 42;

        InvokePrivate(defender, "FixedUpdate");

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.x, Is.EqualTo(0f), "disintegration suppression must release residual X velocity");
        Assert.That(velocity.z, Is.EqualTo(0f));
        Assert.That(velocity.y, Is.EqualTo(-3f), "disintegration suppression must not touch Y");
    }

    /// <summary>
    /// Drives the real (private) <c>AutoPlayerJump</c> coroutine one step - far enough to execute its
    /// Y-only velocity write, not far enough to reach <c>WaitForGuardedPlayerToLand</c>'s body. Passes a
    /// null <c>PlayerIdentifier</c>: that parameter is only dereferenced inside
    /// <c>WaitForGuardedPlayerToLand</c>, which this single step never begins executing - constructing
    /// its enumerator (as the argument to the coroutine's own <c>yield return</c>) does not run its body.
    /// <c>delayPercent</c> is left at its default 0f, which deterministically skips the pre-jump-delay
    /// crossover roll (<c>UtilityFunctions.GetRandomFloat(0, 100)</c> can never return less than 0),
    /// so this step reaches the jump write unconditionally rather than depending on a random roll.
    /// </summary>
    [Test]
    public void AutoPlayerJump_PreservesExistingPlanarVelocity()
    {
        AutoPlayerDefense defender = BuildDefender(out Rigidbody rigidBody, movementSpeed: 10f);
        SetPrivateField(defender, "jumpForce", 12f);
        rigidBody.linearVelocity = new Vector3(3f, 0f, -4f);

        StepPrivateCoroutineOnce(defender, "AutoPlayerJump", new object[] { null });

        Vector3 velocity = rigidBody.linearVelocity;
        Assert.That(velocity.y, Is.EqualTo(12f), "the contest jump's vertical velocity must land");
        Assert.That(velocity.x, Is.EqualTo(3f), "the jump must not discard existing X velocity");
        Assert.That(velocity.z, Is.EqualTo(-4f), "the jump must not discard existing Z velocity");
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

    private static void StepPrivateCoroutineOnce(object target, string methodName, object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"{target.GetType().Name} must declare {methodName}()");
        IEnumerator coroutine = (IEnumerator)method.Invoke(target, args);
        coroutine.MoveNext();
    }
}
