using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 2: migrates <c>StatsManager.prefab</c>'s directly-owned legacy <see cref="Text"/>
/// components and <c>Assets/Resources/Prefabs/stats/highScoreRow.prefab</c>'s six legacy Text columns
/// to <see cref="TextMeshProUGUI"/> on the same project-owned Neon Pixel-7 SDF font asset Options used
/// (see docs/ui-menu-audit-2026-08-17.md AUD-092). Reuses <see cref="MenuTextConversion"/> for every
/// low-level mechanic Phase 1 already proved; this class contributes only the Stats-specific
/// orchestration - which prefabs, which named fields map to which <see cref="StatsUiObjects"/>/
/// <see cref="StatsTableHighScoreRow"/> reference, and the permanent per-screen contract.
///
/// <c>StatsManager.prefab</c> nests two nested prefab instances that <see cref="MenuTextConversion"/>'s
/// nested-instance exclusion already keeps out of scope: <c>touch_joystick.prefab</c> (shared by every
/// critical/menu prefab, one Text) and an inactive authoring-leftover <c>highScoreRow.prefab</c>
/// instance (six Text) that carries no <c>m_Text</c> override of its own - it picks up TMP automatically
/// once <see cref="MigrateHighScoreRow"/> lands, with nothing left to reconcile.
/// </summary>
public static class StatsTextMeshProMigration
{
    private const string StatsManagerPrefabPath = "Assets/Resources/Prefabs/menu_stats/StatsManager.prefab";
    private const string HighScoreRowPrefabPath = "Assets/Resources/Prefabs/stats/highScoreRow.prefab";
    private const string ScenePath = "Assets/Scenes/level_00_stats.unity";

    /// <summary>
    /// StatsManager.prefab's 11 production text references used to be individually [SerializeField] Text
    /// fields directly on <see cref="StatsManager"/> (see StatsManager.cs before AUD-092 Phase 2). Those
    /// fields are gone from the compiled class now that StatsManager resolves display text from
    /// <see cref="StatsUiObjects"/> instead (matching how Button references already worked, AUD-103), so
    /// this migration cannot recover "which GameObject was field X" via SerializedObject reflection
    /// against StatsManager the way <see cref="MenuTextMeshProMigration.ConvertSingleText"/>'s Selectable
    /// rewiring does. Every name below was captured directly from the pre-migration prefab
    /// (StatsManager's serialized <c>m_Script</c> field values) and re-verified unique among this
    /// prefab's directly-owned Text GameObjects before this migration ran; it is the authoritative
    /// mapping this one-off tool exists to apply.
    /// </summary>
    private static readonly (string GameObjectName, string UiFieldName)[] NamedTextFields =
    {
        ("mode_select_name", "modeSelectText"),                     // modeSelectButtonText
        ("hardcore_name_button", "modeSelectHardcoreText"),          // modeSelectButtonHardcoreText
        ("mode_select_name_online", "modeSelectOnlineText"),         // modeSelectButtonOnlineText
        ("page_number_local", "pageNumberLocalText"),                // pageNumberLocalSelectButtonText
        ("page_number_online", "pageNumberOnlineText"),              // pageNumberOnlineSelectButtonText
        ("traffic_value_button", "trafficOptionValueText"),          // trafficSelectOptionText
        ("hardcore_value_button", "hardcoreOptionValueText"),        // hardcoreSelectOptionText
        ("enemies_value_button", "enemiesOptionValueText"),          // enemySelectOptionText
        ("sniper_value_button", "sniperOptionValueText"),            // sniperSelectOptionText
        ("submitButton", "submittedHighscoresText"),                 // submittedHighscoresText
        ("submitCount", "numUnsubmittedHighscoresText"),              // numUnsubmittedHighscoresText
    };

    private static readonly (string GameObjectName, string RowFieldName)[] HighScoreRowLabelFields =
    {
        ("username", "userNameLabel"),
        ("score", "scoreLabel"),
        ("character", "characterLabel"),
        ("level", "levelLabel"),
        ("date", "dateLabel"),
        ("hardcore", "hardcoreLabel"),
    };

    // ---------------------------------------------------------------------------------------------
    // Characterization report (read-only)
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Level5/Report Stats TMP Migration")]
    public static void Report()
    {
        ReportPrefab(StatsManagerPrefabPath);
        ReportPrefab(HighScoreRowPrefabPath);
    }

    private static void ReportPrefab(string prefabPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            StringBuilder summary = new StringBuilder();
            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            summary.AppendLine(
                prefabPath + " : " + texts.Count + " legacy Text component(s) owned directly by this prefab, "
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

            Debug.Log("StatsTextMeshProMigration.Report complete for " + prefabPath + ".\n" + summary);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Migration
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Level5/Migrate Stats To TMP")]
    public static void MigrateAll()
    {
        MigrateStatsManager();
        MigrateHighScoreRow();
    }

    /// <summary>
    /// Idempotent Text -&gt; TextMeshProUGUI conversion for every legacy Text directly owned by
    /// <see cref="StatsManagerPrefabPath"/>, wiring the 11 named fields (<see cref="NamedTextFields"/>)
    /// into <see cref="StatsUiObjects"/>. No-ops (logged) if none remain. Aborts without saving on any
    /// per-Text failure, a null targetGraphic left on a Selectable, or a named field that cannot be
    /// resolved - see <see cref="MenuTextMeshProMigration.Migrate"/> for why <c>LoadPrefabContents</c>
    /// makes an abort here fully safe (a disposable scratch copy).
    /// </summary>
    [MenuItem("Level5/Migrate Stats Manager To TMP")]
    public static void MigrateStatsManager()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogError(
                "StatsTextMeshProMigration.MigrateStatsManager: TMP Essential Resources are not present."
                    + " Run Level5/Import TMP Essential Resources first, then re-run this.");
            return;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            Debug.LogError("StatsTextMeshProMigration.MigrateStatsManager: could not create/load the Neon Pixel-7 SDF font asset; aborting.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(StatsManagerPrefabPath);
        try
        {
            StatsUiObjects ui = root.GetComponentInChildren<StatsUiObjects>(true);
            if (ui == null)
            {
                Debug.LogError(
                    "StatsTextMeshProMigration.MigrateStatsManager: no StatsUiObjects component found in "
                        + StatsManagerPrefabPath);
                return;
            }

            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            if (texts.Count == 0 && root.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0)
            {
                Debug.Log(
                    "StatsTextMeshProMigration.MigrateStatsManager: no directly-owned legacy Text remains in "
                        + StatsManagerPrefabPath + "; nothing to do (" + nestedTexts.Count
                        + " Text component(s) inside nested prefab instances are intentionally left untouched).");
                return;
            }

            List<string> errors = new List<string>();
            bool ok = MenuTextConversion.ConvertOwnedTextsAndWireNamedFields(
                root, texts, font, NamedTextFields, ui, checkSelectableTargetGraphics: true,
                out int convertedCount, out int outlineCompensatedCount, errors);

            if (!ok)
            {
                Debug.LogError(
                    "StatsTextMeshProMigration.MigrateStatsManager aborted without saving - " + errors.Count
                        + " error(s):\n- " + string.Join("\n- ", errors));
                return;
            }

            MenuTextConversion.PersistLooseUnderlayMaterials(root);
            PrefabUtility.SaveAsPrefabAsset(root, StatsManagerPrefabPath);
            Debug.Log(
                "StatsTextMeshProMigration.MigrateStatsManager complete: converted " + convertedCount
                    + " Text component(s) (" + NamedTextFields.Length + " wired into StatsUiObjects), compensated "
                    + outlineCompensatedCount + " legacy Outline effect(s) via TMP underlay material.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Idempotent Text -&gt; TextMeshProUGUI conversion for <see cref="HighScoreRowPrefabPath"/>'s six
    /// columns, wiring <see cref="HighScoreRowLabelFields"/> into <see cref="StatsTableHighScoreRow"/>.
    /// This prefab nests nothing and has no Selectable, so it skips those checks Options/StatsManager
    /// need.
    /// </summary>
    [MenuItem("Level5/Migrate Stats High Score Row To TMP")]
    public static void MigrateHighScoreRow()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogError(
                "StatsTextMeshProMigration.MigrateHighScoreRow: TMP Essential Resources are not present."
                    + " Run Level5/Import TMP Essential Resources first, then re-run this.");
            return;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            Debug.LogError("StatsTextMeshProMigration.MigrateHighScoreRow: could not create/load the Neon Pixel-7 SDF font asset; aborting.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(HighScoreRowPrefabPath);
        try
        {
            StatsTableHighScoreRow row = root.GetComponent<StatsTableHighScoreRow>();
            if (row == null)
            {
                Debug.LogError(
                    "StatsTextMeshProMigration.MigrateHighScoreRow: no StatsTableHighScoreRow component found on "
                        + HighScoreRowPrefabPath);
                return;
            }

            Text[] allTexts = root.GetComponentsInChildren<Text>(true);
            List<Text> texts = new List<Text>();
            List<Text> nestedTexts = new List<Text>();
            MenuTextConversion.PartitionByNestedPrefabInstance(allTexts, root, texts, nestedTexts);

            if (texts.Count == 0 && root.GetComponentsInChildren<TextMeshProUGUI>(true).Length > 0)
            {
                Debug.Log(
                    "StatsTextMeshProMigration.MigrateHighScoreRow: no legacy Text remains in "
                        + HighScoreRowPrefabPath + "; nothing to do.");
                return;
            }

            List<string> errors = new List<string>();
            bool ok = MenuTextConversion.ConvertOwnedTextsAndWireNamedFields(
                root, texts, font, HighScoreRowLabelFields, row, checkSelectableTargetGraphics: false,
                out int convertedCount, out int outlineCompensatedCount, errors);

            if (!ok)
            {
                Debug.LogError(
                    "StatsTextMeshProMigration.MigrateHighScoreRow aborted without saving - " + errors.Count
                        + " error(s):\n- " + string.Join("\n- ", errors));
                return;
            }

            MenuTextConversion.PersistLooseUnderlayMaterials(root);
            PrefabUtility.SaveAsPrefabAsset(root, HighScoreRowPrefabPath);
            Debug.Log(
                "StatsTextMeshProMigration.MigrateHighScoreRow complete: converted " + convertedCount
                    + " Text component(s) (" + HighScoreRowLabelFields.Length
                    + " wired into StatsTableHighScoreRow), compensated " + outlineCompensatedCount
                    + " legacy Outline effect(s) via TMP underlay material.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectStatsTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AUD-092 Phase 2 permanent regression guard, mirroring
    /// <see cref="MenuTextMeshProMigration.CollectContractErrors"/>'s shape: zero legacy Text remain in
    /// either prefab, every TextMeshProUGUI has a font asset, every StatsUiObjects/StatsTableHighScoreRow
    /// reference this migration wired is non-null, no Selectable has a null targetGraphic, and no
    /// property modification in <see cref="ScenePath"/> targets a legacy Text.
    /// </summary>
    public static List<string> CollectContractErrors()
    {
        List<string> errors = new List<string>();
        CollectStatsManagerContractErrors(errors);
        CollectHighScoreRowContractErrors(errors);
        MenuTextConversion.CollectDanglingSceneTextOverrides(ScenePath, StatsManagerPrefabPath, errors);
        return errors;
    }

    private static void CollectStatsManagerContractErrors(List<string> errors)
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(StatsManagerPrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(StatsManagerPrefabPath + " : could not load StatsManager prefab asset.");
            return;
        }

        List<Text> ownedLegacyTexts = new List<Text>();
        List<Text> nestedLegacyTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedLegacyTexts, nestedLegacyTexts);
        if (ownedLegacyTexts.Count > 0)
        {
            errors.Add(
                StatsManagerPrefabPath + " : " + ownedLegacyTexts.Count
                    + " legacy Text component(s) directly owned by this prefab remain (expected 0).");
        }

        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null)
            {
                errors.Add(
                    StatsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, prefabRoot)
                        + " : TextMeshProUGUI has no font asset.");
            }
        }

        foreach (Selectable selectable in prefabRoot.GetComponentsInChildren<Selectable>(true))
        {
            if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, prefabRoot))
            {
                continue;
            }

            if (selectable.targetGraphic == null)
            {
                errors.Add(
                    StatsManagerPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(selectable.gameObject, prefabRoot)
                        + " : " + selectable.GetType().Name + " has a null targetGraphic.");
            }
        }

        StatsUiObjects ui = prefabRoot.GetComponentInChildren<StatsUiObjects>(true);
        if (ui == null)
        {
            errors.Add(StatsManagerPrefabPath + " : no StatsUiObjects component found.");
            return;
        }

        List<string> missing = new List<string>();
        ui.Validate(missing);
        foreach (string field in missing)
        {
            errors.Add(StatsManagerPrefabPath + " -> " + field + " is not wired.");
        }
    }

    private static void CollectHighScoreRowContractErrors(List<string> errors)
    {
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(HighScoreRowPrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(HighScoreRowPrefabPath + " : could not load highScoreRow prefab asset.");
            return;
        }

        Text[] legacyTexts = prefabRoot.GetComponentsInChildren<Text>(true);
        if (legacyTexts.Length > 0)
        {
            errors.Add(
                HighScoreRowPrefabPath + " : " + legacyTexts.Length + " legacy Text component(s) remain (expected 0).");
        }

        foreach (TextMeshProUGUI tmp in prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.font == null)
            {
                errors.Add(
                    HighScoreRowPrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, prefabRoot)
                        + " : TextMeshProUGUI has no font asset.");
            }
        }

        StatsTableHighScoreRow row = prefabRoot.GetComponent<StatsTableHighScoreRow>();
        if (row == null)
        {
            errors.Add(HighScoreRowPrefabPath + " : no StatsTableHighScoreRow component found.");
            return;
        }

        if (row.userNameLabel == null) errors.Add(HighScoreRowPrefabPath + " -> userNameLabel is not wired.");
        if (row.scoreLabel == null) errors.Add(HighScoreRowPrefabPath + " -> scoreLabel is not wired.");
        if (row.characterLabel == null) errors.Add(HighScoreRowPrefabPath + " -> characterLabel is not wired.");
        if (row.levelLabel == null) errors.Add(HighScoreRowPrefabPath + " -> levelLabel is not wired.");
        if (row.dateLabel == null) errors.Add(HighScoreRowPrefabPath + " -> dateLabel is not wired.");
        if (row.hardcoreLabel == null) errors.Add(HighScoreRowPrefabPath + " -> hardcoreLabel is not wired.");
    }
}
