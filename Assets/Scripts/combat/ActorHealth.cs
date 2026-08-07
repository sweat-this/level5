using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// The health, damage, and death behaviour shared by every non-player actor.
///
/// AUD-004: `EnemyHealth` and `BodyGuardHealth` were byte-for-byte copies of each other from
/// `TakeDamage` down - the same clamping setter, the same death latch, the same event raises - and
/// had already drifted in small ways (the bodyguard's max-health property was still literally named
/// `MaxEnemyHealth`, and the two spelled the hardcore bonus differently). Any fix to damage,
/// clamping, or death ordering had to be applied twice or the two actor types silently diverged.
///
/// Subclasses own only what genuinely differs: how max health is chosen, and when.
/// `PlayerHealth` deliberately does not derive from this - it carries block, special, and respawn
/// state that no AI actor has - but it implements the same <see cref="IDamageable"/> contract and
/// uses the same `Health` / `MaxHealth` / `IsDead` vocabulary.
/// </summary>
public abstract class ActorHealth : MonoBehaviour, IDamageable
{
    [SerializeField]
    protected int health;

    // FormerlySerializedAs keeps the value already serialized on enemy and bodyguard prefabs when
    // the two differently-named fields collapsed into this one.
    [SerializeField]
    [FormerlySerializedAs("maxEnemyHealth")]
    [FormerlySerializedAs("maxBodyGuardHealth")]
    protected int maxHealth;

    private bool isDead;

    public event Action OnHealthChanged;
    public event Action OnDied;

    public bool TakeDamage(float damage)
    {
        return ApplyDamage(new DamageInfo(damage));
    }

    public bool ApplyDamage(DamageInfo damageInfo)
    {
        if (damageInfo.Amount <= 0 || IsDead)
        {
            return IsDead;
        }

        Health -= Mathf.CeilToInt(damageInfo.Amount);
        return IsDead;
    }

    public void Heal(float amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        Health += Mathf.CeilToInt(amount);
    }

    public int Health
    {
        get => health;
        set
        {
            int clampedHealth = Mathf.Clamp(value, 0, maxHealth);
            if (health == clampedHealth)
            {
                return;
            }

            health = clampedHealth;
            OnHealthChanged?.Invoke();

            if (health <= 0 && !IsDead)
            {
                IsDead = true;
            }
        }
    }

    public int MaxHealth
    {
        get => maxHealth;
        set
        {
            maxHealth = Mathf.Max(0, value);
            Health = health;
        }
    }

    public bool IsDead
    {
        get => isDead;
        set
        {
            if (isDead == value)
            {
                return;
            }

            isDead = value;
            if (isDead && health > 0)
            {
                health = 0;
                OnHealthChanged?.Invoke();
            }

            if (isDead)
            {
                OnDied?.Invoke();
            }
        }
    }

    public float CurrentHealth => Health;
    public float CurrentMaxHealth => MaxHealth;

    /// <summary>
    /// Applies the hardcore bonus and refills to full. Used by the spawn/initialization paths,
    /// which are the only thing the two subclasses still implement separately.
    /// </summary>
    protected void ResetToMaxHealth(int configuredMax)
    {
        maxHealth = Mathf.Max(0, configuredMax);
        if (GameOptions.hardcoreModeEnabled)
        {
            maxHealth += maxHealth / 4;
        }

        isDead = false;
        health = maxHealth;
        OnHealthChanged?.Invoke();
    }
}
