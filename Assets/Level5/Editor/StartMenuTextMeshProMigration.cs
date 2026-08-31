using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 6A: migrates the Start menu's (<c>level_00_start</c>) runtime-mutated text contract
/// from legacy <see cref="Text"/> to TextMeshProUGUI, without touching the other 14 of
/// <see cref="StartMenuUiObjects"/>' 41 legacy Text fields (static labels/unbound - Phase 6B).
///
/// Unlike every earlier AUD-092 phase, the Start scene's dynamic Text is neither directly scene-owned
/// under one Text-holding root (Account) nor prefab-owned (Options/Stats/Progression/Credits): every
/// one of StartMenuUiObjects' 41 fields is authored null on <c>StartMenuUiObjects.prefab</c> - which
/// has no children at all - and wired entirely through <c>level_00_start.unity</c>'s prefab-instance
/// property modifications, each pointing at a Text component that is itself directly scene-owned
/// (confirmed against dev HEAD d2673847b: every one of the 27 dynamic candidates' backing Text has
/// <c>m_PrefabInstance: {fileID: 0}</c>). Migration therefore:
///
/// 1. adds the permanent <see cref="StartMenuTextUiObjects"/> view component to the SAME prefab
///    GameObject StartMenuUiObjects lives on (its own one-time, idempotent prefab-asset edit), wired
///    via <see cref="StartMenuUiObjects.TextUi"/> rather than a second singleton;
/// 2. opens the scene, resolves each of the 27 dynamic fields' CURRENT Text by reading the legacy
///    StartMenuUiObjects field's own serialized value (never by GameObject name);
/// 3. converts that Text in place via <see cref="MenuTextConversion.ConvertSingleText"/>, wires the
///    result into StartMenuTextUiObjects' identically-named field on the same scene instance, then
///    reverts the now-redundant legacy field override via
///    <see cref="PrefabUtility.RevertPropertyOverride"/> - never an explicit null override, since the
///    prefab default is already null.
///
/// Reuses every mechanic <see cref="MenuTextConversion"/> already proved; this class contributes only
/// the Start-specific field mapping, the prefab-instance-field-driven boundary resolution, and the
/// permanent per-field contract.
/// </summary>
internal static class StartMenuTextMeshProMigration
{
    internal const string ScenePath = "Assets/Scenes/level_00_start.unity";
    internal const string StartMenuUiObjectsPrefabPath = "Assets/Resources/Prefabs/menu_start/StartMenuUiObjects.prefab";
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

            summary.AppendLine("StartMenuUiObjects legacy Text fields: " + textFieldCount);
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
    // Migration entry point
    // ---------------------------------------------------------------------------------------------

    internal const string StartManagerPrefabPath = "Assets/Resources/Prefabs/menu_start/start_manager_test.prefab";

    [MenuItem("Level5/Migrate Start Menu Text To TMP")]
    public static void Migrate()
    {
        const string LogPrefix = "StartMenuTextMeshProMigration.Migrate";

        if (!EnsurePrefabHasTextUiComponent(out List<string> prefabErrors))
        {
            MenuTextConversion.LogAbort(LogPrefix, prefabErrors);
            return;
        }

        MenuTextConversion.RunSceneMigration(ScenePath, LogPrefix, MigrateSceneInMemory);
        ReserializeStartManagerPrefab();
    }

    /// <summary>
    /// AUD-092 Phase 6A section 12: <c>start_manager_test.prefab</c> (the real production StartManager
    /// prefab instanced into <c>level_00_start.unity</c>, despite its name) still carried a serialized
    /// <c>friendSelectUnlockText</c> entry for the dead field removed from StartManager in this same
    /// change, plus entries for the seven traffic/hardcore/enemy/sniper/difficulty/obstacle Text fields
    /// whose <c>[SerializeField]</c> this change also removed (GetUiObjectReferences always overwrites
    /// them from StartMenuUiObjects.instance.TextUi, so an authored value was always dead weight).
    /// Loading and re-saving the prefab asset reserializes it against StartManager's current field set,
    /// dropping every orphaned key automatically - idempotent, and safe since neither the prefab nor
    /// this method touches m_Modifications (start_manager_test.prefab is not a PrefabInstance).
    /// </summary>
    private static void ReserializeStartManagerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(StartManagerPrefabPath);
        try
        {
            PrefabUtility.SaveAsPrefabAsset(root, StartManagerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// One-time, idempotent prefab-asset edit: adds <see cref="StartMenuTextUiObjects"/> to the same
    /// GameObject <see cref="StartMenuUiObjects"/> lives on and wires <c>textUi</c> to it. Runs before
    /// the scene is even opened, so every scene instance already carries the (null-by-default) new
    /// component by the time the scene pass resolves it.
    /// </summary>
    private static bool EnsurePrefabHasTextUiComponent(out List<string> errors)
    {
        errors = new List<string>();
        GameObject root = PrefabUtility.LoadPrefabContents(StartMenuUiObjectsPrefabPath);
        try
        {
            StartMenuUiObjects legacyUi = root.GetComponent<StartMenuUiObjects>();
            if (legacyUi == null)
            {
                errors.Add(StartMenuUiObjectsPrefabPath + " : no StartMenuUiObjects component found.");
                return false;
            }

            StartMenuTextUiObjects textUi = root.GetComponent<StartMenuTextUiObjects>();
            bool dirty = textUi == null;
            if (textUi == null)
            {
                textUi = root.AddComponent<StartMenuTextUiObjects>();
            }

            SerializedObject serializedLegacyUi = new SerializedObject(legacyUi);
            SerializedProperty textUiProperty = serializedLegacyUi.FindProperty("textUi");
            if (textUiProperty == null)
            {
                errors.Add(StartMenuUiObjectsPrefabPath + " : StartMenuUiObjects has no serialized field named 'textUi'.");
                return false;
            }

            if (textUiProperty.objectReferenceValue != textUi)
            {
                textUiProperty.objectReferenceValue = textUi;
                serializedLegacyUi.ApplyModifiedProperties();
                dirty = true;
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, StartMenuUiObjectsPrefabPath);
                Debug.Log("StartMenuTextMeshProMigration: added/wired StartMenuTextUiObjects on " + StartMenuUiObjectsPrefabPath + ".");
            }

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static List<string> MigrateSceneInMemory(Scene scene)
    {
        List<string> errors = new List<string>();

        StartMenuUiObjects legacyUi = FindSingleComponent<StartMenuUiObjects>(scene, errors, ScenePath);
        if (legacyUi == null)
        {
            return errors;
        }

        StartMenuTextUiObjects textUi = legacyUi.TextUi;
        if (textUi == null)
        {
            errors.Add(ScenePath + " : StartMenuUiObjects.TextUi is null - the prefab-asset step did not take effect.");
            return errors;
        }

        SerializedObject serializedLegacyUi = new SerializedObject(legacyUi);
        SerializedObject serializedTextUi = new SerializedObject(textUi);

        // Resolve every field's current state by its OWN serialized value - never by GameObject name -
        // and classify each of the 27 as needing conversion, already migrated, or an error. Grouping by
        // Text identity (not by field) means a Text two fields both happen to reference converts once.
        Dictionary<Text, List<string>> fieldsByText = new Dictionary<Text, List<string>>();
        foreach (string fieldName in DynamicFieldNames)
        {
            SerializedProperty legacyProperty = serializedLegacyUi.FindProperty(fieldName);
            SerializedProperty newProperty = serializedTextUi.FindProperty(fieldName);
            if (legacyProperty == null)
            {
                errors.Add("StartMenuUiObjects has no field named '" + fieldName + "'.");
                continue;
            }

            if (newProperty == null)
            {
                errors.Add("StartMenuTextUiObjects has no field named '" + fieldName + "'.");
                continue;
            }

            Text legacyText = legacyProperty.objectReferenceValue as Text;
            Object newValue = newProperty.objectReferenceValue;

            if (legacyText == null && newValue == null)
            {
                errors.Add(ScenePath + " : StartMenuUiObjects." + fieldName + " is unresolved (neither the legacy Text nor the migrated TMP field is set).");
                continue;
            }

            if (legacyText != null && newValue != null)
            {
                errors.Add(ScenePath + " : StartMenuUiObjects." + fieldName + " and StartMenuTextUiObjects." + fieldName + " are both set - ambiguous migration state.");
                continue;
            }

            if (legacyText == null)
            {
                continue; // already migrated on a prior run
            }

            if (!fieldsByText.TryGetValue(legacyText, out List<string> fields))
            {
                fields = new List<string>();
                fieldsByText[legacyText] = fields;
            }

            fields.Add(fieldName);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (fieldsByText.Count == 0)
        {
            Debug.Log("StartMenuTextMeshProMigration.Migrate: no directly-owned eligible legacy Text remains in " + ScenePath + "; nothing to do.");
            return errors; // idempotent no-op
        }

        // Ownership gate (AUD-092 Phase 6A section 8): every candidate must be direct scene-owned.
        HashSet<Text> candidateTexts = new HashSet<Text>(fieldsByText.Keys);
        foreach (Text text in candidateTexts)
        {
            if (PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) != null)
            {
                errors.Add(
                    MenuTextConversion.BuildHierarchyPath(text.gameObject, null)
                        + " : belongs to a nested/shared prefab instance; Phase 6A only supports direct scene-owned Text.");
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        // Unsupported-consumer gate: scan every scene root except StartMenuUiObjects' own (its 41
        // fields are the known, already-captured consumer resolved field-by-field above) for any other
        // serialized reference into the candidate set.
        HashSet<Object> textSet = new HashSet<Object>(candidateTexts);
        List<string> unsupported = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root == legacyUi.gameObject)
            {
                continue;
            }

            MenuTextConversion.CollectUnsupportedConsumers(root, textSet, unsupported);
        }

        if (unsupported.Count > 0)
        {
            errors.AddRange(unsupported);
            return errors;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            errors.Add("could not create/load the Neon Pixel-7 SDF font asset.");
            return errors;
        }

        // Several of the 27 candidates share the same root (e.g. multiple Texts under
        // .../column2/options_tab/) - resolving each distinct root's Selectable list once and reusing
        // it across every Text under that root avoids ConvertSingleText redundantly re-walking the same
        // subtree once per sibling Text.
        HashSet<GameObject> convertedRoots = new HashSet<GameObject>();
        Dictionary<Text, TextMeshProUGUI> converted = new Dictionary<Text, TextMeshProUGUI>();
        Dictionary<GameObject, List<Selectable>> selectablesByRoot = new Dictionary<GameObject, List<Selectable>>();
        foreach (Text text in candidateTexts)
        {
            GameObject textScopeRoot = text.gameObject.transform.root.gameObject;
            if (!selectablesByRoot.TryGetValue(textScopeRoot, out List<Selectable> selectables))
            {
                selectables = new List<Selectable>(textScopeRoot.GetComponentsInChildren<Selectable>(true));
                selectablesByRoot[textScopeRoot] = selectables;
            }

            TextMeshProUGUI tmp = MenuTextConversion.ConvertSingleText(textScopeRoot, text, font, selectables);
            if (tmp == null)
            {
                errors.Add(MenuTextConversion.BuildHierarchyPath(text.gameObject, null) + " : conversion failed to add TextMeshProUGUI.");
                continue;
            }

            converted[text] = tmp;
            convertedRoots.Add(textScopeRoot);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        foreach (KeyValuePair<Text, List<string>> pair in fieldsByText)
        {
            if (!converted.TryGetValue(pair.Key, out TextMeshProUGUI tmp))
            {
                errors.Add(MenuTextConversion.BuildHierarchyPath(pair.Key.gameObject, null) + " : no converted TextMeshProUGUI was resolved for this Text.");
                continue;
            }

            foreach (string fieldName in pair.Value)
            {
                SerializedProperty newProperty = serializedTextUi.FindProperty(fieldName);
                newProperty.objectReferenceValue = tmp;

                SerializedProperty legacyProperty = serializedLegacyUi.FindProperty(fieldName);
                PrefabUtility.RevertPropertyOverride(legacyProperty, InteractionMode.AutomatedAction);
                if (legacyProperty.objectReferenceValue != null)
                {
                    errors.Add(
                        "StartMenuUiObjects." + fieldName
                            + " : RevertPropertyOverride did not clear the legacy scene override back to the prefab's null default.");
                }
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        serializedTextUi.ApplyModifiedProperties();

        foreach (GameObject root in convertedRoots)
        {
            MenuTextConversion.PersistLooseUnderlayMaterials(root);
        }

        errors.AddRange(CollectNullTargetGraphicErrors(scene));
        return errors;
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

            List<string> missing = new List<string>();
            textUi.Validate(missing);
            foreach (string field in missing)
            {
                errors.Add(ScenePath + " : " + field + " is not resolved.");
            }

            TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
            SerializedObject serializedTextUi = new SerializedObject(textUi);
            SerializedObject serializedLegacyUi = new SerializedObject(legacyUi);
            foreach (string fieldName in DynamicFieldNames)
            {
                SerializedProperty newProperty = serializedTextUi.FindProperty(fieldName);
                Object newValue = newProperty != null ? newProperty.objectReferenceValue : null;
                if (newValue != null)
                {
                    if (!(newValue is TextMeshProUGUI tmp))
                    {
                        errors.Add(ScenePath + " : StartMenuTextUiObjects." + fieldName + " is not a TextMeshProUGUI.");
                    }
                    else if (neonPixel == null || tmp.font != neonPixel)
                    {
                        errors.Add(ScenePath + " : StartMenuTextUiObjects." + fieldName + " does not use the shared Neon Pixel-7 SDF font asset.");
                    }
                }

                SerializedProperty legacyProperty = serializedLegacyUi.FindProperty(fieldName);
                if (legacyProperty != null && legacyProperty.objectReferenceValue != null)
                {
                    errors.Add(ScenePath + " : StartMenuUiObjects." + fieldName + " still resolves a legacy Text; it must remain null now that StartMenuTextUiObjects owns this binding.");
                }
            }

            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(legacyUi.gameObject);
            if (instanceRoot != null)
            {
                PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot) ?? Array.Empty<PropertyModification>();
                foreach (PropertyModification modification in modifications)
                {
                    if (modification.target == legacyUi && Array.IndexOf(DynamicFieldNames, modification.propertyPath) >= 0)
                    {
                        errors.Add(
                            ScenePath + " : leftover StartMenuUiObjects." + modification.propertyPath
                                + " scene override remains (must be reverted to the prefab's null default).");
                    }
                }
            }

            errors.AddRange(CollectNullTargetGraphicErrors(scene));
            return true;
        });

        return errors;
    }
}
