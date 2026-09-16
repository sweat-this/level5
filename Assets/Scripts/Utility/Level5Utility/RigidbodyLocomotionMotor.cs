using UnityEngine;

/// <summary>
/// AUD-012 Phase 4 Slice 72: the one planar Rigidbody locomotion write <c>PlayerController</c> and
/// <c>AutoPlayerController</c> both need for ordinary navigation - apply a requested world-space X/Z
/// velocity while leaving whatever Y velocity gravity, a jump, or another explicit impulse already
/// owns untouched. Kept as a plain static helper taking the Rigidbody as a parameter, matching
/// <see cref="RigidbodyFreezeHelper"/>'s shape, since these controllers are otherwise unrelated and a
/// base class would be a much larger structural change than this shared write justifies.
///
/// Deliberately knows nothing about actors, input, targets, arrival, grounding, animation, combat,
/// shooting, AI state, or movement speeds - callers own all of that policy and pass in an
/// already-scaled world-units-per-second velocity, not a per-step displacement.
/// </summary>
public static class RigidbodyLocomotionMotor
{
    public static void SetPlanarVelocity(Rigidbody rigidBody, float xVelocity, float zVelocity)
    {
        Vector3 velocity = rigidBody.linearVelocity;
        velocity.x = xVelocity;
        velocity.z = zVelocity;
        rigidBody.linearVelocity = velocity;
    }
}
