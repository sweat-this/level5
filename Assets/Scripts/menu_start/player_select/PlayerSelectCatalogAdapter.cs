using System.Collections.Generic;
using Level5.Core.PlayerSelection;
using Level5.Core.Progression;
using UnityEngine;

/// <summary>
/// Projects the currently loaded human/CPU <see cref="CharacterProfile"/> lists into the read-only
/// <see cref="CharacterSelectOption"/> values player select works with.
///
/// This is the one place that reads live <c>CharacterProfile</c> data for selection purposes. It
/// does not become another source of character truth: everything here is derived from the same
/// SQLite-backed profiles the rest of the menu already shows, recomputed whenever the loaded data
/// changes rather than cached indefinitely.
///
/// <see cref="CharacterSelectOption.IsUnlocked"/> comes from the caller-supplied
/// <see cref="UnlockSnapshot"/> rather than reading <c>CharacterProfile.IsLocked</c> directly, so
/// this is no longer a second place that decides account unlock state - it defers to the same
/// snapshot every other unlock-aware call site uses.
/// </summary>
public static class PlayerSelectCatalogAdapter
{
    /// <summary>
    /// Builds the primary and CPU catalogs from the current loaded profile lists, preserving
    /// catalog order. The legacy CPU id-0 "none" record is never included in
    /// <see cref="PlayerSelectCatalog.CpuOptions"/> - it is exposed separately, for the view only,
    /// as <see cref="PlayerSelectCatalog.CpuNoneDisplayName"/>/<see cref="PlayerSelectCatalog.CpuNoneVisuals"/>.
    /// </summary>
    public static PlayerSelectCatalog Project(
        IReadOnlyList<CharacterProfile> primaryProfiles,
        IReadOnlyList<CharacterProfile> cpuProfiles,
        UnlockSnapshot unlock)
    {
        List<CharacterSelectOption> primaryOptions = new List<CharacterSelectOption>();
        List<CharacterSelectOption> cpuOptions = new List<CharacterSelectOption>();
        Dictionary<int, CharacterSelectVisuals> visuals = new Dictionary<int, CharacterSelectVisuals>();
        string cpuNoneDisplayName = string.Empty;
        CharacterSelectVisuals cpuNoneVisuals = CharacterSelectVisuals.Empty;

        if (primaryProfiles != null)
        {
            foreach (CharacterProfile profile in primaryProfiles)
            {
                if (profile == null)
                {
                    continue;
                }

                primaryOptions.Add(ToOption(profile, unlock));
                visuals[profile.PlayerId] = ToVisuals(profile);
            }
        }

        if (cpuProfiles != null)
        {
            foreach (CharacterProfile profile in cpuProfiles)
            {
                if (profile == null)
                {
                    continue;
                }

                // Legacy CPU id 0 is the authored "no CPU here" record. It stays out of the
                // selectable catalog entirely; the view renders an empty slot from the values
                // below instead of treating it as a real option.
                if (profile.PlayerId == 0)
                {
                    cpuNoneDisplayName = profile.PlayerDisplayName;
                    cpuNoneVisuals = ToVisuals(profile);
                    continue;
                }

                cpuOptions.Add(ToOption(profile, unlock));
                if (!visuals.ContainsKey(profile.PlayerId))
                {
                    visuals[profile.PlayerId] = ToVisuals(profile);
                }
            }
        }

        return new PlayerSelectCatalog(primaryOptions, cpuOptions, visuals, cpuNoneDisplayName, cpuNoneVisuals);
    }

    private static CharacterSelectOption ToOption(CharacterProfile profile, UnlockSnapshot unlock)
    {
        // Normalization boundary: the old view recalculated CharacterProfile.Level and
        // CharacterProfile.Clutch on every render. Gameplay reads Clutch directly off this same
        // profile object at match launch (CharacterProfile.intializeShooterStatsFromProfile), so
        // the effective-clutch rule (min(level, 100)) still has to land on the profile somewhere -
        // moved here, to the projection step that runs when loaded data changes, instead of on
        // every render.
        profile.Level = CharacterLevel.FromExperience(profile.Experience);
        int effectiveClutch = CharacterLevel.EffectiveClutchFromLevel(profile.Level);
        profile.Clutch = effectiveClutch;

        CharacterSelectStats stats = new CharacterSelectStats(
            level: profile.Level,
            experience: profile.Experience,
            experienceToNextLevel: CharacterLevel.ExperienceToNextLevel(profile.Experience),
            pointsAvailable: profile.PointsAvailable,
            accuracy3Pt: profile.Accuracy3Pt,
            accuracy4Pt: profile.Accuracy4Pt,
            accuracy7Pt: profile.Accuracy7Pt,
            release: profile.Release,
            range: profile.Range,
            speedPercent: profile.calculateSpeedToPercent(),
            jumpPercent: profile.calculateJumpValueToPercent(),
            luck: profile.Luck,
            effectiveClutch: effectiveClutch);

        return new CharacterSelectOption(
            profile.PlayerId,
            profile.PlayerDisplayName,
            profile.PlayerObjectName,
            profile.IsShooter,
            profile.IsFighter,
            unlock != null && unlock.IsCharacterUnlocked(profile.PlayerId),
            stats);
    }

    private static CharacterSelectVisuals ToVisuals(CharacterProfile profile)
    {
        return new CharacterSelectVisuals(profile.PlayerPortrait, profile.winPortrait, profile.losePortrait);
    }
}

/// <summary>The portrait assets for one character. Kept out of <see cref="CharacterSelectOption"/> so the pure core never references <see cref="Sprite"/>.</summary>
public sealed class CharacterSelectVisuals
{
    public static readonly CharacterSelectVisuals Empty = new CharacterSelectVisuals(null, null, null);

    public CharacterSelectVisuals(Sprite portrait, Sprite winPortrait, Sprite losePortrait)
    {
        Portrait = portrait;
        WinPortrait = winPortrait;
        LosePortrait = losePortrait;
    }

    public Sprite Portrait { get; }

    public Sprite WinPortrait { get; }

    public Sprite LosePortrait { get; }
}

/// <summary>The projected catalogs and visuals the current loaded data produces.</summary>
public sealed class PlayerSelectCatalog
{
    public PlayerSelectCatalog(
        IReadOnlyList<CharacterSelectOption> primaryOptions,
        IReadOnlyList<CharacterSelectOption> cpuOptions,
        IReadOnlyDictionary<int, CharacterSelectVisuals> visuals,
        string cpuNoneDisplayName,
        CharacterSelectVisuals cpuNoneVisuals)
    {
        PrimaryOptions = primaryOptions ?? new List<CharacterSelectOption>();
        CpuOptions = cpuOptions ?? new List<CharacterSelectOption>();
        Visuals = visuals ?? new Dictionary<int, CharacterSelectVisuals>();
        CpuNoneDisplayName = cpuNoneDisplayName ?? string.Empty;
        CpuNoneVisuals = cpuNoneVisuals ?? CharacterSelectVisuals.Empty;
    }

    public IReadOnlyList<CharacterSelectOption> PrimaryOptions { get; }

    /// <summary>Never contains the legacy CPU "none" record - only real, selectable CPU characters.</summary>
    public IReadOnlyList<CharacterSelectOption> CpuOptions { get; }

    public IReadOnlyDictionary<int, CharacterSelectVisuals> Visuals { get; }

    /// <summary>Display name for an inactive CPU slot, sourced from the legacy "none" record for visual parity.</summary>
    public string CpuNoneDisplayName { get; }

    /// <summary>Portrait for an inactive CPU slot, sourced from the legacy "none" record for visual parity.</summary>
    public CharacterSelectVisuals CpuNoneVisuals { get; }

    public CharacterSelectVisuals VisualsFor(int characterId)
    {
        return Visuals.TryGetValue(characterId, out CharacterSelectVisuals visuals) ? visuals : CharacterSelectVisuals.Empty;
    }

    public CharacterSelectOption FindPrimary(int characterId)
    {
        return CharacterSelectOptions.Find(PrimaryOptions, characterId);
    }

    public CharacterSelectOption FindCpu(int characterId)
    {
        return CharacterSelectOptions.Find(CpuOptions, characterId);
    }
}
