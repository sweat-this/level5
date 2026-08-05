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

    private void Awake()
    {
        enemyController = gameObject.transform.root.GetComponent<EnemyController>();
    }

    private void OnEnable()
    {
        ResetForSpawn();
    }

    public void ResetForSpawn()
    {
        if (enemyController != null)
        {
            if (enemyController.IsMinion)
            {
                maxEnemyHealth = 50;
            }
            else if (enemyController.IsBoss)
            {
                maxEnemyHealth = 150;
            }
            else
            {
                maxEnemyHealth = 50;
            }
            if (GameOptions.hardcoreModeEnabled)
            {
                maxEnemyHealth += (maxEnemyHealth / 4);
            }

            isDead = false;
            health = maxEnemyHealth;
            OnHealthChanged?.Invoke();
        }
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
