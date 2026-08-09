using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// How a gameplay scene gets the rules it is being played under.
///
/// Normally that is the <see cref="ActiveMatch"/> configuration the launch path built and
/// validated. A gameplay scene can also be entered with no launch at all - pressing play on the
/// scene in the editor, or reloading one directly - and that has to keep working, so this falls
/// back to reading the legacy globals.
///
/// The fallback is read-only and one-shot. It never writes back into a configuration and never
/// becomes authoritative: when a real match is running, <see cref="ActiveMatch"/> is the answer and
/// the globals are only its shadow.
/// </summary>
public static class MatchRuntime
{
    public static MatchConfiguration Configuration => ActiveMatch.Configuration;

    public static bool HasConfiguration => ActiveMatch.IsActive;

    /// <summary>The resolved rules for this match, or the legacy globals for a directly entered scene.</summary>
    public static ResolvedMatchRules Rules
    {
        get
        {
            MatchConfiguration configuration = Configuration;
            // Deliberately rebuilt each time rather than cached: the fallback exists to reflect
            // the globals as they are now, and code outside the configuration still writes them.
            // A cached answer would be a third source of truth with its own staleness.
            return configuration != null ? configuration.Rules : RulesFromLegacyGlobals();
        }
    }

    /// <summary>The roster for this match, or one reconstructed from the legacy globals.</summary>
    public static PlayerRoster Roster
    {
        get
        {
            MatchConfiguration configuration = Configuration;
            return configuration != null ? configuration.Roster : RosterFromLegacyGlobals();
        }
    }

    public static GameModeId ModeId => Configuration != null
        ? Configuration.ModeId
        : GameModeIds.FromInt(GameOptions.gameModeSelectedId);

    /// <summary>The arena this match is in, or null when the scene was entered directly.</summary>
    public static LevelDefinition Level => Configuration != null ? Configuration.Level : null;

    // Arena facts gameplay reads. Each falls back to the legacy global, so this stays the one place
    // that knows the fallback exists rather than every caller having to.

    public static bool CustomCamera => Level != null && Level.CustomCamera;

    public static bool LevelRequiresTimeOfDay => Level != null
        ? Level.RequiresTimeOfDay
        : GameOptions.levelRequiresTimeOfDay;

    public static bool LevelHasWeather => Level != null ? Level.HasWeather : GameOptions.levelRequiresWeather;

    public static bool LevelHasSevenPointers => Level != null
        ? Level.HasSevenPointers
        : GameOptions.levelHasSevenPointers;

    public static string LevelDisplayName => Level != null ? Level.DisplayName : GameOptions.levelDisplayName;

    public static int LevelId => Level != null ? Level.LevelId : GameOptions.levelId;

    /// <summary>The display name of the character in slot zero.</summary>
    public static string PrimaryCharacterDisplayName
    {
        get
        {
            PlayerSlot primary = PrimarySlot;
            return primary != null && primary.Character != null && !string.IsNullOrEmpty(primary.Character.DisplayName)
                ? primary.Character.DisplayName
                : GameOptions.characterDisplayName;
        }
    }

    /// <summary>The stored mode number, for the call sites still comparing against <c>Modes</c>.</summary>
    public static int RawModeId => Configuration != null
        ? Configuration.Mode.RawModeId
        : GameOptions.gameModeSelectedId;

    public static string ModeDisplayName => Configuration != null
        ? Configuration.Mode.DisplayName
        : GameOptions.gameModeSelectedName;

    /// <summary>How many participants are in the match.</summary>
    public static int ParticipantCount => Roster.Count;

    /// <summary>The cheerleader and the shooting bonuses she contributes.</summary>
    public static CheerleaderSelection Cheerleader => Configuration != null
        ? Configuration.Cheerleader
        : CheerleaderFromLegacyGlobals();

    /// <summary>The prefab name of the character in slot zero - the one the player is playing as.</summary>
    public static string PrimaryCharacterObjectName
    {
        get
        {
            PlayerSlot primary = PrimarySlot;
            return primary != null && primary.Character != null && !string.IsNullOrEmpty(primary.Character.ObjectName)
                ? primary.Character.ObjectName
                : GameOptions.characterObjectName;
        }
    }

    /// <summary>The id of the character in slot zero.</summary>
    public static int PrimaryCharacterId
    {
        get
        {
            PlayerSlot primary = PrimarySlot;
            return primary != null && primary.Character != null && primary.Character.CharacterId != 0
                ? primary.Character.CharacterId
                : GameOptions.characterId;
        }
    }

    /// <summary>
    /// Which local input device a participant listens to, or -1 when it is not a local human.
    /// The roster assigns these once at build time instead of counting past the CPUs on every ask.
    /// </summary>
    public static int LocalInputSlotFor(int slotId)
    {
        PlayerSlot slot = Roster.GetBySlotId(slotId);
        if (slot == null)
        {
            return GameOptions.GetHumanPlayerInputSlot(slotId);
        }

        return slot.LocalInputSlot ?? -1;
    }

    private static PlayerSlot PrimarySlot => Roster.GetBySlotId(0);

    /// <summary>
    /// The cheerleader for a directly entered scene. Only the identity survives as a global; the
    /// bonuses were deleted with their last consumer, so they come back as zero here. A scene
    /// entered without a launch has no cheerleader bonuses, which is the honest answer.
    /// </summary>
    private static CheerleaderSelection CheerleaderFromLegacyGlobals()
    {
        return new CheerleaderSelection(0, GameOptions.cheerleaderObjectName, GameOptions.cheerleaderDisplayName);
    }

    /// <summary>
    /// Warns once per scene when gameplay is running without a validated configuration, so the
    /// difference between "launched from the menu" and "entered directly" is visible in the log
    /// rather than being something to guess at from behaviour.
    /// </summary>
    public static void WarnIfUnconfigured(Object context)
    {
        if (HasConfiguration)
        {
            return;
        }

        Debug.LogWarning(
            "This scene is running without a validated match configuration, so its rules are being "
            + "read from the legacy GameOptions globals. That is expected when playing a gameplay "
            + "scene directly; launching from the start menu builds a configuration.",
            context);
    }

    private static ResolvedMatchRules RulesFromLegacyGlobals()
    {
        return new ResolvedMatchRules(
            objective: MatchObjective.Score,
            clockMode: LegacyClockMode(),
            customTimerSeconds: GameOptions.customTimer,
            matchLengthSeconds: MatchClock.StartSeconds(GameOptions.customTimer),
            combatMode: LegacyCombatMode(),
            shotRule: LegacyShotRule(),
            shotMarkers: LegacyShotMarkers(),
            requiresBasketball: GameOptions.gameModeRequiresBasketball,
            basketballCount: Mathf.Max(1, GameOptions.numPlayers),
            allowsCpuShooters: GameOptions.gameModeAllowsCpuShooters,
            enemiesEnabled: GameOptions.enemiesEnabled,
            trafficEnabled: GameOptions.trafficEnabled,
            obstaclesEnabled: GameOptions.obstaclesEnabled,
            sniper: LegacySniperMode(),
            difficulty: MatchDifficulties.FromInt(GameOptions.difficultySelected),
            hardcore: GameOptions.hardcoreModeEnabled,
            addsImplicitDefender: GameOptions.gameModeSelectedId == Modes.Lockdown,
            enemiesOnly: GameOptions.EnemiesOnlyEnabled);
    }

    private static MatchClockMode LegacyClockMode()
    {
        if (GameOptions.gameModeRequiresCountDown)
        {
            return MatchClockMode.Countdown;
        }

        return GameOptions.gameModeRequiresCounter ? MatchClockMode.CountUp : MatchClockMode.None;
    }

    private static CombatMode LegacyCombatMode()
    {
        CombatMode combat = CombatMode.None;
        if (GameOptions.battleRoyalEnabled)
        {
            combat |= CombatMode.BattleRoyal;
        }

        if (GameOptions.cageMatchEnabled)
        {
            combat |= CombatMode.Cage;
        }

        if (combat == CombatMode.None && GameOptions.EnemiesOnlyEnabled)
        {
            combat = CombatMode.Standard;
        }

        return combat;
    }

    private static ShotRule LegacyShotRule()
    {
        ShotRule rule = ShotRule.Any;
        if (GameOptions.gameModeThreePointContest)
        {
            rule |= ShotRule.ThreePoint;
        }

        if (GameOptions.gameModeFourPointContest)
        {
            rule |= ShotRule.FourPoint;
        }

        if (GameOptions.gameModeSevenPointContest)
        {
            rule |= ShotRule.SevenPoint;
        }

        if (GameOptions.gameModeAllPointContest)
        {
            rule |= ShotRule.AllRanges;
        }

        return rule;
    }

    private static ShotMarkerRequirement LegacyShotMarkers()
    {
        ShotMarkerRequirement markers = ShotMarkerRequirement.None;
        if (GameOptions.gameModeRequiresShotMarkers3s)
        {
            markers |= ShotMarkerRequirement.ThreePoint;
        }

        if (GameOptions.gameModeRequiresShotMarkers4s)
        {
            markers |= ShotMarkerRequirement.FourPoint;
        }

        if (GameOptions.gameModeRequiresShotMarkers7s)
        {
            markers |= ShotMarkerRequirement.SevenPoint;
        }

        return markers;
    }

    private static SniperMode LegacySniperMode()
    {
        if (GameOptions.sniperEnabledLaser)
        {
            return SniperMode.Laser;
        }

        if (GameOptions.sniperEnabledBulletAuto)
        {
            return SniperMode.MachineGun;
        }

        return GameOptions.sniperEnabledBullet ? SniperMode.Bullet : SniperMode.None;
    }

    private static PlayerRoster RosterFromLegacyGlobals()
    {
        int count = Mathf.Clamp(GameOptions.numPlayers, 1, PlayerRoster.MaxSlots);
        List<PlayerRosterEntry> entries = new List<PlayerRosterEntry>();

        for (int slot = 0; slot < count; slot++)
        {
            string objectName = GameOptions.characterObjectNames != null
                && slot < GameOptions.characterObjectNames.Count
                    ? GameOptions.characterObjectNames[slot]
                    : string.Empty;

            CharacterSelection character = new CharacterSelection(
                slot == 0 ? GameOptions.characterId : 0,
                objectName,
                slot == 0 ? GameOptions.characterDisplayName : objectName,
                true,
                true);

            entries.Add(new PlayerRosterEntry(
                GameOptions.IsCpuPlayer(slot) ? PlayerControlType.Cpu : PlayerControlType.LocalHuman,
                character));
        }

        return PlayerRoster.Build(entries);
    }
}
