using Assets.Scripts.Utility;
using UnityEngine;

public class EnemyCollisions : MonoBehaviour
{
    [SerializeField]
    EnemyController enemyController;
    [SerializeField]
    EnemyHealthBar enemyHealthBar;

    [SerializeField]
    EnemyHealth enemyHealth;
    [SerializeField]
    int luck;

    private void Start()
    {
        enemyController = gameObject.transform.root.GetComponent<EnemyController>();
        enemyHealth = GetComponent<EnemyHealth>();
        enemyHealthBar = transform.parent != null
            ? transform.parent.GetComponentInChildren<EnemyHealthBar>()
            : null;

        if (enemyController == null)
        {
            Debug.LogError(
                "EnemyCollisions on " + name + " found no EnemyController on its hierarchy root. Disabling.",
                this);
            enabled = false;
            return;
        }

        if (luck == 0)
        {
            if (enemyController.IsBoss) { luck = 10; };
            if (enemyController.IsMinion) { luck = 5; };
        }
    }

    private void OnTriggerEnter(Collider other)
    {

        // if this object is enemy hitbox and (player attack box or enemy attack box)
        if (gameObject.CompareTag("enemyHitbox")
            // NOT enemy projectile
            && !other.transform.root.CompareTag("enemyProjectile")
            && (other.CompareTag("playerAttackBox") || other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox"))
            && enemyHealth != null
            && enemyHealthBar != null)
        {
            PlayerAttackBox playerAttackBox = null;
            EnemyAttackBox enemyAttackBox = null; ;

            // check for enemy dodge
            bool enemyDodge = false;
            if (UtilityFunctions.rollForCriticalInt(luck))
            {
                enemyDodge = true;
                StartCoroutine(enemyHealthBar.DisplayCustomMessageOnDamageDisplay("dodged"));
            }
            if (!enemyDodge)
            {
                bool enemyKilledByHit = false;
                if (other.CompareTag("playerAttackBox"))
                {
                    playerAttackBox = other.GetComponent<PlayerAttackBox>();
                }
                if (other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox"))
                {
                    enemyAttackBox = other.GetComponent<EnemyAttackBox>();
                }

                bool isRake = false;
                string damageDisplayMessage;
                // ------------------ player attacks enemy -----------------------
                // player attack. reduce health
                if (playerAttackBox != null
                    && !enemyController.stateKnockDown)
                {
                    damageDisplayMessage = "-" + playerAttackBox.attackDamage;
                    //if (UtilityFunctions.rollForCriticalInt(luck))
                    //{
                    //    enemyHealth.Health -= playerAttackBox.attackDamage * 2;
                    //    damageDisplayMessage = "2x damage -" + playerAttackBox.attackDamage;
                    //}
                    //else
                    //{
                    //    enemyHealth.Health -= playerAttackBox.attackDamage;
                    //}
                    enemyKilledByHit = ApplyDamage(playerAttackBox.attackDamage, other, "playerAttack");
                    StartCoroutine(enemyHealthBar.DisplayCustomMessageOnDamageDisplay(damageDisplayMessage));
                }
                // ------------------ enemy attacks enemy -----------------------
                // enemy attack. reduce damage to %50
                if (enemyAttackBox != null
                    && enemyHealth != null
                    && !enemyController.stateKnockDown)
                {
                    isRake = enemyAttackBox.isRake;
                    // if rake/obstacle 100% damage
                    if (isRake)
                    {
                        damageDisplayMessage = "-" + enemyAttackBox.attackDamage;
                        enemyKilledByHit = ApplyDamage(enemyAttackBox.attackDamage, other, "obstacleAttack");
                    }
                    // if enemy 50% damage
                    else
                    {
                        damageDisplayMessage = "-" + enemyAttackBox.attackDamage * 0.5f;
                        enemyKilledByHit = ApplyDamage(enemyAttackBox.attackDamage / 2, other, "enemyAttack");
                    }
                    StartCoroutine(enemyHealthBar.DisplayCustomMessageOnDamageDisplay(damageDisplayMessage));
                }

                // check if enemy dead + enemy fails to roll critical to dodge
                if (!enemyKilledByHit && enemyHealth.Health > 0)
                {
                    // player knock down attack
                    if (playerAttackBox != null
                        && playerAttackBox.knockDownAttack
                        && !playerAttackBox.disintegrateAttack)
                    {
                        enemyKnockedDown();
                    }
                    // if !knock down + is disintegrate
                    else if (playerAttackBox != null
                        && !playerAttackBox.knockDownAttack
                        && playerAttackBox.disintegrateAttack)
                    {
                        enemyDisintegrated();
                    }
                    // enemy attack / friendly fire /vehicle
                    else if (enemyAttackBox != null
                        && enemyAttackBox.knockDownAttack
                        && !enemyAttackBox.disintegrateAttack)
                    {
                        enemyKnockedDown();
                    }
                    // if !knock down + is disintegrate
                    else if (enemyAttackBox != null
                        && !enemyAttackBox.knockDownAttack
                        && enemyAttackBox.disintegrateAttack)
                    {
                        enemyDisintegrated();
                    }
                    else
                    {
                        if (!isRake)
                        {
                            enemyTakeDamage();
                        }
                        if (isRake)
                        {
                            enemyStepOnRake(other);
                        }
                    }
                }

                // else enemy is dead
                if (enemyKilledByHit)
                {
                    enemyIsDead(playerAttackBox);
                }
            }
        }
    }

    private bool ApplyDamage(float amount, Collider source, string damageType)
    {
        bool wasAlive = !enemyHealth.IsDead;
        return wasAlive && enemyHealth.ApplyDamage(
            new DamageInfo(amount, source.transform.root.gameObject, source.transform.position, Vector3.zero, damageType));
    }

    // AUD-001: this method used to own a second, slightly different copy of the enemy-death side
    // effects (heal, kill credit, critical flourish, death coroutine). EnemyController.HandleDeath
    // is the single owner now; this only supplies the attacker so the kill is credited to whoever
    // landed the blow rather than to whichever basketball owns the BasketBall.instance static.
    private void enemyIsDead(PlayerAttackBox playerAttackBox)
    {
        // no player attack box means this was friendly fire from another enemy: the enemy still
        // dies, but nobody is rewarded for it - the behaviour this path always had
        bool killedByPlayer = playerAttackBox != null;
        enemyController.HandleDeath(
            killedByPlayer ? playerAttackBox.gameObject : null,
            killedByPlayer);
    }

    private void enemyDisintegrated()
    {
        StartCoroutine(enemyController.disintegrated());
    }

    void enemyTakeDamage()
    {
        StartCoroutine(enemyController.takeDamage());
    }

    void enemyStepOnRake(Collider other)
    {
        Animator rakeAnimator = other.transform.parent != null
            ? other.transform.parent.GetComponentInChildren<Animator>()
            : null;
        if (rakeAnimator != null)
        {
            rakeAnimator.Play("attack");
        }

        StartCoroutine(enemyController.takeDamage());
    }

    void enemyKnockedDown()
    {
        StartCoroutine(enemyController.knockedDown());
    }
}
