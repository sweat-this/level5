using System.Collections;
using UnityEngine;

public class BodyGuardDetection : MonoBehaviour, ICombatDetection
{
    BodyGuardController bodyGuardController;
    [SerializeField]
    bool enemySighted;
    bool enemyDetectionEnabled = true;
    public float enemySightDistance;
    int attackPositionId;
    [SerializeField]
    bool attacking;

    public bool EnemySighted { get => enemySighted; set => enemySighted = value; }

    // AUD-005: the queue's name for the same flag - a bodyguard hunts enemies
    public bool TargetSighted { get => EnemySighted; set => EnemySighted = value; }
    public int AttackPositionId { get => attackPositionId; set => attackPositionId = value; }
    public bool Attacking { get => attacking; set => attacking = value; }

    private void Start()
    {
        bodyGuardController = GetComponent<BodyGuardController>();
        // STEP 1: no longer reaches for GameLevelManager.instance.players[0] directly - the
        // queue now comes from BodyGuardController's explicit protected-actor assignment, read
        // fresh each tick below so this works regardless of component resolution order.
        //if (enemySightDistance == 0)
        //{
        //    enemySightDistance = 5;
        //}
        //// if only enemies, make increase enemy sight
        //if (GameOptions.EnemiesOnlyEnabled)
        //{
        //    enemySightDistance = 20;
        //}

        enemySightDistance = 20;

        InvokeRepeating("CheckPlayerDistance", 0, 0.5f);
        InvokeRepeating("CheckReturnToPatrolStatus", 0, 3f);
    }

    void FindEnemiesToAttack()
    {
        // check player attack queue and go after them

    }

    void CheckPlayerDistance()
    {
        //// if player within enemy sight distance
        //if (bodyGuardController.DistanceFromPlayer < enemySightDistance
        //    && enemyDetectionEnabled)
        //{
        //    //PlayerAttackQueue.instance.CurrentEnemiesQueued
        //    if(PlayerAttackQueue.instance.CurrentEnemiesQueued > 0)
        //    {
        //        //StartCoroutine(PlayerAttackQueue.instance.RequestAddToQueue(gameObject));
        //        // attack enemies[0]
        //    }
        //}
        //// if player NOT within enemy sight distance
        //if (bodyGuardController.DistanceFromPlayer >= enemySightDistance
        //    && enemyDetectionEnabled)
        //{
        //    enemySighted = false;
        //    // move towards player
        //}

        PlayerAttackQueue playerAttackQueue = bodyGuardController != null ? bodyGuardController.TargetQueue : null;
        enemySighted = playerAttackQueue != null && playerAttackQueue.HasQueuedEnemies();
        // stay within certain distance of player

    }

    IEnumerator DelayEnemySight(float seconds)
    {
        enemyDetectionEnabled = false;
        enemySighted = false;
        yield return new WaitForSeconds(seconds);
        enemyDetectionEnabled = true;
    }

    void CheckReturnToPatrolStatus()
    {
        if (bodyGuardController.stateIdle
            && gameObject.transform.position != bodyGuardController.OriginalPosition
            && !enemySighted
            && enemyDetectionEnabled)
        {
            bodyGuardController.statePatrol = true;
        }
        else
        {
            bodyGuardController.statePatrol = false;
        }
    }
}
