using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phases 6A/6B/6C (all complete): migrated the Start menu's (<c>level_00_start</c>) entire
/// legacy <see cref="Text"/> contract to TextMeshProUGUI - Phase 6A the 27 runtime-mutated fields
/// <see cref="StartMenuUiObjects"/> used to carry (now owned by <see cref="StartMenuTextUiObjects"/>),
/// Phase 6B the other 14 static/unbound fields plus every other directly scene-owned Text nothing ever
/// pointed at through a serialized field, Phase 6C the last nested/shared source
/// (<c>confirm_tip.prefab</c>, migrated by <see cref="TipDialogueTextMeshProMigration"/>). Phase 6A/6B
/// converted their candidates in place via <see cref="MenuTextConversion.ConvertSingleText"/>, reverted
/// every now-redundant legacy prefab-instance property override via
/// <see cref="PrefabUtility.RevertPropertyOverride"/>, and were proven idempotent before
/// <see cref="StartMenuUiObjects"/>' 41-field legacy Text schema was removed from source entirely.
///
/// The one-shot migration methods themselves (<c>Migrate</c>/<c>MigratePhase6B</c> and their supporting
/// prefab-asset/scene-in-memory helpers) were deleted once both phases shipped and the schema was gone:
/// re-running either against a <see cref="StartMenuUiObjects"/> that no longer has the fields they
/// resolved by name would only ever report "no such field" and abort - a permanently broken menu item
/// is worse than no menu item, since there is no legitimate scenario where either needs to run again.
/// What remains is permanent: <see cref="Report"/> (read-only characterization) and
/// <see cref="CollectContractErrors"/> (the permanent regression contract backing
/// <c>Level5ProjectValidator.CollectStartTextRenderingContractErrors</c>), plus the field-name data both
/// still depend on. With Phase 6C complete, <see cref="CollectContractErrors"/> now requires zero legacy
/// Text anywhere in the Start scene, including nested prefab instances - see
/// <see cref="CollectDirectAndNestedTextErrors"/>.
/// </summary>
internal static class StartMenuTextMeshProMigration
{
    internal const string ScenePath = "Assets/Scenes/level_00_start.unity";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    /// <summary>
    /// The permanent runtime-mutated dynamic text contract (AUD-092 Phase 6A characterization,
    /// confirmed against dev HEAD d2673847b by tracing every <c>.text =</c> write reachable from
    /// StartManager/PlayerSelectView/CpuSlotBinding back to its StartMenuUiObjects field). Exactly 27
    /// of StartMenuUiObjects' 41 legacy Text fields. <see cref="StartMenuTextUiObjects"/>' fields share
    /// these exact names, so one array drives both the legacy-side and new-side SerializedProperty
    /// lookups below. Internal (not private) so both <see cref="StartMenuTextMeshProMigrationTests"/>
    /// and <see cref="StaticFieldNames"/>'s derivation of the full 41-field set below reference this
    /// single source of truth instead of each keeping their own copy.
    /// </summary>
    internal static readonly string[] DynamicFieldNames =
    {
        "header_username",
        "header_version",
        "header_latestVersion",
        "column1_subgroup_column2_num_players_selected_name_text",
        "column1_subgroup_column2_player_select_name_text",
        "column1_subgroup_column2_friend_selected_name_text",
        "column1_subgroup_column2_level_selected_name_text",
        "column1_subgroup_column2_mode_selected_name_text",
        "column2_level_tab_level_selected_name",
        "column2_level_tab_level_selected_info",
        "column2_mode_tab_mode_selected_name",
        "column2_mode_tab_mode_selected_description",
        "column2_options_tab_traffic_select_option_text",
        "column2_options_tab_hardcore_select_option_text",
        "column2_options_tab_enemy_select_option_text",
        "column2_options_tab_sniper_select_option_text",
        "column2_options_tab_difficulty_select_option_text",
        "column2_options_tab_difficulty_select_description_text",
        "column2_options_tab_obstacle_select_option_text",
        "column3_player_selected_stats_numbers_text",
        "column3_player_selected_progression_stats_text",
        "column3_player_selected_progression_update_points_text",
        "column3_friend_selected_stats_numbers_text",
        "column4_cpu_selected_stats_numbers_text",
        "column4_cpu1_name_text",
        "column4_cpu2_name_text",
        "column4_cpu3_name_text",
    };

    /// <summary>
    /// The 14 of StartMenuUiObjects' 41 legacy Text fields that stay legacy Text for Phase 6B (static
    /// labels/unbound - never written to by StartManager/PlayerSelectView/CpuSlotBinding). Internal so
    /// <see cref="StartMenuTextMeshProMigrationTests"/> shares this same list rather than keeping an
    /// independent copy that could silently drift from it.
    /// </summary>
    internal static readonly string[] StaticFieldNames =
    {
        "column1_subgroup_column2_cpu_selected_name_text",
        "column1_subgroup_column2_options_selected_name_text",
        "column2_options_tab_traffic_select_text",
        "column2_options_tab_hardcore_select_text",
        "column2_options_tab_enemy_select_text",
        "column2_options_tab_sniper_select_text",
        "column2_options_tab_obstacles_select_text",
        "column2_options_tab_difficulty_select_text",
        "column3_player_selected_stats_category_text",
        "column3_player_selected_progression_text",
        "column3_friend_selected_stats_category_text",
        "column3_level_selected_name_text",
        "column3_level_selected_description_text",
        "column4_cpu_selected_stats_category_text",
    };

    // ---------------------------------------------------------------------------------------------
    // Characterization report (read-only)
    // ---------------------------------------------------------------------------------------------

    [MenuItem("Level5/Report Start Menu Text Migration")]
    public static void Report()
    {
        MenuTextConversion.WithOpenScene(ScenePath, scene =>
        {
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("==== " + ScenePath + " ====");

            List<Text> owned = new List<Text>();
            List<Text> nested = new List<Text>();
            PartitionOwnedTexts(scene, owned, nested);
            summary.AppendLine("total legacy Text (scene-wide): " + (owned.Count + nested.Count));
            summary.AppendLine("direct scene-owned legacy Text: " + owned.Count);
            summary.AppendLine("prefab-owned/nested legacy Text: " + nested.Count);

            List<string> errors = new List<string>();
            StartMenuUiObjects legacyUi = FindSingleComponent<StartMenuUiObjects>(scene, errors, ScenePath);
            if (legacyUi == null)
            {
                summary.AppendLine("StartMenuUiObjects not found: " + string.Join("; ", errors));
                Debug.Log(summary.ToString());
                return true;
            }

            SerializedObject serializedLegacyUi = new SerializedObject(legacyUi);
            int textFieldCount = 0;
            int textFieldNonNull = 0;
            foreach (string fieldName in AllLegacyTextFieldNames())
            {
                textFieldCount++;
                SerializedProperty property = serializedLegacyUi.FindProperty(fieldName);
                if (property != null && property.objectReferenceValue != null)
                {
                    textFieldNonNull++;
                }
            }

            // AUD-092 Phase 6B removed all 41 of these from StartMenuUiObjects' source; FindProperty
            // resolves none of them any more; "41"/"0" below reflect that removal, not a live scan.
            summary.AppendLine("StartMenuUiObjects former legacy Text field names (removed by Phase 6B): " + textFieldCount);
            summary.AppendLine("non-null StartMenuUiObjects scene bindings: " + textFieldNonNull);
            summary.AppendLine("runtime-mutated bindings (Phase 6A candidates): " + DynamicFieldNames.Length);
            summary.AppendLine("static/view-only bindings (Phase 6B): " + (textFieldCount - DynamicFieldNames.Length));

            HashSet<Text> distinctCandidates = new HashSet<Text>();
            foreach (string fieldName in DynamicFieldNames)
            {
                SerializedProperty property = serializedLegacyUi.FindProperty(fieldName);
                Text text = property != null ? property.objectReferenceValue as Text : null;
                string ownership = text == null
                    ? "NULL"
                    : PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) == null
                        ? "direct scene-owned"
                        : "NESTED/SHARED PREFAB";
                summary.AppendLine("  " + fieldName + " -> " + (text != null ? MenuTextConversion.BuildHierarchyPath(text.gameObject, null) : "<null>") + " [" + ownership + "]");
                if (text != null)
                {
                    distinctCandidates.Add(text);
                }
            }

            summary.AppendLine("distinct Text components behind the runtime bindings: " + distinctCandidates.Count);

            int targetGraphicConsumers = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                {
                    if (selectable.targetGraphic != null && distinctCandidates.Contains(selectable.targetGraphic as Text))
                    {
                        targetGraphicConsumers++;
                    }
                }
            }

            summary.AppendLine("Selectable.targetGraphic consumers of the 27: " + targetGraphicConsumers);

            HashSet<Object> textSet = new HashSet<Object>(distinctCandidates);
            List<string> unsupported = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == legacyUi.gameObject)
                {
                    continue; // StartMenuUiObjects' own 41 fields are the known, already-captured consumer
                }

                MenuTextConversion.CollectUnsupportedConsumers(root, textSet, unsupported);
            }

            summary.AppendLine("other serialized consumers (excluding StartMenuUiObjects itself): " + unsupported.Count);
            foreach (string finding in unsupported)
            {
                summary.AppendLine("  UNSUPPORTED CONSUMER: " + finding);
            }

            int existingTmp = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                existingTmp += root.GetComponentsInChildren<TextMeshProUGUI>(true).Length;
            }

            summary.AppendLine("existing TextMeshProUGUI (scene-wide): " + existingTmp);

            Debug.Log(summary.ToString());
            return true;
        });
    }

    /// <summary>
    /// The full 41 - every legacy Text field on StartMenuUiObjects - derived from
    /// <see cref="DynamicFieldNames"/> (27) and <see cref="StaticFieldNames"/> (14) rather than its own
    /// independent list, so the "41" the Report/doc comments quote can never silently drift from the
    /// two lists that actually define it.
    /// </summary>
    private static IEnumerable<string> AllLegacyTextFieldNames()
    {
        foreach (string fieldName in DynamicFieldNames)
        {
            yield return fieldName;
        }

        foreach (string fieldName in StaticFieldNames)
        {
            yield return fieldName;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Scene-wide helpers
    // ---------------------------------------------------------------------------------------------

    private static void PartitionOwnedTexts(Scene scene, List<Text> owned, List<Text> nested)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) != null)
                {
                    nested.Add(text);
                }
                else
                {
                    owned.Add(text);
                }
            }
        }
    }

    private static T FindSingleComponent<T>(Scene scene, List<string> errors, string scenePath) where T : Component
    {
        List<T> found = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            found.AddRange(root.GetComponentsInChildren<T>(true));
        }

        if (found.Count != 1)
        {
            errors.Add(scenePath + " : expected exactly 1 " + typeof(T).Name + ", found " + found.Count + ".");
            return null;
        }

        return found[0];
    }

    private static List<string> CollectNullTargetGraphicErrors(Scene scene)
    {
        List<string> errors = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(selectable.gameObject) != null)
                {
                    continue;
                }

                if (selectable.targetGraphic == null)
                {
                    errors.Add(MenuTextConversion.BuildHierarchyPath(selectable.gameObject, null) + " : " + selectable.GetType().Name + " has a null targetGraphic after migration.");
                }
            }
        }

        return errors;
    }

    // ---------------------------------------------------------------------------------------------
    // Scene transaction safety: shared with AccountTextMeshProMigration via
    // MenuTextConversion.RunSceneMigration/WithOpenScene/LogAbort (AUD-092 Phase 6A extracted the
    // mechanics there once a second migration needed the identical sequence).
    // ---------------------------------------------------------------------------------------------

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectStartTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    public static List<string> CollectContractErrors()
    {
        List<string> errors = new List<string>();
        errors.AddRange(CollectLegacyFieldSchemaErrors());

        MenuTextConversion.WithOpenScene(ScenePath, scene =>
        {
            StartMenuUiObjects legacyUi = FindSingleComponent<StartMenuUiObjects>(scene, errors, ScenePath);
            if (legacyUi == null)
            {
                return true;
            }

            StartMenuTextUiObjects textUi = legacyUi.TextUi;
            if (textUi == null)
            {
                errors.Add(ScenePath + " : StartMenuUiObjects.TextUi is not resolved.");
                return true;
            }

            errors.AddRange(CollectDynamicBindingErrors(textUi));
            errors.AddRange(CollectLeftoverLegacyOverrideErrors(legacyUi));
            errors.AddRange(CollectDirectAndNestedTextErrors(scene));
            errors.AddRange(CollectNullTargetGraphicErrors(scene));
            return true;
        });

        return errors;
    }

    /// <summary>
    /// StartMenuUiObjects must carry no field assignable to legacy <see cref="Text"/> at all - the
    /// Phase 6B schema change is a source-level guarantee, but this reflection check gives it a runtime
    /// regression signal too (e.g. against a stale compiled assembly, or a future reintroduction).
    /// </summary>
    private static List<string> CollectLegacyFieldSchemaErrors()
    {
        List<string> errors = new List<string>();
        foreach (FieldInfo field in typeof(StartMenuUiObjects).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (typeof(Text).IsAssignableFrom(field.FieldType))
            {
                errors.Add("StartMenuUiObjects." + field.Name + " is still typed as legacy UnityEngine.UI.Text; the Phase 6B schema removes every legacy Text field.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Every one of the 27 runtime-mutated dynamic bindings that is resolved must be a TextMeshProUGUI
    /// on the shared Neon Pixel-7 SDF font asset. Delegates the "is every REQUIRED binding resolved at
    /// all" question to <see cref="StartMenuTextUiObjects.Validate"/>, the single source of truth for
    /// that contract; this only adds the type/font check on top of whatever Validate found present.
    /// </summary>
    private static List<string> CollectDynamicBindingErrors(StartMenuTextUiObjects textUi)
    {
        List<string> errors = new List<string>();
        List<string> missing = new List<string>();
        textUi.Validate(missing);
        foreach (string field in missing)
        {
            errors.Add(ScenePath + " : " + field + " is not resolved.");
        }

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        SerializedObject serializedTextUi = new SerializedObject(textUi);
        foreach (string fieldName in DynamicFieldNames)
        {
            SerializedProperty newProperty = serializedTextUi.FindProperty(fieldName);
            Object newValue = newProperty != null ? newProperty.objectReferenceValue : null;
            if (newValue == null)
            {
                continue;
            }

            if (!(newValue is TextMeshProUGUI tmp))
            {
                errors.Add(ScenePath + " : StartMenuTextUiObjects." + fieldName + " is not a TextMeshProUGUI.");
            }
            else if (neonPixel == null || tmp.font != neonPixel)
            {
                errors.Add(ScenePath + " : StartMenuTextUiObjects." + fieldName + " does not use the shared Neon Pixel-7 SDF font asset.");
            }
        }

        return errors;
    }

    /// <summary>
    /// None of the 41 former StartMenuUiObjects legacy Text field names (27 dynamic + 14 static) may
    /// survive as a leftover prefab-instance property modification. Checked by literal property-path
    /// string rather than <see cref="SerializedObject.FindProperty"/>, since the C# fields themselves no
    /// longer exist on the class for a property to resolve against.
    /// </summary>
    private static List<string> CollectLeftoverLegacyOverrideErrors(StartMenuUiObjects legacyUi)
    {
        List<string> errors = new List<string>();
        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(legacyUi.gameObject);
        if (instanceRoot == null)
        {
            return errors;
        }

        HashSet<string> allLegacyFieldNames = new HashSet<string>(DynamicFieldNames);
        allLegacyFieldNames.UnionWith(StaticFieldNames);
        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot) ?? Array.Empty<PropertyModification>();
        foreach (PropertyModification modification in modifications)
        {
            if (modification.target == legacyUi && allLegacyFieldNames.Contains(modification.propertyPath))
            {
                errors.Add(
                    ScenePath + " : leftover StartMenuUiObjects." + modification.propertyPath
                        + " scene override remains (must be reverted to the prefab's null default).");
            }
        }

        return errors;
    }

    /// <summary>
    /// Zero legacy Text may remain anywhere in the Start scene - directly scene-owned or nested inside
    /// any prefab instance. AUD-092 Phase 6C migrated confirm_tip.prefab, the only remaining nested
    /// source; a new nested legacy Text source appearing in the Start scene is a regression, not
    /// deferred backlog.
    /// </summary>
    private static List<string> CollectDirectAndNestedTextErrors(Scene scene)
    {
        List<string> errors = new List<string>();
        List<Text> owned = new List<Text>();
        List<Text> nested = new List<Text>();
        PartitionOwnedTexts(scene, owned, nested);
        foreach (Text text in owned)
        {
            errors.Add(MenuTextConversion.BuildHierarchyPath(text.gameObject, null) + " : directly scene-owned legacy Text survives Phase 6B migration.");
        }

        foreach (Text text in nested)
        {
            GameObject nestedInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject);
            string sourcePrefabPath = nestedInstanceRoot != null ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nestedInstanceRoot) : null;
            errors.Add(
                MenuTextConversion.BuildHierarchyPath(text.gameObject, null)
                    + " : nested legacy Text originates from '" + (sourcePrefabPath ?? "<unknown>")
                    + "'; AUD-092 Phase 6C requires zero legacy Text anywhere in the Start scene, including nested prefab instances.");
        }

        return errors;
    }
}
