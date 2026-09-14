using System;
using System.Collections.Generic;
using Level5.Core;
using Level5.Core.Match;
using UnityEngine;

public static class GameOptions
{
    static public string applicationVersion;
    static public string operatingSystemVersion;

    static public int numPlayers = 1;
    static public int numCpuPlayers;
    static public bool player1IsCpu;
    static public bool player2IsCpu;
    static public bool player3IsCpu;
    static public bool player4IsCpu;

    public static void ConfigureSingleHumanRoster(int rosterPlayerCount, bool hasImplicitSecondCpu = false)
    {
        numPlayers = Math.Max(1, Math.Min(4, rosterPlayerCount));
        numCpuPlayers = Math.Max(0, numPlayers - 1) + (hasImplicitSecondCpu ? 1 : 0);
        player1IsCpu = false;
        player2IsCpu = numPlayers > 1 || hasImplicitSecondCpu;
        player3IsCpu = numPlayers > 2;
        player4IsCpu = numPlayers > 3;
    }

    public static bool IsCpuPlayer(int playerId)
    {
        switch (playerId)
        {
            case 0: return player1IsCpu;
            case 1: return player2IsCpu;
            case 2: return player3IsCpu;
            case 3: return player4IsCpu;
            default: return false;
        }
    }

    public static int GetHumanPlayerInputSlot(int playerId)
    {
        if (IsCpuPlayer(playerId))
        {
            return -1;
        }

        int inputSlot = 0;
        for (int slot = 0; slot < playerId; slot++)
        {
            if (!IsCpuPlayer(slot))
            {
                inputSlot++;
            }
        }

        return inputSlot;
    }

    // selected options
    static public string characterDisplayName;
    static public string cheerleaderDisplayName;
    static public int characterId;
    static public string levelSelected;
    static public int gameModeSelectedId;
    static public int levelId;
    // object names
    static public string characterObjectName;
    static public List<string> characterObjectNames;
    static public string cheerleaderObjectName;
    static public string levelSelectedName;
    static public string levelDisplayName;
    static public string gameModeSelectedName;


    // game mode flags for game rules
    static public bool gameModeHasBeenSelected;
    static public bool gameModeRequiresCounter;
    static public bool gameModeRequiresCountDown;
    // if game requires markers be active
    // 3 / 4 point contest / moneyball / etc
    static public bool gameModeRequiresShotMarkers3s;
    static public bool gameModeRequiresShotMarkers4s;
    static public bool gameModeRequiresBasketball;
    static public bool gameModeAllowsCpuShooters;
    // 3 / 4 point contest + trequires timer
    static public bool gameModeThreePointContest;
    static public bool gameModeFourPointContest;
    static public bool gameModeAllPointContest;
    // custom timer used for 3 / 4 / all point contest, change from default of 120 to 80 / 160
    static public float customTimer;

    // start manager selected option indices
    // set default values = 0 (first element in list)
    // using values from game options, will load previous values on next load of start manager
    //
    // Player and CPU selection used to live here as catalog indices (playerSelectedIndex,
    // cpu1SelectedIndex, cpu2SelectedIndex, cpu3SelectedIndex). They are gone: an index is only
    // meaningful against the exact catalog that produced it, and reordering it silently changed
    // what a stored value meant. PlayerSelectionSession now remembers the same draft by stable
    // character id instead.
    static public int levelSelectedIndex = 0;
    static public int modeSelectedIndex = 0;
    static public int friendSelectedIndex = 0;

    static public bool trafficEnabled = false;
    static public bool enemiesEnabled = false;

    static public bool sniperEnabled = false;
    static public bool sniperEnabledBullet = false;
    static public bool sniperEnabledBulletAuto = false;
    static public bool sniperEnabledLaser = false;

    static public int difficultySelected = 1;

    static public string previousSceneName;

    static public bool hardcoreModeEnabled = false;
    static public bool EnemiesOnlyEnabled = false;

    static public bool levelRequiresTimeOfDay = true;
    static public bool levelRequiresWeather = false;
    static public bool levelHasSevenPointers = false;

    // AUD-012 Phase 2b: the backing state moved to Level5.Core.LocalAccountIdentity, so
    // CharacterProgressAccountId (Level5.Player) can read it without depending on Assembly-CSharp.
    // These forward to the same single owner - every existing caller keeps working unchanged.
    static public string userName { get => LocalAccountIdentity.UserName; set => LocalAccountIdentity.UserName = value; }
    static public int userid { get => LocalAccountIdentity.UserId; set => LocalAccountIdentity.UserId = value; }
    // The session bearer token deliberately does not live here. It is a credential; it belongs to
    // APIHelper for the life of the session. Ask APIHelper.HasSession instead.
    static public int numOfLocalUsers;

    static public bool tipDialogueLoadedOnStart;
    static public bool obstaclesEnabled;
    static public bool battleRoyalEnabled;
    static public bool cageMatchEnabled;

    static public bool gameModeRequiresShotMarkers7s;
    static public bool gameModeSevenPointContest;

    // campaign mode stats
    //static public bool isCampaignMode = true;
    static public List<LevelSelected> levelsList;
    //static public bool currentRoundWinnerIsCpu;
    //static public bool currentRoundLoserIsCpu;
    //static public int numberOfContinues = 0;
    //static public int currentRoundWinnerScore;
    //static public int currentRoundLoserScore;
    //static public Sprite currentRoundPlayerWinnerImage;
    //static public Sprite currentRoundPlayerLoserImage;
    //static public Sprite currentRoundCpuWinnerImage;
    //static public Sprite currentRoundCpuLoserImage;

    // AUD-012 Phase 2b Slice 52: MatchRuntime moved into Level5.Match and can no longer read these
    // fields directly for its direct-scene-entry fallback. It resolves through the one reader
    // installed below instead - see LegacyMatchRuntimeSnapshot for why the shape is resolved answers
    // (ResolvedMatchRules, PlayerRoster, typed mode/level identity, ...) rather than a mirror of every
    // field on this class. The reconstruction logic itself is unchanged from what used to live in
    // MatchRuntime.cs; only its owner moved, to the legacy side that already owns the fields it reads.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallMatchRuntimeLegacyFallback()
    {
        MatchRuntime.InstallLegacyFallbackReader(CaptureMatchRuntimeSnapshot);
    }

    /// <summary>
    /// Builds the answers <see cref="MatchRuntime"/> needs for a directly entered gameplay scene,
    /// read from these fields as they are right now. Called fresh on every fallback read - see
    /// <see cref="LegacyMatchRuntimeSnapshot"/> - never cached here either.
    ///
    /// Private: nothing outside this file needs to build one directly - production only ever needs
    /// the reader installed above, and a test that wants the real production mapping (rather than a
    /// fake) reaches this through reflection, the same pattern several fixtures already use for a
    /// private field or method (e.g. the <c>GetPrivateField</c>/<c>InvokePrivate</c> helpers in
    /// <c>Level5PlayerMatchRuntimeCompositionTests</c>).
    /// </summary>
    private static LegacyMatchRuntimeSnapshot CaptureMatchRuntimeSnapshot()
    {
        return new LegacyMatchRuntimeSnapshot(
            rules: BuildResolvedMatchRulesFromLegacyGlobals,
            roster: BuildPlayerRosterFromLegacyGlobals,
            modeId: GameModeIds.FromInt(gameModeSelectedId),
            rawModeId: gameModeSelectedId,
            modeDisplayName: gameModeSelectedName,
            levelDisplayName: levelDisplayName,
            levelId: levelId,
            levelRequiresTimeOfDay: levelRequiresTimeOfDay,
            levelHasWeather: levelRequiresWeather,
            levelHasSevenPointers: levelHasSevenPointers,
            primaryCharacterDisplayName: characterDisplayName,
            primaryCharacterObjectName: characterObjectName,
            primaryCharacterId: characterId,
            cheerleader: new CheerleaderSelection(0, cheerleaderObjectName, cheerleaderDisplayName),
            getHumanPlayerInputSlot: GetHumanPlayerInputSlot);
    }

    private static ResolvedMatchRules BuildResolvedMatchRulesFromLegacyGlobals()
    {
        return new ResolvedMatchRules(
            objective: MatchObjective.Score,
            clockMode: LegacyClockMode(),
            customTimerSeconds: customTimer,
            matchLengthSeconds: MatchClock.StartSeconds(customTimer),
            combatMode: LegacyCombatMode(),
            shotRule: LegacyShotRule(),
            shotMarkers: LegacyShotMarkers(),
            requiresBasketball: gameModeRequiresBasketball,
            basketballCount: Mathf.Max(1, numPlayers),
            allowsCpuShooters: gameModeAllowsCpuShooters,
            enemiesEnabled: enemiesEnabled,
            trafficEnabled: trafficEnabled,
            obstaclesEnabled: obstaclesEnabled,
            sniper: LegacySniperMode(),
            difficulty: MatchDifficulties.FromInt(difficultySelected),
            hardcore: hardcoreModeEnabled,
            addsImplicitDefender: gameModeSelectedId == Modes.Lockdown,
            enemiesOnly: EnemiesOnlyEnabled);
    }

    private static MatchClockMode LegacyClockMode()
    {
        if (gameModeRequiresCountDown)
        {
            return MatchClockMode.Countdown;
        }

        return gameModeRequiresCounter ? MatchClockMode.CountUp : MatchClockMode.None;
    }

    private static CombatMode LegacyCombatMode()
    {
        CombatMode combat = CombatMode.None;
        if (battleRoyalEnabled)
        {
            combat |= CombatMode.BattleRoyal;
        }

        if (cageMatchEnabled)
        {
            combat |= CombatMode.Cage;
        }

        if (combat == CombatMode.None && EnemiesOnlyEnabled)
        {
            combat = CombatMode.Standard;
        }

        return combat;
    }

    private static ShotRule LegacyShotRule()
    {
        ShotRule rule = ShotRule.Any;
        if (gameModeThreePointContest)
        {
            rule |= ShotRule.ThreePoint;
        }

        if (gameModeFourPointContest)
        {
            rule |= ShotRule.FourPoint;
        }

        if (gameModeSevenPointContest)
        {
            rule |= ShotRule.SevenPoint;
        }

        if (gameModeAllPointContest)
        {
            rule |= ShotRule.AllRanges;
        }

        return rule;
    }

    private static ShotMarkerRequirement LegacyShotMarkers()
    {
        ShotMarkerRequirement markers = ShotMarkerRequirement.None;
        if (gameModeRequiresShotMarkers3s)
        {
            markers |= ShotMarkerRequirement.ThreePoint;
        }

        if (gameModeRequiresShotMarkers4s)
        {
            markers |= ShotMarkerRequirement.FourPoint;
        }

        if (gameModeRequiresShotMarkers7s)
        {
            markers |= ShotMarkerRequirement.SevenPoint;
        }

        return markers;
    }

    private static SniperMode LegacySniperMode()
    {
        if (sniperEnabledLaser)
        {
            return SniperMode.Laser;
        }

        if (sniperEnabledBulletAuto)
        {
            return SniperMode.MachineGun;
        }

        return sniperEnabledBullet ? SniperMode.Bullet : SniperMode.None;
    }

    private static PlayerRoster BuildPlayerRosterFromLegacyGlobals()
    {
        int count = Mathf.Clamp(numPlayers, 1, PlayerRoster.MaxSlots);
        List<PlayerRosterEntry> entries = new List<PlayerRosterEntry>();

        for (int slot = 0; slot < count; slot++)
        {
            string objectName = characterObjectNames != null && slot < characterObjectNames.Count
                ? characterObjectNames[slot]
                : string.Empty;

            CharacterSelection character = new CharacterSelection(
                slot == 0 ? characterId : 0,
                objectName,
                slot == 0 ? characterDisplayName : objectName,
                true,
                true);

            entries.Add(new PlayerRosterEntry(
                IsCpuPlayer(slot) ? PlayerControlType.Cpu : PlayerControlType.LocalHuman,
                character));
        }

        return PlayerRoster.Build(entries);
    }
}
