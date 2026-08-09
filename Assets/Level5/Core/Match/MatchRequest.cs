namespace Level5.Core.Match
{
    /// <summary>
    /// What a launch source is asking for. Menus build one of these; they no longer write gameplay
    /// configuration directly.
    ///
    /// A request is not authoritative and is not validated by construction - the whole point is
    /// that any source (the start menu today, a campaign flow, an asynchronous challenge later) can
    /// hand one to <see cref="MatchConfigurationBuilder"/> and get the same verdict.
    /// </summary>
    public sealed class MatchRequest
    {
        public MatchRequest(
            GameModeId modeId,
            int levelId,
            PlayerRoster roster,
            MatchModifiers modifiers = null,
            CheerleaderSelection cheerleader = null,
            string source = null)
        {
            ModeId = modeId;
            LevelId = levelId;
            Roster = roster;
            Modifiers = modifiers ?? MatchModifiers.Default;
            Cheerleader = cheerleader ?? CheerleaderSelection.None;
            Source = string.IsNullOrEmpty(source) ? "unknown" : source;
        }

        public GameModeId ModeId { get; }

        public int LevelId { get; }

        public PlayerRoster Roster { get; }

        public MatchModifiers Modifiers { get; }

        public CheerleaderSelection Cheerleader { get; }

        /// <summary>Where the request came from, for diagnostics only.</summary>
        public string Source { get; }

        public override string ToString()
        {
            return $"{ModeId} on level {LevelId} for {(Roster == null ? 0 : Roster.Count)} player(s) from {Source}";
        }
    }
}
