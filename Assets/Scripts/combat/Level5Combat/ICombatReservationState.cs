/// <summary>
/// The attack-queue reservation state <c>PlayerAttackQueue</c> owns and grants/revokes on a combat
/// actor. Reservation only - no sight/perception concept.
///
/// #57: this interface used to be ICombatDetection and additionally carried a `TargetSighted` flag
/// that read as perception but was, on both concrete implementers, never anything other than an
/// alias for the reservation itself - `PlayerSighted` (enemy) and `EnemySighted` (bodyguard) only
/// ever became true when PlayerAttackQueue granted a reservation. Narrowing the shared contract to
/// reservation state removes that ambiguity: PlayerAttackQueue now writes exactly one thing here,
/// atomically, and an actor's own notion of "is there something I should be fighting" (EnemyDetection's
/// authored sight range for enemies, BodyGuardController's threat model for bodyguards) lives entirely
/// outside this contract.
/// </summary>
public interface ICombatReservationState
{
    /// <summary>Whether this actor currently holds an attack slot.</summary>
    bool HasAttackReservation { get; }

    /// <summary>The slot it holds, or -1 when it holds none.</summary>
    int AttackPositionId { get; }

    /// <summary>
    /// Grants or revokes the reservation atomically - a caller can never observe an active
    /// reservation with an invalid slot id or vice versa.
    /// </summary>
    void SetAttackReservation(bool active, int attackPositionId);
}
