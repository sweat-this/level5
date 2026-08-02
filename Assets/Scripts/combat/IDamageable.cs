using System;

public interface IDamageable
{
    event Action OnHealthChanged;
    event Action OnDied;

    float CurrentHealth { get; }
    float CurrentMaxHealth { get; }
    bool IsDead { get; }

    bool ApplyDamage(DamageInfo damageInfo);
    void Heal(float amount);
}
