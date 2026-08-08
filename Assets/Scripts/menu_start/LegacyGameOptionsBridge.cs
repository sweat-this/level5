using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// Writes a validated <see cref="MatchConfiguration"/> into the legacy <see cref="GameOptions"/>
/// fields, once, so the scripts that have not been migrated yet keep working unchanged.
///
/// TEMPORARY, and one-way on purpose. New configuration flows out to the old globals; nothing flows
/// back. If a legacy system needs to change something during a match, that value is runtime state
/// and belongs to a runtime owner - reading it back into the configuration would restore exactly
/// the two-sources-of-truth problem this replaced.
///
/// The bridge shrinks as consumers migrate, and goes away when the last one has.
/// </summary>
public static class LegacyGameOptionsBridge
{
    /// <summary>
    /// Pushes every match-derived legacy field. Call immediately before the scene load, after the
    /// builder has produced the configuration.
    /// </summary>
    public static void Apply(MatchConfiguration configuration)
    {
        if (configuration == null)
        {
            Debug.LogError("LegacyGameOptionsBridge was given no configuration; legacy fields are unchanged.");
            return;
        }

        GameModeDefinition mode = configuration.Mode;
        LevelDefinition level = configuration.Level;
        ResolvedMatchRules rules = configuration.Rules;

        // tells CharacterProfile to load its profile from LoadedData.instance
        GameOptions.gameModeHasBeenSelected = true;

        ApplyMode(mode, rules);
        ApplyLevel(level);
        ApplyModifiers(rules);
        ApplyRoster(configuration.Roster, mode);
        ApplyCheerleader(configuration.Cheerleader);
    }

    private static void ApplyMode(GameModeDefinition mode, ResolvedMatchRules rules)
    {
        GameOptions.gameModeSelectedId = mode.RawModeId;
        GameOptions.gameModeSelectedName = mode.DisplayName;

        GameOptions.gameModeRequiresCountDown = rules.RequiresCountDown;
        GameOptions.gameModeRequiresCounter = rules.RequiresCounter;

        GameOptions.gameModeRequiresShotMarkers3s = rules.RequiresShotMarkers3s;
        GameOptions.gameModeRequiresShotMarkers4s = rules.RequiresShotMarkers4s;
        GameOptions.gameModeRequiresShotMarkers7s = rules.RequiresShotMarkers7s;

        GameOptions.gameModeThreePointContest = rules.IsThreePointContest;
        GameOptions.gameModeFourPointContest = rules.IsFourPointContest;
        GameOptions.gameModeSevenPointContest = rules.IsSevenPointContest;
        GameOptions.gameModeAllPointContest = rules.IsAllPointContest;

        GameOptions.customTimer = rules.CustomTimerSeconds;

        GameOptions.gameModeRequiresBasketball = rules.RequiresBasketball;
        GameOptions.gameModeAllowsCpuShooters = rules.AllowsCpuShooters;

        GameOptions.EnemiesOnlyEnabled = mode.EnemiesOnly;
        GameOptions.battleRoyalEnabled = rules.IsBattleRoyal;
        GameOptions.cageMatchEnabled = rules.IsCageMatch;
    }

    private static void ApplyLevel(LevelDefinition level)
    {
        GameOptions.levelSelected = level.ObjectName;
        GameOptions.levelId = level.LevelId;
        GameOptions.levelDisplayName = level.DisplayName;
        GameOptions.levelRequiresTimeOfDay = level.RequiresTimeOfDay;
        GameOptions.levelRequiresWeather = level.HasWeather;
        GameOptions.levelHasSevenPointers = level.HasSevenPointers;
    }

    private static void ApplyModifiers(ResolvedMatchRules rules)
    {
        GameOptions.enemiesEnabled = rules.EnemiesEnabled;
        GameOptions.trafficEnabled = rules.TrafficEnabled;
        GameOptions.obstaclesEnabled = rules.ObstaclesEnabled;

        GameOptions.difficultySelected = MatchDifficulties.ToInt(rules.Difficulty);
        GameOptions.hardcoreModeEnabled = rules.Hardcore;

        GameOptions.sniperEnabled = rules.SniperEnabled;
        GameOptions.sniperEnabledBullet = rules.Sniper == SniperMode.Bullet;
        GameOptions.sniperEnabledBulletAuto = rules.Sniper == SniperMode.MachineGun;
        GameOptions.sniperEnabledLaser = rules.Sniper == SniperMode.Laser;
    }

    private static void ApplyRoster(PlayerRoster roster, GameModeDefinition mode)
    {
        List<string> objectNames = new List<string>();
        foreach (PlayerSlot slot in roster.Players)
        {
            objectNames.Add(slot.Character.ObjectName);
        }

        GameOptions.characterObjectNames = objectNames;

        PlayerSlot primary = roster.GetBySlotId(0);
        if (primary != null)
        {
            GameOptions.characterObjectName = primary.Character.ObjectName;
            GameOptions.characterId = primary.Character.CharacterId;
            GameOptions.characterDisplayName = primary.Character.DisplayName;
        }

        // Lockdown's defender is not a roster slot - it is spawned by the mode - but the legacy
        // spawn path reads player2IsCpu to know it exists. Same shape ConfigureSingleHumanRoster
        // produced from (count, hasImplicitSecondCpu).
        bool implicitDefender = mode.AddsImplicitDefender;

        GameOptions.numPlayers = Mathf.Clamp(roster.Count, 1, PlayerRoster.MaxSlots);
        GameOptions.numCpuPlayers = roster.CpuCount + (implicitDefender ? 1 : 0);
        GameOptions.player1IsCpu = IsCpu(roster, 0);
        GameOptions.player2IsCpu = IsCpu(roster, 1) || implicitDefender;
        GameOptions.player3IsCpu = IsCpu(roster, 2);
        GameOptions.player4IsCpu = IsCpu(roster, 3);
    }

    private static void ApplyCheerleader(CheerleaderSelection cheerleader)
    {
        GameOptions.cheerleaderObjectName = cheerleader.ObjectName;
        GameOptions.cheerleaderDisplayName = cheerleader.DisplayName;

    }

    private static bool IsCpu(PlayerRoster roster, int slotId)
    {
        PlayerSlot slot = roster.GetBySlotId(slotId);
        return slot != null && slot.IsCpu;
    }
}
