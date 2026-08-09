namespace Level5.Core.Match
{
    /// <summary>
    /// The orthogonal options a player picks alongside a mode and a level.
    ///
    /// These are requests, not decisions. Whether a modifier is legal for the chosen mode and arena
    /// is decided once by <see cref="GameModeCompatibility"/> and resolved by
    /// <see cref="MatchConfigurationBuilder"/>; gameplay reads the resolved answer from
    /// <see cref="ResolvedMatchRules"/> instead of silently correcting anything mid-match.
    /// </summary>
    public sealed class MatchModifiers
    {
        public static readonly MatchModifiers Default = new MatchModifiers();

        public MatchModifiers(
            MatchDifficulty difficulty = MatchDifficulty.Normal,
            bool trafficRequested = false,
            bool enemiesRequested = false,
            bool obstaclesRequested = false,
            SniperMode sniper = SniperMode.None,
            bool hardcoreRequested = false)
        {
            Difficulty = difficulty;
            TrafficRequested = trafficRequested;
            EnemiesRequested = enemiesRequested;
            ObstaclesRequested = obstaclesRequested;
            Sniper = sniper;
            HardcoreRequested = hardcoreRequested;
        }

        public MatchDifficulty Difficulty { get; }

        public bool TrafficRequested { get; }

        public bool EnemiesRequested { get; }

        public bool ObstaclesRequested { get; }

        public SniperMode Sniper { get; }

        public bool HardcoreRequested { get; }

        /// <summary>Hardcore is implied by the hardest difficulty; the menu also exposes it directly.</summary>
        public bool Hardcore => HardcoreRequested || Difficulty == MatchDifficulty.Hardcore;

        public bool SniperEnabled => Sniper != SniperMode.None;

        public MatchModifiers With(
            MatchDifficulty? difficulty = null,
            bool? traffic = null,
            bool? enemies = null,
            bool? obstacles = null,
            SniperMode? sniper = null,
            bool? hardcore = null)
        {
            return new MatchModifiers(
                difficulty ?? Difficulty,
                traffic ?? TrafficRequested,
                enemies ?? EnemiesRequested,
                obstacles ?? ObstaclesRequested,
                sniper ?? Sniper,
                hardcore ?? HardcoreRequested);
        }
    }
}
