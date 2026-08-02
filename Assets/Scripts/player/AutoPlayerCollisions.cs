
using System.Collections;
using UnityEngine;

public class AutoPlayerCollisions : MonoBehaviour
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

        //if (gameObject.CompareTag("playerHitbox")
        //    && (!GameOptions.battleRoyalEnabled || !GameOptions.cageMatchEnabled || !GameOptions.EnemiesOnlyEnabled)
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
        if (gameObject.CompareTag("autoPlayerHitbox")
        && (other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox") || other.CompareTag("playerAttackBox"))
        && !autoPlayerController.KnockedDown
        && !autoPlayerController.TakeDamage
        && (GameOptions.enemiesEnabled
        || GameOptions.trafficEnabled
        || GameOptions.obstaclesEnabled
        || other.transform.root.name.Contains("snake")
        || GameOptions.sniperEnabled)
        // roll for evade attack chance
        && !rollForPlayerEvadeAttackChance(autoPlayerController.CharacterProfile.Luck)
        && !locked)
        {
            locked = true;
            EnemyAttackBox enemyAttackBox = null;
            PlayerAttackBox playerAttackBox = null;
            int damage = 0;
            bool isKnockdown = false;
            bool isRake = false;
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
            }
            //check if enemy attack
            if (playerAttackBox != null)
            {
                damage = playerAttackBox.attackDamage;
                isKnockdown = playerAttackBox.knockDownAttack;
            }

            // player is not blocking
            if (autoPlayerController.currentState != autoPlayerController.blockState)
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
            if (autoPlayerController.currentState == autoPlayerController.blockState)
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

    void playerTakeDamage()
    {
        autoPlayerController.TakeDamage = true;
        autoPlayerController.KnockedDown = false;
        autoPlayerController.hasBasketball = false;
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
    }
    void playerKnockedDown()
    {
        autoPlayerController.TakeDamage = false;
        autoPlayerController.KnockedDown = true;
        autoPlayerController.hasBasketball = false;
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
    }

    void playerStepOnRake(Collider other)
    {
        other.transform.parent.GetComponentInChildren<Animator>().Play("attack");
        autoPlayerController.TakeDamage = true;
        autoPlayerController.KnockedDown = false;
        autoPlayerController.hasBasketball = false;
        //StartCoroutine(playerState.PlayerFreezeForXSeconds(2f));             
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
    }
}
