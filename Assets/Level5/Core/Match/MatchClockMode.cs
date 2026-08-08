namespace Level5.Core.Match
{
    /// <summary>
    /// How the match clock behaves.
    ///
    /// Legacy data carries this as two independent booleans (<c>modeRequiresCountDown</c> and
    /// <c>modeRequiresCounter</c>) which can both be set at once even though no mode means that.
    /// One value makes the contradiction unrepresentable; the parity validator reports any authored
    /// mode that currently sets both.
    /// </summary>
    public enum MatchClockMode
    {
        /// <summary>No clock. The match ends on its objective or on death.</summary>
        None,

        /// <summary>Clock runs down from the resolved match length and ending it ends the match.</summary>
        Countdown,

        /// <summary>Clock counts up and is the score; used by the spot-up "beat this time" modes.</summary>
        CountUp
    }
}
