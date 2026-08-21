using UnityEngine;
using Level5.Core.Match;

public class EnemyDetection : MonoBehaviour, ICombatReservationState
{
    EnemyController enemyController;
    bool enemyDetectionEnabled = true;
    public float enemySightDistance;
    // STEP 6: detection and pursuit are different ranges - noticing a target happens within
    // enemySightDistance, but once engaged the enemy pursues until PursuitRange is exceeded.
    // 0 (the default on every existing prefab) means "not configured", so it falls back to a
    // wider multiple of enemySightDistance rather than reusing the same value for both, which
    // was the previous (undifferentiated) behaviour.
    [SerializeField]
    private float pursuitRange;
    int attackPositionId = -1;
    [SerializeField]
    bool attacking;

    /// <summary>
    /// ENM-1/#57: whether this enemy currently holds an attack-queue reservation. The only thing
    /// that ever sets it true is <see cref="SetAttackReservation"/>, called from
    /// <c>PlayerAttackQueue</c> when this enemy is granted a slot; proximity alone never sets it.
    /// <see cref="EnemyController"/> gates <c>stateWalk</c> on it, so an enemy that cannot get a
    /// slot stands idle no matter how close the player is.
    ///
    /// That is deliberate crowd control - it is what stops twenty battle-royal enemies converging
    /// at once - and is left as-is. #57 removed the separate `PlayerSighted`/`TargetSighted` name
    /// this same boolean used to be duplicated under (see the interface's history note on
    /// ICombatReservationState) - it never meant anything different, so it was dropped rather than
    /// kept as a second name for one flag.
    /// </summary>
    public bool Attacking => attacking;
    public bool HasAttackReservation => attacking;
    public int AttackPositionId => attackPositionId;
    public float PursuitRange => pursuitRange > 0f ? pursuitRange : enemySightDistance * 1.5f;

    public void SetAttackReservation(bool active, int attackPositionId)
    {
        attacking = active;
        this.attackPositionId = attackPositionId;
    }

    // ENM-5: the value the prefab was authored with. OnEnable overwrites enemySightDistance for
    // certain rule sets, and this component is pooled - without capturing the authored value here,
    // the first respawn under a rule that widens sight permanently replaced it, and no later
    // respawn could ever get it back.
    private float authoredSightDistance;

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        authoredSightDistance = enemySightDistance;
    }

    private void OnEnable()
    {
        attacking = false;
        attackPositionId = -1;
        enemyDetectionEnabled = true;
        // always re-derive from the authored value rather than from whatever the last life left
        enemySightDistance = authoredSightDistance;
        // if only enemies, make increase enemy sight
        if (MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.EnemiesEnabled)
        {
            enemySightDistance = 10;
        }
        if (MatchRuntime.Rules.IsBattleRoyal)
        {
            enemySightDistance = 30;
        }
        InvokeRepeating("CheckPlayerDistance", 0, 0.5f);
        InvokeRepeating("CheckReturnToPatrolStatus", 0, 3f);
    }

    private void OnDisable()
    {
        CancelInvoke();
        StopAllCoroutines();
        attacking = false;
    }

    void CheckPlayerDistance()
    {
        PlayerAttackQueue playerAttackQueue = enemyController != null ? enemyController.TargetQueue : null;
        if (playerAttackQueue == null)
        {
            return;
        }

        // if player within enemy sight distance, acquire
        if (enemyController.DistanceFromPlayer < enemySightDistance
            && enemyDetectionEnabled)
        {
            if (playerAttackQueue.AttackSlotOpen && !attacking)
            {
                playerAttackQueue.TryAddToQueue(gameObject);
            }
        }
        // beyond pursuit range, disengage (STEP 6 - wider than the acquire range, not the same one).
        // #57: the reservation release below is the sole writer of `attacking` for this path -
        // PlayerAttackQueue.RemoveFromQueue calls back into SetAttackReservation(false, -1), so
        // there is no separate direct field write here to keep in sync with it.
        if (enemyController.DistanceFromPlayer >= PursuitRange
            && enemyDetectionEnabled
            && attacking)
        {
            playerAttackQueue.RemoveFromQueue(gameObject, AttackPositionId);
        }
    }

    void CheckReturnToPatrolStatus()
    {
        if (enemyController.stateIdle
            && gameObject.transform.position != enemyController.OriginalPosition
            && !attacking
            && enemyDetectionEnabled)
        {
            enemyController.statePatrol = true;
        }
        else
        {
            enemyController.statePatrol = false;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemySightDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, PursuitRange);
    }
#endif
}
