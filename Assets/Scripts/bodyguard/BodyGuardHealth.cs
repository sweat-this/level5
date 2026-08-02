using System;
using UnityEngine;

public class BodyGuardHealth : MonoBehaviour, IDamageable
{
    [SerializeField]
    int health;
    [SerializeField]
    int maxBodyGuardHealth;
    [SerializeField]
    BodyGuardController bodyGuardController;
    bool isDead;
    [SerializeField]
    bool isMinion;
    [SerializeField]
    bool isBoss;

    public event Action OnHealthChanged;
    public event Action OnDied;

    public bool IsBoss { get => isBoss; set => isBoss = value; }

    private void Start()
    {
        // default
        //if (isMinion)
        //{
        //    maxBodyGuardHealth = 50;
        //}
        //else if (isBoss)
        //{
        //    maxBodyGuardHealth = 150;
        //}
        //else
        //{
        //    maxBodyGuardHealth = 50;
        //}
        maxBodyGuardHealth = 100;

        if (GameOptions.hardcoreModeEnabled)
        {
            maxBodyGuardHealth += Mathf.FloorToInt(maxBodyGuardHealth / 4);
        }
        Health = maxBodyGuardHealth;
        bodyGuardController = transform.parent.GetComponent<BodyGuardController>();
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
            int clampedHealth = Mathf.Clamp(value, 0, maxBodyGuardHealth);
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
        get => maxBodyGuardHealth;
        set
        {
            maxBodyGuardHealth = Mathf.Max(0, value);
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
