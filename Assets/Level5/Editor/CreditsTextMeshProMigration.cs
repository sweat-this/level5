using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
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
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";
    private const int ExpectedTextMeshProCount = 23;

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
        List<InputField> ownedInputFields = FindOwnedInputFields(root);
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

    /// <summary>
    /// AUD-092 Phase 4B: migrates the legacy <c>ReportInputField</c> (<see cref="InputField"/>) itself,
    /// and its two structural Text dependencies, to <see cref="TMP_InputField"/>/<see cref="TextMeshProUGUI"/>.
    /// Idempotent: no-ops (logged) once no directly-owned legacy InputField remains and a TMP_InputField
    /// already does. Aborts without saving on any precondition failure, matching every other migration
    /// in this file - <see cref="PrefabUtility.LoadPrefabContents"/> gives this a disposable scratch
    /// copy, so an abort here truly discards the attempt.
    ///
    /// Follows the ordering required by AUD-092 Phase 4B: resolve and validate the legacy shape, capture
    /// every piece of state that must survive, THEN destroy the legacy InputField, build the TMP
    /// viewport, convert the two Text dependencies (now safe - no live legacy InputField references
    /// them any more), add and configure TMP_InputField, restore captured state, rewire every other
    /// reference that pointed at the destroyed legacy InputField (Navigation targets elsewhere in the
    /// prefab, <see cref="CreditsUiObjects.ReportInputField"/>), and only then save.
    /// </summary>
    [MenuItem("Level5/Migrate Credits InputField To TMP")]
    public static void MigrateInputField()
    {
        const string LogPrefix = "CreditsTextMeshProMigration.MigrateInputField";

        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogError(
                LogPrefix + ": TMP Essential Resources are not present."
                    + " Run Level5/Import TMP Essential Resources first, then re-run this.");
            return;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            Debug.LogError(LogPrefix + ": could not create/load the Neon Pixel-7 SDF font asset; aborting.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CreditsManagerPrefabPath);
        try
        {
            List<InputField> ownedInputFields = FindOwnedInputFields(root);
            if (ownedInputFields.Count == 0)
            {
                if (root.GetComponentInChildren<TMP_InputField>(true) != null)
                {
                    Debug.Log(LogPrefix + ": no directly-owned legacy InputField remains; nothing to do.");
                }
                else
                {
                    AbortMigration(LogPrefix, "no directly-owned legacy InputField and no TMP_InputField either - the InputField boundary could not be resolved.");
                }

                return;
            }

            List<string> errors = new List<string>();
            HashSet<Text> protectedTexts = ResolveProtectedInputFieldTexts(root, errors);
            if (protectedTexts == null)
            {
                AbortMigration(LogPrefix, errors);
                return;
            }

            InputField inputField = ownedInputFields[0];

            // Capture/destroy/rebuild/restore/navigation-repair mechanics live in MenuTextConversion,
            // shared with AccountTextMeshProMigration (AUD-092 Phase 5B) - see its doc comment for the
            // exact sequence and preconditions.
            TMP_InputField tmpInputField = MenuTextConversion.ConvertInputField(root, inputField, font, errors);
            if (tmpInputField == null)
            {
                AbortMigration(LogPrefix, errors);
                return; // root is a disposable LoadPrefabContents scratch copy - safe to abandon without saving
            }

            CreditsUiObjects ui = root.GetComponentInChildren<CreditsUiObjects>(true);
            if (ui == null)
            {
                AbortMigration(LogPrefix, "no CreditsUiObjects component found to rewire.");
                return;
            }

            SerializedObject serializedUi = new SerializedObject(ui);
            SerializedProperty reportInputFieldProperty = serializedUi.FindProperty("reportInputField");
            if (reportInputFieldProperty == null)
            {
                AbortMigration(LogPrefix, "CreditsUiObjects has no 'reportInputField' field.");
                return;
            }

            reportInputFieldProperty.objectReferenceValue = tmpInputField;
            serializedUi.ApplyModifiedProperties();

            RemoveSubmitReportPersistentListener(root, ui);

            List<string> postMutationErrors = new List<string>();
            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, root))
                {
                    continue;
                }

                if (selectable.targetGraphic == null)
                {
                    postMutationErrors.Add(
                        MenuTextConversion.BuildHierarchyPath(selectable.gameObject, root) + " : " + selectable.GetType().Name
                            + " has a null targetGraphic after migration.");
                }
            }

            if (postMutationErrors.Count > 0)
            {
                AbortMigration(LogPrefix, postMutationErrors);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, CreditsManagerPrefabPath);
            Debug.Log(LogPrefix + " complete: ReportInputField migrated to TMP_InputField.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Single, consistently-formatted abort message for every failure path in
    /// <see cref="MigrateInputField"/> - previously each precondition/post-mutation check logged its own
    /// ad hoc <c>Debug.LogError</c>, in a different shape from the aggregated "aborted without saving -
    /// N error(s)" format <see cref="MenuTextConversion.MigratePrefabTexts"/> uses for every other
    /// migration in this file. All abort paths now share that one shape.
    /// </summary>
    private static void AbortMigration(string logPrefix, string singleError)
    {
        AbortMigration(logPrefix, new List<string> { singleError });
    }

    private static void AbortMigration(string logPrefix, IReadOnlyList<string> errors)
    {
        Debug.LogError(logPrefix + " aborted without saving - " + errors.Count + " error(s):\n- " + string.Join("\n- ", errors));
    }

    /// <summary>Runs <see cref="Migrate"/> followed by <see cref="MigrateInputField"/>.</summary>
    [MenuItem("Level5/Migrate All Credits TMP")]
    public static void MigrateAll()
    {
        Migrate();
        MigrateInputField();
    }

    private static List<InputField> FindOwnedInputFields(GameObject root)
    {
        List<InputField> owned = new List<InputField>();
        foreach (InputField candidate in root.GetComponentsInChildren<InputField>(true))
        {
            if (!MenuTextConversion.IsPartOfNestedPrefabInstance(candidate.gameObject, root))
            {
                owned.Add(candidate);
            }
        }

        return owned;
    }

    /// <summary>
    /// AUD-092 Phase 4B: removes the persistent <c>submit_report</c> OnClick -&gt; <c>CreditsManager.SubmitReport</c>
    /// listener authored directly on the prefab, which duplicated the code-owned, guarded registration
    /// <see cref="CreditsManager"/> already performs (<c>submit_report -&gt; SubmitReportIfAllowed -&gt; SubmitReport</c>).
    /// One button click must produce at most one submission through the guarded route; leaving both
    /// wired risked the unguarded persistent call firing independently of the <c>ApiLocked</c>/<c>buttonPressed</c>
    /// checks <c>SubmitReportIfAllowed</c> exists to enforce. Idempotent: a second run finds nothing to
    /// remove.
    /// </summary>
    private static void RemoveSubmitReportPersistentListener(GameObject root, CreditsUiObjects ui)
    {
        CreditsManager manager = root.GetComponentInChildren<CreditsManager>(true);
        Button submitButton = ui.SubmitReportButton;
        if (manager == null || submitButton == null)
        {
            return;
        }

        for (int i = submitButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (submitButton.onClick.GetPersistentTarget(i) == manager
                && submitButton.onClick.GetPersistentMethodName(i) == "SubmitReport")
            {
                UnityEventTools.RemovePersistentListener(submitButton.onClick, i);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectCreditsTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AUD-092 Phase 4B permanent regression guard - the final Credits text-rendering contract,
    /// replacing the temporary Phase 4A InputField boundary exception. Requires: zero directly-owned
    /// legacy <see cref="Text"/>, zero directly-owned legacy <see cref="InputField"/>, exactly one
    /// directly-owned <see cref="TMP_InputField"/> with a valid masked <c>textViewport</c> and
    /// TextMeshProUGUI <c>textComponent</c>/<c>placeholder</c> on the shared Neon Pixel-7 SDF font, its
    /// original characterLimit/lineType/targetGraphic/navigation preserved, exactly
    /// <see cref="ExpectedTextMeshProCount"/> directly-owned TextMeshProUGUI in total (the 21 ordinary
    /// Phase 4A labels plus the InputField's own content/placeholder), no other Selectable with a null
    /// targetGraphic, no dangling scene/prefab overrides, and the nested touch_joystick.prefab instance
    /// untouched.
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
        if (ownedLegacyTexts.Count > 0)
        {
            errors.Add(
                CreditsManagerPrefabPath + " : " + ownedLegacyTexts.Count
                    + " legacy Text component(s) directly owned by this prefab remain (expected 0 - AUD-092 Phase 4B is complete).");
        }

        List<InputField> ownedLegacyInputFields = FindOwnedInputFields(prefabRoot);
        if (ownedLegacyInputFields.Count > 0)
        {
            errors.Add(
                CreditsManagerPrefabPath + " : " + ownedLegacyInputFields.Count
                    + " legacy InputField component(s) directly owned by this prefab remain (expected 0).");
        }

        List<TMP_InputField> ownedTmpInputFields = new List<TMP_InputField>();
        foreach (TMP_InputField candidate in prefabRoot.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (!MenuTextConversion.IsPartOfNestedPrefabInstance(candidate.gameObject, prefabRoot))
            {
                ownedTmpInputFields.Add(candidate);
            }
        }

        if (ownedTmpInputFields.Count != 1)
        {
            errors.Add(
                CreditsManagerPrefabPath + " : expected exactly 1 directly-owned TMP_InputField, found "
                    + ownedTmpInputFields.Count + ".");
        }
        else
        {
            AddTmpInputFieldContractErrors(prefabRoot, ownedTmpInputFields[0], errors);
        }

        List<TextMeshProUGUI> ownedTmpTexts = new List<TextMeshProUGUI>();
        foreach (TextMeshProUGUI candidate in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!MenuTextConversion.IsPartOfNestedPrefabInstance(candidate.gameObject, prefabRoot))
            {
                ownedTmpTexts.Add(candidate);
            }
        }

        if (ownedTmpTexts.Count != ExpectedTextMeshProCount)
        {
            errors.Add(
                CreditsManagerPrefabPath + " : expected exactly " + ExpectedTextMeshProCount
                    + " directly-owned TextMeshProUGUI component(s) (21 ordinary Phase 4A labels plus the"
                    + " InputField's content/placeholder), found " + ownedTmpTexts.Count + ".");
        }

        foreach (TextMeshProUGUI tmp in ownedTmpTexts)
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

    private static void AddTmpInputFieldContractErrors(GameObject prefabRoot, TMP_InputField tmpInputField, List<string> errors)
    {
        string where = CreditsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmpInputField.gameObject, prefabRoot);
        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);

        MenuTextConversion.AddTmpInputFieldViewportContractErrors(where, tmpInputField, errors);
        MenuTextConversion.AddTmpInputFieldSubTextContractErrors(where, "textComponent", tmpInputField.textComponent, neonPixel, errors);
        MenuTextConversion.AddTmpInputFieldSubTextContractErrors(where, "placeholder", tmpInputField.placeholder, neonPixel, errors);

        if (tmpInputField.characterLimit != 255)
        {
            errors.Add(where + " : characterLimit is " + tmpInputField.characterLimit + ", expected 255.");
        }

        if (tmpInputField.lineType != TMP_InputField.LineType.MultiLineSubmit)
        {
            errors.Add(where + " : lineType is " + tmpInputField.lineType + ", expected MultiLineSubmit.");
        }

        // The legacy InputField this replaced never supported rich text; TMP_InputField.richText
        // defaults to true and, if ever re-enabled (e.g. an Inspector edit triggering
        // SetTextComponentRichTextMode()), would let both the user's typed report and the server's
        // echoed result message (CreditsManager.PresentReportResult) interpret TMP markup - including
        // clickable <link> tags - that this field must never render as anything but literal text.
        if (tmpInputField.richText)
        {
            errors.Add(where + " : richText is enabled; ReportInputField must stay plain-text like the legacy InputField it replaced.");
        }

        if (tmpInputField.targetGraphic == null)
        {
            errors.Add(where + " : targetGraphic is null.");
        }

        // Explicit mode (rather than Automatic) is required here specifically because selectOnUp/selectOnDown
        // are load-bearing for this screen's intended flow (itch.io link above <-> this field <-> submit_report
        // below) - not an arbitrary migration artifact. If this ever needs to change, update the flow's
        // Selectables together, not just this check.
        Navigation navigation = tmpInputField.navigation;
        if (navigation.mode != Navigation.Mode.Explicit)
        {
            errors.Add(
                where + " : navigation.mode is " + navigation.mode + ", expected Explicit - ReportInputField's"
                    + " up/down navigation (itch.io / submit_report) is authored explicitly and must stay so.");
        }
        else
        {
            if (navigation.selectOnUp == null)
            {
                errors.Add(where + " : navigation.selectOnUp is null.");
            }

            if (navigation.selectOnDown == null)
            {
                errors.Add(where + " : navigation.selectOnDown is null.");
            }
        }
    }
}
