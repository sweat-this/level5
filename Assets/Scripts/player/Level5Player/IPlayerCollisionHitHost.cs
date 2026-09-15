using System.Collections;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 3 Slice 70: the narrow slice of a collision wrapper's resolved actor state that
/// <see cref="PlayerCollisionHitHandler"/> needs to run the combat-hit sequence shared by
/// <c>PlayerCollisions</c> (human) and <c>AutoPlayerCollisions</c> (CPU shooter) once that wrapper has
/// already decided a collision is an eligible combat hit for its own role.
///
/// Implemented explicitly by both wrappers, translating to whichever concrete controller
/// (<c>PlayerController</c>/<c>AutoPlayerController</c>) they actually resolved in
/// <c>GetPlayerObjects()</c>. Deliberately does not attempt to converge
/// <c>PlayerController</c>/<c>AutoPlayerController</c> themselves - out of scope for this slice, and
/// AUD-002 already declined to do this for the equivalent damage-reaction split (see
/// <c>AutoPlayerDamageReactions</c>'s own remarks). This contract only names what the shared hit
/// mechanics read and write; role/mode eligibility (which hitbox tag qualifies, which match-rule
/// flags enable the hit, human-only dunk/killed-on-idle triggering) stays in the wrappers themselves.
/// </summary>
public interface IPlayerCollisionHitHost
{
    /// <summary>The wrapper's own authored "can this actor be knocked down" tuning flag.</summary>
    bool CanBeKnockedDown { get; }

    /// <summary>Whether the actor's current animator state is its block state.</summary>
    bool IsBlocking { get; }

    /// <summary>The actor's evade-chance stat, read from its <c>CharacterProfile</c>.</summary>
    float Luck { get; }

    /// <summary>The actor's health/block component, already resolved by the wrapper.</summary>
    PlayerHealth Health { get; }

    /// <summary>Runs a coroutine on the wrapper's own MonoBehaviour.</summary>
    void RunCoroutine(IEnumerator routine);

    /// <summary>
    /// Notifies the host that an enemy/obstacle attack box was hit, before disintegrate/damage
    /// resolution. Human-only policy (forwarding <see cref="IAttackBoxHitInfo.IsKilledOnIdle"/> into
    /// the bound <c>GameRules.killedOnIdle</c> callback) lives entirely in the host's implementation;
    /// the CPU host is a no-op, matching <c>AutoPlayerCollisions</c>'s existing behavior of never
    /// reading this field.
    /// </summary>
    void NotifyEnemyAttackBoxHit(IAttackBoxHitInfo hit);

    void ApplyDisintegrateReaction();

    void ApplyDamageReaction();

    void ApplyKnockdownReaction();

    /// <summary>Applies the take-damage reaction plus the rake-specific attack animation on the source.</summary>
    void ApplyRakeReaction(Collider rakeSource);
}
