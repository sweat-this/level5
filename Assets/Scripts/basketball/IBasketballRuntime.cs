using UnityEngine;
using Level5.Core;

/// <summary>
/// Basketball-domain contract for binding a spawned basketball to the participant that owns it.
///
/// AUD-013: composition code (<see cref="SpawnCoordinator"/>) binds a basketball's runtime identity
/// explicitly through this contract instead of the basketball copying it out of a second,
/// hand-synced <c>PlayerIdentifier</c> instance placed on the basketball object itself. The actor-side
/// <c>PlayerIdentifier</c> (on the player/CPU GameObject, reached through <see cref="PlayerRegistry"/>)
/// remains the single authoritative participant identity; this contract only carries what the
/// basketball object itself needs, bound once at spawn time.
///
/// Implemented directly on <see cref="BasketBall"/> and <see cref="BasketBallAuto"/> - one contract,
/// not separate human/CPU abstractions.
/// </summary>
public interface IBasketballRuntime
{
    /// <summary>
    /// The owning participant's runtime slot identity - <c>PlayerIdentifier.pid</c> as assigned during
    /// match composition. Not the stable authored <c>CharacterProfile.PlayerId</c>.
    /// </summary>
    int ParticipantId { get; }

    bool IsCpu { get; }

    /// <summary>
    /// True for the one ball every "the local player's ball" consumer means (camera follow, the
    /// free-play stat save, the ui-stats toggle) - always the slot 0 ball, which composition
    /// guarantees is spawned first and is always human.
    /// </summary>
    bool IsPrimary { get; }

    /// <summary>The actor GameObject (human player or CPU auto-player) this ball belongs to.</summary>
    GameObject OwnerActor { get; }

    IShooterActor Actor { get; }

    BasketBallState State { get; }

    GameStats Stats { get; }

    float LastShotDistance { get; }

    /// <summary>
    /// Binds this basketball to its owning participant. Composition (<see cref="SpawnCoordinator"/>)
    /// calls this once, immediately after instantiating the ball and before Unity calls any of its
    /// components' <c>Start()</c>.
    /// </summary>
    void BindOwner(int participantId, bool isCpu, bool isPrimary, GameObject ownerActor, IShooterActor actor);
}
