using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Progression;

/// <summary>
/// What the start menu currently has selected for mode/level/friend/options, and nothing else.
///
/// Player and CPU selection moved to the player-select subsystem
/// (<see cref="PlayerSelectCoordinator"/>); this class no longer owns a character index or builds
/// a roster. It receives an already-built <see cref="PlayerRoster"/> and turns the rest of the
/// current selection into a <see cref="MatchRequest"/>.
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
    public int FriendIndex { get; set; }

    public int LevelIndex { get; set; }

    public int ModeIndex { get; set; }

    public bool TrafficEnabled { get; set; }

    public bool HardcoreEnabled { get; set; }

    public bool EnemiesEnabled { get; set; }

    public bool ObstaclesEnabled { get; set; }

    public SniperMode Sniper { get; set; } = SniperMode.None;

    public MatchDifficulty Difficulty { get; set; } = MatchDifficulty.Normal;

    // ---- persistence of menu preferences ----------------------------------------------------
    // The only place that touches the GameOptions menu-index fields this class still owns. Player
    // and CPU selection persist through PlayerSelectionSession instead (plan phase 6).

    public void LoadPersistedPreferences()
    {
        FriendIndex = GameOptions.friendSelectedIndex;
        LevelIndex = GameOptions.levelSelectedIndex;
        ModeIndex = GameOptions.modeSelectedIndex;
        TrafficEnabled = GameOptions.trafficEnabled;
        HardcoreEnabled = GameOptions.hardcoreModeEnabled;
        ObstaclesEnabled = GameOptions.obstaclesEnabled;

        // Difficulty deliberately does not persist: the menu has always reset it to normal on load.
        Difficulty = MatchDifficulty.Normal;
    }

    public void SavePersistedPreferences()
    {
        GameOptions.friendSelectedIndex = FriendIndex;
        GameOptions.levelSelectedIndex = LevelIndex;
        GameOptions.modeSelectedIndex = ModeIndex;
    }

    // ---- bounded cycling ----------------------------------------------------------------------

    /// <summary>
    /// Moves the level selection by one, skipping arenas the current mode cannot use, that are not
    /// selectable, or that this account has not unlocked.
    ///
    /// The old version called itself until it found one, so a mode with no compatible level
    /// recursed until the stack ran out. This walks the catalog at most once and stops.
    ///
    /// <paramref name="unlock"/> may be null - callers that have not been migrated get the previous
    /// mode/arena-only behavior, unchanged.
    /// </summary>
    public void CycleLevel(GameModeCompatibility compatibility, int step, UnlockSnapshot unlock = null)
    {
        if (compatibility == null || compatibility.Levels.Count == 0)
        {
            return;
        }

        GameModeDefinition mode = CurrentMode(compatibility);
        LevelIndex = LevelEligibility.NextEligibleLevelIndex(compatibility, mode, LevelIndex, step, unlock);
    }

    /// <summary>Moves the mode selection by one, then pulls the level to something eligible for it.</summary>
    public void CycleMode(GameModeCompatibility compatibility, int step, UnlockSnapshot unlock = null)
    {
        if (compatibility == null || compatibility.Modes.Count == 0)
        {
            return;
        }

        ModeIndex = IndexMath.Wrap(ModeIndex + step, compatibility.Modes.Count);
        LevelIndex = LevelEligibility.EligibleLevelIndexFor(compatibility, CurrentMode(compatibility), LevelIndex, step, unlock);
    }

    public void CycleFriend(int friendCount, int step)
    {
        if (friendCount <= 0)
        {
            return;
        }

        FriendIndex = IndexMath.Wrap(FriendIndex + step, friendCount);
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

        return compatibility.Modes.Definitions[IndexMath.Wrap(ModeIndex, compatibility.Modes.Count)];
    }

    public LevelDefinition CurrentLevel(GameModeCompatibility compatibility)
    {
        if (compatibility == null || compatibility.Levels.Count == 0)
        {
            return null;
        }

        return compatibility.Levels.Definitions[IndexMath.Wrap(LevelIndex, compatibility.Levels.Count)];
    }

    // ---- request construction -----------------------------------------------------------------

    /// <summary>
    /// Turns the current selection into a request, given an already-built roster. This no longer
    /// builds or mutates the roster itself - the player-select subsystem does that, explicitly,
    /// before this is called. Reconciling a required CPU opponent is player select's job
    /// (<c>PlayerSelectCoordinator.TryBuildRoster</c>); building a request here never changes what
    /// was selected.
    /// </summary>
    public MatchRequest BuildRequest(
        GameModeCompatibility compatibility,
        PlayerRoster roster,
        IReadOnlyList<CheerleaderProfile> cheerleaders)
    {
        GameModeDefinition mode = CurrentMode(compatibility);
        LevelDefinition level = CurrentLevel(compatibility);
        if (mode == null || level == null || roster == null)
        {
            return null;
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
            roster,
            modifiers,
            ToSelection(Get(cheerleaders, FriendIndex)),
            "start menu");
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

    private static T Get<T>(IReadOnlyList<T> list, int index) where T : class
    {
        return list != null && index >= 0 && index < list.Count ? list[index] : null;
    }

}
