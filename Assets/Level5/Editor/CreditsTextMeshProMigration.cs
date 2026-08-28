using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 4A: migrates <c>creditsManager.prefab</c>'s directly-owned ordinary display/button
/// legacy <see cref="Text"/> components to <see cref="TextMeshProUGUI"/> on the same project-owned Neon
/// Pixel-7 SDF font asset every other menu screen used, while deliberately leaving the legacy
/// <c>ReportInputField</c> (<see cref="InputField"/>) and its two structural Text dependencies
/// (<c>textComponent</c>, <c>placeholder</c>) as legacy Text until Phase 4B migrates the InputField
/// itself to <c>TMP_InputField</c>. Reuses <see cref="MenuTextConversion"/> for every low-level mechanic
/// Phase 1-3 already proved; this class contributes only the Credits-specific orchestration, the
/// InputField-boundary protection, and the permanent per-screen contract.
///
/// <c>creditsManager.prefab</c> nests one shared prefab instance that
/// <see cref="MenuTextConversion"/>'s nested-instance exclusion already keeps out of scope:
/// <c>touch_joystick.prefab</c> (shared by every critical/menu prefab).
/// </summary>
public static class CreditsTextMeshProMigration
{
    private const string CreditsManagerPrefabPath = "Assets/Resources/Prefabs/menu_credits/creditsManager.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_credits.unity";
    private const string TouchJoystickPrefabPath = "Assets/Resources/Prefabs/critical/touch_joystick.prefab";

    // ---------------------------------------------------------------------------------------------
    // Characterization report (read-only)
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Level5/Report Credits TMP Migration")]
    public static void Report()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CreditsManagerPrefabPath);
        try
        {
            StringBuilder summary = new StringBuilder();
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            List<string> boundaryErrors = new List<string>();
            HashSet<Text> protectedTexts = ResolveProtectedInputFieldTexts(root, boundaryErrors);
            foreach (string error in boundaryErrors)
            {
                summary.AppendLine("  INPUTFIELD BOUNDARY ERROR: " + error);
            }

            summary.AppendLine(
                CreditsManagerPrefabPath + " : " + texts.Count + " legacy Text component(s) owned directly by this prefab, "
                    + nestedTexts.Count + " more inside nested prefab instance(s) (out of scope), "
                    + (protectedTexts != null ? protectedTexts.Count.ToString() : "UNRESOLVED")
                    + " protected as the legacy InputField boundary.");

            HashSet<Object> textSet = new HashSet<Object>(allTexts);

            foreach (Text text in nestedTexts)
            {
                summary.AppendLine(
                    "  SKIPPED (nested prefab instance " + PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject).name
                        + "): " + MenuTextConversion.BuildHierarchyPath(text.gameObject, root));
            }

            foreach (Text text in texts)
            {
                string path = MenuTextConversion.BuildHierarchyPath(text.gameObject, root);
                bool isProtected = protectedTexts != null && protectedTexts.Contains(text);
                Outline outline = text.GetComponent<Outline>();
                bool hasEnabledOutline = outline != null && outline.enabled;
                summary.AppendLine(
                    "  " + path
                        + (isProtected ? " [PROTECTED InputField dependency]" : " [Phase 4A candidate]")
                        + " text=\"" + MenuTextConversion.Truncate(text.text, 40) + "\""
                        + " font=" + (text.font != null ? text.font.name : "<none>")
                        + " size=" + text.fontSize
                        + " style=" + text.fontStyle
                        + " align=" + text.alignment
                        + " color=" + text.color
                        + " raycastTarget=" + text.raycastTarget
                        + " maskable=" + text.maskable
                        + " richText=" + text.supportRichText
                        + " bestFit=" + text.resizeTextForBestFit
                        + " hOverflow=" + text.horizontalOverflow
                        + " vOverflow=" + text.verticalOverflow
                        + " lineSpacing=" + text.lineSpacing
                        + " enabled=" + text.enabled
                        + (hasEnabledOutline ? " [has enabled Outline]" : string.Empty));
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable.targetGraphic != null && textSet.Contains(selectable.targetGraphic))
                {
                    summary.AppendLine(
                        "  SUPPORTED targetGraphic: " + MenuTextConversion.BuildHierarchyPath(selectable.gameObject, root)
                            + " (" + selectable.GetType().Name + ") -> "
                            + MenuTextConversion.BuildHierarchyPath(selectable.targetGraphic.gameObject, root));
                }

                if (selectable is InputField inputField)
                {
                    summary.AppendLine(
                        "  InputField " + MenuTextConversion.BuildHierarchyPath(inputField.gameObject, root)
                            + " : textComponent=" + (inputField.textComponent != null
                                ? MenuTextConversion.BuildHierarchyPath(inputField.textComponent.gameObject, root)
                                : "<null>")
                            + " placeholder=" + (inputField.placeholder != null
                                ? MenuTextConversion.BuildHierarchyPath(inputField.placeholder.gameObject, root)
                                    + " (" + inputField.placeholder.GetType().Name + ")"
                                : "<null>")
                            + " characterLimit=" + inputField.characterLimit
                            + " contentType=" + inputField.contentType
                            + " lineType=" + inputField.lineType);
                }
            }

            List<string> unsupportedConsumers = new List<string>();
            MenuTextConversion.CollectUnsupportedConsumers(root, textSet, unsupportedConsumers);
            foreach (string finding in unsupportedConsumers)
            {
                summary.AppendLine("  UNSUPPORTED CONSUMER: " + finding);
            }

            Debug.Log("CreditsTextMeshProMigration.Report complete.\n" + summary);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Migration
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves the exact two legacy Text components the <c>ReportInputField</c>
    /// (<see cref="InputField"/>) contract requires to remain legacy - its <c>textComponent</c> and its
    /// <c>placeholder</c> (when the placeholder is itself a <see cref="Text"/>, as it is on this prefab
    /// today) - by reading the actual object references off the InputField component, never by
    /// hierarchy-name assumption. Returns null (and logs every problem found) if the prefab does not
    /// carry exactly the expected shape: exactly one directly-owned <see cref="InputField"/>, with a
    /// non-null <c>textComponent</c> and a non-null <c>placeholder</c> that is itself a legacy
    /// <see cref="Text"/>.
    /// </summary>
    private static HashSet<Text> ResolveProtectedInputFieldTexts(GameObject root, List<string> errors)
    {
        List<InputField> ownedInputFields = new List<InputField>();
        foreach (InputField candidate in root.GetComponentsInChildren<InputField>(true))
        {
            if (!MenuTextConversion.IsPartOfNestedPrefabInstance(candidate.gameObject, root))
            {
                ownedInputFields.Add(candidate);
            }
        }

        if (ownedInputFields.Count != 1)
        {
            errors.Add(
                CreditsManagerPrefabPath + " : expected exactly 1 directly-owned legacy InputField, found "
                    + ownedInputFields.Count + ".");
            return null;
        }

        InputField inputField = ownedInputFields[0];
        if (inputField.textComponent == null)
        {
            errors.Add(
                CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(inputField.gameObject, root)
                    + " : InputField.textComponent is null.");
            return null;
        }

        if (inputField.placeholder == null)
        {
            errors.Add(
                CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(inputField.gameObject, root)
                    + " : InputField.placeholder is null.");
            return null;
        }

        if (!(inputField.placeholder is Text placeholderText))
        {
            errors.Add(
                CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(inputField.gameObject, root)
                    + " : InputField.placeholder is a " + inputField.placeholder.GetType().Name
                    + ", not a legacy Text - this migration only supports a Text placeholder.");
            return null;
        }

        // Must be two DISTINCT Text components, not the same object serving both roles - otherwise the
        // HashSet below would silently collapse to a single protected Text and every check downstream
        // that counts/compares against "the 2 protected Text components" would be comparing against 1,
        // masking a genuinely broken InputField instead of reporting it.
        if (ReferenceEquals(inputField.textComponent, placeholderText))
        {
            errors.Add(
                CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(inputField.gameObject, root)
                    + " : InputField.textComponent and InputField.placeholder must be two distinct Text components,"
                    + " not the same one.");
            return null;
        }

        return new HashSet<Text> { inputField.textComponent, placeholderText };
    }

    /// <summary>
    /// AUD-092 Phase 4A: resolves the two now-redundant scene <c>m_Text</c> overrides
    /// (<c>website</c>/<c>music</c>) into <see cref="CreditsManagerPrefabPath"/>'s own Text defaults
    /// before any Text is destroyed - must run before <see cref="Migrate"/>. See
    /// <see cref="MenuTextConversion.ResolveSceneTextOverrides"/>'s doc comment for why order matters.
    /// </summary>
    [MenuItem("Level5/Resolve Credits Scene Text Overrides")]
    public static void ResolveSceneTextOverrides()
    {
        int resolved = MenuTextConversion.ResolveSceneTextOverrides(ScenePath, CreditsManagerPrefabPath);
        if (resolved < 0)
        {
            Debug.LogError("CreditsTextMeshProMigration.ResolveSceneTextOverrides: aborted, see errors above.");
            return;
        }

        Debug.Log("CreditsTextMeshProMigration.ResolveSceneTextOverrides: resolved " + resolved + " override(s).");
    }

    /// <summary>
    /// Idempotent Text -&gt; TextMeshProUGUI conversion for every ORDINARY (non-InputField-owned) legacy
    /// <see cref="Text"/> directly owned by <see cref="CreditsManagerPrefabPath"/>. No-ops (logged) once
    /// only the two protected InputField Text dependencies remain. Aborts without saving if any per-Text
    /// step fails, if the InputField boundary cannot be resolved (see
    /// <see cref="ResolveProtectedInputFieldTexts"/>), or if a <see cref="Selectable"/> is left with a
    /// null <c>targetGraphic</c> it did not have before.
    /// </summary>
    [MenuItem("Level5/Migrate Credits To TMP")]
    public static void Migrate()
    {
        MenuTextConversion.MigratePrefabTexts(
            CreditsManagerPrefabPath,
            "CreditsTextMeshProMigration.Migrate",
            ResolveProtectedInputFieldTexts);
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectCreditsTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AUD-092 Phase 4A permanent regression guard, mirroring the shape of
    /// <see cref="ProgressionTextMeshProMigration.CollectContractErrors"/> but requiring EXACTLY the two
    /// InputField-owned legacy Text components to remain (not zero) - the temporary AUD-092 Phase 4A
    /// InputField boundary, scheduled for removal in Phase 4B. Checks: every migrated TMP component has
    /// a font asset, no Selectable (other than the InputField itself) has a null targetGraphic, no
    /// property modification in <see cref="ScenePath"/> targets a legacy Text, the nested
    /// touch_joystick.prefab instance carries no per-instance Text/TMP override, and the InputField
    /// boundary is exactly as expected.
    /// </summary>
    public static List<string> CollectContractErrors()
    {
        List<string> errors = new List<string>();

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsManagerPrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(CreditsManagerPrefabPath + " : could not load creditsManager prefab asset.");
            return errors;
        }

        List<Text> ownedLegacyTexts = new List<Text>();
        List<Text> nestedLegacyTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedLegacyTexts, nestedLegacyTexts);

        List<string> boundaryErrors = new List<string>();
        HashSet<Text> protectedTexts = ResolveProtectedInputFieldTexts(prefabRoot, boundaryErrors);
        errors.AddRange(boundaryErrors);

        if (protectedTexts != null)
        {
            foreach (Text unexpected in ownedLegacyTexts.FindAll(text => !protectedTexts.Contains(text)))
            {
                errors.Add(
                    CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(unexpected.gameObject, prefabRoot)
                        + " : unexpected directly-owned legacy Text outside the temporary AUD-092 Phase 4A"
                        + " InputField boundary (scheduled for removal in Phase 4B).");
            }

            // Checked directly against membership, not inferred from set sizes: the InputField's
            // textComponent/placeholder could in principle resolve to a Text that lives outside this
            // prefab's own directly-owned Text set (e.g. inside a nested prefab instance), which a
            // count comparison alone would not reliably catch once more than one such reference exists.
            foreach (Text expected in protectedTexts)
            {
                if (!ownedLegacyTexts.Contains(expected))
                {
                    errors.Add(
                        CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(expected.gameObject, prefabRoot)
                            + " : InputField.textComponent/placeholder does not resolve to a directly-owned legacy Text on this prefab.");
                }
            }
        }

        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null)
            {
                errors.Add(
                    CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, prefabRoot)
                        + " : TextMeshProUGUI has no font asset.");
            }
        }

        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, prefabRoot))
            {
                continue; // touch_joystick - a shared nested prefab instance, out of scope for this contract
            }

            if (selectable.targetGraphic == null)
            {
                errors.Add(
                    CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(selectable.gameObject, prefabRoot) + " : "
                        + selectable.GetType().Name + " has a null targetGraphic.");
            }
        }

        MenuTextConversion.CollectDanglingSceneTextOverrides(ScenePath, CreditsManagerPrefabPath, errors);
        MenuTextConversion.CollectDanglingPrefabTextOverrides(CreditsManagerPrefabPath, TouchJoystickPrefabPath, errors);

        return errors;
    }
}
