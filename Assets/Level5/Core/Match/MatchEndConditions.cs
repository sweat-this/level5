namespace Level5.Core.Match
{
    /// <summary>
    /// The rules for when a match is over, as plain functions.
    ///
    /// These decisions used to live inside <c>Timer.Update</c> and <c>GameRules.IsGameOver</c>,
    /// wrapped in null guards and Unity lookups, which meant the rule and the plumbing could only be
    /// read - and only be wrong - together. Pulling the rules out makes them testable and leaves the
    /// components doing what they should: noticing the situation and reporting it.
    ///
    /// Extracting them is only safe now because the characterization matrix exists to say what the
    /// current behaviour is. Each one below is that behaviour, oddities included.
    /// </summary>
    public static class MatchEndConditions
    {
        /// <summary>
        /// A consecutive-shots run has to be worth continuing for the clock to be extended past
        /// zero. Three is the authored threshold; the original comment beside it said two.
        /// </summary>
        public const int ConsecutiveShotsToPlayOn = 3;

        /// <summary>
        /// Whether a countdown reaching zero ends the match right now.
        ///
        /// Two different rules depending on the mode:
        ///
        /// - normally the clock waits politely. A shot already in the air still counts, and a player
        ///   mid-jump gets to land, so time only runs out with the ball unthrown and the player on
        ///   the ground.
        /// - a consecutive-shots mode ignores both of those and instead lets a live streak play on.
        ///   The match ends at zero only if the streak is too short to be worth continuing.
        /// </summary>
        public static bool TimeExpired(
            bool requiresConsecutiveShots,
            bool ballThrown,
            bool playerGrounded,
            int consecutiveShotsMade)
        {
            if (requiresConsecutiveShots)
            {
                return consecutiveShotsMade < ConsecutiveShotsToPlayOn;
            }

            return !ballThrown && playerGrounded;
        }

        /// <summary>A contest ends when its last shot marker has been cleared.</summary>
        public static bool MarkersCleared(int markersRemaining)
        {
            return markersRemaining <= 0;
        }

        /// <summary>
        /// The reason to report for a clock that has run out, so the end-of-match record says what
        /// happened rather than "unknown".
        /// </summary>
        public static MatchEndReason TimeExpiredReason(bool requiresConsecutiveShots)
        {
            return requiresConsecutiveShots
                ? new MatchEndReason(MatchEndCause.TimeExpired, "streak too short to play on")
                : MatchEndReason.TimeExpired;
        }
    }
}
