namespace Level5.Core.Match
{
    /// <summary>
    /// Where a match is in its life.
    ///
    /// <c>Preparing -&gt; Countdown -&gt; Playing -&gt; Ending -&gt; Completed</c>. Transitions only ever go
    /// forward, which is what makes "end the match" safe to call from the clock, a marker, a death
    /// and the pause menu at the same time.
    /// </summary>
    public enum MatchPhase
    {
        /// <summary>Scene loaded, participants not yet spawned or not yet live.</summary>
        Preparing,

        /// <summary>Pre-match countdown, if the mode has one.</summary>
        Countdown,

        /// <summary>Live.</summary>
        Playing,

        /// <summary>An end has been accepted and the durable end-of-match work is running.</summary>
        Ending,

        /// <summary>All end-of-match work finished.</summary>
        Completed
    }

    /// <summary>Why a match ended. Used for diagnostics and for the end-of-match presentation.</summary>
    public enum MatchEndCause
    {
        Unknown,
        TimeExpired,
        ObjectiveComplete,
        PlayerDied,
        LastPlayerStanding,
        CampaignRoundComplete,
        Abandoned
    }

    /// <summary>The reason a match ended, with an optional note for logs.</summary>
    public readonly struct MatchEndReason
    {
        public MatchEndReason(MatchEndCause cause, string detail = null)
        {
            Cause = cause;
            Detail = detail ?? string.Empty;
        }

        public MatchEndCause Cause { get; }

        public string Detail { get; }

        public static readonly MatchEndReason Unknown = new MatchEndReason(MatchEndCause.Unknown);

        public static MatchEndReason TimeExpired => new MatchEndReason(MatchEndCause.TimeExpired);

        public static MatchEndReason ObjectiveComplete => new MatchEndReason(MatchEndCause.ObjectiveComplete);

        public static MatchEndReason PlayerDied => new MatchEndReason(MatchEndCause.PlayerDied);

        public override string ToString()
        {
            return string.IsNullOrEmpty(Detail) ? Cause.ToString() : Cause + " (" + Detail + ")";
        }
    }
}
