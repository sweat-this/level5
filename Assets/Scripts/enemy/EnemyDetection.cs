using System.Collections;
using UnityEngine;

public class EnemyDetection : MonoBehaviour
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
    public int AttackPositionId { get => attackPositionId; set => attackPositionId = value; }
    public bool Attacking { get => attacking; set => attacking = value; }

    private void Start()
    {
        enemyController = GetComponent<EnemyController>();
        playerAttackQueue = GameLevelManager.instance.PlayerController1.PlayerAttackQueue;
        // if only enemies, make increase enemy sight
        if (GameOptions.EnemiesOnlyEnabled || GameOptions.enemiesEnabled)
        {
            enemySightDistance = 10;
        }
        if (GameOptions.battleRoyalEnabled)
        {
            enemySightDistance = 30;
        }
        InvokeRepeating("CheckPlayerDistance", 0, 0.5f);
        InvokeRepeating("CheckReturnToPatrolStatus", 0, 3f);
    }

    void CheckPlayerDistance()
    {
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
