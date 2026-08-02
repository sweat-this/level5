
using System.Collections;
using UnityEngine;

public class PlayerCollisions : MonoBehaviour
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
    bool locked = false;

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
            GameLevelManager.instance.Player1.transform.position 
                = GameLevelManager.instance.PlayerSpawnLocation.transform.position;
        }

        if (gameObject.CompareTag("playerHitbox")
            && !GameOptions.battleRoyalEnabled
            && !GameOptions.cageMatchEnabled
            && !GameOptions.EnemiesOnlyEnabled
            && playerController.InAir
            && playerController.currentState != playerController.dunkState
            && (other.name.Equals("dunk_position_left") || other.name.Equals("dunk_position_right")))
        {
            StartCoroutine(GameLevelManager.instance.PlayerController1.PlayerDunk.TriggerDunkSequence());
        }
        // player sometimes gets stuck in inair dunk state
        if (gameObject.CompareTag("playerHitbox")
            && other.CompareTag("ground")
            && playerController.currentState == playerController.inAirDunkState)
        {
            playerController.SetPlayerAnim("jump", false);
        }

        // if collsion between hitbox, vehicle, knocked down
        if (gameObject.CompareTag("playerHitbox")
        && (other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox") || other.CompareTag("playerAttackBox"))
        && !playerController.KnockedDown
        && !playerController.TakeDamage
        && (GameOptions.enemiesEnabled
        || GameOptions.trafficEnabled
        || GameOptions.obstaclesEnabled
        || other.transform.root.name.Contains("snake")
        || GameOptions.sniperEnabled
        || GameOptions.sniperEnabledBullet
        || GameOptions.sniperEnabledLaser
        || other.transform.root.name.Contains("projectile_bullet_instantkill_enemy"))
        // roll for evade attack chance
        && !rollForPlayerEvadeAttackChance(playerController.CharacterProfile.Luck)
        && !locked)
        {
            locked = true;
            EnemyAttackBox enemyAttackBox = null;
            PlayerAttackBox playerAttackBox = null;
            int damage = 0;
            bool isKnockdown = false;
            bool isRake = false;
            bool isDisintegrate = false;
            // get attack box player/enemy
            if (other.CompareTag("playerAttackBox"))
            {
                playerAttackBox = other.GetComponent<PlayerAttackBox>();
            }
            if (other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox"))
            {
                enemyAttackBox = other.GetComponent<EnemyAttackBox>();
            }
            // check if player attack
            if (enemyAttackBox != null)
            {
                isRake = enemyAttackBox.isRake;
                damage = enemyAttackBox.attackDamage;
                isKnockdown = enemyAttackBox.knockDownAttack;
                isDisintegrate = enemyAttackBox.disintegrateAttack;
                if (enemyAttackBox.isKilledOnIdle)
                {
                    GameRules.instance.killedOnIdle = true;
                }
                if (isDisintegrate)
                {
                    locked = true;
                    playerDisintegrated();
                }

            }
            //check if enemy attack
            if (playerAttackBox != null)
            {
                damage = playerAttackBox.attackDamage;
                isKnockdown = playerAttackBox.knockDownAttack;
                isDisintegrate = playerAttackBox.disintegrateAttack;
                if (isDisintegrate)
                {
                    locked = true;
                    playerDisintegrated();
                }
            }

            // player is not blocking
            if (playerController.CurrentState != playerController.BlockState && !isDisintegrate)
            {
                locked = true;
                playerHealth.TakeDamage(damage);
                if (PlayerHealthBar.instance != null && PlayerHealthBar.instance.IsTracking(playerHealth))
                {
                    StartCoroutine(PlayerHealthBar.instance.DisplayDamageTakenValue(damage));
                }

                if (playerHealth.IsDead)
                {
                    locked = false;
                    return;
                }

                // player can be knocked down and other
                if (playerCanBeKnockedDown && isKnockdown)
                {
                    playerKnockedDown();
                }
                else
                {
                    playerTakeDamage();
                    // if stepped on rake
                    if (isRake)
                    {
                        Debug.Log("stepped on rake");
                        playerStepOnRake(other);
                    }
                }
            }
            // player is blocking
            if (playerController.CurrentState == playerController.BlockState)
            {
                // blocking play sound
                // block meter goes down
                SFXBB.instance.playSFX(SFXBB.instance.blocked);
                if (enemyAttackBox != null)
                {
                    playerHealth.SpendBlock(enemyAttackBox.attackDamage);
                }
                locked = false;
            }
            locked = false;
        }
    }

    // player has a chance to evade attack based on character profile's luck value
    bool rollForPlayerEvadeAttackChance(float maxPercent)
    {
        float percent = UnityEngine.Random.Range(0f, 100f);
        if (percent < maxPercent)
        {
            if (PlayerHealthBar.instance != null)
            {
                StartCoroutine(PlayerHealthBar.instance.DisplayCustomMessageOnDamageDisplay("dodged"));
            }
            return true;
        }

        return false;
    }

    void playerDisintegrated()
    {
        playerController.TakeDamage = false;
        playerController.KnockedDown = false;
        playerController.hasBasketball = false;
        playerController.Disintegrated = true;
        playerController.SetPlayerAnim("hasBasketball", false);
    }

    void playerTakeDamage()
    {
        playerController.TakeDamage = true;
        playerController.KnockedDown = false;
        playerController.hasBasketball = false;
        playerController.SetPlayerAnim("hasBasketball", false);
    }
    void playerKnockedDown()
    {
        playerController.TakeDamage = false;
        playerController.KnockedDown = true;
        playerController.hasBasketball = false;
        playerController.SetPlayerAnim("hasBasketball", false);
    }

    void playerStepOnRake(Collider other)
    {
        other.transform.parent.GetComponentInChildren<Animator>().Play("attack");
        playerController.TakeDamage = true;
        playerController.KnockedDown = false;
        playerController.hasBasketball = false;
        //StartCoroutine(playerState.PlayerFreezeForXSeconds(2f));             
        playerController.SetPlayerAnim("hasBasketball", false);
    }
}
