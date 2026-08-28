using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 3: migrates <c>progressionScreen.prefab</c>'s directly-owned legacy <see cref="Text"/>
/// components to <see cref="TextMeshProUGUI"/> on the same project-owned Neon Pixel-7 SDF font asset
/// Options/Stats used (see docs/ui-menu-audit-2026-08-17.md AUD-092). Reuses
/// <see cref="MenuTextConversion"/> for every low-level mechanic Phase 1/2 already proved; this class
/// contributes only the Progression-specific orchestration and permanent contract.
///
/// Unlike Stats, <see cref="ProgressionUiObjects"/> does not live inside this prefab - it is added as
/// a component only on the scene instance of <c>progression_manager</c> in
/// <c>level_00_progression.unity</c>, because <c>ProgressionManager</c> (progression_manager.prefab)
/// and progressionScreen are separate source prefabs joined only in the scene. This migration
/// therefore does not wire named display-text fields the way
/// <see cref="StatsTextMeshProMigration.MigrateStatsManager"/> does; that wiring happens in
/// <see cref="MenuUiObjectsWiring.WireProgression"/> instead, once the Text components below exist as
/// TMP. This mirrors <see cref="MenuTextMeshProMigration"/>'s (Options) shape rather than Stats'.
///
/// <c>progressionScreen.prefab</c> nests one shared prefab instance that
/// <see cref="MenuTextConversion"/>'s nested-instance exclusion already keeps out of scope:
/// <c>touch_joystick.prefab</c> (shared by every critical/menu prefab). It also has no Outline
/// component and no legacy Best Fit usage (confirmed by direct inspection), so this migration needs no
/// Underlay compensation and no autosizing mapping.
///
/// This class also hosts the migration/contract for the SHARED <c>confirm_update.prefab</c> dialogue -
/// see the "SHARED confirm_update dialogue" region below for why that is not Progression-owned despite
/// living in this file.
/// </summary>
public static class ProgressionTextMeshProMigration
{
    private const string ProgressionScreenPrefabPath = "Assets/Resources/Prefabs/menu_progression/progressionScreen.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_progression.unity";

    private const string ConfirmUpdatePrefabPath = "Assets/Resources/Prefabs/misc/confirm_update.prefab";
    private const string ProgressionManagerPrefabPath = "Assets/Resources/Prefabs/menu_progression/progression_manager.prefab";

    // ---------------------------------------------------------------------------------------------
    // Characterization report (read-only)
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Level5/Report Progression TMP Migration")]
    public static void Report()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ProgressionScreenPrefabPath);
        try
        {
            StringBuilder summary = new StringBuilder();
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            summary.AppendLine(
                ProgressionScreenPrefabPath + " : " + texts.Count + " legacy Text component(s) owned directly by this prefab, "
                    + nestedTexts.Count + " more inside nested prefab instance(s) (out of scope).");

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

            Debug.Log("ProgressionTextMeshProMigration.Report complete.\n" + summary);
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
    /// Idempotent Text -&gt; TextMeshProUGUI conversion for every legacy <see cref="Text"/> directly
    /// owned by <see cref="ProgressionScreenPrefabPath"/>. No-ops (logged) if none remain. Aborts
    /// without saving if any per-Text step fails or if a <see cref="Selectable"/> is left with a null
    /// <c>targetGraphic</c> it did not have before - <see cref="PrefabUtility.LoadPrefabContents"/>
    /// gives every mutation below a disposable scratch copy, so an abort here truly discards the
    /// attempt. Named display-text fields are wired separately, in the scene, by
    /// <see cref="MenuUiObjectsWiring.WireProgression"/> - see this class's doc comment.
    /// </summary>
    [MenuItem("Level5/Migrate Progression To TMP")]
    public static void Migrate()
    {
        MenuTextConversion.MigratePrefabTexts(ProgressionScreenPrefabPath, "ProgressionTextMeshProMigration.Migrate");
    }

    /// <summary>
    /// Idempotent Text -&gt; TextMeshProUGUI conversion for every legacy <see cref="Text"/> directly owned
    /// by the SHARED <see cref="ConfirmUpdatePrefabPath"/> (see this class's doc comment for why it is
    /// migrated here rather than folded into <see cref="Migrate"/>). Same shape/guarantees as
    /// <see cref="Migrate"/>: no-ops (logged) once no legacy Text remains, aborts without saving on any
    /// per-Text failure or a <see cref="Selectable"/> left with a null <c>targetGraphic</c> it did not
    /// have before. <c>confirm_update.prefab</c> has no nested prefab instances of its own, so every
    /// owned <see cref="Text"/>/<see cref="Selectable"/> found here belongs to this prefab directly.
    /// </summary>
    [MenuItem("Level5/Migrate Confirm Dialogue To TMP")]
    public static void MigrateConfirmDialogue()
    {
        MenuTextConversion.MigratePrefabTexts(ConfirmUpdatePrefabPath, "ProgressionTextMeshProMigration.MigrateConfirmDialogue");
    }

    /// <summary>Runs <see cref="Migrate"/> followed by <see cref="MigrateConfirmDialogue"/>.</summary>
    [MenuItem("Level5/Migrate All Progression TMP")]
    public static void MigrateAll()
    {
        Migrate();
        MigrateConfirmDialogue();
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectProgressionTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AUD-092 Phase 3 permanent regression guard, mirroring
    /// <see cref="MenuTextMeshProMigration.CollectContractErrors"/>'s shape: zero legacy Text remain in
    /// <see cref="ProgressionScreenPrefabPath"/>, every TextMeshProUGUI has a font asset, no
    /// <see cref="Selectable"/> has a null <c>targetGraphic</c>, no property modification in
    /// <see cref="ScenePath"/> targets a legacy Text, and (since <see cref="ProgressionUiObjects"/> is
    /// scene-owned rather than prefab-owned here, unlike StatsUiObjects) every display-text/image
    /// reference on the scene's ProgressionUiObjects is wired.
    /// </summary>
    public static List<string> CollectContractErrors()
    {
        List<string> errors = new List<string>();

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ProgressionScreenPrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(ProgressionScreenPrefabPath + " : could not load progressionScreen prefab asset.");
            return errors;
        }

        List<Text> ownedLegacyTexts = new List<Text>();
        List<Text> nestedLegacyTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedLegacyTexts, nestedLegacyTexts);
        if (ownedLegacyTexts.Count > 0)
        {
            errors.Add(
                ProgressionScreenPrefabPath + " : " + ownedLegacyTexts.Count
                    + " legacy Text component(s) directly owned by this prefab remain (expected 0).");
        }

        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null)
            {
                errors.Add(
                    ProgressionScreenPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, prefabRoot)
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
                    ProgressionScreenPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(selectable.gameObject, prefabRoot) + " : "
                        + selectable.GetType().Name + " has a null targetGraphic.");
            }
        }

        MenuTextConversion.CollectDanglingSceneTextOverrides(ScenePath, ProgressionScreenPrefabPath, errors);
        CollectSceneUiObjectsContractErrors(errors);

        return errors;
    }

    /// <summary>
    /// ProgressionUiObjects is added only on the scene instance of progression_manager, not on either
    /// source prefab (see this class's doc comment), so this is the one contract check in this file
    /// that has to open the scene rather than read a prefab asset. Never closes a scene the developer
    /// already had open, matching <see cref="Level5ProjectValidator.CollectGameplaySceneObjectErrors"/>.
    /// </summary>
    private static void CollectSceneUiObjectsContractErrors(List<string> errors)
    {
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        bool alreadyOpen = existing.IsValid() && existing.isLoaded;
        Scene scene = alreadyOpen ? existing : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            ProgressionManager manager = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                manager = root.GetComponentInChildren<ProgressionManager>(true);
                if (manager != null)
                {
                    break;
                }
            }

            if (manager == null)
            {
                errors.Add(ScenePath + " : no ProgressionManager found.");
                return;
            }

            ProgressionUiObjects ui = manager.GetComponent<ProgressionUiObjects>();
            if (ui == null)
            {
                errors.Add(ScenePath + " : ProgressionManager has no ProgressionUiObjects component.");
                return;
            }

            List<string> missing = new List<string>();
            ui.Validate(missing);
            foreach (string field in missing)
            {
                errors.Add(ScenePath + " -> " + field + " is not wired.");
            }
        }
        finally
        {
            if (!alreadyOpen)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract for the SHARED confirm_update dialogue
    // (backs Level5ProjectValidator.CollectConfirmationDialogueTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AUD-092 Phase 3 permanent regression guard for the SHARED <see cref="ConfirmUpdatePrefabPath"/>
    /// (used by both progression_manager.prefab and DialogueManager.prefab - see this class's doc
    /// comment): zero legacy Text remain, every TextMeshProUGUI has a font asset, <c>confirm_button</c>
    /// and <c>cancel_button</c> each still carry a TextMeshProUGUI with a valid, matching
    /// <c>Selectable.targetGraphic</c>, no other Selectable in the prefab has a null targetGraphic, the
    /// <see cref="ConfirmDialogue"/> component is still present, and progression_manager.prefab's nested
    /// instance of this prefab carries no leftover legacy-Text property override.
    /// </summary>
    public static List<string> CollectConfirmationDialogueContractErrors()
    {
        List<string> errors = new List<string>();

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmUpdatePrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(ConfirmUpdatePrefabPath + " : could not load confirm_update prefab asset.");
            return errors;
        }

        List<Text> ownedLegacyTexts = new List<Text>();
        List<Text> nestedLegacyTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedLegacyTexts, nestedLegacyTexts);
        if (ownedLegacyTexts.Count > 0)
        {
            errors.Add(
                ConfirmUpdatePrefabPath + " : " + ownedLegacyTexts.Count
                    + " legacy Text component(s) directly owned by this SHARED confirm/cancel dialogue prefab remain"
                    + " (expected 0). confirm_update.prefab is used by both progression_manager.prefab and"
                    + " DialogueManager.prefab.");
        }

        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null)
            {
                errors.Add(
                    ConfirmUpdatePrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, prefabRoot)
                        + " : TextMeshProUGUI has no font asset.");
            }
        }

        // Matches every sibling CollectContractErrors (Options/Stats/Progression): a nested prefab
        // instance's pre-existing Selectables are out of this migration's scope, so a null targetGraphic
        // there is not this contract's concern. confirm_update.prefab has none today, but this guard
        // keeps that true if one is ever added rather than silently starting to flag it.
        HashSet<GameObject> reportedNullTargetGraphic = new HashSet<GameObject>();
        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, prefabRoot))
            {
                continue;
            }

            if (selectable.targetGraphic == null)
            {
                errors.Add(
                    ConfirmUpdatePrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(selectable.gameObject, prefabRoot) + " : "
                        + selectable.GetType().Name + " has a null targetGraphic.");
                reportedNullTargetGraphic.Add(selectable.gameObject);
            }
        }

        Dictionary<string, Transform> childrenByName = MenuTextConversion.IndexChildrenByName(prefabRoot);
        string[] expectedButtonNames = { "confirm_button", "cancel_button" };
        foreach (string buttonName in expectedButtonNames)
        {
            if (!childrenByName.TryGetValue(buttonName, out Transform buttonTransform))
            {
                errors.Add(ConfirmUpdatePrefabPath + " : expected GameObject '" + buttonName + "' was not found.");
                continue;
            }

            TextMeshProUGUI tmp = buttonTransform.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                errors.Add(ConfirmUpdatePrefabPath + " -> " + buttonName + " : has no TextMeshProUGUI component.");
            }

            Selectable selectable = buttonTransform.GetComponent<Selectable>();
            if (selectable == null)
            {
                errors.Add(ConfirmUpdatePrefabPath + " -> " + buttonName + " : has no Selectable/Button component.");
            }
            else if (tmp != null && selectable.targetGraphic != tmp && !reportedNullTargetGraphic.Contains(buttonTransform.gameObject))
            {
                // The null-targetGraphic case for this exact GameObject was already reported by the
                // general Selectable loop above; only report here when targetGraphic is non-null but
                // wrong (points at something other than this button's own migrated TextMeshProUGUI), so
                // one underlying defect doesn't produce two errors describing it two different ways.
                errors.Add(
                    ConfirmUpdatePrefabPath + " -> " + buttonName
                        + " : Selectable.targetGraphic does not reference the migrated TextMeshProUGUI.");
            }
        }

        if (prefabRoot.GetComponentInChildren<ConfirmDialogue>(true) == null)
        {
            errors.Add(ConfirmUpdatePrefabPath + " : ConfirmDialogue component is missing.");
        }

        MenuTextConversion.CollectDanglingPrefabTextOverrides(ProgressionManagerPrefabPath, ConfirmUpdatePrefabPath, errors);

        return errors;
    }
}
