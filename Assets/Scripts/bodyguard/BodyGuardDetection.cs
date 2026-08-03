using System.Collections;
using UnityEngine;

public class BodyGuardDetection : MonoBehaviour
{
    BodyGuardController bodyGuardController;
    PlayerAttackQueue playerAttackQueue;
    [SerializeField]
    bool enemySighted;
    bool enemyDetectionEnabled = true;
    public float enemySightDistance;
    int attackPositionId;
    [SerializeField]
    bool attacking;

    public bool EnemySighted { get => enemySighted; set => enemySighted = value; }
    public int AttackPositionId { get => attackPositionId; set => attackPositionId = value; }
    public bool Attacking { get => attacking; set => attacking = value; }

    private void Start()
    {
        bodyGuardController = GetComponent<BodyGuardController>();
        playerAttackQueue = GameLevelManager.instance.players[0].GetComponent<PlayerAttackQueue>();
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

        if (playerAttackQueue != null && playerAttackQueue.HasQueuedEnemies())
        {
            enemySighted = true;
        }
        else
        {
            enemySighted = false;
        }
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
