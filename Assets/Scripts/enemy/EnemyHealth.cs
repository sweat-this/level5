using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField]
    int health;
    [SerializeField]
    int maxEnemyHealth;
    [SerializeField]
    EnemyController enemyController;
    bool isDead;
    [SerializeField]
    bool isMinion;
    [SerializeField]
    bool isBoss;

    public event Action OnHealthChanged;
    public event Action OnDied;

    // the inspector-configured max, captured before any spawn reset rewrites it. ResetForSpawn
    // runs on every OnEnable (so on every pool reuse), and the hardcore bonus below is applied
    // each time - it has to scale a stable base or it compounds across respawns.
    int configuredMaxEnemyHealth;

    private void Awake()
    {
        enemyController = gameObject.transform.root.GetComponent<EnemyController>();
        configuredMaxEnemyHealth = maxEnemyHealth;
    }

    private void OnEnable()
    {
        ResetForSpawn();
    }

    const int DefaultSpawnHealth = 50;
    const int BossSpawnHealth = 150;

    public void ResetForSpawn()
    {
        // this whole body used to be wrapped in `if (enemyController != null)`, so a missing
        // controller skipped the reset silently. With no controller AND no inspector-set health,
        // maxEnemyHealth stayed 0 - and the Health setter then clamps every write to zero,
        // producing an enemy that can neither take damage nor die.
        if (enemyController == null)
        {
            Debug.LogError(
                "EnemyHealth on " + name + " found no EnemyController on its hierarchy root, so it "
                + "cannot tell a boss from a minion. Falling back to its configured max health.",
                this);

            // respect a health value configured on the prefab; only the zero case is the bug
            maxEnemyHealth = configuredMaxEnemyHealth > 0
                ? configuredMaxEnemyHealth
                : DefaultSpawnHealth;
        }
        else
        {
            // minion is checked before boss, matching the original precedence for an actor
            // that somehow carries both flags
            bool isBossSpawn = !enemyController.IsMinion && enemyController.IsBoss;
            maxEnemyHealth = isBossSpawn ? BossSpawnHealth : DefaultSpawnHealth;
        }

        if (GameOptions.hardcoreModeEnabled)
        {
            maxEnemyHealth += (maxEnemyHealth / 4);
        }

        isDead = false;
        health = maxEnemyHealth;
        OnHealthChanged?.Invoke();
    }

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
            int clampedHealth = Mathf.Clamp(value, 0, maxEnemyHealth);
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

    public int MaxEnemyHealth
    {
        get => maxEnemyHealth;
        set
        {
            maxEnemyHealth = Mathf.Max(0, value);
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
    public float CurrentMaxHealth => MaxEnemyHealth;
}
