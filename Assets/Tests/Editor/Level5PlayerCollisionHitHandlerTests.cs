using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 3 Slice 70: <see cref="PlayerCollisions"/> and <see cref="AutoPlayerCollisions"/> used
/// to carry byte-identical private combat-hit methods (resolve <see cref="IAttackBoxHitInfo"/>, apply
/// damage/knockdown/disintegrate/rake, spend block). They now both call into one
/// <see cref="PlayerCollisionHitHandler"/> through <see cref="IPlayerCollisionHitHost"/>.
///
/// Exercises the shared mechanics directly against a fake host - proving convergence (identical
/// reaction dispatch for identical inputs from differently-shaped hosts, standing in for the human and
/// CPU wrappers) and that the one intentional hook
/// (<see cref="IPlayerCollisionHitHost.NotifyEnemyAttackBoxHit"/>, human-only killed-on-idle
/// forwarding) is host-decided rather than baked into the shared sequence: the handler always calls it
/// for an enemy/obstacle hit and never inspects <see cref="IAttackBoxHitInfo.IsKilledOnIdle"/> itself -
/// see the two real wrappers' own <c>NotifyEnemyAttackBoxHit</c> implementations for the actual
/// human-forwards/CPU-no-ops split.
/// </summary>
public class Level5PlayerCollisionHitHandlerTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        PlayerHealthBar.instance = null;
        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private PlayerHealth MakeHealth(float startingHealth = 100f)
    {
        PlayerHealth health = Spawn("health").AddComponent<PlayerHealth>();
        health.Health = startingHealth;
        return health;
    }

    private FakeAttackBox MakeAttackBox(string tag, int damage = 0, bool knockDown = false, bool disintegrate = false, bool rake = false, bool killedOnIdle = false)
    {
        GameObject go = Spawn("attack-box-" + tag);
        go.tag = tag;
        go.AddComponent<BoxCollider>();
        FakeAttackBox box = go.AddComponent<FakeAttackBox>();
        box.AttackDamage = damage;
        box.KnockDownAttack = knockDown;
        box.DisintegrateAttack = disintegrate;
        box.IsRake = rake;
        box.IsKilledOnIdle = killedOnIdle;
        return box;
    }

    private static PlayerCollisionHitHandler MakeHandler(FakeCollisionHitHost host)
    {
        return new PlayerCollisionHitHandler(host);
    }

    [Test]
    public void IdenticalInputsFromHumanLikeAndCpuLikeHostsBothApplyKnockdown()
    {
        // The regression this convergence removes: two independently-maintained copies of this
        // sequence could silently drift. Two differently-shaped fake hosts (only Luck/health differ,
        // matching the two real wrappers reading from two different CharacterProfiles) must reach the
        // same reaction for the same attack-box input.
        FakeAttackBox enemyBox = MakeAttackBox("enemyAttackBox", damage: 10, knockDown: true);

        FakeCollisionHitHost humanLike = new FakeCollisionHitHost { Luck = 0f, CanBeKnockedDown = true, Health = MakeHealth() };
        FakeCollisionHitHost cpuLike = new FakeCollisionHitHost { Luck = 3f, CanBeKnockedDown = true, Health = MakeHealth() };

        MakeHandler(humanLike).HandleAttackBoxHit(enemyBox.GetComponent<Collider>());
        MakeHandler(cpuLike).HandleAttackBoxHit(enemyBox.GetComponent<Collider>());

        Assert.AreEqual(new[] { "Knockdown" }, humanLike.Calls, "human-like host must react to a knockdown attack box with a knockdown reaction");
        Assert.AreEqual(new[] { "Knockdown" }, cpuLike.Calls, "cpu-like host must reach the identical reaction for the identical input");
    }

    [Test]
    public void KnockdownAttackFallsBackToDamageReactionWhenActorCannotBeKnockedDown()
    {
        FakeAttackBox enemyBox = MakeAttackBox("enemyAttackBox", damage: 10, knockDown: true);
        FakeCollisionHitHost host = new FakeCollisionHitHost { Luck = 0f, CanBeKnockedDown = false, Health = MakeHealth() };

        MakeHandler(host).HandleAttackBoxHit(enemyBox.GetComponent<Collider>());

        Assert.AreEqual(new[] { "Damage" }, host.Calls,
            "CanBeKnockedDown=false must fall back to the ordinary damage reaction even for a knockdown-flagged attack");
    }

    [Test]
    public void RakeAttackAppliesDamageReactionThenRakeReaction()
    {
        FakeAttackBox rakeBox = MakeAttackBox("enemyAttackBox", damage: 5, rake: true);
        FakeCollisionHitHost host = new FakeCollisionHitHost { Luck = 0f, CanBeKnockedDown = true, Health = MakeHealth() };

        MakeHandler(host).HandleAttackBoxHit(rakeBox.GetComponent<Collider>());

        Assert.AreEqual(new[] { "Damage", "Rake" }, host.Calls,
            "a rake attack must apply the ordinary damage reaction and then the rake-specific reaction, in that order");
    }

    [Test]
    public void DisintegrateAttackAppliesOnlyTheDisintegrateReaction()
    {
        FakeAttackBox disintegrateBox = MakeAttackBox("enemyAttackBox", damage: 50, disintegrate: true);
        PlayerHealth health = MakeHealth();
        FakeCollisionHitHost host = new FakeCollisionHitHost { Luck = 0f, CanBeKnockedDown = true, Health = health };

        MakeHandler(host).HandleAttackBoxHit(disintegrateBox.GetComponent<Collider>());

        Assert.AreEqual(new[] { "Disintegrate" }, host.Calls,
            "a disintegrate attack must skip the damage/knockdown branch entirely - preserved oddity, not a regression");
        Assert.AreEqual(100f, health.Health, "a disintegrate attack must not apply raw health damage through this path");
    }

    [Test]
    public void ActorDyingFromTheHitShortCircuitsBeforeAnyReaction()
    {
        FakeAttackBox lethalBox = MakeAttackBox("enemyAttackBox", damage: 999, knockDown: true);
        PlayerHealth health = MakeHealth(startingHealth: 1f);
        FakeCollisionHitHost host = new FakeCollisionHitHost { Luck = 0f, CanBeKnockedDown = true, Health = health };

        MakeHandler(host).HandleAttackBoxHit(lethalBox.GetComponent<Collider>());

        Assert.IsTrue(health.IsDead, "precondition: the hit must have killed the actor");
        Assert.IsEmpty(host.Calls, "a lethal hit must return before any knockdown/damage/rake reaction runs");
    }

    [Test]
    public void EvadedAttackAppliesNoReactionAndNoDamage()
    {
        FakeAttackBox enemyBox = MakeAttackBox("enemyAttackBox", damage: 10, knockDown: true);
        PlayerHealth health = MakeHealth();
        // Luck=100 forces the evade roll (Random.Range(0,100) < 100) on every iteration bar the
        // effectively-zero-probability exact-100.0f edge.
        FakeCollisionHitHost host = new FakeCollisionHitHost { Luck = 100f, CanBeKnockedDown = true, Health = health };

        MakeHandler(host).HandleAttackBoxHit(enemyBox.GetComponent<Collider>());

        Assert.IsEmpty(host.Calls, "a dodged attack must apply no reaction");
        Assert.AreEqual(100f, health.Health, "a dodged attack must apply no damage");
    }

    [Test]
    public void EnemyAttackBoxHitNotifiesTheHostRegardlessOfKilledOnIdle()
    {
        FakeAttackBox box = MakeAttackBox("enemyAttackBox", damage: 1, killedOnIdle: true);
        FakeCollisionHitHost host = new FakeCollisionHitHost { Luck = 0f, CanBeKnockedDown = true, Health = MakeHealth() };

        MakeHandler(host).HandleAttackBoxHit(box.GetComponent<Collider>());

        Assert.IsNotNull(host.NotifiedHit, "the handler must notify the host of the resolved enemy attack-box hit");
        Assert.IsTrue(host.NotifiedHit.IsKilledOnIdle, "the notified hit must carry the real IsKilledOnIdle value for the host to act on");
    }

    [Test]
    public void PlayerAttackBoxNeverNotifiesKilledOnIdle()
    {
        // A player-authored attack box has no killed-on-idle concept (IAttackBoxHitInfo's own
        // constant-false implementation) - the notify hook only ever fires for enemy/obstacle hits.
        FakeAttackBox box = MakeAttackBox("playerAttackBox", damage: 1);
        FakeCollisionHitHost host = new FakeCollisionHitHost { Luck = 0f, CanBeKnockedDown = true, Health = MakeHealth() };

        MakeHandler(host).HandleAttackBoxHit(box.GetComponent<Collider>());

        Assert.IsNull(host.NotifiedHit, "a playerAttackBox hit must never notify the killed-on-idle hook");
    }

    private sealed class FakeAttackBox : MonoBehaviour, IAttackBoxHitInfo
    {
        public int AttackDamage;
        public bool KnockDownAttack;
        public bool DisintegrateAttack;
        public bool IsRake;
        public bool IsKilledOnIdle;

        int IAttackBoxHitInfo.AttackDamage => AttackDamage;
        bool IAttackBoxHitInfo.KnockDownAttack => KnockDownAttack;
        bool IAttackBoxHitInfo.DisintegrateAttack => DisintegrateAttack;
        bool IAttackBoxHitInfo.IsRake => IsRake;
        bool IAttackBoxHitInfo.IsKilledOnIdle => IsKilledOnIdle;
    }

    private sealed class FakeCollisionHitHost : IPlayerCollisionHitHost
    {
        public bool CanBeKnockedDown { get; set; }
        public bool IsBlocking { get; set; }
        public float Luck { get; set; }
        public PlayerHealth Health { get; set; }
        public IAttackBoxHitInfo NotifiedHit { get; private set; }
        public readonly List<string> Calls = new List<string>();

        void IPlayerCollisionHitHost.RunCoroutine(IEnumerator routine)
        {
            // Never driven to completion - PlayerHealthBar.instance is null in these tests, so
            // production code never actually calls this; kept as a no-op in case a future change
            // starts calling it unconditionally.
        }

        void IPlayerCollisionHitHost.NotifyEnemyAttackBoxHit(IAttackBoxHitInfo hit) => NotifiedHit = hit;

        void IPlayerCollisionHitHost.ApplyDisintegrateReaction() => Calls.Add("Disintegrate");
        void IPlayerCollisionHitHost.ApplyDamageReaction() => Calls.Add("Damage");
        void IPlayerCollisionHitHost.ApplyKnockdownReaction() => Calls.Add("Knockdown");
        void IPlayerCollisionHitHost.ApplyRakeReaction(Collider rakeSource) => Calls.Add("Rake");
    }
}
