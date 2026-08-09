using System.Collections;
using UnityEngine;
using Level5.Core.Match;

public class EnemyDetection : MonoBehaviour, ICombatDetection
{
    EnemyController enemyController;
    PlayerAttackQueue playerAttackQueue;
    [SerializeField]
    bool playerSighted;
    bool enemyDetectionEnabled = true;
    public float enemySightDistance;
    int attackPositionId;
    [SerializeField]
    bool attacking;

    public bool PlayerSighted { get => playerSighted; set => playerSighted = value; }

    // AUD-005: the queue's name for the same flag - an enemy hunts the player
    public bool TargetSighted { get => PlayerSighted; set => PlayerSighted = value; }
    public int AttackPositionId { get => attackPositionId; set => attackPositionId = value; }
    public bool Attacking { get => attacking; set => attacking = value; }

    private void Awake()
    {
        enemyController = GetComponent<EnemyController>();
    }

    private void OnEnable()
    {
        playerSighted = false;
        attacking = false;
        attackPositionId = -1;
        enemyDetectionEnabled = true;
        playerAttackQueue = GameLevelManager.instance != null && GameLevelManager.instance.PlayerController1 != null
            ? GameLevelManager.instance.PlayerController1.PlayerAttackQueue
            : null;
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
        if (playerAttackQueue == null)
        {
            return;
        }

        // if player within enemy sight distance
        if (enemyController.DistanceFromPlayer < enemySightDistance
            && enemyDetectionEnabled)
        {
            if (playerAttackQueue.AttackSlotOpen && !attacking)
            {
                playerAttackQueue.TryAddToQueue(gameObject);
            }
        }
        // if player NOT within enemy sight distance
        if (enemyController.DistanceFromPlayer >= enemySightDistance
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
}
