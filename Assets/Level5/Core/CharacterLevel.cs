using UnityEngine;

/// <summary>
/// The experience-to-level curve, in one place.
///
/// The divisor used to be the bare literal 3000 in eight places, plus a ninth site that derived
/// "experience to next level" as `(level + 1) * 3000 - experience`. Changing the curve meant
/// finding all nine, and a missed one showed a level in the menu that disagreed with the level
/// written to the database.
///
/// This is the spend side of progression; MatchExperience is the earn side.
/// </summary>
public static class CharacterLevel
{
    /// <summary>Experience required to advance one level.</summary>
    public const int ExperiencePerLevel = 3000;

    /// <summary>Level reached at <paramref name="experience"/>. Level 0 is the starting level.</summary>
    public static int FromExperience(int experience)
    {
        if (experience <= 0)
        {
            return 0;
        }

        return experience / ExperiencePerLevel;
    }

    /// <summary>Level reached at a float experience total, for callers that accumulate in float.</summary>
    public static int FromExperience(float experience)
    {
        if (experience <= 0f)
        {
            return 0;
        }

        return Mathf.FloorToInt(experience / ExperiencePerLevel);
    }

    /// <summary>
    /// Experience still needed to reach the next level. Always in 1..ExperiencePerLevel, so a
    /// player sitting exactly on a level boundary is shown a full level's worth of progress
    /// rather than 0.
    /// </summary>
    public static int ExperienceToNextLevel(int experience)
    {
        int safeExperience = Mathf.Max(0, experience);
        return ((FromExperience(safeExperience) + 1) * ExperiencePerLevel) - safeExperience;
    }
}
