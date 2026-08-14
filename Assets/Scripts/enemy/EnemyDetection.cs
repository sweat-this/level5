using System.Collections;
using UnityEngine;
using Level5.Core.Match;

public class EnemyDetection : MonoBehaviour, ICombatDetection
{
    EnemyController enemyController;
    [SerializeField]
    bool playerSighted;
    bool enemyDetectionEnabled = true;
    public float enemySightDistance;
    // STEP 6: detection and pursuit are different ranges - noticing a target happens within
    // enemySightDistance, but once engaged the enemy pursues until PursuitRange is exceeded.
    // 0 (the default on every existing prefab) means "not configured", so it falls back to a
    // wider multiple of enemySightDistance rather than reusing the same value for both, which
    // was the previous (undifferentiated) behaviour.
    [SerializeField]
    private float pursuitRange;
    int attackPositionId;
    [SerializeField]
    bool attacking;

    /// <summary>
    /// ENM-1: this reads as "the player is within sight", and it is not. The only thing that ever
    /// sets it true is <c>PlayerAttackQueue.SetAttackerDetection</c>, when this enemy is granted an
    /// attack reservation; proximity alone never sets it. <see cref="EnemyController"/> gates
    /// <c>stateWalk</c> on it, so an enemy that cannot get a slot stands idle no matter how close
    /// the player is.
    ///
    /// That is deliberate crowd control - it is what stops twenty battle-royal enemies converging
    /// at once - and is left as-is. Renaming it to say so would touch the ICombatDetection contract
    /// and both actor types; the behaviour is what matters and the behaviour is correct.
    /// </summary>
    public bool PlayerSighted { get => playerSighted; set => playerSighted = value; }

    // AUD-005: the queue's name for the same flag - an enemy hunts the player
    public bool TargetSighted { get => PlayerSighted; set => PlayerSighted = value; }
    public int AttackPositionId { get => attackPositionId; set => attackPositionId = value; }
    public bool Attacking { get => attacking; set => attacking = value; }
    public float PursuitRange => pursuitRange > 0f ? pursuitRange : enemySightDistance * 1.5f;

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
        playerSighted = false;
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
        playerSighted = false;
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
        // beyond pursuit range, disengage (STEP 6 - wider than the acquire range, not the same one)
        if (enemyController.DistanceFromPlayer >= PursuitRange
            && enemyDetectionEnabled)
        {
            playerSighted = false;
            // if attacking, remove from queue
            if (attacking)
            {
                attacking = false;
                playerAttackQueue.RemoveFromQueue(gameObject, AttackPositionId);
            }
        }
    }

    IEnumerator DelayEnemySight(float seconds)
    {
        enemyDetectionEnabled = false;
        playerSighted = false;
        yield return new WaitForSeconds(seconds);
        enemyDetectionEnabled = true;
    }

    void CheckReturnToPatrolStatus()
    {
        if (enemyController.stateIdle
            && gameObject.transform.position != enemyController.OriginalPosition
            && !playerSighted
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
