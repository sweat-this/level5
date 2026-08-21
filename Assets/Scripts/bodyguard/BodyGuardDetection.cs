using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// #57: narrowed from an independent bodyguard threat-detection component down to authored
/// sight-distance tuning plus the pure distance-policy helpers <see cref="BodyGuardController"/>
/// now calls directly.
///
/// Before this change, this component ran its own 0.5s scan over
/// <c>PlayerAttackQueue.EnemiesQueued</c> (reservation-holding enemies only, measured from this
/// bodyguard's own position) and wrote <c>enemySighted</c>/<c>EnemySighted</c>/<c>TargetSighted</c>
/// - a second, competing answer to "is there a threat I care about" alongside
/// <c>BodyGuardController.currentThreat</c>, which is scored over every active enemy (reservation or
/// not) relative to the protected actor. <c>CheckReturnToPatrolStatus</c> then made the patrol
/// decision from this component's cruder signal instead of the controller's richer one. Both the
/// scan and the patrol decision moved to <c>BodyGuardController</c> (see
/// <c>RefreshThreatTarget</c>/<c>IsActionableThreat</c>/<c>CheckReturnToPatrolStatus</c> there) so
/// there is a single owner. #57 Section 8: no production path ever grants a bodyguard a
/// PlayerAttackQueue reservation (only <c>EnemyDetection</c> calls
/// <c>TryAddToQueue</c>/<c>TryReserve</c>), so this component never implements
/// <c>ICombatReservationState</c> and carries no reservation fields.
///
/// This component still exists, per #57's guidance, as the narrow serialized home for the authored
/// sight-distance tuning value so no prefab data is lost, and as the home for the pure distance
/// helpers introduced/kept for BG-2/#57 - <see cref="AnyEnemyWithinSight"/> has no production caller
/// left after this change but is kept as a tested, documented pure utility (BG-2 characterization
/// coverage in Level5AutonomousActorTests still exercises it directly).
/// </summary>
public class BodyGuardDetection : MonoBehaviour
{
    /// <summary>
    /// BG-2: the reach a bodyguard falls back to when its prefab never authored one.
    /// </summary>
    public const float DefaultEnemySightDistance = 20f;

    [SerializeField]
    private float enemySightDistance;

    /// <summary>The authored sight reach, or <see cref="DefaultEnemySightDistance"/> when unauthored (0 or less).</summary>
    public float EnemySightDistance => enemySightDistance > 0f ? enemySightDistance : DefaultEnemySightDistance;

    /// <summary>
    /// BG-2: how far this bodyguard's own sight actually reaches. The authored
    /// <c>enemySightDistance</c> is the designer's dial, but it cannot sit below the range at which
    /// <see cref="BodyGuardController.MaximumInterceptionDistance"/> would already send the
    /// bodyguard in - otherwise a threat could sit inside the interception leash while this
    /// component's own reach still called it unsighted. The only authored value in the project is
    /// 4, against an interception leash of 6, so this invariant is load-bearing rather than
    /// theoretical.
    /// </summary>
    public static float EffectiveSightDistance(float authoredSightDistance, float maximumInterceptionDistance)
    {
        return Mathf.Max(authoredSightDistance, maximumInterceptionDistance);
    }

    /// <summary>
    /// #57: true when a hostile is close enough to be worth a bodyguard breaking formation for -
    /// either within this bodyguard's own effective sight reach (guard-relative; see
    /// <see cref="EffectiveSightDistance"/>) or within the protected actor's interception envelope
    /// (protected-actor-relative; the same <c>maximumInterceptionDistance</c> that already gates
    /// <c>BodyGuardController.ShouldMoveForProtection</c>/<c>pursuePlayer</c>).
    ///
    /// Two reference points, combined with OR rather than one flooring the other, because the
    /// controller's movement decisions have always been protected-actor-relative while a
    /// bodyguard's authored sight has always been guard-relative - flooring one at the other (the
    /// pre-#57 <see cref="EffectiveSightDistance"/>-only approach) could only ever make them agree
    /// when a bodyguard stands on top of its protected actor. This is what lets a hostile at 5
    /// units register consistently whether it is sight (authored 4, floored to 6) or the
    /// interception leash (6) that actually reaches it.
    /// </summary>
    public static bool IsActionableRange(
        float distanceToProtectedActor,
        float distanceToGuard,
        float authoredSightDistance,
        float maximumInterceptionDistance)
    {
        if (distanceToProtectedActor <= maximumInterceptionDistance)
        {
            return true;
        }

        float reach = EffectiveSightDistance(authoredSightDistance, maximumInterceptionDistance);
        return distanceToGuard <= reach;
    }

    /// <summary>
    /// BG-2: true when any of <paramref name="enemyPositions"/> lies within
    /// <paramref name="sightDistance"/> of <paramref name="bodyGuardPosition"/>. Kept pure and
    /// static so the reach rule can be covered without standing a scene up.
    /// </summary>
    public static bool AnyEnemyWithinSight(
        Vector3 bodyGuardPosition, IReadOnlyList<Vector3> enemyPositions, float sightDistance)
    {
        if (enemyPositions == null || sightDistance <= 0f)
        {
            return false;
        }

        float sightDistanceSquared = sightDistance * sightDistance;
        for (int i = 0; i < enemyPositions.Count; i++)
        {
            if ((enemyPositions[i] - bodyGuardPosition).sqrMagnitude <= sightDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }
}
