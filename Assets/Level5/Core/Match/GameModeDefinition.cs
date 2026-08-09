using UnityEngine;

namespace Level5.Core.Match
{
    /// <summary>
    /// Authored rules for one game mode: the single source of truth for what a mode is.
    ///
    /// This describes rules. It must never hold score, time, the selected player, or anything else
    /// that changes while a match is played - a ScriptableObject written at runtime keeps its value
    /// in the editor between play sessions, which is how authored data silently rots.
    ///
    /// During migration these are produced from the legacy <c>StartScreenModeSelected</c> prefab
    /// components by <c>GameModeDefinitionFactory</c>, and a parity validator asserts the two agree.
    /// </summary>
    [CreateAssetMenu(menuName = "Level 5/Match/Game Mode Definition", fileName = "GameModeDefinition")]
    public class GameModeDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable numeric id. This is contract with save data and the backend - never change it.")]
        [SerializeField] private int modeId;
        [SerializeField] private string displayName;
        [SerializeField] private string objectName;
        [SerializeField] private string description;
        [SerializeField] private string highScoreField;

        [Header("Rules")]
        [SerializeField] private MatchObjective objective = MatchObjective.Score;
        [SerializeField] private MatchClockMode clockMode = MatchClockMode.Countdown;
        [Tooltip("Match length in seconds. 0 means use the default match length.")]
        [SerializeField] private float customTimerSeconds;
        [SerializeField] private CombatMode combatMode = CombatMode.None;
        [SerializeField] private ShotRule shotRule = ShotRule.Any;
        [SerializeField] private ShotMarkerRequirement shotMarkers = ShotMarkerRequirement.None;

        [Header("Requirements")]
        [SerializeField] private bool requiresBasketball = true;
        [SerializeField] private bool requiresMoneyBall;
        [SerializeField] private bool requiresConsecutiveShots;
        [SerializeField] private bool requiresPlayerSurvive;
        [SerializeField] private bool allowsCpuShooters = true;
        [Tooltip("The mode is a fighting mode: enemies instead of shooting.")]
        [SerializeField] private bool enemiesOnly;
        [SerializeField] private bool arcadeMode;

        [Header("Roster")]
        [SerializeField] private int minPlayers = 1;
        [SerializeField] private int maxPlayers = PlayerRoster.MaxSlots;
        [Tooltip("The mode always adds one CPU opponent even when the player picked none.")]
        [SerializeField] private bool requiresCpuOpponent;
        [Tooltip("The mode adds a defender the roster count does not include (Lockdown).")]
        [SerializeField] private bool addsImplicitDefender;

        [Header("Arena")]
        [SerializeField] private ArenaCapability requiredArenaCapabilities = ArenaCapability.None;
        [SerializeField] private ArenaCapability forbiddenArenaCapabilities = ArenaCapability.None;

        public GameModeId Id => GameModeIds.FromInt(modeId);

        /// <summary>The raw stored id, including ids this build does not declare.</summary>
        public int RawModeId => modeId;

        public string DisplayName => displayName;

        public string ObjectName => objectName;

        public string Description => description;

        public string HighScoreField => highScoreField;

        public MatchObjective Objective => objective;

        public MatchClockMode ClockMode => clockMode;

        public float CustomTimerSeconds => customTimerSeconds;

        public CombatMode CombatMode => combatMode;

        public ShotRule ShotRule => shotRule;

        public ShotMarkerRequirement ShotMarkers => shotMarkers;

        public bool RequiresBasketball => requiresBasketball;

        public bool RequiresMoneyBall => requiresMoneyBall;

        public bool RequiresConsecutiveShots => requiresConsecutiveShots;

        public bool RequiresPlayerSurvive => requiresPlayerSurvive;

        public bool AllowsCpuShooters => allowsCpuShooters;

        public bool EnemiesOnly => enemiesOnly;

        public bool ArcadeMode => arcadeMode;

        public int MinPlayers => minPlayers;

        public int MaxPlayers => maxPlayers;

        public bool RequiresCpuOpponent => requiresCpuOpponent;

        public bool AddsImplicitDefender => addsImplicitDefender;

        public ArenaCapability RequiredArenaCapabilities => requiredArenaCapabilities;

        public ArenaCapability ForbiddenArenaCapabilities => forbiddenArenaCapabilities;

        // ---- derived legacy views -------------------------------------------------------------
        // These exist so the legacy bridge and the parity validator can reproduce the old booleans
        // exactly without any of them being stored twice. New code should read the dimensions above.

        public bool IsThreePointContest => (shotRule & ShotRule.ThreePoint) != 0;

        public bool IsFourPointContest => (shotRule & ShotRule.FourPoint) != 0;

        public bool IsSevenPointContest => (shotRule & ShotRule.SevenPoint) != 0;

        public bool IsAllPointContest => (shotRule & ShotRule.AllRanges) != 0;

        public bool IsContest => shotRule != ShotRule.Any;

        public bool IsBattleRoyal => (combatMode & CombatMode.BattleRoyal) != 0;

        public bool IsCageMatch => (combatMode & CombatMode.Cage) != 0;

        public bool UsesEnemies => combatMode != CombatMode.None || enemiesOnly;

        public bool RequiresCountDown => clockMode == MatchClockMode.Countdown;

        public bool RequiresCounter => clockMode == MatchClockMode.CountUp;

        public bool RequiresShotMarkers3s => (shotMarkers & ShotMarkerRequirement.ThreePoint) != 0;

        public bool RequiresShotMarkers4s => (shotMarkers & ShotMarkerRequirement.FourPoint) != 0;

        public bool RequiresShotMarkers7s => (shotMarkers & ShotMarkerRequirement.SevenPoint) != 0;

        /// <summary>
        /// Builds a definition in code. Used by the legacy factory, by the editor migration utility
        /// and by tests; authored assets fill the serialized fields through the inspector instead.
        /// </summary>
        public static GameModeDefinition Create(GameModeDefinitionData data)
        {
            GameModeDefinition definition = CreateInstance<GameModeDefinition>();
            definition.Apply(data);
            definition.name = string.IsNullOrEmpty(data.ObjectName)
                ? "mode_" + data.ModeId
                : data.ObjectName;
            return definition;
        }

        /// <summary>Overwrites every authored field. Editor migration only - never call at runtime.</summary>
        public void Apply(GameModeDefinitionData data)
        {
            modeId = data.ModeId;
            displayName = data.DisplayName;
            objectName = data.ObjectName;
            description = data.Description;
            highScoreField = data.HighScoreField;
            objective = data.Objective;
            clockMode = data.ClockMode;
            customTimerSeconds = data.CustomTimerSeconds;
            combatMode = data.CombatMode;
            shotRule = data.ShotRule;
            shotMarkers = data.ShotMarkers;
            requiresBasketball = data.RequiresBasketball;
            requiresMoneyBall = data.RequiresMoneyBall;
            requiresConsecutiveShots = data.RequiresConsecutiveShots;
            requiresPlayerSurvive = data.RequiresPlayerSurvive;
            allowsCpuShooters = data.AllowsCpuShooters;
            enemiesOnly = data.EnemiesOnly;
            arcadeMode = data.ArcadeMode;
            minPlayers = data.MinPlayers;
            maxPlayers = data.MaxPlayers;
            requiresCpuOpponent = data.RequiresCpuOpponent;
            addsImplicitDefender = data.AddsImplicitDefender;
            requiredArenaCapabilities = data.RequiredArenaCapabilities;
            forbiddenArenaCapabilities = data.ForbiddenArenaCapabilities;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(displayName) ? "mode " + modeId : displayName;
        }
    }

    /// <summary>Plain carrier for the authored mode fields, so construction has one signature.</summary>
    public struct GameModeDefinitionData
    {
        public int ModeId;
        public string DisplayName;
        public string ObjectName;
        public string Description;
        public string HighScoreField;
        public MatchObjective Objective;
        public MatchClockMode ClockMode;
        public float CustomTimerSeconds;
        public CombatMode CombatMode;
        public ShotRule ShotRule;
        public ShotMarkerRequirement ShotMarkers;
        public bool RequiresBasketball;
        public bool RequiresMoneyBall;
        public bool RequiresConsecutiveShots;
        public bool RequiresPlayerSurvive;
        public bool AllowsCpuShooters;
        public bool EnemiesOnly;
        public bool ArcadeMode;
        public int MinPlayers;
        public int MaxPlayers;
        public bool RequiresCpuOpponent;
        public bool AddsImplicitDefender;
        public ArenaCapability RequiredArenaCapabilities;
        public ArenaCapability ForbiddenArenaCapabilities;

        /// <summary>Defaults matching a plain single-player basketball mode.</summary>
        public static GameModeDefinitionData Default(int modeId)
        {
            return new GameModeDefinitionData
            {
                ModeId = modeId,
                DisplayName = string.Empty,
                ObjectName = string.Empty,
                Description = string.Empty,
                HighScoreField = string.Empty,
                Objective = MatchObjective.Score,
                ClockMode = MatchClockMode.Countdown,
                CustomTimerSeconds = 0f,
                CombatMode = CombatMode.None,
                ShotRule = ShotRule.Any,
                ShotMarkers = ShotMarkerRequirement.None,
                RequiresBasketball = true,
                RequiresMoneyBall = false,
                RequiresConsecutiveShots = false,
                RequiresPlayerSurvive = false,
                AllowsCpuShooters = true,
                EnemiesOnly = false,
                ArcadeMode = false,
                MinPlayers = 1,
                MaxPlayers = PlayerRoster.MaxSlots,
                RequiresCpuOpponent = false,
                AddsImplicitDefender = false,
                RequiredArenaCapabilities = ArenaCapability.None,
                ForbiddenArenaCapabilities = ArenaCapability.None
            };
        }
    }
}
