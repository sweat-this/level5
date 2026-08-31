using System.Collections.Generic;
using Level5.Core.PlayerSelection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Passive rendering for the player-select portion of the start screen.
///
/// Every widget reference here already exists as a serialized <see cref="StartMenuUiObjects"/>/
/// <see cref="StartMenuTextUiObjects"/> field - this class adds no new serialized state to the
/// scene, it only owns how those existing references get written. It never mutates
/// <see cref="CharacterProfile"/>, changes selection, loads data, writes <see cref="GameOptions"/>,
/// builds a match, or looks anything up by GameObject name; the coordinator resolves values, this
/// class only assigns them to widgets.
/// </summary>
public sealed class PlayerSelectView
{
    private readonly TMP_Text primaryNameText;
    private readonly Image primaryPortraitImage;
    private readonly GameObject primaryLockOverlay;
    private readonly TMP_Text primaryStatsText;
    private readonly TMP_Text progressionStatsText;
    private readonly TMP_Text progressionPointsText;
    private readonly TMP_Text participantCountText;
    private readonly TMP_Text focusedCpuStatsText;
    private readonly IReadOnlyList<CpuSlotBinding> cpuSlots;

    public PlayerSelectView(
        TMP_Text primaryNameText,
        Image primaryPortraitImage,
        GameObject primaryLockOverlay,
        TMP_Text primaryStatsText,
        TMP_Text progressionStatsText,
        TMP_Text progressionPointsText,
        TMP_Text participantCountText,
        TMP_Text focusedCpuStatsText,
        IReadOnlyList<CpuSlotBinding> cpuSlots)
    {
        this.primaryNameText = primaryNameText;
        this.primaryPortraitImage = primaryPortraitImage;
        this.primaryLockOverlay = primaryLockOverlay;
        this.primaryStatsText = primaryStatsText;
        this.progressionStatsText = progressionStatsText;
        this.progressionPointsText = progressionPointsText;
        this.participantCountText = participantCountText;
        this.focusedCpuStatsText = focusedCpuStatsText;
        this.cpuSlots = cpuSlots ?? new List<CpuSlotBinding>();
    }

    public void RenderPrimary(CharacterSelectOption primary, Sprite portrait, int participantCount)
    {
        if (primary == null)
        {
            return;
        }

        if (primaryNameText != null)
        {
            primaryNameText.text = primary.DisplayName;
        }

        if (primaryPortraitImage != null)
        {
            primaryPortraitImage.sprite = portrait;
        }

        if (primaryLockOverlay != null)
        {
            primaryLockOverlay.SetActive(!primary.IsUnlocked);
        }

        if (participantCountText != null)
        {
            participantCountText.text = participantCount.ToString();
        }

        CharacterSelectStats stats = primary.Stats;
        if (primaryStatsText != null)
        {
            primaryStatsText.text = FormatPrimaryStats(stats);
        }

        if (progressionStatsText != null)
        {
            progressionStatsText.text = CharacterLevel.FormatProgressionStats(
                stats.Level,
                stats.Experience,
                stats.ExperienceToNextLevel);
        }

        if (progressionPointsText != null)
        {
            progressionPointsText.text = FormatPoints(stats.PointsAvailable);
        }
    }

    public void RenderCpuSlot(int slotIndex, string name, Sprite portrait)
    {
        if (slotIndex < 0 || slotIndex >= cpuSlots.Count)
        {
            return;
        }

        CpuSlotBinding binding = cpuSlots[slotIndex];
        if (binding.NameText != null)
        {
            binding.NameText.text = name ?? string.Empty;
        }

        if (binding.Portrait != null)
        {
            binding.Portrait.sprite = portrait;
        }
    }

    public void RenderFocusedCpuStats(CharacterSelectOption focused)
    {
        if (focusedCpuStatsText == null)
        {
            return;
        }

        focusedCpuStatsText.text = focused != null ? FormatCpuStats(focused.Stats) : string.Empty;
    }

    /// <summary>
    /// The shared nine-line stat block. Primary and CPU panels differ only in whether Range shows
    /// a unit suffix and whether Level is appended - both legacy quirks, preserved as parameters
    /// instead of two independently-maintained copies of the same nine lines.
    /// </summary>
    private static string FormatStats(CharacterSelectStats stats, bool rangeInFeet, bool includeLevel)
    {
        string range = rangeInFeet ? stats.Range.ToString("F0") + " ft" : stats.Range.ToString("F0");
        string text = stats.Accuracy3Pt.ToString("F0") + "\n"
            + stats.Accuracy4Pt.ToString("F0") + "\n"
            + stats.Accuracy7Pt.ToString("F0") + "\n"
            + stats.Release.ToString("F0") + "\n"
            + range + "\n"
            + stats.SpeedPercent.ToString("F0") + "\n"
            + stats.JumpPercent.ToString("F0") + "\n"
            + stats.Luck.ToString("F0") + "\n"
            + stats.EffectiveClutch.ToString("F0");

        return includeLevel ? text + "\n" + stats.Level.ToString("F0") : text;
    }

    private static string FormatPrimaryStats(CharacterSelectStats stats)
    {
        return FormatStats(stats, rangeInFeet: false, includeLevel: false);
    }

    private static string FormatCpuStats(CharacterSelectStats stats)
    {
        return FormatStats(stats, rangeInFeet: true, includeLevel: true);
    }

    private static string FormatPoints(int pointsAvailable)
    {
        if (pointsAvailable == 0)
        {
            return string.Empty;
        }

        return pointsAvailable > 0 ? "+" + pointsAvailable : pointsAvailable.ToString();
    }
}

/// <summary>One CPU draft slot's widgets: a button/portrait/name group instead of three named fields.</summary>
public readonly struct CpuSlotBinding
{
    public CpuSlotBinding(GameObject button, Image portrait, TMP_Text nameText)
    {
        Button = button;
        Portrait = portrait;
        NameText = nameText;
    }

    public GameObject Button { get; }

    public Image Portrait { get; }

    public TMP_Text NameText { get; }
}
