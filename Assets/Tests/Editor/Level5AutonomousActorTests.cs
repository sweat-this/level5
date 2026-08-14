using System.Collections.Generic;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regression coverage for the autonomous-actor audit fixes: CPU shooters, the lockdown defender,
/// enemy/bodyguard combat AI, ambient NPCs and the cheerleader system.
///
/// These cover the decision rules that regressed, not the MonoBehaviour plumbing around them - the
/// rules were deliberately separated out (static helpers, plain classes) so they are reachable
/// without spawning prefabs or entering Play Mode.
/// </summary>
public class Level5AutonomousActorTests
{
    // ---------------------------------------------------------------- ID-1

    [Test]
    public void EachHumanSlotResolvesItsOwnCharacter()
    {
        // The regression: PlayerIdentifier.setPlayer read MatchRuntime.PrimaryCharacterId - roster
        // slot zero - for every human it wired, so a second local human was rebuilt with the first
        // human's stats, level, display name and PlayerId.
        PlayerSlot primary = new PlayerSlot(
            0,
            PlayerControlType.LocalHuman,
            new CharacterSelection(11, "drblood", "Dr Blood", true, false),
            0);
        PlayerSlot second = new PlayerSlot(
            1,
            PlayerControlType.LocalHuman,
            new CharacterSelection(24, "oldreal", "Old Real", true, false),
            1);

        Assert.That(SpawnCoordinator.ResolveHumanCharacterId(primary), Is.EqualTo(11));
        Assert.That(
            SpawnCoordinator.ResolveHumanCharacterId(second),
            Is.EqualTo(24),
            "the second human must load its own character, not the primary slot's");
    }

    [Test]
    public void HumanSlotWithoutACharacterFallsBackToThePrimaryId()
    {
        // A slot carrying CharacterSelection.None has always resolved to whatever the primary id
        // resolves to. Preserved so a single-human match is unaffected by the fix above.
        PlayerSlot empty = new PlayerSlot(1, PlayerControlType.LocalHuman, CharacterSelection.None, 1);

        Assert.That(
            SpawnCoordinator.ResolveHumanCharacterId(empty),
            Is.EqualTo(MatchRuntime.PrimaryCharacterId));
        Assert.That(SpawnCoordinator.ResolveHumanCharacterId(null), Is.EqualTo(MatchRuntime.PrimaryCharacterId));
    }

    // ---------------------------------------------------------------- ENM-2

    [Test]
    public void QueueCapacityIsNeverBelowTheSpawnCap()
    {
        // The invariant that made the two independently-derived enemy counts harmless while they
        // lived in separate files. If it ever breaks, enemies the spawner created will stand idle
        // unable to reserve an attack slot, because EnemyController gates stateWalk on holding one.
        foreach (ResolvedMatchRules rules in EnemyPopulationCases())
        {
            int queued = EnemyPopulationRules.MaxQueued(rules);
            int alive = EnemyPopulationRules.MaxAlive(rules, hasConfiguration: true, halveForMobile: false);
            int aliveMobile = EnemyPopulationRules.MaxAlive(rules, hasConfiguration: true, halveForMobile: true);

            Assert.That(queued, Is.GreaterThanOrEqualTo(alive), Describe(rules));
            Assert.That(queued, Is.GreaterThanOrEqualTo(aliveMobile), Describe(rules) + " (mobile)");
        }
    }

    [Test]
    public void EnemyCapsMatchTheValuesTheyReplaced()
    {
        // Pinned to the numbers the two original branch chains produced, so extracting them into
        // EnemyPopulationRules cannot have quietly retuned any mode.
        ResolvedMatchRules standard = Rules(hardcore: false, enemiesOnly: false, battleRoyal: false, cage: false);
        ResolvedMatchRules hardcore = Rules(hardcore: true, enemiesOnly: false, battleRoyal: false, cage: false);
        ResolvedMatchRules hardcoreOnly = Rules(hardcore: true, enemiesOnly: true, battleRoyal: false, cage: false);
        ResolvedMatchRules battleRoyal = Rules(hardcore: false, enemiesOnly: false, battleRoyal: true, cage: false);
        ResolvedMatchRules battleRoyalCage = Rules(hardcore: false, enemiesOnly: false, battleRoyal: true, cage: true);

        Assert.That(EnemyPopulationRules.MaxAlive(standard, true, false), Is.EqualTo(4));
        Assert.That(EnemyPopulationRules.MaxAlive(hardcore, true, false), Is.EqualTo(6));
        Assert.That(EnemyPopulationRules.MaxAlive(hardcoreOnly, true, false), Is.EqualTo(8));
        // staged in gradually, then raised once battle royal spawning starts
        Assert.That(EnemyPopulationRules.MaxAlive(battleRoyal, true, false), Is.EqualTo(2));
        Assert.That(EnemyPopulationRules.MaxAliveForBattleRoyal(), Is.EqualTo(20));
        Assert.That(EnemyPopulationRules.MaxAlive(battleRoyalCage, true, false), Is.EqualTo(4));
        // no resolved configuration falls back to the standard cap even in battle royal
        Assert.That(EnemyPopulationRules.MaxAlive(battleRoyal, false, false), Is.EqualTo(4));
        Assert.That(EnemyPopulationRules.MaxAlive(standard, true, true), Is.EqualTo(2));

        Assert.That(EnemyPopulationRules.MaxQueued(standard), Is.EqualTo(4));
        Assert.That(EnemyPopulationRules.MaxQueued(hardcore), Is.EqualTo(6));
        Assert.That(EnemyPopulationRules.MaxQueued(hardcoreOnly), Is.EqualTo(8));
        Assert.That(EnemyPopulationRules.MaxQueued(battleRoyal), Is.EqualTo(20));
        Assert.That(EnemyPopulationRules.MaxQueued(battleRoyalCage), Is.EqualTo(20));
    }

    private static System.Collections.Generic.IEnumerable<ResolvedMatchRules> EnemyPopulationCases()
    {
        foreach (bool hardcore in new[] { false, true })
        foreach (bool enemiesOnly in new[] { false, true })
        foreach (bool battleRoyal in new[] { false, true })
        foreach (bool cage in new[] { false, true })
        {
            yield return Rules(hardcore, enemiesOnly, battleRoyal, cage);
        }
    }

    private static string Describe(ResolvedMatchRules rules)
    {
        return $"hardcore={rules.Hardcore} enemiesOnly={rules.EnemiesOnly} "
            + $"battleRoyal={rules.IsBattleRoyal} cage={rules.IsCageMatch}";
    }

    private static ResolvedMatchRules Rules(bool hardcore, bool enemiesOnly, bool battleRoyal, bool cage)
    {
        CombatMode combatMode = CombatMode.Standard;
        if (battleRoyal)
        {
            combatMode |= CombatMode.BattleRoyal;
        }

        if (cage)
        {
            combatMode |= CombatMode.Cage;
        }

        return new ResolvedMatchRules(
            combatMode: combatMode,
            enemiesEnabled: true,
            hardcore: hardcore,
            enemiesOnly: enemiesOnly);
    }

    // ---------------------------------------------------------------- BG-2

    [Test]
    public void ABodyGuardOnlySightsEnemiesWithinItsOwnReach()
    {
        // The regression: detection was `queue.HasQueuedEnemies()` - one scene-wide boolean, the
        // same answer for every bodyguard regardless of where it stood. A bodyguard across the
        // level "sighted" an enemy the instant anything anywhere engaged.
        Vector3 bodyGuard = new Vector3(0f, 0f, 0f);
        List<Vector3> farAwayEnemy = new List<Vector3> { new Vector3(50f, 0f, 0f) };

        Assert.IsFalse(
            BodyGuardDetection.AnyEnemyWithinSight(bodyGuard, farAwayEnemy, 6f),
            "an enemy 50 units away is not sighted by a bodyguard with a 6 unit reach");

        List<Vector3> nearbyEnemy = new List<Vector3> { new Vector3(3f, 0f, 0f) };
        Assert.IsTrue(
            BodyGuardDetection.AnyEnemyWithinSight(bodyGuard, nearbyEnemy, 6f),
            "an enemy 3 units away is sighted by a bodyguard with a 6 unit reach");
    }

    [Test]
    public void ABodyGuardSightsWhenAnyQueuedEnemyIsInReachAndNotWhenTheQueueIsEmpty()
    {
        Vector3 bodyGuard = Vector3.zero;

        Assert.IsFalse(
            BodyGuardDetection.AnyEnemyWithinSight(bodyGuard, new List<Vector3>(), 6f),
            "no queued enemies means nothing is sighted");

        List<Vector3> mixed = new List<Vector3>
        {
            new Vector3(40f, 0f, 0f),
            new Vector3(0f, 0f, 2f),
        };
        Assert.IsTrue(
            BodyGuardDetection.AnyEnemyWithinSight(bodyGuard, mixed, 6f),
            "one enemy in reach is enough, even when others are far away");
    }

    [Test]
    public void ABodyGuardNeverGoesBlindInsideItsOwnInterceptionRange()
    {
        // The invariant: BodyGuardController breaks formation to intercept a threat within
        // maximumInterceptionDistance. If detection reported "unsighted" inside that range,
        // CheckReturnToPatrolStatus would send the bodyguard back to patrol mid-charge.
        // The only authored enemySightDistance in the project is 4, against a leash of 6.
        Assert.AreEqual(
            6f,
            BodyGuardDetection.EffectiveSightDistance(4f, 6f),
            "an authored reach below the interception leash is raised to it");

        Assert.AreEqual(
            20f,
            BodyGuardDetection.EffectiveSightDistance(20f, 6f),
            "an authored reach above the interception leash is left alone");
    }

    [Test]
    public void ADestroyedEnemyIsNotSightedAndAZeroReachSightsNothing()
    {
        Assert.IsFalse(
            BodyGuardDetection.AnyEnemyWithinSight(Vector3.zero, null, 6f),
            "a null position list is not a sighting");

        Assert.IsFalse(
            BodyGuardDetection.AnyEnemyWithinSight(Vector3.zero, new List<Vector3> { Vector3.zero }, 0f),
            "a zero reach sights nothing, even an enemy standing on top of the bodyguard");
    }

    // ---------------------------------------------------------------- DEF-1

    [Test]
    public void TheLockdownDefenderOutrunsEveryAuthoredPlayerSpeed()
    {
        // DEF-1 made AutoPlayerDefense.speed literally units/second. The old spring form had no
        // cap and self-corrected at any separation; this one cannot exceed `speed`, so a defender
        // slower than the player it guards falls behind without bound. The authored 6 tied the
        // fastest runSpeedHasBall and lost to the fastest runSpeed, leaving it unable to close a
        // gap or hold one. Read from the authored assets so retuning either side re-checks this.
        const string defenderPrefabPath =
            "Assets/Resources/Prefabs/characters/cpu_players_defense/cpu_player_defense_oldreal.prefab";

        GameObject defenderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(defenderPrefabPath);
        Assert.IsNotNull(defenderPrefab, $"lockdown defender prefab missing at {defenderPrefabPath}");

        AutoPlayerDefense defense = defenderPrefab.GetComponentInChildren<AutoPlayerDefense>(true);
        Assert.IsNotNull(defense, "lockdown defender prefab has no AutoPlayerDefense");

        SerializedProperty speedProperty = new SerializedObject(defense).FindProperty("speed");
        Assert.IsNotNull(speedProperty, "AutoPlayerDefense.speed is no longer a serialized field");
        float defenderSpeed = speedProperty.floatValue;

        float fastestPlayerSpeed = 0f;
        string fastestCharacter = "none";
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs/characters" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CharacterProfile profile = prefab != null ? prefab.GetComponentInChildren<CharacterProfile>(true) : null;
            if (profile == null)
            {
                continue;
            }

            float fastest = Mathf.Max(profile.Speed, Mathf.Max(profile.RunSpeed, profile.RunSpeedHasBall));
            if (fastest > fastestPlayerSpeed)
            {
                fastestPlayerSpeed = fastest;
                fastestCharacter = System.IO.Path.GetFileNameWithoutExtension(path);
            }
        }

        Assert.Greater(fastestPlayerSpeed, 0f, "found no authored character speeds to compare against");
        Assert.Greater(
            defenderSpeed,
            fastestPlayerSpeed,
            $"lockdown defender speed {defenderSpeed} does not beat the fastest authored character "
                + $"({fastestCharacter} at {fastestPlayerSpeed}); it cannot close a gap or hold one");
    }
}
