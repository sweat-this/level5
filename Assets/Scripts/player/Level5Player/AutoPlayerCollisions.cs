
using System.Collections;
using UnityEngine;
using Level5.Core.Match;

public class AutoPlayerCollisions : MonoBehaviour, IPlayerCollisionHitHost
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

    // AUD-012 Phase 3 Slice 70: the shared combat-hit mechanics this wrapper used to carry as private
    // methods byte-identical to PlayerCollisions's own copies. See PlayerCollisionHitHandler and
    // IPlayerCollisionHitHost. This wrapper still owns every role-specific decision: which hitbox tag
    // is valid, CPU attack-source eligibility, and fall-respawn.
    private readonly PlayerCollisionHitHandler combatHitHandler;

    public AutoPlayerCollisions()
    {
        combatHitHandler = new PlayerCollisionHitHandler(this);
    }

    // AUD-012 Phase 3 Slice 70: this CPU actor's shooter controller is missing when this component sits
    // on a non-shooter CPU role - the lockdown defender (AutoPlayerDefense, no AutoPlayerController).
    // Combat-hit semantics for that role are not established (see AutoPlayerDefense's docs and the
    // Phase 3 restructuring notes) - rather than invent them or dereference a null controller, combat-hit
    // processing is disabled for this instance and reported once here, not per-collision.
    private bool hasShooterController;

    // AUD-012 Phase 2b: match rules and the fall-respawn destination, composed by
    // SpawnCoordinator.BindAutoPlayerCollisionsContext instead of this component reading
    // MatchRuntime.Rules / GameLevelManager.instance directly.
    private ResolvedMatchRules matchRules;
    private Transform fallRespawnDestination;

    /// <summary>
    /// Binds the rules this match is being played under. Bind-once, the same shape
    /// <c>PlayerCollisions.BindMatchRules</c> uses.
    /// </summary>
    public void BindMatchRules(ResolvedMatchRules rules)
    {
        if (matchRules != null)
        {
            Debug.LogError($"AutoPlayerCollisions on '{gameObject.name}' already has bound match rules; ignoring a second BindMatchRules call.", this);
            return;
        }

        if (rules == null)
        {
            Debug.LogError($"AutoPlayerCollisions on '{gameObject.name}' was bound with null match rules; remaining unbound.", this);
            return;
        }

        matchRules = rules;
    }

    /// <summary>
    /// Explicit binding of the human spawn point, from
    /// <c>SpawnCoordinator.BindAutoPlayerCollisionsContext</c> - replaces this component's former
    /// direct <c>GameLevelManager.instance.PlayerSpawnLocation</c> read.
    /// </summary>
    public void BindFallRespawnDestination(Transform destination)
    {
        fallRespawnDestination = destination;
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
            hasShooterController = autoPlayerController != null;
            if (!hasShooterController)
            {
                // Warning, not Error: this is the lockdown defender's normal, permanent composition
                // (AutoPlayerDefense, no AutoPlayerController), not a one-off composition mistake - it
                // fires once per defender spawn, every time that mode is played. Debug.LogError is
                // reserved elsewhere on this type for genuine composition defects (see BindMatchRules
                // above); this is a known, deferred design gap (CPU-defender combat-hit semantics are
                // unresolved - see the Phase 3 restructuring notes), not one.
                Debug.LogWarning(
                    $"AutoPlayerCollisions on '{gameObject.name}' found no AutoPlayerController on CPU actor "
                    + $"'{playerIdentifier.autoPlayer.name}' (a non-shooter CPU role, e.g. AutoPlayerDefense); "
                    + "combat-hit processing is disabled for this participant.",
                    this);
            }
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

        //if (gameObject.CompareTag("playerHitbox")
        //    && (!MatchRuntime.Rules.IsBattleRoyal || !MatchRuntime.Rules.IsCageMatch || !MatchRuntime.Rules.EnemiesOnly)
        //    && autoPlayerController.InAir
        //    && autoPlayerController.currentState != autoPlayerController.dunkState
        //    && (other.name.Equals("dunk_position_left") || other.name.Equals("dunk_position_right")))
        //{
        //    StartCoroutine(GameLevelManager.instance.autoPlayerController1.PlayerDunk.TriggerDunkSequence());
        //}

        // player sometimes gets stuck in inair dunk state
        //if (gameObject.CompareTag("autoPlayerHitbox")
        //    && other.CompareTag("ground")
        //    && autoPlayerController.currentState == autoPlayerController.inAirDunkState)
        //{
        //    autoPlayerController.SetPlayerAnim("jump", false);
        //}

        // if collsion between hitbox, vehicle, knocked down
        if (matchRules != null
        && hasShooterController
        && gameObject.CompareTag("autoPlayerHitbox")
        && (other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox") || other.CompareTag("playerAttackBox"))
        && !autoPlayerController.KnockedDown
        && !autoPlayerController.TakeDamage
        && (matchRules.EnemiesEnabled
        || matchRules.TrafficEnabled
        || matchRules.ObstaclesEnabled
        || other.transform.root.name.Contains("snake")
        || matchRules.SniperEnabled))
        {
            combatHitHandler.HandleAttackBoxHit(other);
        }
    }

    // ==================== IPlayerCollisionHitHost ====================
    // Explicit implementation: these exist only for PlayerCollisionHitHandler to reach this wrapper's
    // resolved CPU actor state, so they stay off the ordinary public surface. Never invoked unless
    // hasShooterController is true - see OnTriggerEnter's guard above.

    bool IPlayerCollisionHitHost.CanBeKnockedDown => playerCanBeKnockedDown;

    bool IPlayerCollisionHitHost.IsBlocking => autoPlayerController.currentState == autoPlayerController.blockState;

    float IPlayerCollisionHitHost.Luck => autoPlayerController.CharacterProfile.Luck;

    PlayerHealth IPlayerCollisionHitHost.Health => playerHealth;

    void IPlayerCollisionHitHost.RunCoroutine(IEnumerator routine) => StartCoroutine(routine);

    void IPlayerCollisionHitHost.NotifyEnemyAttackBoxHit(IAttackBoxHitInfo hit)
    {
        // AutoPlayerCollisions never read IsKilledOnIdle - human-only policy, unchanged by this slice.
    }

    void IPlayerCollisionHitHost.ApplyDisintegrateReaction()
    {
        autoPlayerController.TakeDamage = false;
        autoPlayerController.KnockedDown = false;
        autoPlayerController.hasBasketball = false;
        autoPlayerController.Disintegrated = true;
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
    }

    void IPlayerCollisionHitHost.ApplyDamageReaction()
    {
        autoPlayerController.TakeDamage = true;
        autoPlayerController.KnockedDown = false;
        autoPlayerController.hasBasketball = false;
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
    }

    void IPlayerCollisionHitHost.ApplyKnockdownReaction()
    {
        autoPlayerController.TakeDamage = false;
        autoPlayerController.KnockedDown = true;
        autoPlayerController.hasBasketball = false;
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
    }

    void IPlayerCollisionHitHost.ApplyRakeReaction(Collider rakeSource)
    {
        rakeSource.transform.parent.GetComponentInChildren<Animator>().Play("attack");
        autoPlayerController.TakeDamage = true;
        autoPlayerController.KnockedDown = false;
        autoPlayerController.hasBasketball = false;
        //StartCoroutine(playerState.PlayerFreezeForXSeconds(2f));
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
    }
}
