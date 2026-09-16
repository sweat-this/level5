#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 4 Slice 72: <c>AutoPlayerController</c>'s ordinary navigation moved from
/// <c>Rigidbody.MovePosition</c> to <see cref="RigidbodyLocomotionMotor"/>. <c>Level5AutoPlayerController
/// LocomotionTests</c> (EditMode) proves the velocity command and the arrival transition as pure state,
/// including that a new <c>moveToPosition</c> call resumes navigation after arrival; this file lets real
/// Play Mode physics actually integrate those commands over several fixed steps - the same "does the
/// commanded velocity really move the body, and does clearing it really stop it" question
/// <c>PlayerMovementPhysicsTests</c> answers for the human path - without re-proving the pure-state facts
/// the EditMode fixture already covers deterministically and faster.
///
/// The controller is hand-composed with only a <see cref="Rigidbody"/> - <c>rigidBody</c>/
/// <c>movementSpeed</c> set via <see cref="RealScenePlayModeTestSupport.SetField"/> - because full
/// composition (<c>PlayerIdentifier</c>, <c>CharacterProfile</c>, <c>CallBallToPlayer</c>, bound
/// match-runtime, ...) belongs to the CPU shoot-cycle, which this slice's locomotion change does not
/// touch. Unlike an EditMode fixture, real Play Mode calls <c>Start()</c> on this newly-added component
/// automatically; with no bound <c>IPlayerMatchRuntime</c> it fails its existing early guard (logs once,
/// disables itself) before reaching the line that would otherwise overwrite this test's own
/// <c>rigidBody</c> assignment - expected and asserted below, not worked around. Driving
/// <c>moveToPosition</c>/the arrival transition directly afterwards is still the real controller path -
/// the same entry points production's own <c>FixedUpdate</c>/<c>Update</c> call - just without
/// redundantly re-proving unrelated, unchanged shoot-cycle composition. Disabling the component does not
/// block explicit method calls; only Unity's automatic per-frame <c>Update</c>/<c>FixedUpdate</c> dispatch
/// is skipped, which is exactly what these tests want - they drive the two methods under test themselves.
/// </summary>
public class AutoPlayerLocomotionPlayModeTests
{
    private GameObject actorGo;
    private Rigidbody rigidBody;
    private AutoPlayerController controller;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        LogAssert.Expect(LogType.Error, new Regex("no bound IPlayerMatchRuntime"));

        actorGo = new GameObject("cpu-locomotion-playmode-actor");
        rigidBody = actorGo.AddComponent<Rigidbody>();
        rigidBody.useGravity = false;
        controller = actorGo.AddComponent<AutoPlayerController>();

        RealScenePlayModeTestSupport.SetField(controller, "rigidBody", rigidBody);
        // Deliberately slow relative to the 0.05 arrival threshold: at the default 0.02s fixed step
        // this moves ~0.04 units/step, smaller than the threshold window, so the approach loop below
        // is guaranteed to land inside it rather than risk stepping clean over a window narrower than
        // one frame's travel.
        RealScenePlayModeTestSupport.SetField(controller, "movementSpeed", 2f);

        yield return null;
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
    public IEnumerator NavigateThenArrive_RealPhysicsMovesTowardTargetThenStopsAtArrival()
    {
        Vector3 target = actorGo.transform.position + new Vector3(2f, 0f, 0f);

        // navigate: real FixedUpdate ticks integrate the commanded velocity into actual displacement,
        // exactly like production's own moveToPosition -> PhysX step.
        float navigateDeadline = Time.realtimeSinceStartup + 3f;
        while (Vector3.Distance(actorGo.transform.position, target) > 0.05f
            && Time.realtimeSinceStartup < navigateDeadline)
        {
            controller.moveToPosition(target);
            yield return new WaitForFixedUpdate();
        }

        Assert.That(Vector3.Distance(actorGo.transform.position, target), Is.LessThanOrEqualTo(0.05f),
            "the CPU never reached its own arrival threshold - velocity-driven navigation did not "
            + "actually move it, or moved it the wrong amount");

        RealScenePlayModeTestSupport.Invoke(controller, "ApplyArrivalTransition");
        Vector3 positionAtArrival = actorGo.transform.position;

        // let physics keep running with no further moveToPosition calls, the way production leaves the
        // body alone once arrivedAtTarget gates FixedUpdate's call to moveToPosition.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        float driftAfterArrival = Vector3.Distance(actorGo.transform.position, positionAtArrival);
        Assert.That(driftAfterArrival, Is.LessThan(0.01f),
            "the CPU kept sliding after arrival - the old MovePosition path stopped producing "
            + "displacement the instant navigation stopped calling it, so velocity-driven navigation "
            + "must explicitly release its last command on arrival");
    }

    [UnityTest]
    public IEnumerator LocomotionUpdate_DoesNotFlattenAConcurrentYVelocity()
    {
        // simulate a jump/knockback already in flight, the way AutoPlayerJump or a hit reaction would
        // leave it, before an ordinary navigation frame runs.
        rigidBody.useGravity = true;
        rigidBody.linearVelocity = new Vector3(0f, 5f, 0f);

        controller.moveToPosition(actorGo.transform.position + new Vector3(2f, 0f, 0f));
        yield return new WaitForFixedUpdate();

        Assert.That(rigidBody.linearVelocity.y, Is.Not.EqualTo(0f),
            "ordinary planar navigation must not zero out a concurrent vertical velocity - jumps and "
            + "knockbacks would stop rising/falling the instant the CPU also had a target to walk to");
    }
}
#endif
