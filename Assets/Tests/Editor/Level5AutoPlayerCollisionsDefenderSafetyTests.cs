using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 3 Slice 70: the regression this slice fixes. The lockdown defender
/// (<c>Assets/Resources/Prefabs/characters/cpu_players_defense/cpu_player_defense_oldreal.prefab</c>)
/// is a CPU participant (<see cref="PlayerIdentifier.isCpu"/>) that carries <see cref="AutoPlayerDefense"/>,
/// not <see cref="AutoPlayerController"/>. Before this slice, <c>AutoPlayerCollisions.GetPlayerObjects()</c>
/// still unconditionally resolved <c>GetComponent&lt;AutoPlayerController&gt;()</c> for any CPU actor
/// and left it null for this composition; <c>OnTriggerEnter</c>'s combat-hit eligibility check then read
/// <c>autoPlayerController.KnockedDown</c>/<c>.TakeDamage</c> unconditionally, so the first time this
/// defender's hitbox touched an attack box under a rule set that enables combat, it null-dereferenced.
///
/// Drives the real private <c>Start</c>/<c>OnTriggerEnter</c> composition path via reflection, the same
/// technique <see cref="Level5SpawnCoordinatorFallRespawnCompositionTests"/> and
/// <see cref="Level5BasketballMarkerOwnershipTests"/> already use, rather than a stand-in for either
/// method.
/// </summary>
public class Level5AutoPlayerCollisionsDefenderSafetyTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
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

    private static void InvokeStart(AutoPlayerCollisions collisions)
    {
        MethodInfo method = typeof(AutoPlayerCollisions).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "AutoPlayerCollisions must declare Start");
        method.Invoke(collisions, null);
    }

    private static void InvokeOnTriggerEnter(AutoPlayerCollisions collisions, Collider other)
    {
        MethodInfo method = typeof(AutoPlayerCollisions).GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "AutoPlayerCollisions must declare OnTriggerEnter");
        method.Invoke(collisions, new object[] { other });
    }

    /// <summary>
    /// Mirrors the real composition on <c>cpu_player_defense_oldreal.prefab</c>:
    /// <see cref="PlayerIdentifier"/> (isCpu) and <see cref="AutoPlayerDefense"/> on the root,
    /// <see cref="AutoPlayerCollisions"/> and <see cref="PlayerHealth"/> on an
    /// "autoPlayerHitbox"-tagged child - and, deliberately, no <see cref="AutoPlayerController"/>
    /// anywhere in the hierarchy.
    /// </summary>
    private GameObject SpawnDefenderShapedCpu(out AutoPlayerCollisions collisions)
    {
        GameObject root = Spawn("defender-root");
        PlayerIdentifier identifier = root.AddComponent<PlayerIdentifier>();
        root.AddComponent<AutoPlayerDefense>();
        identifier.setIds(0, true);
        identifier.autoPlayer = root;

        GameObject hitbox = new GameObject("defender-hitbox");
        hitbox.transform.SetParent(root.transform);
        hitbox.tag = "autoPlayerHitbox";
        hitbox.AddComponent<BoxCollider>();
        hitbox.AddComponent<PlayerHealth>();
        collisions = hitbox.AddComponent<AutoPlayerCollisions>();
        spawned.Add(hitbox);

        return root;
    }

    private BoxCollider MakeAttackBoxCollider(string tag)
    {
        GameObject go = Spawn("attack-box");
        go.tag = tag;
        return go.AddComponent<BoxCollider>();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        field.SetValue(target, value);
    }

    [Test]
    public void StartOnADefenderShapedCpuLogsOnceAndDoesNotThrow()
    {
        SpawnDefenderShapedCpu(out AutoPlayerCollisions collisions);

        LogAssert.Expect(LogType.Warning, new Regex("AutoPlayerCollisions on '.*' found no AutoPlayerController.*"));

        Assert.DoesNotThrow(() => InvokeStart(collisions));
    }

    [Test]
    public void AttackBoxCollisionOnADefenderShapedCpuDoesNotThrowOrDereferenceAMissingController()
    {
        GameObject root = SpawnDefenderShapedCpu(out AutoPlayerCollisions collisions);
        LogAssert.Expect(LogType.Warning, new Regex("AutoPlayerCollisions on '.*' found no AutoPlayerController.*"));
        InvokeStart(collisions);

        MethodInfo bindRules = typeof(AutoPlayerCollisions).GetMethod("BindMatchRules");
        Assert.IsNotNull(bindRules, "AutoPlayerCollisions must declare BindMatchRules");
        bindRules.Invoke(collisions, new object[] { new ResolvedMatchRules(enemiesEnabled: true) });

        Collider enemyAttackBox = MakeAttackBoxCollider("enemyAttackBox");
        enemyAttackBox.gameObject.AddComponent<FakeDefenderAttackBox>();

        Assert.DoesNotThrow(() => InvokeOnTriggerEnter(collisions, enemyAttackBox),
            "a defender-shaped CPU with no AutoPlayerController must never null-dereference one on an attack-box collision");
    }

    [Test]
    public void OrdinaryCpuShooterCompositionStillProcessesCombatHits()
    {
        // The safety guard must not become a blanket "CPU never processes combat hits" regression -
        // an ordinary CPU shooter (AutoPlayerController present) must still be able to enter the
        // combat-hit branch. This does not assert the full reaction (that is
        // Level5PlayerCollisionHitHandlerTests' job); it only proves hasShooterController does not
        // false-negative for the composition every real cpu_player_*.prefab actually uses.
        GameObject root = Spawn("cpu-shooter-root");
        PlayerIdentifier identifier = root.AddComponent<PlayerIdentifier>();
        CharacterProfile profile = root.AddComponent<CharacterProfile>();
        profile.Luck = 0;
        AutoPlayerController controller = root.AddComponent<AutoPlayerController>();
        // AutoPlayerController.Start() is not driven here (it needs a bound IPlayerMatchRuntime and a
        // full animator/rigidbody rig this test has no need for) - CharacterProfile is set directly so
        // AutoPlayerCollisions's Luck read (via IPlayerCollisionHitHost) does not dereference a null
        // CharacterProfile.
        controller.CharacterProfile = profile;
        // currentState/blockState both default to 0 on an undriven AutoPlayerController, which would
        // make IsBlocking (currentState == blockState) accidentally true and route into the SFXBB
        // block-sound branch - unreachable in real gameplay, where getAnimatorStateHashes() gives
        // blockState a real animator hash. Separated here so this test exercises the damage branch.
        controller.blockState = 999;
        // AutoPlayerController.SetPlayerAnim (called by the shared handler's damage/knockdown/rake
        // reactions through IPlayerCollisionHitHost) drives this Animator directly - it is never null
        // in real gameplay because Start() resolves it, which is not driven here.
        Animator animator = root.AddComponent<Animator>();
        SetPrivateField(controller, "anim", animator);
        identifier.setIds(0, true);
        identifier.autoPlayer = root;

        GameObject hitbox = new GameObject("cpu-shooter-hitbox");
        hitbox.transform.SetParent(root.transform);
        hitbox.tag = "autoPlayerHitbox";
        hitbox.AddComponent<BoxCollider>();
        hitbox.AddComponent<PlayerHealth>();
        AutoPlayerCollisions collisions = hitbox.AddComponent<AutoPlayerCollisions>();
        spawned.Add(hitbox);

        InvokeStart(collisions);

        MethodInfo bindRules = typeof(AutoPlayerCollisions).GetMethod("BindMatchRules");
        bindRules.Invoke(collisions, new object[] { new ResolvedMatchRules(enemiesEnabled: true) });

        Collider enemyAttackBox = MakeAttackBoxCollider("enemyAttackBox");
        enemyAttackBox.gameObject.AddComponent<FakeDefenderAttackBox>();

        // The damage reaction drives this test's bare Animator (no RuntimeAnimatorController - this
        // test has no need for one) through SetPlayerAnim, which logs this warning rather than
        // throwing; expected and consumed here rather than left to fail the run as an unhandled log.
        LogAssert.Expect(LogType.Warning, "Animator is not playing an AnimatorController");
        Assert.DoesNotThrow(() => InvokeOnTriggerEnter(collisions, enemyAttackBox));
        LogAssert.NoUnexpectedReceived();
    }

    private sealed class FakeDefenderAttackBox : MonoBehaviour, IAttackBoxHitInfo
    {
        int IAttackBoxHitInfo.AttackDamage => 10;
        bool IAttackBoxHitInfo.KnockDownAttack => false;
        bool IAttackBoxHitInfo.DisintegrateAttack => false;
        bool IAttackBoxHitInfo.IsRake => false;
        bool IAttackBoxHitInfo.IsKilledOnIdle => false;
    }
}
