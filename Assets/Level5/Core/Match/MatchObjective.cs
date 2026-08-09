namespace Level5.Core.Match
{
    /// <summary>
    /// What finishing the match actually means for a mode.
    ///
    /// One mode has exactly one primary objective, so this is a single value rather than a set of
    /// booleans. Secondary conditions (the clock running out, the player dying) are separate
    /// dimensions and can end any match regardless of objective.
    /// </summary>
    public enum MatchObjective
    {
        /// <summary>Score as many points as possible before the clock runs out.</summary>
        Score,

        /// <summary>Make as many shots of a given type as possible before the clock runs out.</summary>
        MakeCount,

        /// <summary>Accumulate shot distance.</summary>
        Distance,

        /// <summary>Build the longest streak of made shots.</summary>
        ConsecutiveShots,

        /// <summary>Clear every active shot marker; the clock is the pressure, not the goal.</summary>
        ContestCompletion,

        /// <summary>Stay alive; the run ends when the player dies.</summary>
        Survival,

        /// <summary>Be the last participant standing.</summary>
        LastPlayerStanding,

        /// <summary>Complete a campaign round and advance.</summary>
        CampaignProgression
    }
}
