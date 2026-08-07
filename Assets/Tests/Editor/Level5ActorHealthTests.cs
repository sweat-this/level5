using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-004: these assertions used to have no home, because the behaviour they cover was duplicated
/// in `EnemyHealth` and `BodyGuardHealth` and testing it meant testing it twice. Now that both
/// derive from <see cref="ActorHealth"/> there is one implementation to pin down.
///
/// The clamping setter, the death latch, and the event ordering are the parts that any future fix
/// is most likely to disturb.
/// </summary>
public class Level5ActorHealthTests
{
    /// <summary>Minimal concrete subclass - ActorHealth is abstract by design.</summary>
    private sealed class TestActorHealth : ActorHealth
    {
        public void Configure(int max)
        {
            ResetToMaxHealth(max);
        }
    }

    private GameObject host;
    private TestActorHealth actorHealth;
    private bool previousHardcore;

    [SetUp]
    public void SetUp()
    {
        previousHardcore = GameOptions.hardcoreModeEnabled;
        GameOptions.hardcoreModeEnabled = false;

        host = new GameObject("actor-health-test");
        actorHealth = host.AddComponent<TestActorHealth>();
        actorHealth.Configure(100);
    }

    [TearDown]
    public void TearDown()
    {
        GameOptions.hardcoreModeEnabled = previousHardcore;
        if (host != null)
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void ResetStartsAtFullHealthAndAlive()
    {
        Assert.That(actorHealth.MaxHealth, Is.EqualTo(100));
        Assert.That(actorHealth.Health, Is.EqualTo(100));
        Assert.That(actorHealth.IsDead, Is.False);
    }

    [Test]
    public void HardcoreAddsAQuarterOfMaxHealth()
    {
        GameOptions.hardcoreModeEnabled = true;
        actorHealth.Configure(100);

        Assert.That(actorHealth.MaxHealth, Is.EqualTo(125));
        Assert.That(actorHealth.Health, Is.EqualTo(125));
    }

    [Test]
    public void DamageIsClampedAtZeroAndLatchesDeath()
    {
        bool died = actorHealth.ApplyDamage(new DamageInfo(40));
        Assert.That(died, Is.False);
        Assert.That(actorHealth.Health, Is.EqualTo(60));

        died = actorHealth.ApplyDamage(new DamageInfo(500));
        Assert.That(died, Is.True);
        Assert.That(actorHealth.Health, Is.EqualTo(0), "health must clamp at 0, never go negative");
        Assert.That(actorHealth.IsDead, Is.True);
    }

    [Test]
    public void DamageAndHealingAreIgnoredOnceDead()
    {
        actorHealth.ApplyDamage(new DamageInfo(1000));
        Assert.That(actorHealth.IsDead, Is.True);

        actorHealth.Heal(50);
        Assert.That(actorHealth.Health, Is.EqualTo(0), "a dead actor must not be healed back");

        Assert.That(actorHealth.ApplyDamage(new DamageInfo(10)), Is.True);
        Assert.That(actorHealth.Health, Is.EqualTo(0));
    }

    [Test]
    public void HealingIsClampedToMaxHealth()
    {
        actorHealth.ApplyDamage(new DamageInfo(30));
        actorHealth.Heal(1000);

        Assert.That(actorHealth.Health, Is.EqualTo(actorHealth.MaxHealth));
    }

    [Test]
    public void NonPositiveAmountsAreNoOps()
    {
        actorHealth.ApplyDamage(new DamageInfo(0));
        actorHealth.ApplyDamage(new DamageInfo(-25));
        actorHealth.Heal(-25);

        Assert.That(actorHealth.Health, Is.EqualTo(100));
        Assert.That(actorHealth.IsDead, Is.False);
    }

    [Test]
    public void DiedIsRaisedExactlyOnce()
    {
        int diedCount = 0;
        actorHealth.OnDied += () => diedCount++;

        actorHealth.ApplyDamage(new DamageInfo(1000));
        actorHealth.ApplyDamage(new DamageInfo(1000));
        actorHealth.IsDead = true;

        Assert.That(diedCount, Is.EqualTo(1), "the death latch must not re-fire OnDied");
    }

    [Test]
    public void HealthChangedIsNotRaisedForAnUnchangedValue()
    {
        int changedCount = 0;
        actorHealth.OnHealthChanged += () => changedCount++;

        actorHealth.Health = actorHealth.Health;
        Assert.That(changedCount, Is.EqualTo(0));

        actorHealth.ApplyDamage(new DamageInfo(10));
        Assert.That(changedCount, Is.EqualTo(1));
    }

    [Test]
    public void SettingIsDeadDirectlyZeroesHealth()
    {
        actorHealth.IsDead = true;

        Assert.That(actorHealth.Health, Is.EqualTo(0));
        Assert.That(actorHealth.CurrentHealth, Is.EqualTo(0f));
    }

    [Test]
    public void DamageableContractReportsTheSameNumbers()
    {
        IDamageable damageable = actorHealth;
        actorHealth.ApplyDamage(new DamageInfo(25));

        Assert.That(damageable.CurrentHealth, Is.EqualTo(75f));
        Assert.That(damageable.CurrentMaxHealth, Is.EqualTo(100f));
        Assert.That(damageable.IsDead, Is.False);
    }
}
