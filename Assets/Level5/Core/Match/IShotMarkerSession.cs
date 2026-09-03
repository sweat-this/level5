namespace Level5.Core.Match
{
    /// <summary>
    /// Live session-wide shot-marker state: the remaining-marker count, one marker's completion, and
    /// routing a cleared objective into match end.
    ///
    /// AUD-010 Phase 1c: <see cref="BasketBallShotMarker"/> reads/calls this instead of
    /// <c>GameRules.instance.MarkersRemaining</c> / <c>IsGameOver()</c> / <c>RequestGameOver()</c>
    /// directly. <c>GameRules</c> implements it over its existing marker-session state - a read/mutate
    /// boundary over that existing ownership, not a new owner of it. See docs/shot-lifecycle.md.
    /// </summary>
    public interface IShotMarkerSession
    {
        /// <summary>Live read of the session-wide remaining-marker count.</summary>
        int MarkersRemaining { get; }

        /// <summary>
        /// Records one marker's completion: decrements <see cref="MarkersRemaining"/> by exactly one
        /// and returns whether the objective is now cleared. Does not itself request match end - the
        /// caller decides when to call <see cref="RequestMatchEnd"/> with the result.
        /// </summary>
        bool RecordMarkerCompleted();

        /// <summary>Routes a cleared objective into the existing match-end path.</summary>
        void RequestMatchEnd();
    }
}
