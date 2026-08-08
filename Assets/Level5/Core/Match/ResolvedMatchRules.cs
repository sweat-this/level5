namespace Level5.Core.Match
{
    /// <summary>
    /// The authored rules after the level, roster and modifiers have had their say.
    ///
    /// Everything that used to be "corrected" somewhere downstream is decided here, once, before
    /// the scene loads: enemies forced on for a fighting mode (was <c>GameLevelManager.Awake</c>),
    /// traffic forced off on a level without any (was <c>StartManager.setGameOptions</c>), the
    /// basketball count for Lockdown (was <c>checkBasketballPrefabExists</c>). Runtime reads this
    /// and does not adjust it.
    ///
    /// Constructed by name rather than through an object initializer because Unity's netstandard
    /// profile has no <c>IsExternalInit</c>, so <c>init</c> accessors do not compile here.
    /// </summary>
    public sealed class ResolvedMatchRules
    {
        public ResolvedMatchRules(
            MatchObjective objective = MatchObjective.Score,
            MatchClockMode clockMode = MatchClockMode.Countdown,
            float customTimerSeconds = 0f,
            float matchLengthSeconds = MatchClock.DefaultMatchSeconds,
            CombatMode combatMode = CombatMode.None,
            ShotRule shotRule = ShotRule.Any,
            ShotMarkerRequirement shotMarkers = ShotMarkerRequirement.None,
            bool requiresBasketball = true,
            int basketballCount = 1,
            bool requiresMoneyBall = false,
            bool requiresConsecutiveShots = false,
            bool requiresPlayerSurvive = false,
            bool allowsCpuShooters = true,
            bool enemiesEnabled = false,
            bool trafficEnabled = false,
            bool obstaclesEnabled = false,
            SniperMode sniper = SniperMode.None,
            MatchDifficulty difficulty = MatchDifficulty.Normal,
            bool hardcore = false,
            bool arcadeMode = false,
            bool addsImplicitDefender = false,
            bool enemiesOnly = false)
        {
            AddsImplicitDefender = addsImplicitDefender;
            EnemiesOnly = enemiesOnly;
            Objective = objective;
            ClockMode = clockMode;
            CustomTimerSeconds = customTimerSeconds;
            MatchLengthSeconds = matchLengthSeconds;
            CombatMode = combatMode;
            ShotRule = shotRule;
            ShotMarkers = shotMarkers;
            RequiresBasketball = requiresBasketball;
            BasketballCount = basketballCount;
            RequiresMoneyBall = requiresMoneyBall;
            RequiresConsecutiveShots = requiresConsecutiveShots;
            RequiresPlayerSurvive = requiresPlayerSurvive;
            AllowsCpuShooters = allowsCpuShooters;
            EnemiesEnabled = enemiesEnabled;
            TrafficEnabled = trafficEnabled;
            ObstaclesEnabled = obstaclesEnabled;
            Sniper = sniper;
            Difficulty = difficulty;
            Hardcore = hardcore;
            ArcadeMode = arcadeMode;
        }

        public MatchObjective Objective { get; }

        public MatchClockMode ClockMode { get; }

        /// <summary>The mode's own match length, or 0 when it uses the default. Kept for the legacy bridge.</summary>
        public float CustomTimerSeconds { get; }

        /// <summary>The clock the match actually starts on, custom length or default.</summary>
        public float MatchLengthSeconds { get; }

        public CombatMode CombatMode { get; }

        public ShotRule ShotRule { get; }

        public ShotMarkerRequirement ShotMarkers { get; }

        public bool RequiresBasketball { get; }

        /// <summary>How many balls to spawn. One per participant, except where a mode pins the count.</summary>
        public int BasketballCount { get; }

        public bool RequiresMoneyBall { get; }

        public bool RequiresConsecutiveShots { get; }

        public bool RequiresPlayerSurvive { get; }

        public bool AllowsCpuShooters { get; }

        public bool EnemiesEnabled { get; }

        public bool TrafficEnabled { get; }

        public bool ObstaclesEnabled { get; }

        public SniperMode Sniper { get; }

        public MatchDifficulty Difficulty { get; }

        public bool Hardcore { get; }

        public bool ArcadeMode { get; }

        /// <summary>
        /// The mode brings its own opponent that is not a roster slot - Lockdown's defender. The
        /// spawn path needs this without having to know which mode it is playing.
        /// </summary>
        public bool AddsImplicitDefender { get; }

        /// <summary>
        /// This is a fighting mode: enemies instead of shooting. Distinct from
        /// <see cref="EnemiesEnabled"/>, which is also true when a shooting mode has enemies
        /// switched on as a modifier.
        /// </summary>
        public bool EnemiesOnly { get; }

        public bool SniperEnabled => Sniper != SniperMode.None;

        public bool RequiresCountDown => ClockMode == MatchClockMode.Countdown;

        public bool RequiresCounter => ClockMode == MatchClockMode.CountUp;

        public bool RequiresShotMarkers3s => (ShotMarkers & ShotMarkerRequirement.ThreePoint) != 0;

        public bool RequiresShotMarkers4s => (ShotMarkers & ShotMarkerRequirement.FourPoint) != 0;

        public bool RequiresShotMarkers7s => (ShotMarkers & ShotMarkerRequirement.SevenPoint) != 0;

        public bool RequiresAnyShotMarkers => ShotMarkers != ShotMarkerRequirement.None;

        public bool IsThreePointContest => (ShotRule & ShotRule.ThreePoint) != 0;

        public bool IsFourPointContest => (ShotRule & ShotRule.FourPoint) != 0;

        public bool IsSevenPointContest => (ShotRule & ShotRule.SevenPoint) != 0;

        public bool IsAllPointContest => (ShotRule & ShotRule.AllRanges) != 0;

        public bool IsContest => ShotRule != ShotRule.Any;

        public bool IsBattleRoyal => (CombatMode & CombatMode.BattleRoyal) != 0;

        public bool IsCageMatch => (CombatMode & CombatMode.Cage) != 0;
    }
}
