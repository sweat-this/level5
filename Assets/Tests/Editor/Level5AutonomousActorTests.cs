using Level5.Core.Match;
using NUnit.Framework;

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
}
