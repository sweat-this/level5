using System;
using System.Collections;
using UnityEngine;
using Level5.Core.Match;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField]
    float health = 0;
    [SerializeField]
    int maxHealth = 100;
    [SerializeField]
    float block;
    [SerializeField]
    int maxBlock = 30;
    [SerializeField]
    float special;
    [SerializeField]
    int maxSpecial = 100;
    [SerializeField]
    float regenerateBlockRate;
    [SerializeField]
    float regenerateHealthRate;
    [SerializeField]
    float regenerateSpecialRate;
    [SerializeField]
    float regenerateTimeDelay;
    [SerializeField]
    bool isDead = false;

    bool regenerateBlock = false;
    bool regenerateSpecial = false;
    bool regenerateHealth = false;

    public event Action OnHealthChanged;
    public event Action OnBlockChanged;
    public event Action OnSpecialChanged;
    public event Action OnDied;

    private void Awake()
    {
        Health = maxHealth;
        Block = maxBlock;
        Special = maxSpecial;
    }

    private void Start()
    {
        // regenerate rate is +1 per interval
        // rate of 0.4f is equal to +1 every 0.5 second or +25 in 10 secs
        // rate of 1f is equal to +1 every 1 second or +100 in 100 seconds (1 min 40 secs)
        // rate of 0.04f is equal to +1 every 0.04 second or +100 in 4 seconds
        regenerateBlockRate = 0.5f;
        regenerateHealthRate = 2f;
        regenerateSpecialRate = 0.04f;
    }

    private void Update()
    {
        if (health <= 0 && !IsDead)
        {
            IsDead = true;
        }

        if (health > maxHealth)
        {
            Health = maxHealth;
        }

        if (MatchRuntime.Rules.EnemiesEnabled
            || MatchRuntime.Rules.SniperEnabled
            || MatchRuntime.Rules.Sniper == SniperMode.Bullet
            || MatchRuntime.Rules.Sniper == SniperMode.Laser
            || MatchRuntime.Rules.ObstaclesEnabled)
        {
            if (block < MaxBlock && !regenerateBlock)
            {
                StartCoroutine(RegenerateBlock());
            }

            if (health < maxHealth && !IsDead && !regenerateHealth)
            {
                StartCoroutine(RegenerateHealth());
            }

            if (special < maxSpecial && !regenerateSpecial)
            {
                StartCoroutine(RegenerateSpecial());
            }
        }
    }

    IEnumerator RegenerateSpecial()
    {
        regenerateSpecial = true;
        yield return new WaitForSeconds(regenerateSpecialRate);
        Special += 1f;
        regenerateSpecial = false;
    }

    IEnumerator RegenerateBlock()
    {
        regenerateBlock = true;
        yield return new WaitForSeconds(regenerateBlockRate);
        Block += 1f;
        regenerateBlock = false;
    }

    IEnumerator RegenerateHealth()
    {
        regenerateHealth = true;
        yield return new WaitForSeconds(regenerateHealthRate);
        Health += 1f;
        regenerateHealth = false;
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

        Health -= damageInfo.Amount;
        return IsDead;
    }

    public void Heal(float amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        Health += amount;
    }

    public void SpendBlock(float amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Block -= amount;
    }

    public void SpendSpecial(float amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Special -= amount;
    }

    public float Health
    {
        get => health;
        set
        {
            float clampedHealth = Mathf.Clamp(value, 0, maxHealth);
            if (Mathf.Approximately(health, clampedHealth))
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

    public float Block
    {
        get => block;
        set
        {
            float clampedBlock = Mathf.Clamp(value, 0, maxBlock);
            if (Mathf.Approximately(block, clampedBlock))
            {
                return;
            }

            block = clampedBlock;
            OnBlockChanged?.Invoke();
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

    public int MaxHealth { get => maxHealth; set => maxHealth = value; }
    public int MaxBlock { get => maxBlock; set => maxBlock = value; }

    public float Special
    {
        get => special;
        set
        {
            float clampedSpecial = Mathf.Clamp(value, 0, maxSpecial);
            if (Mathf.Approximately(special, clampedSpecial))
            {
                return;
            }

            special = clampedSpecial;
            OnSpecialChanged?.Invoke();
        }
    }

    public int MaxSpecial { get => maxSpecial; set => maxSpecial = value; }
    public float CurrentHealth => Health;
    public float CurrentMaxHealth => MaxHealth;
}
