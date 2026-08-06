/// <summary>
/// Percentage rolls with exact endpoint behaviour.
///
/// The forms this replaces were off by one in both directions: `Random.Range(0, 100) &lt;= chance`
/// made a 0% chance succeed 1% of the time, and `Random.Range(1, 100) &lt;= chance` made a 99%
/// chance certain because the int overload of Random.Range is max-exclusive and never returned 100.
///
/// Callers supply the roll so the decision stays testable without the engine's RNG.
/// UtilityFunctions.RollPercent is the Unity-side wrapper that feeds it Random.value.
/// </summary>
public static class PercentChance
{
    /// <summary>
    /// True when a roll lands inside <paramref name="chancePercent"/>.
    /// </summary>
    /// <param name="chancePercent">Chance of success, 0-100. Values outside that range are clamped by the endpoint checks.</param>
    /// <param name="roll01">A uniform roll in the inclusive range 0-1, such as UnityEngine.Random.value.</param>
    public static bool Succeeds(float chancePercent, float roll01)
    {
        // Handled explicitly so the endpoints are exact rather than depending on
        // whether the caller's RNG happens to be inclusive of its upper bound.
        if (chancePercent <= 0f)
        {
            return false;
        }

        if (chancePercent >= 100f)
        {
            return true;
        }

        return roll01 * 100f < chancePercent;
    }
}
