#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 4 Slice 73: <c>AutoPlayerDefense</c>'s ordinary locomotion moved from
/// <c>Rigidbody.MovePosition</c> to <see cref="RigidbodyLocomotionMotor"/>. <c>Level5AutoPlayerDefense
/// LocomotionTests</c> (EditMode) proves the velocity command and every release path as pure state; this
/// file lets real Play Mode physics actually integrate those commands over several fixed steps, mirroring
/// what <c>AutoPlayerLocomotionPlayModeTests</c> does for the CPU-shooter role.
///
/// Unlike that sibling controller, <c>AutoPlayerDefense.Start()</c> does not return early after its own
/// composition guard fires (<c>ResolveGuardedPlayerIfMissing</c> logs and sets <c>enabled = false</c>, but
/// execution falls through the rest of <c>Start()</c> regardless) - it still runs
/// <c>rigidBody = GetComponent&lt;Rigidbody&gt;()</c> and <c>movementSpeed = speed</c> unconditionally, so
/// this fixture's own field overrides are applied after <c>yield return null</c> lets that automatic
/// <c>Start()</c> finish, not before it the way <c>AutoPlayerLocomotionPlayModeTests</c> can. Disabling the
/// component does not block explicit method calls; only Unity's automatic per-frame
/// <c>Update</c>/<c>FixedUpdate</c> dispatch is skipped, which is what these tests want - they drive
/// <c>moveToPosition</c> directly, the same real entry point production's own <c>FixedUpdate</c> calls.
/// </summary>
public class AutoPlayerDefenseLocomotionPlayModeTests
{
    private GameObject actorGo;
    private Rigidbody rigidBody;
    private AutoPlayerDefense defender;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        LogAssert.Expect(LogType.Error, new Regex("could not resolve a player to guard"));
        LogAssert.Expect(LogType.Error, new Regex("found no 'drop_shadow' child"));

        actorGo = new GameObject("defender-locomotion-playmode-actor");
        rigidBody = actorGo.AddComponent<Rigidbody>();
        rigidBody.useGravity = false;
        defender = actorGo.AddComponent<AutoPlayerDefense>();

        // Let the automatic Start() run and disable the component first - it unconditionally
        // overwrites `rigidBody`/`movementSpeed` from its own uncomposed fields, so this fixture's real
        // values have to be applied afterwards, not before.
        yield return null;

        RealScenePlayModeTestSupport.SetField(defender, "rigidBody", rigidBody);
        // Deliberately slow relative to the 0.05 arrival threshold, matching
        // AutoPlayerLocomotionPlayModeTests' own reasoning: at the default 0.02s fixed step this moves
        // ~0.04 units/step, smaller than the threshold window, so the approach loop below is guaranteed
        // to land inside it rather than risk stepping clean over a window narrower than one frame's travel.
        defender.movementSpeed = 2f;
    }

    [TearDown]
    public void TearDown()
    {
        if (actorGo != null)
        {
            Object.Destroy(actorGo);
        }
    }

    [UnityTest]
    public IEnumerator ContinuousTracking_RealPhysicsMovesTowardTargetWithoutOvershootThenStopsWhenReleased()
    {
        Vector3 target = actorGo.transform.position + new Vector3(2f, 0f, 0f);

        // This defender has no arrival gate - production's own FixedUpdate calls moveToPosition every
        // tick it is not suppressed, tracking a continuously moving guard point. Drive it the same way.
        float navigateDeadline = Time.realtimeSinceStartup + 3f;
        while (Vector3.Distance(actorGo.transform.position, target) > 0.05f
            && Time.realtimeSinceStartup < navigateDeadline)
        {
            defender.moveToPosition(target);
            yield return new WaitForFixedUpdate();
        }

        Assert.That(Vector3.Distance(actorGo.transform.position, target), Is.LessThanOrEqualTo(0.05f),
            "the defender never reached its own arrival threshold - velocity-driven tracking did not "
            + "actually move it, or moved it the wrong amount");

        // Keep tracking a few more ticks at essentially zero remaining distance, the way production's
        // continuous FixedUpdate call does once the guard point is reached - the near-target clamp must
        // keep this from overshooting past the target and oscillating.
        for (int i = 0; i < 5; i++)
        {
            defender.moveToPosition(target);
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(actorGo.transform.position, target), Is.LessThanOrEqualTo(0.05f),
                "the near-target clamp must keep every subsequent tick within the arrival threshold");
        }

        Vector3 positionAtRelease = actorGo.transform.position;

        // Stop calling moveToPosition entirely, the way production does once suppressed by crossover/
        // knockdown/disintegration - moveToPosition's own zero-distance release already fired on the
        // loop's last iterations, so no residual planar velocity should remain to keep sliding the body.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        float driftAfterRelease = Vector3.Distance(actorGo.transform.position, positionAtRelease);
        Assert.That(driftAfterRelease, Is.LessThan(0.01f),
            "the defender kept sliding once locomotion stopped being called - the old MovePosition path "
            + "stopped producing displacement the instant navigation stopped calling it, so velocity-driven "
            + "tracking must explicitly release its last command when locomotion stops");
    }

    [UnityTest]
    public IEnumerator MoveToPosition_DoesNotFlattenAConcurrentYVelocity()
    {
        // simulate a contest jump already in flight, the way AutoPlayerJump would leave it, before an
        // ordinary tracking tick runs.
        rigidBody.useGravity = true;
        rigidBody.linearVelocity = new Vector3(0f, 5f, 0f);

        defender.moveToPosition(actorGo.transform.position + new Vector3(2f, 0f, 0f));
        yield return new WaitForFixedUpdate();

        Assert.That(rigidBody.linearVelocity.y, Is.Not.EqualTo(0f),
            "ordinary planar tracking must not zero out a concurrent vertical velocity - a contest jump "
            + "would stop rising/falling the instant the defender also had a guard point to track");
    }
}
#endif
