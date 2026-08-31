using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 6A: the Start menu's (<c>level_00_start</c>) 27 runtime-mutated legacy Text fields
/// were migrated to TextMeshProUGUI via the new <see cref="StartMenuTextUiObjects"/> view. Mirrors
/// <c>AccountTextMeshProMigrationTests</c>' shape: permanent contract tests delegate to
/// <see cref="Level5ProjectValidator"/>/<see cref="StartMenuTextMeshProMigration"/>, the rest open the
/// real scene directly and inspect its already-migrated state. Migration idempotence (a second run is
/// a byte-identical no-op) and editor scene-setup restoration were verified manually via Unity
/// batchmode against the real project, not re-exercised here - see the Phase 6A final report.
/// </summary>
public class StartMenuTextMeshProMigrationTests
{
    private const string ScenePath = "Assets/Scenes/level_00_start.unity";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    // Both lists are read directly from StartMenuTextMeshProMigration rather than re-declared here -
    // a hand-copied duplicate could silently drift from the migration's own list and under-test.
    private static readonly string[] DynamicFieldNames = StartMenuTextMeshProMigration.DynamicFieldNames;
    private static readonly string[] StaticFieldNames = StartMenuTextMeshProMigration.StaticFieldNames;

    private readonly List<Scene> _openedByThisTest = new List<Scene>();

    [TearDown]
    public void TearDown()
    {
        foreach (Scene scene in _openedByThisTest)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        _openedByThisTest.Clear();
    }

    private Scene OpenScene()
    {
        Scene existing = SceneManager.GetSceneByPath(ScenePath);
        if (existing.IsValid() && existing.isLoaded)
        {
            return existing;
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        _openedByThisTest.Add(scene);
        return scene;
    }

    private static StartMenuUiObjects FindStartMenuUiObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            StartMenuUiObjects found = root.GetComponentInChildren<StartMenuUiObjects>(true);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------------------------------------
    // 1/6/17. Permanent contract; targetGraphics; Options/Stats/Progression/Credits/Account stay green
    // is covered by the rest of the EditMode suite passing alongside this file.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void StartTextRenderingContractHasNoErrors()
    {
        List<string> errors = Level5ProjectValidator.CollectStartTextRenderingContractErrors();
        Assert.IsEmpty(errors, "Start text rendering contract errors:\n- " + string.Join("\n- ", errors));
    }

    // ---------------------------------------------------------------------------------------------
    // 5/6. StartMenuTextUiObjects exists exactly once; every required dynamic binding resolves.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void StartMenuTextUiObjectsExistsExactlyOnce()
    {
        Scene scene = OpenScene();
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            count += root.GetComponentsInChildren<StartMenuTextUiObjects>(true).Length;
        }

        Assert.AreEqual(1, count, "expected exactly one StartMenuTextUiObjects in " + ScenePath);
    }

    [Test]
    public void StartMenuUiObjectsResolvesTextUi()
    {
        Scene scene = OpenScene();
        StartMenuUiObjects ui = FindStartMenuUiObjects(scene);
        Assert.IsNotNull(ui, "no StartMenuUiObjects found in " + ScenePath);
        Assert.IsNotNull(ui.TextUi, "StartMenuUiObjects.TextUi is not resolved");
    }

    [Test]
    public void AllTwentySevenDynamicBindingsResolve()
    {
        Scene scene = OpenScene();
        StartMenuUiObjects ui = FindStartMenuUiObjects(scene);
        Assert.IsNotNull(ui);
        Assert.IsNotNull(ui.TextUi);

        List<string> missing = new List<string>();
        ui.TextUi.Validate(missing);
        Assert.IsEmpty(missing, "unresolved StartMenuTextUiObjects field(s): " + string.Join(", ", missing));
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Every migrated component uses the shared Neon Pixel-7 SDF font asset.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void AllTwentySevenDynamicBindingsAreTextMeshProUGUIOnNeonPixelFont()
    {
        Scene scene = OpenScene();
        StartMenuUiObjects ui = FindStartMenuUiObjects(scene);
        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        Assert.IsNotNull(neonPixel, "could not load " + NeonPixelFontAssetPath);

        SerializedObject serializedTextUi = new SerializedObject(ui.TextUi);
        foreach (string fieldName in DynamicFieldNames)
        {
            SerializedProperty property = serializedTextUi.FindProperty(fieldName);
            Assert.IsNotNull(property, "StartMenuTextUiObjects has no field named '" + fieldName + "'.");
            Object value = property.objectReferenceValue;
            Assert.IsNotNull(value, fieldName + " is null.");
            Assert.IsInstanceOf<TextMeshProUGUI>(value, fieldName + " is not a TextMeshProUGUI.");
            TMP_FontAsset font = ((TextMeshProUGUI)value).font;
            Assert.AreEqual(neonPixel, font, fieldName + " does not use the shared Neon Pixel-7 SDF font asset.");
        }
    }

    [Test]
    public void ExactlyTwentySevenDistinctTextMeshProComponentsBackTheDynamicBindings()
    {
        Scene scene = OpenScene();
        StartMenuUiObjects ui = FindStartMenuUiObjects(scene);
        SerializedObject serializedTextUi = new SerializedObject(ui.TextUi);
        HashSet<Object> distinct = new HashSet<Object>();
        foreach (string fieldName in DynamicFieldNames)
        {
            distinct.Add(serializedTextUi.FindProperty(fieldName).objectReferenceValue);
        }

        Assert.AreEqual(27, distinct.Count, "expected 27 distinct TextMeshProUGUI components behind the 27 dynamic bindings.");
    }

    // ---------------------------------------------------------------------------------------------
    // AUD-092 Phase 6B: the 41-field legacy Text schema (27 dynamic + 14 static) is gone from
    // StartMenuUiObjects entirely - not merely nulled - and no former field name survives as a
    // leftover prefab-instance property modification.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void StartMenuUiObjectsHasNoLegacyTextFields()
    {
        List<string> offenders = new List<string>();
        foreach (FieldInfo field in typeof(StartMenuUiObjects).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (typeof(Text).IsAssignableFrom(field.FieldType))
            {
                offenders.Add(field.Name);
            }
        }

        Assert.IsEmpty(offenders, "StartMenuUiObjects still carries legacy Text field(s): " + string.Join(", ", offenders));
    }

    [Test]
    public void NoLeftoverPrefabInstancePropertyModificationsForFormerLegacyFields()
    {
        Scene scene = OpenScene();
        StartMenuUiObjects ui = FindStartMenuUiObjects(scene);
        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(ui.gameObject);
        Assert.IsNotNull(instanceRoot, "StartMenuUiObjects is expected to be a prefab instance.");

        HashSet<string> allFormerLegacyFieldNames = new HashSet<string>(DynamicFieldNames);
        allFormerLegacyFieldNames.UnionWith(StaticFieldNames);

        PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(instanceRoot) ?? Array.Empty<PropertyModification>();
        foreach (PropertyModification modification in modifications)
        {
            if (modification.target == ui)
            {
                Assert.IsFalse(
                    allFormerLegacyFieldNames.Contains(modification.propertyPath),
                    "leftover StartMenuUiObjects." + modification.propertyPath + " scene override remains.");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 10. Selectable.targetGraphics survived conversion (11 known consumers of the 27).
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void SelectableTargetGraphicsRemainValidAfterMigration()
    {
        Scene scene = OpenScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(selectable.gameObject) != null)
                {
                    continue;
                }

                Assert.IsNotNull(
                    selectable.targetGraphic,
                    MenuTextConversion.BuildHierarchyPath(selectable.gameObject, null) + " has a null targetGraphic after migration.");
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 13/16. StartManager's migrated runtime working fields are TMP_Text; dead fields were not recreated.
    // ---------------------------------------------------------------------------------------------

    private static readonly string[] StartManagerTmpFieldNames =
    {
        "numPlayersSelectOptionText", "levelSelectOptionText", "friendSelectOptionText",
        "modeSelectOptionText", "modeSelectOptionNameText", "ModeSelectOptionDescriptionText",
        "trafficSelectOptionText", "hardcoreSelectOptionText", "enemySelectOptionText",
        "sniperSelectOptionText", "difficultySelectOptionText", "difficultySelectOptionDescriptionText",
        "obstacleSelectOptionText", "versionText", "latestVersionText", "userNameText",
    };

    [Test]
    public void StartManagerMigratedWorkingFieldsAreTmpTextNotLegacyText()
    {
        foreach (string fieldName in StartManagerTmpFieldNames)
        {
            FieldInfo field = typeof(StartManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "StartManager has no field named '" + fieldName + "'.");
            Assert.IsTrue(
                typeof(TMP_Text).IsAssignableFrom(field.FieldType),
                "StartManager." + fieldName + " is " + field.FieldType.Name + ", expected a TMP_Text.");
            Assert.IsFalse(
                typeof(Text).IsAssignableFrom(field.FieldType),
                "StartManager." + fieldName + " is still typed as legacy UnityEngine.UI.Text.");
        }
    }

    [Test]
    public void StartManagerMigratedWorkingFieldsAreNotSerialized()
    {
        // AUD-092 Phase 6A section 11: these seven were always overwritten by GetUiObjectReferences
        // before anything read them, so a serialized authored value was dead weight - removed.
        string[] previouslySerialized =
        {
            "trafficSelectOptionText", "hardcoreSelectOptionText", "enemySelectOptionText",
            "sniperSelectOptionText", "difficultySelectOptionText", "difficultySelectOptionDescriptionText",
            "obstacleSelectOptionText",
        };

        foreach (string fieldName in previouslySerialized)
        {
            FieldInfo field = typeof(StartManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field);
            Assert.IsNull(
                field.GetCustomAttribute<SerializeField>(),
                "StartManager." + fieldName + " should no longer carry [SerializeField] - it is always populated from StartMenuUiObjects.instance.TextUi.");
        }
    }

    [Test]
    public void DeadLegacyFieldsWereRemovedAndNotRecreatedAsTmp()
    {
        Assert.IsNull(typeof(StartManager).GetField("friendSelectUnlockText", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public));
        Assert.IsNull(typeof(StartManager).GetField("levelSelectOptionDescriptionText", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public));
    }

    // ---------------------------------------------------------------------------------------------
    // 14/15. PlayerSelectView/CpuSlotBinding use TMP.
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void PlayerSelectViewConstructorUsesTmpTextNotLegacyText()
    {
        ConstructorInfo constructor = typeof(PlayerSelectView).GetConstructors()[0];
        foreach (ParameterInfo parameter in constructor.GetParameters())
        {
            if (parameter.ParameterType == typeof(Text))
            {
                Assert.Fail("PlayerSelectView constructor parameter '" + parameter.Name + "' is still legacy UnityEngine.UI.Text.");
            }
        }
    }

    [Test]
    public void CpuSlotBindingNameTextIsTmpText()
    {
        PropertyInfo property = typeof(CpuSlotBinding).GetProperty("NameText");
        Assert.IsNotNull(property);
        Assert.AreEqual(typeof(TMP_Text), property.PropertyType);
    }

    // ---------------------------------------------------------------------------------------------
    // AUD-092 Phase 6B/6C: zero legacy Text remains anywhere in the Start scene - directly scene-owned
    // or nested inside any prefab instance (confirm_tip.prefab, the last nested source, was migrated
    // by TipDialogueTextMeshProMigration in Phase 6C).
    // ---------------------------------------------------------------------------------------------

    [Test]
    public void DirectlyScreenOwnedLegacyTextCountIsZero()
    {
        Scene scene = OpenScene();
        List<string> owned = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject) == null)
                {
                    owned.Add(MenuTextConversion.BuildHierarchyPath(text.gameObject, null));
                }
            }
        }

        Assert.IsEmpty(owned, "directly scene-owned legacy Text survives Phase 6B migration:\n- " + string.Join("\n- ", owned));
    }

    [Test]
    public void NoNestedLegacyTextRemainsAnywhereInTheStartScene()
    {
        // AUD-092 Phase 6C migrated confirm_tip.prefab, the only nested/shared source ever allowlisted
        // here; the allowlist itself (StartMenuTextMeshProMigration.Phase6CDeferredSourcePrefabAllowlist)
        // was removed once that migration shipped, so any nested legacy Text found now - from
        // confirm_tip.prefab or anywhere else - is a regression, not deferred backlog.
        Scene scene = OpenScene();
        List<string> offenders = new List<string>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(text.gameObject);
                if (instanceRoot == null)
                {
                    continue;
                }

                string sourcePrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
                offenders.Add(MenuTextConversion.BuildHierarchyPath(text.gameObject, null) + " (source: " + sourcePrefabPath + ")");
            }
        }

        Assert.IsEmpty(offenders, "nested legacy Text remains in the Start scene:\n- " + string.Join("\n- ", offenders));
    }

    [Test]
    public void ConfirmTipPrefabHasNoLegacyTextAfterPhase6C()
    {
        const string ConfirmTipPrefabPath = "Assets/Resources/Prefabs/misc/confirm_tip.prefab";
        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmTipPrefabPath);
        Assert.IsNotNull(prefabRoot, "could not load " + ConfirmTipPrefabPath);

        Assert.AreEqual(0, prefabRoot.GetComponentsInChildren<Text>(true).Length, ConfirmTipPrefabPath + " : Phase 6C must convert every legacy Text component.");
        Assert.AreEqual(4, prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true).Length, ConfirmTipPrefabPath + " : expected exactly 4 TextMeshProUGUI components (header, tip body, next-button label, close-button label).");
    }

    [Test]
    public void DialogueManagerNoLongerReferencesConfirmTipPrefab()
    {
        // AUD-092 Phase 6C ownership investigation: DialogueManager.confirmationDialogTip serialized a
        // fileID that did not correspond to any object in confirm_tip.prefab (a dangling reference), and
        // DialogueManager itself only ever exists in level_00_account_loginLocal.unity - never
        // level_00_start.unity, where confirm_tip.prefab's instance actually lives - so its tip branch
        // could never run in production. The field, the TipDialogue constant, and the tip-specific
        // GameObject.Find/Instantiate branch were removed entirely; this asserts they stay gone.
        Assert.IsNull(typeof(DialogueManager).GetField("confirmationDialogTip"), "DialogueManager.confirmationDialogTip should have been removed - it only ever held a dangling reference to a deleted component.");
        Assert.IsNull(typeof(DialogueManager).GetField("TipDialogue"), "DialogueManager.TipDialogue should have been removed with confirmationDialogTip.");
        Assert.IsNotNull(typeof(DialogueManager).GetField("confirmationDialog"), "DialogueManager.confirmationDialog (the live ConfirmDialogue path) must remain.");
    }

    [Test]
    public void StaticLabelsRequireNoRuntimeTmpBinding()
    {
        // The 14 former static StartMenuUiObjects fields never gained a StartMenuTextUiObjects
        // counterpart - static presentation needs no runtime view binding (unlike the 27 dynamic
        // bindings StartManager/PlayerSelectView/CpuSlotBinding actually write .text into).
        FieldInfo[] tmpFields = Array.FindAll(
            typeof(StartMenuTextUiObjects).GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => field.FieldType == typeof(TMP_Text));
        Assert.AreEqual(DynamicFieldNames.Length, tmpFields.Length, "StartMenuTextUiObjects should expose exactly the 27 dynamic bindings, no more.");

        foreach (FieldInfo field in tmpFields)
        {
            Assert.IsFalse(
                Array.IndexOf(StaticFieldNames, field.Name) >= 0,
                "StartMenuTextUiObjects." + field.Name + " mirrors a former static label field; static presentation needs no runtime binding.");
        }
    }

    [Test]
    public void DynamicAndStaticFieldListsAreDisjointAndTotalFortyOne()
    {
        // Regression guard for the 27/14/41 split quoted throughout this migration's doc comments:
        // the two canonical lists (StartMenuTextMeshProMigration.DynamicFieldNames/StaticFieldNames)
        // must never overlap and must always account for all 41 of StartMenuUiObjects' former legacy
        // Text fields between them - both lists remain load-bearing permanently, as the leftover
        // property-modification scan's known-name set.
        Assert.AreEqual(27, DynamicFieldNames.Length);
        Assert.AreEqual(14, StaticFieldNames.Length);

        HashSet<string> union = new HashSet<string>(DynamicFieldNames);
        foreach (string fieldName in StaticFieldNames)
        {
            Assert.IsTrue(union.Add(fieldName), "'" + fieldName + "' appears in both DynamicFieldNames and StaticFieldNames.");
        }

        Assert.AreEqual(41, union.Count);
    }
}
