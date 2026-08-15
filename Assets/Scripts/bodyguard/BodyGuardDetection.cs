using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyGuardDetection : MonoBehaviour, ICombatDetection
{
    /// <summary>
    /// BG-2: the reach a bodyguard falls back to when its prefab never authored one. This is the
    /// value <see cref="Start"/> used to force on every bodyguard unconditionally.
    /// </summary>
    public const float DefaultEnemySightDistance = 20f;

    BodyGuardController bodyGuardController;

    // BG-2: reused so the per-tick sweep does not allocate.
    private readonly List<Vector3> queuedEnemyPositions = new List<Vector3>();
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

        // BG-2: enemySightDistance used to be overwritten with a hard 20 here and then read by
        // nothing, because CheckPlayerDistance measured no distance at all - it asked the queue
        // whether any enemy was engaged anywhere, which is the same answer for every bodyguard in
        // the scene regardless of where it is standing. The authored value is now honoured and
        // actually used. Prefabs that never authored one (0) keep the previous effective reach so
        // this is not a silent nerf on existing content.
        if (enemySightDistance <= 0)
        {
            enemySightDistance = DefaultEnemySightDistance;
        }

        InvokeRepeating("CheckPlayerDistance", 0, 0.5f);
        InvokeRepeating("CheckReturnToPatrolStatus", 0, 3f);
    }

    void FindEnemiesToAttack()
    {
        // check player attack queue and go after them

    }

    void CheckPlayerDistance()
    {
        // BG-2: this used to be `enemySighted = queue != null && queue.HasQueuedEnemies()` - one
        // scene-wide boolean, so a bodyguard on the far side of the level "sighted" an enemy the
        // instant anything anywhere engaged, and the only consumer (CheckReturnToPatrolStatus)
        // held every bodyguard off patrol for the whole fight. Detection is now measured from this
        // bodyguard against its own authored reach.
        if (!enemyDetectionEnabled)
        {
            enemySighted = false;
            return;
        }

        PlayerAttackQueue playerAttackQueue = bodyGuardController != null ? bodyGuardController.TargetQueue : null;
        if (playerAttackQueue == null || !playerAttackQueue.HasQueuedEnemies())
        {
            enemySighted = false;
            return;
        }

        // HasQueuedEnemies above ran the queue's stale-entry cleanup, so EnemiesQueued holds live
        // entries; a destroyed GameObject can still surface as a null element, hence the guard.
        queuedEnemyPositions.Clear();
        IReadOnlyList<GameObject> enemiesQueued = playerAttackQueue.EnemiesQueued;
        for (int i = 0; i < enemiesQueued.Count; i++)
        {
            GameObject enemy = enemiesQueued[i];
            if (enemy != null)
            {
                queuedEnemyPositions.Add(enemy.transform.position);
            }
        }

        // Read the controller's leash per tick rather than in Start: BodyGuardController applies
        // its own defaults in Start, and component start order is not guaranteed.
        float reach = EffectiveSightDistance(enemySightDistance, bodyGuardController.MaximumInterceptionDistance);
        enemySighted = AnyEnemyWithinSight(transform.position, queuedEnemyPositions, reach);
    }

    /// <summary>
    /// BG-2: how far this bodyguard actually looks. The authored <c>enemySightDistance</c> is the
    /// designer's dial, but it cannot sit below the range at which
    /// <see cref="BodyGuardController.MaximumInterceptionDistance"/> would already send the
    /// bodyguard in - otherwise the controller breaks formation to intercept a threat that
    /// detection simultaneously reports as unsighted, and
    /// <see cref="CheckReturnToPatrolStatus"/> sends it back to patrol mid-charge. The only
    /// authored value in the project is 4, against an interception leash of 6, so this invariant
    /// is load-bearing rather than theoretical.
    /// </summary>
    public static float EffectiveSightDistance(float authoredSightDistance, float maximumInterceptionDistance)
    {
        return Mathf.Max(authoredSightDistance, maximumInterceptionDistance);
    }

    /// <summary>
    /// BG-2: true when any of <paramref name="enemyPositions"/> lies within
    /// <paramref name="sightDistance"/> of <paramref name="bodyGuardPosition"/>. Kept pure and
    /// static so the reach rule can be covered without standing a scene up.
    /// </summary>
    public static bool AnyEnemyWithinSight(
        Vector3 bodyGuardPosition, IReadOnlyList<Vector3> enemyPositions, float sightDistance)
    {
        if (enemyPositions == null || sightDistance <= 0f)
        {
            return false;
        }

        float sightDistanceSquared = sightDistance * sightDistance;
        for (int i = 0; i < enemyPositions.Count; i++)
        {
            if ((enemyPositions[i] - bodyGuardPosition).sqrMagnitude <= sightDistanceSquared)
            {
                return true;
            }
        }

        return false;
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
