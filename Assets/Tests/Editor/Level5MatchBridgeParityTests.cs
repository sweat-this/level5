using System.Collections.Generic;
using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// The legacy bridge writes what the old launch path wrote.
///
/// Until every consumer has migrated, the globals are how most of the game learns what it is
/// playing. A configuration that resolves correctly but reaches the old code as different values is
/// the failure mode of a strangler migration, so each of these pins one group of fields to what
/// <c>StartManager.setGameOptions</c> used to assign.
/// </summary>
public class Level5MatchBridgeParityTests
{
    private GameOptionsSnapshot snapshot;

    [SetUp]
    public void SetUp()
    {
        snapshot = GameOptionsSnapshot.Capture();
    }

    [TearDown]
    public void TearDown()
    {
        snapshot.Restore();
        ActiveMatch.Clear();
    }

    private static MatchConfiguration Configure(
        GameModeDefinition mode,
        LevelDefinition level,
        PlayerRoster roster = null,
        MatchModifiers modifiers = null)
    {
        roster ??= TestDefinitions.SoloRoster();
        modifiers ??= MatchModifiers.Default;
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { level }));

        MatchBuildResult result = builder.Build(
            new MatchRequest(mode.Id, level.LevelId, roster, modifiers));
        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
        return result.Configuration;
    }

    [Test]
    public void ModeFlagsReachTheOldGlobalsUnchanged()
    {
        GameModeDefinition mode = TestDefinitions.Mode(
            GameModeId.AllPointContest,
            clockMode: MatchClockMode.Countdown,
            shotRule: ShotRule.AllRanges,
            markers: ShotMarkerRequirement.ThreePoint | ShotMarkerRequirement.FourPoint,
            customTimerSeconds: 160f);

        LegacyGameOptionsBridge.Apply(Configure(mode, TestDefinitions.Level(1)));

        Assert.That(GameOptions.gameModeSelectedId, Is.EqualTo((int)GameModeId.AllPointContest));
        Assert.That(GameOptions.gameModeHasBeenSelected, Is.True);
        Assert.That(GameOptions.gameModeRequiresCountDown, Is.True);
        Assert.That(GameOptions.gameModeRequiresCounter, Is.False);
        Assert.That(GameOptions.gameModeRequiresShotMarkers3s, Is.True);
        Assert.That(GameOptions.gameModeRequiresShotMarkers4s, Is.True);
        Assert.That(GameOptions.gameModeRequiresShotMarkers7s, Is.False);
        Assert.That(GameOptions.gameModeAllPointContest, Is.True);
        Assert.That(GameOptions.gameModeThreePointContest, Is.False);
        Assert.That(GameOptions.gameModeFourPointContest, Is.False);
        Assert.That(GameOptions.gameModeSevenPointContest, Is.False);
        Assert.That(GameOptions.customTimer, Is.EqualTo(160f));
    }

    [Test]
    public void AModeWithoutACustomTimerWritesZeroNotTheDefaultLength()
    {
        // customTimer is the "does this mode override the length?" signal, not the length itself.
        // Writing 180 here would make every mode look like it had a custom timer.
        LegacyGameOptionsBridge.Apply(
            Configure(TestDefinitions.Mode(GameModeId.TotalPoints), TestDefinitions.Level(1)));

        Assert.That(GameOptions.customTimer, Is.EqualTo(0f));
    }

    [Test]
    public void CombatModeReachesTheOldBattleRoyalAndCageFlags()
    {
        LegacyGameOptionsBridge.Apply(Configure(
            TestDefinitions.Mode(
                GameModeId.CageMatch,
                combatMode: CombatMode.Cage,
                enemiesOnly: true,
                requiresBasketball: false),
            TestDefinitions.Level(1, ArenaCapability.Combat | ArenaCapability.Cage)));

        Assert.That(GameOptions.cageMatchEnabled, Is.True);
        Assert.That(GameOptions.battleRoyalEnabled, Is.False);
        Assert.That(GameOptions.EnemiesOnlyEnabled, Is.True);
        Assert.That(GameOptions.enemiesEnabled, Is.True, "a fighting mode has enemies whether or not they were asked for");
    }

    [Test]
    public void LevelCapabilitiesReachTheOldPerLevelBooleans()
    {
        LevelDefinition level = TestDefinitions.Level(
            7,
            ArenaCapability.Basketball
            | ArenaCapability.SevenPointLine
            | ArenaCapability.Weather
            | ArenaCapability.TimeOfDay,
            objectName: "level_07_dome");

        LegacyGameOptionsBridge.Apply(Configure(TestDefinitions.Mode(GameModeId.TotalPoints), level));

        Assert.That(GameOptions.levelId, Is.EqualTo(7));
        Assert.That(GameOptions.levelSelected, Is.EqualTo("level_07_dome"));
        Assert.That(GameOptions.levelHasSevenPointers, Is.True);
        Assert.That(GameOptions.levelRequiresWeather, Is.True);
        Assert.That(GameOptions.levelRequiresTimeOfDay, Is.True);
    }

    [Test]
    public void ARosterOfOneHumanAndTwoCpusProducesTheOldSlotBooleans()
    {
        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("me")),
            PlayerRosterEntry.Cpu(TestDefinitions.Character("cpu1")),
            PlayerRosterEntry.Cpu(TestDefinitions.Character("cpu2"))
        });

        LegacyGameOptionsBridge.Apply(Configure(
            TestDefinitions.Mode(GameModeId.VersusCpu),
            TestDefinitions.Level(1),
            roster));

        // Exactly what GameOptions.ConfigureSingleHumanRoster(3) produced.
        Assert.That(GameOptions.numPlayers, Is.EqualTo(3));
        Assert.That(GameOptions.numCpuPlayers, Is.EqualTo(2));
        Assert.That(GameOptions.player1IsCpu, Is.False);
        Assert.That(GameOptions.player2IsCpu, Is.True);
        Assert.That(GameOptions.player3IsCpu, Is.True);
        Assert.That(GameOptions.player4IsCpu, Is.False);
        Assert.That(GameOptions.characterObjectNames, Is.EqualTo(new List<string> { "me", "cpu1", "cpu2" }));
        Assert.That(GameOptions.characterObjectName, Is.EqualTo("me"));
    }

    [Test]
    public void LockdownsImplicitDefenderStillShowsUpAsPlayer2IsCpu()
    {
        // ConfigureSingleHumanRoster(1, hasImplicitSecondCpu: true). The defender is not a roster
        // slot, but the legacy spawn path reads player2IsCpu to know it exists.
        LegacyGameOptionsBridge.Apply(Configure(
            TestDefinitions.Mode(GameModeId.Lockdown, maxPlayers: 1, addsImplicitDefender: true),
            TestDefinitions.Level(1)));

        Assert.That(GameOptions.numPlayers, Is.EqualTo(1));
        Assert.That(GameOptions.numCpuPlayers, Is.EqualTo(1));
        Assert.That(GameOptions.player1IsCpu, Is.False);
        Assert.That(GameOptions.player2IsCpu, Is.True);
    }

    [Test]
    public void SniperVariantsReachTheirThreeSeparateBooleans()
    {
        LegacyGameOptionsBridge.Apply(Configure(
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Level(1),
            modifiers: new MatchModifiers(sniper: SniperMode.MachineGun)));

        Assert.That(GameOptions.sniperEnabled, Is.True);
        Assert.That(GameOptions.sniperEnabledBulletAuto, Is.True);
        Assert.That(GameOptions.sniperEnabledBullet, Is.False);
        Assert.That(GameOptions.sniperEnabledLaser, Is.False);
    }

    [Test]
    public void HardcoreDifficultyStillSetsTheHardcoreFlag()
    {
        // The old launch path did `if (difficultySelected == 2) hardcoreEnabled = true;`.
        LegacyGameOptionsBridge.Apply(Configure(
            TestDefinitions.Mode(GameModeId.TotalPoints),
            TestDefinitions.Level(1),
            modifiers: new MatchModifiers(difficulty: MatchDifficulty.Hardcore)));

        Assert.That(GameOptions.difficultySelected, Is.EqualTo(2));
        Assert.That(GameOptions.hardcoreModeEnabled, Is.True);
    }

    [Test]
    public void TheRuntimeReadsBackTheSameRulesTheBridgeWrote()
    {
        // The direct-scene-entry fallback reconstructs rules from the globals. If it disagreed with
        // the configuration, a scene entered directly would play by different rules than the same
        // scene launched from the menu.
        GameModeDefinition mode = TestDefinitions.Mode(
            GameModeId.SevenPointContest,
            shotRule: ShotRule.SevenPoint,
            markers: ShotMarkerRequirement.SevenPoint,
            customTimerSeconds: 90f,
            required: ArenaCapability.SevenPointLine);
        LevelDefinition level = TestDefinitions.Level(
            1,
            ArenaCapability.Basketball | ArenaCapability.SevenPointLine | ArenaCapability.Multiplayer);

        MatchConfiguration configuration = Configure(mode, level);
        LegacyGameOptionsBridge.Apply(configuration);
        ActiveMatch.Clear();

        ResolvedMatchRules reconstructed = MatchRuntime.Rules;

        Assert.That(reconstructed.ClockMode, Is.EqualTo(configuration.Rules.ClockMode));
        Assert.That(reconstructed.ShotRule, Is.EqualTo(configuration.Rules.ShotRule));
        Assert.That(reconstructed.ShotMarkers, Is.EqualTo(configuration.Rules.ShotMarkers));
        Assert.That(reconstructed.MatchLengthSeconds, Is.EqualTo(configuration.Rules.MatchLengthSeconds));
        Assert.That(reconstructed.CombatMode, Is.EqualTo(configuration.Rules.CombatMode));
        Assert.That(reconstructed.RequiresBasketball, Is.EqualTo(configuration.Rules.RequiresBasketball));
        Assert.That(reconstructed.EnemiesEnabled, Is.EqualTo(configuration.Rules.EnemiesEnabled));
        Assert.That(reconstructed.Hardcore, Is.EqualTo(configuration.Rules.Hardcore));
    }
}
