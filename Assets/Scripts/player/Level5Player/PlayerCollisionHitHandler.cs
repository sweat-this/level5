using UnityEngine;

/// <summary>
/// AUD-012 Phase 3 Slice 70: the combat-hit mechanics <c>PlayerCollisions</c> and
/// <c>AutoPlayerCollisions</c> used to carry as byte-identical private methods on
/// <c>OnTriggerEnter</c>, extracted into one implementation both wrappers now call once they have
/// already decided (their own tag/mode/state eligibility check) that a collision is a combat hit worth
/// processing.
///
/// Deliberately a plain object, not a <c>MonoBehaviour</c>, the same shape as
/// <see cref="PlayerDamageReactions"/>/<see cref="AutoPlayerDamageReactions"/>: one instance per wrapper
/// component, constructed once and held as a field, reaching its host only through
/// <see cref="IPlayerCollisionHitHost"/> - never a concrete <c>PlayerController</c>/
/// <c>AutoPlayerController</c>. <see cref="locked"/> is this handler's own re-entrancy guard, replacing
/// the two wrappers' former identical <c>bool locked</c> fields.
///
/// This is a line-for-line transplant of the original method bodies (evade-roll-then-locked-check
/// ordering, the redundant repeated <c>locked = true</c> assignments, the two independent non-`else`
/// blocking/non-blocking branches, the unconditional trailing <c>locked = false</c>) - preserved
/// exactly rather than tidied, since this handler's whole purpose is to stop being two copies of the
/// same fragile sequence, not to change what that sequence does.
/// </summary>
public sealed class PlayerCollisionHitHandler
{
    private readonly IPlayerCollisionHitHost host;
    private bool locked;

    public PlayerCollisionHitHandler(IPlayerCollisionHitHost host)
    {
        this.host = host;
    }

    /// <summary>
    /// Runs the shared combat-hit sequence against <paramref name="other"/>. Callers must have already
    /// established this collision is an eligible combat hit for their own role (correct hitbox tag,
    /// <paramref name="other"/> tagged one of the attack-box tags, match rules/mode enabling it, actor
    /// not already reacting) - this method does not re-check any of that.
    /// </summary>
    public void HandleAttackBoxHit(Collider other)
    {
        if (RollForEvadeChance(host.Luck))
        {
            return;
        }

        if (locked)
        {
            return;
        }

        locked = true;
        IAttackBoxHitInfo enemyAttackBoxHit = null;
        IAttackBoxHitInfo playerAttackBoxHit = null;
        int damage = 0;
        bool isKnockdown = false;
        bool isRake = false;
        bool isDisintegrate = false;

        // get attack box player/enemy
        if (other.CompareTag("playerAttackBox"))
        {
            playerAttackBoxHit = other.GetComponent<IAttackBoxHitInfo>();
        }
        if (other.CompareTag("enemyAttackBox") || other.CompareTag("obstacleAttackBox"))
        {
            enemyAttackBoxHit = other.GetComponent<IAttackBoxHitInfo>();
        }

        // check if player attack
        if (enemyAttackBoxHit != null)
        {
            isRake = enemyAttackBoxHit.IsRake;
            damage = enemyAttackBoxHit.AttackDamage;
            isKnockdown = enemyAttackBoxHit.KnockDownAttack;
            isDisintegrate = enemyAttackBoxHit.DisintegrateAttack;
            host.NotifyEnemyAttackBoxHit(enemyAttackBoxHit);
            if (isDisintegrate)
            {
                locked = true;
                host.ApplyDisintegrateReaction();
            }
        }
        // check if enemy attack
        if (playerAttackBoxHit != null)
        {
            damage = playerAttackBoxHit.AttackDamage;
            isKnockdown = playerAttackBoxHit.KnockDownAttack;
            isDisintegrate = playerAttackBoxHit.DisintegrateAttack;
            if (isDisintegrate)
            {
                locked = true;
                host.ApplyDisintegrateReaction();
            }
        }

        // player is not blocking
        if (!host.IsBlocking && !isDisintegrate)
        {
            locked = true;
            host.Health.TakeDamage(damage);
            if (PlayerHealthBar.instance != null && PlayerHealthBar.instance.IsTracking(host.Health))
            {
                host.RunCoroutine(PlayerHealthBar.instance.DisplayDamageTakenValue(damage));
            }

            if (host.Health.IsDead)
            {
                locked = false;
                return;
            }

            // player can be knocked down and other
            if (host.CanBeKnockedDown && isKnockdown)
            {
                host.ApplyKnockdownReaction();
            }
            else
            {
                host.ApplyDamageReaction();
                // if stepped on rake
                if (isRake)
                {
                    Debug.Log("stepped on rake");
                    host.ApplyRakeReaction(other);
                }
            }
        }
        // player is blocking
        if (host.IsBlocking)
        {
            // blocking play sound
            // block meter goes down
            SFXBB.instance.playSFX(SFXBB.instance.blocked);
            if (enemyAttackBoxHit != null)
            {
                host.Health.SpendBlock(enemyAttackBoxHit.AttackDamage);
            }
            locked = false;
        }
        locked = false;
    }

    // player has a chance to evade attack based on character profile's luck value
    private bool RollForEvadeChance(float maxPercent)
    {
        float percent = Random.Range(0f, 100f);
        if (percent < maxPercent)
        {
            if (PlayerHealthBar.instance != null)
            {
                host.RunCoroutine(PlayerHealthBar.instance.DisplayCustomMessageOnDamageDisplay("dodged"));
            }
            return true;
        }

        return false;
    }
}
