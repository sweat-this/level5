using UnityEngine;

/// <summary>
/// Everything the end-of-match experience award depends on, captured as plain data so the
/// calculation does not have to reach into GameStats components or GameOptions statics.
/// </summary>
public struct MatchExperienceInput
{
    // shooting
    public int ShotAttempts;
    public int TwoPointerMade;
    public int ThreePointerMade;
    public int FourPointerMade;
    public int SevenPointerMade;
    public float TotalDistance;
    public int MostConsecutiveShots;
    public int TotalPoints;

    // sniper
    public int SniperShots;
    public int SniperHits;

    // combat
    public int MinionsKilled;
    public int BossKilled;

    // mode modifiers
    public bool TrafficEnabled;
    public bool EnemiesEnabled;
    public bool HardcoreEnabled;
    public bool SniperEnabled;
    public bool ArcadeMode;

    /// <summary>0 = easy (half XP), 1 = normal, 2 = hard (1.5x XP).</summary>
    public int DifficultySelected;
}

public static class MatchExperience
{
    /// <summary>Maximum bonus for evading the sniper for a whole match.</summary>
    public const int MaxSniperEvasionBonus = 500;

    public const int ExperiencePerShotAttempt = 10;
    public const int ExperiencePerSniperHit = 15;
    public const int ExperiencePerConsecutiveShot = 25;
    public const int ExperiencePerMinion = 50;
    public const int ExperiencePerBoss = 150;

    public static int Calculate(MatchExperienceInput input)
    {
        int experience = 0;

        experience += SniperEvasionBonus(input.SniperHits, input.SniperShots);

        // taking sniper fire is still worth something, so a passive player is not
        // strictly better off than one who plays through it
        experience += input.SniperHits * ExperiencePerSniperHit;

        experience += input.ShotAttempts * ExperiencePerShotAttempt;
        experience += input.TwoPointerMade * 20;
        experience += input.ThreePointerMade * 30;
        experience += input.FourPointerMade * 40;
        experience += input.SevenPointerMade * 70;
        experience += Mathf.RoundToInt(input.TotalDistance * 0.5f);
        experience += input.MostConsecutiveShots * ExperiencePerConsecutiveShot;
        experience += input.TotalPoints;

        if (input.TrafficEnabled)
        {
            experience = Mathf.RoundToInt(experience * 1.15f);
        }

        if (input.EnemiesEnabled)
        {
            experience += input.MinionsKilled * ExperiencePerMinion;
            experience += input.BossKilled * ExperiencePerBoss;
            experience = Mathf.RoundToInt(experience * 1.25f);
        }

        if (input.HardcoreEnabled)
        {
            experience = Mathf.RoundToInt(experience * 1.5f);
        }

        if (input.SniperEnabled)
        {
            experience = Mathf.RoundToInt(experience * 1.25f);
        }

        if (input.ArcadeMode)
        {
            return 0;
        }

        if (input.DifficultySelected == 0)
        {
            experience = experience / 2;
        }

        if (input.DifficultySelected == 2)
        {
            experience = Mathf.RoundToInt(experience * 1.5f);
        }

        return experience;
    }

    /// <summary>
    /// Scales <see cref="MaxSniperEvasionBonus"/> by the share of sniper shots the player dodged.
    /// Returns 0 when the sniper never fired - "no sniper in this mode" is not "perfect evasion".
    /// </summary>
    public static int SniperEvasionBonus(int sniperHits, int sniperShots)
    {
        if (sniperShots <= 0)
        {
            return 0;
        }

        int hits = Mathf.Clamp(sniperHits, 0, sniperShots);
        float evadedFraction = 1f - ((float)hits / sniperShots);
        return Mathf.RoundToInt(MaxSniperEvasionBonus * evadedFraction);
    }
}
