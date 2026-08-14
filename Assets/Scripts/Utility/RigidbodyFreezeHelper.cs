using UnityEngine;

/// <summary>
/// The exact freeze/unfreeze constraint pair was duplicated identically across
/// <c>PlayerController</c>, <c>AutoPlayerController</c>, and <c>RacingVehicleController</c> - each
/// with its own <c>FreezePlayerPosition</c>/<c>UnFreezePlayerPosition</c> pair of public methods
/// calling this. Kept as a plain static helper taking the Rigidbody as a parameter rather than a
/// shared base class, since these three controllers are otherwise unrelated and a base class would
/// be a much larger structural change than this duplication justifies.
/// </summary>
public static class RigidbodyFreezeHelper
{
    public static void FreezePosition(Rigidbody rigidBody)
    {
        rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
            | RigidbodyConstraints.FreezeRotationY
            | RigidbodyConstraints.FreezeRotationZ
            | RigidbodyConstraints.FreezePositionX
            | RigidbodyConstraints.FreezePositionY
            | RigidbodyConstraints.FreezePositionZ;
    }

    public static void UnfreezeRotationOnly(Rigidbody rigidBody)
    {
        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }
}
