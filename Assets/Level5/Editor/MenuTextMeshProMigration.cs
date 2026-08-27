using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 1: migrates <c>OptionManager.prefab</c>'s 103 legacy <see cref="Text"/> components to
/// <see cref="TextMeshProUGUI"/> on a project-owned Neon Pixel-7 SDF font asset, the pilot for the wider
/// menu-text migration (see docs/ui-menu-audit-2026-08-17.md AUD-092 and
/// docs/ui-input-architecture.md Phase 6). Deliberately scoped to the Options screen only.
///
/// Mirrors <see cref="MenuLayoutOwnershipMigration"/>'s shape: <see cref="PrefabUtility"/>/
/// <see cref="SerializedObject"/> only (never hand-edited YAML), one-off menu-item actions plus
/// permanent <see cref="CollectContractErrors"/> infrastructure that stays after the migration lands.
///
/// AUD-092 Phase 2 extracted this class's low-level conversion mechanics into
/// <see cref="MenuTextConversion"/> so the Stats screen migration (<see cref="StatsTextMeshProMigration"/>)
/// could reuse them without duplicating the property-mapping/outline-compensation logic. This class now
/// keeps only Options-specific orchestration (which prefab/scene, which contract).
/// </summary>
public static class MenuTextMeshProMigration
{
    private const string PrefabPath = "Assets/Resources/Prefabs/critical/OptionManager.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_options.unity";

    // ---------------------------------------------------------------------------------------------
    // Step 1: TMP Essential Resources
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Imports the same "TMP Essential Resources.unitypackage" that
    /// <c>Window &gt; TextMeshPro &gt; Import TMP Essential Resources</c> imports
    /// (<see cref="TMP_PackageUtilities.ImportProjectResourcesMenu"/>), via
    /// <see cref="AssetDatabase.ImportPackage"/> with <c>interactive: false</c> so it is safe to run
    /// from batchmode. Idempotent: a project that already has a <see cref="TMP_Settings"/> asset is left
    /// untouched.
    ///
    /// <see cref="AssetDatabase.ImportPackage"/> is asynchronous - it only finishes over subsequent
    /// Editor update ticks. Called from an interactive Editor that is harmless (the import completes
    /// naturally while the user keeps working); called from a one-shot <c>-executeMethod</c> batchmode
    /// invocation with <c>-quit</c>, the process was observed to exit before the import actually landed
    /// anything on disk, silently producing nothing despite this method's own log line claiming success.
    /// In batchmode this now waits for <see cref="AssetDatabase.importPackageCompleted"/> and exits the
    /// process itself from that callback, and also self-exits on the already-present no-op path - so a
    /// batchmode caller of this method must omit <c>-quit</c> in both cases, since which path is taken
    /// is not known in advance.
    /// </summary>
    [MenuItem("Level5/Import TMP Essential Resources")]
    public static void ImportTmpEssentialResourcesIfNeeded()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0)
        {
            Debug.Log("MenuTextMeshProMigration: TMP Essential Resources already present; nothing to import.");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }

            return;
        }

        string packagePath = TMP_EditorUtility.packageFullPath + "/Package Resources/TMP Essential Resources.unitypackage";

        if (Application.isBatchMode)
        {
            AssetDatabase.importPackageCompleted += OnBatchModeEssentialResourcesImportCompleted;
        }

        AssetDatabase.ImportPackage(packagePath, false);
        Debug.Log("MenuTextMeshProMigration: importing TMP Essential Resources from " + packagePath);
    }

    private static void OnBatchModeEssentialResourcesImportCompleted(string packageName)
    {
        AssetDatabase.importPackageCompleted -= OnBatchModeEssentialResourcesImportCompleted;
        AssetDatabase.SaveAssets();
        Debug.Log("MenuTextMeshProMigration: TMP Essential Resources import completed; exiting batchmode process.");
        EditorApplication.Exit(0);
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: Neon Pixel-7 SDF font asset
    // ---------------------------------------------------------------------------------------------

    /// <summary>Delegates to the project-wide <see cref="MenuTextConversion.EnsureNeonPixelFontAsset"/>.</summary>
    [MenuItem("Level5/Create Neon Pixel TMP Font Asset")]
    public static TMP_FontAsset EnsureNeonPixelFontAsset()
    {
        return MenuTextConversion.EnsureNeonPixelFontAsset();
    }

    // ---------------------------------------------------------------------------------------------
    // Step 3: resolve the two scene m_Text overrides before any component is touched
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>level_00_options.unity</c> carries two <c>m_Text</c> prefab-instance overrides against
    /// legacy <see cref="Text"/> components ("space, left mouse", "shoot"). Converting those Text
    /// components to TMP without resolving this first would leave a dangling override pointing at a
    /// destroyed component's fileID. Delegates to <see cref="MenuTextConversion.ResolveSceneTextOverrides"/>.
    /// Idempotent: a second run finds nothing left to resolve.
    /// </summary>
    [MenuItem("Level5/Resolve Options Scene Text Overrides")]
    public static void ResolveSceneTextOverrides()
    {
        int resolvedCount = MenuTextConversion.ResolveSceneTextOverrides(ScenePath, PrefabPath);
        if (resolvedCount < 0)
        {
            return;
        }

        if (resolvedCount == 0)
        {
            Debug.Log("MenuTextMeshProMigration.ResolveSceneTextOverrides: nothing to resolve.");
            return;
        }

        Debug.Log(
            "MenuTextMeshProMigration.ResolveSceneTextOverrides: resolved " + resolvedCount
                + " scene text override(s) into the prefab.");
    }

    // ---------------------------------------------------------------------------------------------
    // Characterization report (read-only)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Read-only characterization of every legacy <see cref="Text"/> in <see cref="PrefabPath"/>: full
    /// visual property snapshot, which <see cref="Selectable"/>s target it (the one supported reference
    /// consumer), and a scan of every other component's serialized object-reference properties for any
    /// unsupported consumer this migration would need to handle explicitly. Never modifies the asset.
    /// </summary>
    [MenuItem("Level5/Report Options TMP Migration")]
    public static void Report()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            StringBuilder summary = new StringBuilder();
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            summary.AppendLine(
                PrefabPath + " : " + texts.Count + " legacy Text component(s) owned directly by this prefab, "
                    + nestedTexts.Count + " more inside nested prefab instance(s) (out of scope for this migration).");

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
                summary.AppendLine(
                    "  " + path
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
                        + (text.resizeTextForBestFit
                            ? " (min=" + text.resizeTextMinSize + " max=" + text.resizeTextMaxSize + ")"
                            : string.Empty)
                        + " hOverflow=" + text.horizontalOverflow
                        + " vOverflow=" + text.verticalOverflow
                        + " lineSpacing=" + text.lineSpacing
                        + " enabled=" + text.enabled
                        + (text.GetComponent<Outline>() != null ? " [has Outline]" : string.Empty));
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
            }

            List<string> unsupportedConsumers = new List<string>();
            MenuTextConversion.CollectUnsupportedConsumers(root, textSet, unsupportedConsumers);
            foreach (string finding in unsupportedConsumers)
            {
                summary.AppendLine("  UNSUPPORTED CONSUMER: " + finding);
            }

            Debug.Log("MenuTextMeshProMigration.Report complete.\n" + summary);
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
    /// Idempotent Text -&gt; TextMeshProUGUI conversion for every legacy <see cref="Text"/> in
    /// <see cref="PrefabPath"/>. No-ops (logged) if none remain. Aborts without saving if any per-Text
    /// step fails or if a <see cref="Selectable"/> is left with a null <c>targetGraphic</c> it did not
    /// have before - <see cref="PrefabUtility.LoadPrefabContents"/> gives every mutation below a
    /// disposable scratch copy, so an abort here truly discards the attempt rather than leaving a
    /// partially-migrated prefab on disk.
    /// </summary>
    [MenuItem("Level5/Migrate Options To TMP")]
    public static void Migrate()
    {
        // Deliberately does not call ImportTmpEssentialResourcesIfNeeded() itself: that import is
        // asynchronous and, in batchmode, self-exits the process once it completes (see that method's
        // doc comment) - calling it from here would kill this method's own batch process mid-migration
        // whenever essentials were already present. Essential Resources are a real prerequisite (the
        // font asset's SDF material shader only exists once they are imported), so this requires them
        // to already be present rather than orchestrating the import inline.
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogError(
                "MenuTextMeshProMigration.Migrate: TMP Essential Resources are not present. Run"
                    + " Level5/Import TMP Essential Resources first, then re-run this.");
            return;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            Debug.LogError("MenuTextMeshProMigration.Migrate: could not create/load the Neon Pixel-7 SDF font asset; aborting.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            if (texts.Count == 0 && root.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0)
            {
                Debug.Log(
                    "MenuTextMeshProMigration.Migrate: no directly-owned legacy Text remains in " + PrefabPath
                        + "; nothing to do (" + nestedTexts.Count
                        + " Text component(s) inside nested prefab instances are intentionally left untouched).");
                return;
            }

            List<string> errors = new List<string>();
            int convertedCount = 0;
            int outlineCompensatedCount = 0;

            foreach (Text text in texts)
            {
                string path = MenuTextConversion.BuildHierarchyPath(text.gameObject, root);
                if (text.resizeTextForBestFit)
                {
                    errors.Add(path + " has Best Fit enabled; this migration does not support autosizing conversion.");
                    continue;
                }

                bool hadEnabledOutline = text.TryGetComponent(out Outline outline) && outline.enabled;
                TextMeshProUGUI tmp = ConvertSingleText(root, text, font);
                if (tmp == null)
                {
                    errors.Add(path + " : conversion failed to add TextMeshProUGUI.");
                    continue;
                }

                convertedCount++;
                if (hadEnabledOutline)
                {
                    outlineCompensatedCount++;
                }
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, root))
                {
                    continue; // pre-existing state of a nested prefab instance (e.g. touch_joystick) - not this migration's concern
                }

                if (selectable.targetGraphic == null)
                {
                    errors.Add(
                        MenuTextConversion.BuildHierarchyPath(selectable.gameObject, root) + " : " + selectable.GetType().Name
                            + " has a null targetGraphic after migration.");
                }
            }

            if (errors.Count > 0)
            {
                Debug.LogError(
                    "MenuTextMeshProMigration.Migrate aborted without saving - " + errors.Count + " error(s):\n- "
                        + string.Join("\n- ", errors));
                return;
            }

            MenuTextConversion.PersistLooseUnderlayMaterials(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log(
                "MenuTextMeshProMigration.Migrate complete: converted " + convertedCount
                    + " Text component(s), compensated " + outlineCompensatedCount
                    + " legacy Outline effect(s) via TMP underlay material.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Thin wrapper over <see cref="MenuTextConversion.ConvertSingleText"/> kept for source
    /// compatibility with <c>OptionsTextMeshProMigrationTests</c>, which exercises the conversion
    /// directly against throwaway objects.
    /// </summary>
    internal static TextMeshProUGUI ConvertSingleText(GameObject scopeRoot, Text text, TMP_FontAsset font)
    {
        return MenuTextConversion.ConvertSingleText(scopeRoot, text, font);
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectOptionsTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AUD-092 Phase 1 permanent regression guard: zero legacy <see cref="Text"/> remain in
    /// <see cref="PrefabPath"/>, every <see cref="TextMeshProUGUI"/> has a font asset, no
    /// <see cref="Selectable"/> has a null <c>targetGraphic</c>, and no property modification in
    /// <see cref="ScenePath"/> still targets a legacy Text. Do not remove when Phase 1 closes - later
    /// phases add their own screen-specific equivalent rather than replacing this one.
    /// </summary>
    public static List<string> CollectContractErrors()
    {
        List<string> errors = new List<string>();

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(PrefabPath + " : could not load OptionManager prefab asset.");
            return errors;
        }

        List<Text> ownedLegacyTexts = new List<Text>();
        List<Text> nestedLegacyTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedLegacyTexts, nestedLegacyTexts);
        if (ownedLegacyTexts.Count > 0)
        {
            errors.Add(
                PrefabPath + " : " + ownedLegacyTexts.Count
                    + " legacy Text component(s) directly owned by this prefab remain (expected 0).");
        }

        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null)
            {
                errors.Add(
                    PrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, prefabRoot)
                        + " : TextMeshProUGUI has no font asset.");
            }
        }

        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, prefabRoot))
            {
                continue; // e.g. touch_joystick - a shared nested prefab instance, out of scope for this contract
            }

            if (selectable.targetGraphic == null)
            {
                errors.Add(
                    PrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(selectable.gameObject, prefabRoot) + " : "
                        + selectable.GetType().Name + " has a null targetGraphic.");
            }
        }

        MenuTextConversion.CollectDanglingSceneTextOverrides(ScenePath, PrefabPath, errors);

        return errors;
    }
}
