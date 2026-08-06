using UnityEngine;

public static class PlayerTouchInputState
{
    private static bool jumpOrShootQueued;
    private static Vector2 jumpOrShootPosition;
    private static bool attackQueued;
    private static bool specialQueued;

    public static bool BlockHeld { get; set; }

    public static void QueueJumpOrShoot(Vector2 touchPosition)
    {
        jumpOrShootQueued = true;
        jumpOrShootPosition = touchPosition;
    }

    public static bool ConsumeJumpOrShoot(out Vector2 touchPosition)
    {
        touchPosition = jumpOrShootPosition;
        if (!jumpOrShootQueued)
        {
            return false;
        }

        jumpOrShootQueued = false;
        return true;
    }

    public static void QueueAttack()
    {
        attackQueued = true;
    }

    public static bool ConsumeAttack()
    {
        if (!attackQueued)
        {
            return false;
        }

        attackQueued = false;
        return true;
    }

    public static void QueueSpecial()
    {
        specialQueued = true;
    }

    public static bool ConsumeSpecial()
    {
        if (!specialQueued)
        {
            return false;
        }

        specialQueued = false;
        return true;
    }

    /// <summary>
    /// Drops every queued and held input.
    ///
    /// Call this whenever the touch controller goes away - a queued tap that outlives its scene is
    /// consumed by the next scene's freshly spawned player as a phantom input. `ResetState` below
    /// covers the same fields but only runs once per application launch, not per scene load.
    /// </summary>
    public static void Clear()
    {
        jumpOrShootQueued = false;
        jumpOrShootPosition = Vector2.zero;
        attackQueued = false;
        specialQueued = false;
        BlockHeld = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        Clear();
    }
}
