/// <summary>
/// The detection state the attack queue needs to drive on whoever it reserves a slot for.
///
/// AUD-005: <c>PlayerAttackQueue</c> used to set these three fields through two concrete component
/// types - <c>EnemyDetection</c> and <c>BodyGuardDetection</c> - in two near-identical blocks,
/// duplicated again for the clear path. The only real difference was the name of the "I can see my
/// target" flag: the enemy called it <c>PlayerSighted</c>, the bodyguard <c>EnemySighted</c>.
///
/// With this interface the queue sets the state once, and a new melee actor type joins by
/// implementing it rather than by editing the queue.
/// </summary>
public interface ICombatDetection
{
    /// <summary>Whether this actor currently holds an attack slot.</summary>
    bool Attacking { get; set; }

    /// <summary>The slot it holds, or -1 when it holds none.</summary>
    int AttackPositionId { get; set; }

    /// <summary>
    /// Whether this actor can see whatever it is hunting. The concrete components keep their own
    /// names for it (<c>PlayerSighted</c>, <c>EnemySighted</c>) - this is the name the queue uses,
    /// because the queue does not care which side the target is on.
    /// </summary>
    bool TargetSighted { get; set; }
}
