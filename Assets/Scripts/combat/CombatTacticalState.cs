/// <summary>
/// The tactical intention a combat AI actor currently holds. This is the AI's own state, kept
/// separate from animator state (which reflects what is currently playing, not what the actor
/// is trying to do) - see the architecture notes on <c>EnemyController</c> and
/// <c>BodyGuardController</c> for how the two relate.
///
/// Not every actor uses every value: enemies use <see cref="ReturnToPatrol"/>, bodyguards use
/// <see cref="FollowProtectedActor"/>, <see cref="InterceptThreat"/> and
/// <see cref="ReturnToProtectedActor"/>. The shared values in between cover both.
/// </summary>
public enum CombatTacticalState
{
    Idle,
    AcquireTarget,
    Approach,
    Engage,
    Attack,
    Recover,
    Disengage,

    // enemy-specific
    ReturnToPatrol,

    // bodyguard-specific
    FollowProtectedActor,
    InterceptThreat,
    ReturnToProtectedActor,
}

/// <summary>
/// The bookkeeping half of a tactical-state transition, shared so Enemy and Bodyguard don't each
/// carry their own copy of "only touch lastTransitionReason on an actual change". Which state
/// comes next is genuinely different per actor type (different priority orders, different
/// states in play) and stays local to each controller - only the commit step is common.
/// </summary>
public static class CombatTacticalStateTransitions
{
    public static bool TryCommit(ref CombatTacticalState current, CombatTacticalState next, out string reason)
    {
        if (next == current)
        {
            reason = null;
            return false;
        }

        reason = current + " -> " + next;
        current = next;
        return true;
    }
}
