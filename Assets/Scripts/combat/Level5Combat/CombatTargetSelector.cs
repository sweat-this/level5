using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministic target-selection policy shared by Enemy and Bodyguard AI.
///
/// This is scoring only: no Rigidbody, animation, attack, damage or queue-reservation
/// responsibilities. Callers pass in candidates and get back the best valid one (or null); what
/// happens with that choice - approach it, reserve an attack slot, attack it - is up to the
/// caller. Kept intentionally simple: candidate count per decision is a handful of enemies or
/// bodyguards, not a crowd, so a linear scan with a documented scoring formula is preferable to a
/// general utility-AI framework.
/// </summary>
public static class CombatTargetSelector
{
    /// <summary>
    /// Bonus applied to whatever the caller reports as its current target, so equally-scored
    /// candidates don't cause target flicker between decision ticks. Deliberately small relative
    /// to this game's combat distances (attack ranges under 1 unit, sight/pursuit/protection
    /// ranges of several units to a few dozen) - it only has to survive positional jitter and a
    /// marginally-closer rival, not keep a stale target against a genuinely closer or higher-tier
    /// one.
    /// </summary>
    public const float TargetStickinessBonus = 2f;

    /// <summary>Score lost per unit distance - closer candidates are preferred, all else equal.</summary>
    private const float DistancePenaltyWeight = 1f;

    // ---- Bodyguard threat tiers (STEP 3 of the AI architecture task) ----
    //
    // ICombatReservationState.HasAttackReservation becomes true the moment PlayerAttackQueue grants
    // a reservation, not only on the animation frame a swing lands (see the interface's doc
    // comment) - so a reservation alone is "en route to attack", and a reservation held while already standing
    // inside ImminentThreatRange of the protected actor is "about to land a hit". That distinction
    // is what lets reservation state keep informing bodyguard threat scoring without letting raw
    // queue order (first reserved == first attacked) stand in for tactical priority.
    public const float ImminentThreatScore = 1000f;
    public const float ReservedThreatScore = 500f;
    public const float NearProtectedActorScore = 100f;
    public const float DistantHostileScore = 0f;
    public const float ImminentThreatRange = 2.5f;

    /// <summary>
    /// Picks the nearest valid candidate to <paramref name="origin"/>, holding
    /// <paramref name="currentTarget"/> against a near-equal rival. Used by Enemy target
    /// acquisition (hostility/alive/active/distance/continuity - STEP 2's enemy criteria).
    /// </summary>
    public static ICombatAgent SelectNearestValidTarget(
        IReadOnlyList<ICombatAgent> candidates,
        Vector3 origin,
        ICombatAgent currentTarget)
    {
        if (candidates == null)
        {
            return null;
        }

        ICombatAgent best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            ICombatAgent candidate = candidates[i];
            if (!IsValidCandidate(candidate))
            {
                continue;
            }

            float distance = Vector3.Distance(origin, candidate.CombatTransform.position);
            float score = -distance * DistancePenaltyWeight;
            if (ReferenceEquals(candidate, currentTarget))
            {
                score += TargetStickinessBonus;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// #57: overload that additionally accepts <paramref name="actionableCandidates"/> - a
    /// caller-filtered subset of <paramref name="candidates"/> that are close enough (to the guard
    /// or to the protected actor) to be worth breaking formation for right now. Selection prefers
    /// that subset when it is non-empty, falling back to the full candidate list only when nothing
    /// is actionable - so a reserved attacker on the far side of the map (tier score 500, see
    /// ReservedThreatScore) cannot outrank an unreserved enemy already standing next to the
    /// protected actor (tier score 100) purely because CombatTargetSelector's reservation bonus
    /// dwarfs its distance penalty. <paramref name="hasActionableThreat"/> reports whether the
    /// selected threat came from the actionable subset - callers use this for actionability
    /// decisions (e.g. patrol) that must not treat "a threat exists" the same as "this threat is
    /// close enough to matter".
    /// </summary>
    public static ICombatAgent SelectBodyguardThreat(
        IReadOnlyList<ICombatAgent> candidates,
        IReadOnlyList<ICombatAgent> actionableCandidates,
        Vector3 guardPosition,
        Vector3 protectedActorPosition,
        ICombatAgent currentTarget,
        float protectionRadius,
        out bool hasActionableThreat)
    {
        bool anyActionable = actionableCandidates != null && actionableCandidates.Count > 0;
        IReadOnlyList<ICombatAgent> pool = anyActionable ? actionableCandidates : candidates;

        ICombatAgent selected = SelectBodyguardThreat(
            pool, guardPosition, protectedActorPosition, currentTarget, protectionRadius);

        hasActionableThreat = anyActionable && selected != null;
        return selected;
    }

    /// <summary>
    /// Picks which hostile actor a bodyguard should treat as the priority threat, using the
    /// hierarchy from STEP 3: an actor with a reservation close enough to be about to land a hit
    /// outranks one merely reserved, which outranks one simply near the protected actor, which
    /// outranks any other valid hostile. Distance to the guard itself only breaks ties within a
    /// tier, and <paramref name="currentTarget"/> is held against a near-equal rival.
    /// </summary>
    public static ICombatAgent SelectBodyguardThreat(
        IReadOnlyList<ICombatAgent> candidates,
        Vector3 guardPosition,
        Vector3 protectedActorPosition,
        ICombatAgent currentTarget,
        float protectionRadius)
    {
        if (candidates == null)
        {
            return null;
        }

        ICombatAgent best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            ICombatAgent candidate = candidates[i];
            if (!IsValidCandidate(candidate))
            {
                continue;
            }

            Transform candidateTransform = candidate.CombatTransform;
            float distanceToProtectedActor = Vector3.Distance(protectedActorPosition, candidateTransform.position);
            float distanceToGuard = Vector3.Distance(guardPosition, candidateTransform.position);
            bool hasReservation = HasActiveReservation(candidate);

            float tierScore;
            if (hasReservation && distanceToProtectedActor <= ImminentThreatRange)
            {
                tierScore = ImminentThreatScore;
            }
            else if (hasReservation)
            {
                tierScore = ReservedThreatScore;
            }
            else if (distanceToProtectedActor <= protectionRadius)
            {
                tierScore = NearProtectedActorScore;
            }
            else
            {
                tierScore = DistantHostileScore;
            }

            float score = tierScore - (distanceToGuard * DistancePenaltyWeight);
            if (ReferenceEquals(candidate, currentTarget))
            {
                score += TargetStickinessBonus;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// #57: true when <paramref name="candidate"/> qualifies for the ImminentThreatScore tier above
    /// - reserved and already within <see cref="ImminentThreatRange"/> of the protected actor, i.e.
    /// "about to land a hit". Exposed so a caller's own actionable-candidate filter (see
    /// <c>BodyGuardController.IsActionableThreat</c>) can treat this tier as unconditionally
    /// actionable, independent of that caller's own distance tuning
    /// (<c>maximumInterceptionDistance</c>, authored sight) - otherwise a bodyguard prefab authored
    /// with a <c>maximumInterceptionDistance</c> smaller than <see cref="ImminentThreatRange"/>
    /// could filter the single highest-priority candidate out of the actionable pool before
    /// selection ever sees it, letting a lower-tier candidate win instead.
    /// </summary>
    public static bool IsImminentThreat(ICombatAgent candidate, Vector3 protectedActorPosition)
    {
        if (!IsValidCandidate(candidate))
        {
            return false;
        }

        float distanceToProtectedActor = Vector3.Distance(protectedActorPosition, candidate.CombatTransform.position);
        return HasActiveReservation(candidate) && distanceToProtectedActor <= ImminentThreatRange;
    }

    private static bool HasActiveReservation(ICombatAgent candidate)
    {
        ICombatReservationState reservation = candidate.CombatObject.GetComponent<ICombatReservationState>();
        return reservation != null && reservation.HasAttackReservation;
    }

    private static bool IsValidCandidate(ICombatAgent candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        // A destroyed MonoBehaviour reached through an interface reference is not C#-null, and
        // Unity throws MissingReferenceException from most of its members (including CanAct,
        // whose real implementations read isActiveAndEnabled) once the native object is gone.
        // Routing the check through UnityEngine.Object's overloaded == is what safely catches
        // that state without risking the throw - a plain `candidate == null` above does not.
        if (candidate is Object unityObject && unityObject == null)
        {
            return false;
        }

        if (!candidate.CanAct)
        {
            return false;
        }

        GameObject combatObject = candidate.CombatObject;
        Transform combatTransform = candidate.CombatTransform;
        return combatObject != null && combatObject.activeInHierarchy && combatTransform != null;
    }
}
