/// <summary>
/// Match-length rules, in one place.
///
/// Timer and GameRules both used to decide the starting clock, with different conditions, in an
/// order Unity does not define. GameRules owns the decision now; this holds the shared constant and
/// the rule itself so the two can never drift again.
/// </summary>
public static class MatchClock
{
    /// <summary>Match length used when a mode does not specify its own.</summary>
    public const float DefaultMatchSeconds = 180f;

    /// <summary>
    /// Starting clock for a match. A custom timer wins when the mode sets one; note that being a
    /// contest mode is NOT on its own a reason to use `customTimer` - that conflation is what let a
    /// contest mode with an unset CustomTimer start the clock at zero and end instantly.
    /// </summary>
    public static float StartSeconds(float customTimerSeconds)
    {
        return customTimerSeconds > 0f ? customTimerSeconds : DefaultMatchSeconds;
    }
}
