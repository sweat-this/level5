using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// What the start menu currently has selected, and nothing else.
///
/// Browsing the menu changes only this. Nothing here writes gameplay configuration - highlighting a
/// level used to set <c>GameOptions.levelSelected</c>, and cycling a mode used to set
/// <c>gameModeSelectedId</c>, which meant simply looking at an option changed what the next match
/// would be. Pressing start turns this into a <see cref="MatchRequest"/>; the builder decides the
/// rest.
///
/// The cycling helpers take the catalogs as arguments rather than holding them so this stays a
/// plain object the tests can drive without a scene.
/// </summary>
public sealed class StartMenuSelectionState
{
    public int PlayerIndex { get; set; }

    public int FriendIndex { get; set; }

    public int LevelIndex { get; set; }

    public int ModeIndex { get; set; }

    public int Cpu1Index { get; set; }

    public int Cpu2Index { get; set; }

    public int Cpu3Index { get; set; }

    public bool TrafficEnabled { get; set; }

    public bool HardcoreEnabled { get; set; }

    public bool EnemiesEnabled { get; set; }

    public bool ObstaclesEnabled { get; set; }

    public SniperMode Sniper { get; set; } = SniperMode.None;

    public MatchDifficulty Difficulty { get; set; } = MatchDifficulty.Normal;

    /// <summary>How many participants the current CPU picks add up to, including the player.</summary>
    public int ParticipantCount => 1
        + (Cpu1Index != 0 ? 1 : 0)
        + (Cpu2Index != 0 ? 1 : 0)
        + (Cpu3Index != 0 ? 1 : 0);

    public int GetCpuIndex(int cpuSlot)
    {
        switch (cpuSlot)
        {
            case 1: return Cpu1Index;
            case 2: return Cpu2Index;
            case 3: return Cpu3Index;
            default: return 0;
        }
    }

    public void SetCpuIndex(int cpuSlot, int value)
    {
        switch (cpuSlot)
        {
            case 1:
                Cpu1Index = value;
                break;
            case 2:
                Cpu2Index = value;
                break;
            case 3:
                Cpu3Index = value;
                break;
        }
    }

    // ---- persistence of menu preferences ----------------------------------------------------
    // The only place that touches the GameOptions menu-index fields. When those move to a menu
    // preference owner (plan phase 11) this is the seam that changes, not every call site.

    public void LoadPersistedPreferences()
    {
        PlayerIndex = GameOptions.playerSelectedIndex;
        FriendIndex = GameOptions.friendSelectedIndex;
        LevelIndex = GameOptions.levelSelectedIndex;
        ModeIndex = GameOptions.modeSelectedIndex;
        Cpu1Index = GameOptions.cpu1SelectedIndex;
        Cpu2Index = GameOptions.cpu2SelectedIndex;
        Cpu3Index = GameOptions.cpu3SelectedIndex;
        TrafficEnabled = GameOptions.trafficEnabled;
        HardcoreEnabled = GameOptions.hardcoreModeEnabled;
        ObstaclesEnabled = GameOptions.obstaclesEnabled;

        // Difficulty deliberately does not persist: the menu has always reset it to normal on load.
        Difficulty = MatchDifficulty.Normal;
    }

    public void SavePersistedPreferences()
    {
        GameOptions.playerSelectedIndex = PlayerIndex;
        GameOptions.friendSelectedIndex = FriendIndex;
        GameOptions.levelSelectedIndex = LevelIndex;
        GameOptions.modeSelectedIndex = ModeIndex;
        GameOptions.cpu1SelectedIndex = Cpu1Index;
        GameOptions.cpu2SelectedIndex = Cpu2Index;
        GameOptions.cpu3SelectedIndex = Cpu3Index;
    }

    // ---- bounded cycling ----------------------------------------------------------------------

    /// <summary>
    /// Moves the level selection by one, skipping arenas the current mode cannot use.
    ///
    /// The old version called itself until it found one, so a mode with no compatible level
    /// recursed until the stack ran out. This walks the catalog at most once and stops.
    /// </summary>
    public void CycleLevel(GameModeCompatibility compatibility, int step)
    {
        if (compatibility == null || compatibility.Levels.Count == 0)
        {
            return;
        }

        GameModeDefinition mode = CurrentMode(compatibility);
        LevelIndex = compatibility.NextCompatibleLevelIndex(mode, LevelIndex, step);
    }

    /// <summary>Moves the mode selection by one, then pulls the level to something the mode can use.</summary>
    public void CycleMode(GameModeCompatibility compatibility, int step)
    {
        if (compatibility == null || compatibility.Modes.Count == 0)
        {
            return;
        }

        ModeIndex = Wrap(ModeIndex + step, compatibility.Modes.Count);
        LevelIndex = compatibility.CompatibleLevelIndexFor(CurrentMode(compatibility), LevelIndex, step);
    }

    /// <summary>
    /// Moves the character selection by one, skipping characters that cannot play the way the
    /// current mode and modifiers require - a fighting setup needs a fighter, a shooting setup
    /// needs a shooter. Bounded for the same reason as <see cref="CycleLevel"/>.
    /// </summary>
    public void CyclePlayer(IReadOnlyList<CharacterProfile> characters, GameModeCompatibility compatibility, int step)
    {
        if (characters == null || characters.Count == 0 || step == 0)
        {
            return;
        }

        GameModeDefinition mode = CurrentMode(compatibility);
        bool fightersRequired = EnemiesEnabled || (mode != null && mode.EnemiesOnly);

        for (int offset = 1; offset <= characters.Count; offset++)
        {
            int index = Wrap(PlayerIndex + (offset * step), characters.Count);
            CharacterProfile candidate = characters[index];
            if (candidate == null)
            {
                continue;
            }

            if (fightersRequired ? candidate.IsFighter : candidate.IsShooter)
            {
                PlayerIndex = index;
                return;
            }
        }

        // Nothing qualifies. Step once anyway so the control still responds, rather than looking
        // broken; the launch validation is what refuses an unplayable combination.
        PlayerIndex = Wrap(PlayerIndex + step, characters.Count);
    }

    public void CycleFriend(int friendCount, int step)
    {
        if (friendCount <= 0)
        {
            return;
        }

        FriendIndex = Wrap(FriendIndex + step, friendCount);
    }

    public void CycleCpu(int cpuSlot, int cpuCount, int step)
    {
        if (cpuCount <= 0)
        {
            return;
        }

        SetCpuIndex(cpuSlot, Wrap(GetCpuIndex(cpuSlot) + step, cpuCount));
    }

    public void CycleDifficulty()
    {
        Difficulty = Difficulty == MatchDifficulty.Hardcore
            ? MatchDifficulty.Easy
            : (MatchDifficulty)(MatchDifficulties.ToInt(Difficulty) + 1);
    }

    /// <summary>Cycles OFF -> bullet -> machine gun -> laser -> OFF, as the menu always has.</summary>
    public void CycleSniper()
    {
        switch (Sniper)
        {
            case SniperMode.None:
                Sniper = SniperMode.Bullet;
                break;
            case SniperMode.Bullet:
                Sniper = SniperMode.MachineGun;
                break;
            case SniperMode.MachineGun:
                Sniper = SniperMode.Laser;
                break;
            default:
                Sniper = SniperMode.None;
                break;
        }
    }

    public GameModeDefinition CurrentMode(GameModeCompatibility compatibility)
    {
        if (compatibility == null || compatibility.Modes.Count == 0)
        {
            return null;
        }

        return compatibility.Modes.Definitions[Wrap(ModeIndex, compatibility.Modes.Count)];
    }

    public LevelDefinition CurrentLevel(GameModeCompatibility compatibility)
    {
        if (compatibility == null || compatibility.Levels.Count == 0)
        {
            return null;
        }

        return compatibility.Levels.Definitions[Wrap(LevelIndex, compatibility.Levels.Count)];
    }

    // ---- request construction -----------------------------------------------------------------

    /// <summary>
    /// Turns the current selection into a request. The roster is one local human followed by the
    /// chosen CPUs, which is the shape every current mode launches with; a mode that always plays
    /// against the computer gets one even when the player picked none.
    /// </summary>
    public MatchRequest BuildRequest(
        GameModeCompatibility compatibility,
        IReadOnlyList<CharacterProfile> characters,
        IReadOnlyList<CharacterProfile> cpuCharacters,
        IReadOnlyList<CheerleaderProfile> cheerleaders,
        string playerObjectNameOverride = null)
    {
        GameModeDefinition mode = CurrentMode(compatibility);
        LevelDefinition level = CurrentLevel(compatibility);
        if (mode == null || level == null)
        {
            return null;
        }

        List<PlayerRosterEntry> entries = new List<PlayerRosterEntry>
        {
            PlayerRosterEntry.LocalHuman(ToSelection(Get(characters, PlayerIndex), playerObjectNameOverride))
        };

        // Lockdown brings its own defender and ignores the CPU picks entirely, exactly as the old
        // launch path did when it skipped them for that mode.
        if (!mode.AddsImplicitDefender)
        {
            AddCpu(entries, cpuCharacters, Cpu1Index);
            AddCpu(entries, cpuCharacters, Cpu2Index);
            AddCpu(entries, cpuCharacters, Cpu3Index);

            if (mode.RequiresCpuOpponent && entries.Count == 1)
            {
                // Index 1 is the first real CPU character; index 0 is the "none" entry.
                Cpu1Index = 1;
                AddCpu(entries, cpuCharacters, Cpu1Index);
            }
        }

        MatchModifiers modifiers = new MatchModifiers(
            difficulty: Difficulty,
            trafficRequested: TrafficEnabled,
            enemiesRequested: EnemiesEnabled,
            obstaclesRequested: ObstaclesEnabled,
            sniper: Sniper,
            hardcoreRequested: HardcoreEnabled);

        return new MatchRequest(
            mode.Id,
            level.LevelId,
            PlayerRoster.Build(entries),
            modifiers,
            ToSelection(Get(cheerleaders, FriendIndex)),
            "start menu");
    }

    private static void AddCpu(List<PlayerRosterEntry> entries, IReadOnlyList<CharacterProfile> cpuCharacters, int index)
    {
        if (index == 0)
        {
            return;
        }

        CharacterProfile profile = Get(cpuCharacters, index);
        if (profile != null)
        {
            entries.Add(PlayerRosterEntry.Cpu(ToSelection(profile)));
        }
    }

    private static T Get<T>(IReadOnlyList<T> list, int index) where T : class
    {
        return list != null && index >= 0 && index < list.Count ? list[index] : null;
    }

    public static CharacterSelection ToSelection(CharacterProfile profile, string objectNameOverride = null)
    {
        if (profile == null)
        {
            return CharacterSelection.None;
        }

        return new CharacterSelection(
            profile.PlayerId,
            string.IsNullOrEmpty(objectNameOverride) ? profile.PlayerObjectName : objectNameOverride,
            profile.PlayerDisplayName,
            profile.IsShooter,
            profile.IsFighter);
    }

    public static CheerleaderSelection ToSelection(CheerleaderProfile profile)
    {
        if (profile == null)
        {
            return CheerleaderSelection.None;
        }

        return new CheerleaderSelection(
            profile.CheerleaderId,
            profile.CheerleaderObjectName,
            profile.CheerleaderDisplayName,
            profile.bonus3Accuracy,
            profile.bonus4Accuracy,
            profile.bonus7Accuracy,
            profile.bonusRelease,
            profile.bonusRange,
            profile.bonusLuck,
            profile.bonusClutch);
    }

    private static int Wrap(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        int wrapped = value % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
