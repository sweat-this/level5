
using System;
using System.Collections;
using UnityEngine;
using Level5.Core.Match;

public class PlayerCollisions : MonoBehaviour, IPlayerCollisionHitHost
{
    [SerializeField]
    PlayerIdentifier playerIdentifier;
    [SerializeField]
    PlayerController playerController;
    [SerializeField]
    AutoPlayerController autoPlayerController;
    [SerializeField]
    PlayerHealth playerHealth;
    [SerializeField]
    bool playerCanBeKnockedDown;

    // AUD-012 Phase 3 Slice 70: the shared combat-hit mechanics (damage/knockdown/rake/disintegrate
    // resolution, health application, block handling) this wrapper used to carry as private methods
    // byte-identical to AutoPlayerCollisions's own copies. See PlayerCollisionHitHandler and
    // IPlayerCollisionHitHost. This wrapper still owns every role-specific decision: which hitbox tag
    // is valid, human-only dunk handling, killed-on-idle forwarding, and fall-respawn.
    private readonly PlayerCollisionHitHandler combatHitHandler;

    public PlayerCollisions()
    {
        combatHitHandler = new PlayerCollisionHitHandler(this);
    }

    // AUD-012 Phase 2b: match rules, the fall-respawn destination and the GameRules.killedOnIdle
    // forwarding callback, composed by SpawnCoordinator.BindPlayerCollisionsContext instead of this
    // component reading MatchRuntime.Rules / GameLevelManager.instance / GameRules.instance directly.
    private ResolvedMatchRules matchRules;
    private Transform fallRespawnDestination;
    private Action markKilledOnIdle;

    /// <summary>
    /// Binds the rules this match is being played under. Bind-once, the same shape
    /// <c>CallBallToPlayer</c>/<c>PlayerHealth</c> already use.
    /// </summary>
    public void BindMatchRules(ResolvedMatchRules rules)
    {
        if (matchRules != null)
        {
            Debug.LogError($"PlayerCollisions on '{gameObject.name}' already has bound match rules; ignoring a second BindMatchRules call.", this);
            return;
        }

        if (rules == null)
        {
            Debug.LogError($"PlayerCollisions on '{gameObject.name}' was bound with null match rules; remaining unbound.", this);
            return;
        }

        matchRules = rules;
    }

    /// <summary>
    /// Explicit binding of the human spawn point, from <c>SpawnCoordinator.BindPlayerCollisionsContext</c> -
    /// replaces this component's former direct <c>GameLevelManager.instance.PlayerSpawnLocation</c> read.
    /// </summary>
    public void BindFallRespawnDestination(Transform destination)
    {
        fallRespawnDestination = destination;
    }

    /// <summary>
    /// Explicit binding of the <c>GameRules.killedOnIdle</c> forwarding callback, from
    /// <c>SpawnCoordinator.BindPlayerCollisionsContext</c> - replaces this component's former direct
    /// <c>GameRules.instance.killedOnIdle = true</c> write.
    /// </summary>
    public void BindKilledOnIdleCallback(Action callback)
    {
        markKilledOnIdle = callback;
    }

    private void Start()
    {
        GetPlayerObjects();
    }

    private void GetPlayerObjects()
    {
        playerIdentifier = GetComponentInParent<PlayerIdentifier>();
        if (playerIdentifier.isCpu)
        {
            autoPlayerController = playerIdentifier.autoPlayer.GetComponent<AutoPlayerController>();
        }
        else
        {
            playerController = playerIdentifier.player.GetComponent<PlayerController>();
        }

        playerHealth = playerIdentifier.isCpu
            ? playerIdentifier.autoPlayer.GetComponentInChildren<PlayerHealth>()
            : playerIdentifier.player.GetComponentInChildren<PlayerHealth>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // check for fall respawner
        if (gameObject.CompareTag("playerHitbox") && other.CompareTag("fallRespawner"))
        {
            if (fallRespawnDestination != null)
            {
                playerIdentifier.transform.position = fallRespawnDestination.position;
            }
        }

        if (matchRules != null
            && gameObject.CompareTag("playerHitbox")
            && !matchRules.IsBattleRoyal
            && !matchRules.IsCageMatch
            && !matchRules.EnemiesOnly
            && playerController.InAir
            && playerController.currentState != playerController.dunkState
            && (other.name.Equals("dunk_position_left") || other.name.Equals("dunk_position_right")))
        {
            StartCoroutine(playerController.PlayerDunk.TriggerDunkSequence());
        }
        // player sometimes gets stuck in inair dunk state
        if (gameObject.CompareTag("playerHitbox")
            && other.CompareTag("ground")
            && playerController.currentState == playerController.inAirDunkState)
        {
            playerController.SetPlayerAnim("jump", false);
        }

        // if collsion between hitbox, vehicle, knocked down
        if (matchRules != null
        && gameObject.CompareTag("playerHitbox")
        && (other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox") || other.CompareTag("playerAttackBox"))
        && !playerController.KnockedDown
        && !playerController.TakeDamage
        && (matchRules.EnemiesEnabled
        || matchRules.TrafficEnabled
        || matchRules.ObstaclesEnabled
        || other.transform.root.name.Contains("snake")
        || matchRules.SniperEnabled
        || matchRules.Sniper == SniperMode.Bullet
        || matchRules.Sniper == SniperMode.Laser
        || other.transform.root.name.Contains("projectile_bullet_instantkill_enemy")))
        {
            combatHitHandler.HandleAttackBoxHit(other);
        }
    }

    // ==================== IPlayerCollisionHitHost ====================
    // Explicit implementation: these exist only for PlayerCollisionHitHandler to reach this wrapper's
    // resolved human actor state, so they stay off the ordinary public surface.

    bool IPlayerCollisionHitHost.CanBeKnockedDown => playerCanBeKnockedDown;

    bool IPlayerCollisionHitHost.IsBlocking => playerController.CurrentState == playerController.BlockState;

    float IPlayerCollisionHitHost.Luck => playerController.CharacterProfile.Luck;

    PlayerHealth IPlayerCollisionHitHost.Health => playerHealth;

    void IPlayerCollisionHitHost.RunCoroutine(IEnumerator routine) => StartCoroutine(routine);

    void IPlayerCollisionHitHost.NotifyEnemyAttackBoxHit(IAttackBoxHitInfo hit)
    {
        if (hit.IsKilledOnIdle)
        {
            markKilledOnIdle?.Invoke();
        }
    }

    void IPlayerCollisionHitHost.ApplyDisintegrateReaction()
    {
        playerController.TakeDamage = false;
        playerController.KnockedDown = false;
        playerController.hasBasketball = false;
        playerController.Disintegrated = true;
        playerController.SetPlayerAnim("hasBasketball", false);
    }

    void IPlayerCollisionHitHost.ApplyDamageReaction()
    {
        playerController.TakeDamage = true;
        playerController.KnockedDown = false;
        playerController.hasBasketball = false;
        playerController.SetPlayerAnim("hasBasketball", false);
    }

    void IPlayerCollisionHitHost.ApplyKnockdownReaction()
    {
        playerController.TakeDamage = false;
        playerController.KnockedDown = true;
        playerController.hasBasketball = false;
        playerController.SetPlayerAnim("hasBasketball", false);
    }

    void IPlayerCollisionHitHost.ApplyRakeReaction(Collider rakeSource)
    {
        rakeSource.transform.parent.GetComponentInChildren<Animator>().Play("attack");
        playerController.TakeDamage = true;
        playerController.KnockedDown = false;
        playerController.hasBasketball = false;
        //StartCoroutine(playerState.PlayerFreezeForXSeconds(2f));
        playerController.SetPlayerAnim("hasBasketball", false);
    }
}
