using UnityEngine;

/// <summary>
/// The shot-accuracy modifier math, extracted from the duplicated shot pipeline.
///
/// AUD-017: `BasketBall` (human) and `BasketBallAuto` (CPU) carried byte-identical copies of
/// `getAccuracyModifier`, `getRangeModifier`, and `getReleaseModifier` - the only textual
/// difference across all three was whether the slider value came from `playerController` or
/// `autoPlayerController`. Every fix to shot feel had to be applied twice or the human and CPU
/// paths would silently diverge, which is exactly how AUD-015 and AUD-016 happened.
///
/// These are characterization extractions: the arithmetic is reproduced exactly as it was,
/// including the parts that look like mistakes (see `AccuracyMultiplierFor`). Nothing here changes
/// shot behaviour. Rolls are injected so the math is testable without the engine RNG, the same
/// approach <see cref="PercentChance"/> uses.
/// </summary>
public static class ShotModifiers
{
    /// <summary>
    /// The per-shot-type multiplier applied to the accuracy shortfall.
    ///
    /// Three-pointers use 0.02 and every other shot type uses 0.01 - so a three-pointer is punished
    /// twice as hard for the same accuracy shortfall. That asymmetry is preserved deliberately: it
    /// reads like a typo, but both copies of the original agreed on it, so it is long-standing
    /// behaviour rather than a slip in one of them. Changing it is a balance decision, not a fix.
    /// </summary>
    public static float AccuracyMultiplierFor(bool threePoints)
    {
        return threePoints ? 0.02f : 0.01f;
    }

    /// <summary>
    /// Aim error from the shot meter and the character's accuracy in the active shot type.
    ///
    /// A perfect slider (100) yields 0 regardless of accuracy, because the accuracy term is scaled
    /// by the slider term rather than added to it.
    /// </summary>
    /// <param name="sliderValueOnButtonPress">Raw shot-meter value; ceilinged, as the original did.</param>
    /// <param name="shotTypeAccuracy">The character's accuracy for the shot being taken, 0-100.</param>
    /// <param name="threePoints">Whether this is a three-pointer - see <see cref="AccuracyMultiplierFor"/>.</param>
    /// <param name="direction">+1 or -1; which side of the rim the error pushes toward.</param>
    public static float AccuracyModifier(
        float sliderValueOnButtonPress,
        float shotTypeAccuracy,
        bool threePoints,
        int direction)
    {
        int slider = Mathf.CeilToInt(sliderValueOnButtonPress);
        float sliderModifier = (100 - slider) * 0.025f;
        float accuracyModifier = (100 - shotTypeAccuracy) * AccuracyMultiplierFor(threePoints);

        return (sliderModifier + (accuracyModifier * sliderModifier)) * direction;
    }

    /// <summary>
    /// How far short of the rim the shot falls, as a fraction of the distance.
    ///
    /// `range / (distance * 6)` is "what fraction of the way there can this character throw" - the
    /// 6 converts world units to feet. At or beyond 1 the character can reach, so there is no
    /// penalty. Below 1 the character still gets a roll at `modifier * 100` percent to shoot clean
    /// anyway, so a marginal shot is not automatically short.
    /// </summary>
    /// <param name="range">The character's range stat.</param>
    /// <param name="shotDistanceWorldUnits">Distance to the rim in world units, not feet.</param>
    /// <param name="rolledClean">
    /// Result of rolling <c>modifier * 100</c> percent. Injected rather than rolled here so this
    /// stays a pure function; the caller passes <c>UtilityFunctions.RollPercent(MaxCleanChance(...))</c>.
    /// </param>
    public static float RangeModifier(float range, float shotDistanceWorldUnits, bool rolledClean)
    {
        float modifier = range / (shotDistanceWorldUnits * 6f);

        if (modifier >= 1f || rolledClean)
        {
            return 0f;
        }

        return modifier;
    }

    /// <summary>
    /// The percentage to roll for <see cref="RangeModifier"/>'s clean-shot chance. Exposed so the
    /// caller rolls exactly what the original rolled.
    /// </summary>
    public static float MaxCleanChance(float range, float shotDistanceWorldUnits)
    {
        return (range / (shotDistanceWorldUnits * 6f)) * 100f;
    }

    /// <summary>
    /// Whether the character can reach the rim from here, in which case there is no range penalty
    /// and - importantly - no roll.
    ///
    /// The original was <c>if (modifier >= 1 || rollForCriticalRangeChance(maxChance))</c>, and
    /// <c>||</c> short-circuits: an in-range shot never consumed a random value. Callers must check
    /// this before rolling, or they draw an extra number and shift the whole RNG sequence.
    /// </summary>
    public static bool ReachesRim(float range, float shotDistanceWorldUnits)
    {
        return range / (shotDistanceWorldUnits * 6f) >= 1f;
    }

    /// <summary>
    /// Vertical error from an imperfect release.
    ///
    /// The release stat is itself the chance to shoot clean - a release of 85 removes the modifier
    /// 85% of the time. When it does not, the shortfall is scaled by 0.75. (The original's comment
    /// claimed the inverse of this; AUD-030 corrected the comment, not the code.)
    /// </summary>
    /// <param name="release">The character's release stat, 0-100.</param>
    /// <param name="direction">+1 or -1.</param>
    /// <param name="rolledClean">Result of rolling <paramref name="release"/> percent.</param>
    public static float ReleaseModifier(float release, int direction, bool rolledClean)
    {
        if (rolledClean)
        {
            return 0f;
        }

        float accuracyModifier = (100 - release) * 0.01f;
        return accuracyModifier * 0.75f * direction;
    }
}
