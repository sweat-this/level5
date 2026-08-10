using Level5.Core.PlayerSelection;
using UnityEngine;

/// <summary>
/// The one legacy character-launch special case: Wizard of Boat resolves to one of two runtime
/// object names, chosen at random when the match launches.
///
/// Quarantined here deliberately rather than generalized into a skin/variant framework. Display-
/// name matching is exactly what the old <c>StartManager.GetPlayerObjectNameOverride</c> did; this
/// keeps the same rule at the one conversion seam (draft -&gt; <c>CharacterSelection</c>) instead of
/// leaving it in the menu manager.
/// </summary>
public static class LegacyCharacterVariantResolver
{
    private const string WizardOfBoatNameMarker = "boat";
    private const string VariantOne = "wob1";
    private const string VariantTwo = "wob2";

    /// <summary>
    /// The runtime object name to launch with, or null to use the option's own object name
    /// unchanged. Resolved fresh on every call, so the random choice happens at launch time, not
    /// while browsing.
    /// </summary>
    public static string ResolveObjectName(CharacterSelectOption primary)
    {
        if (primary == null || string.IsNullOrEmpty(primary.DisplayName))
        {
            return null;
        }

        if (!primary.DisplayName.ToLowerInvariant().Contains(WizardOfBoatNameMarker))
        {
            return null;
        }

        return Random.Range(1, 100) > 50 ? VariantOne : VariantTwo;
    }
}
