using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// AUD-092 Phase 6C: migrates the shared tip dialogue source prefab
/// (<see cref="PrefabPath"/>) - nested directly in <c>level_00_start.unity</c>, and previously the only
/// deferred nested legacy-Text source on <see cref="StartMenuTextMeshProMigration"/>'s Phase 6C
/// allowlist (now removed) - from its four directly-owned legacy <see cref="Text"/> components (header,
/// tip body, next-button label, close-button label) to TextMeshProUGUI on the shared Neon Pixel-7 SDF
/// font asset, and establishes <see cref="TipDialogueUiObjects"/> as the one typed UI view
/// <see cref="StartScreenTipDialogueManager"/> resolves its header/tip/button references from -
/// replacing the two <c>GameObject.Find</c> button lookups the manager used to perform on itself at
/// <c>Awake()</c>.
///
/// Ownership investigation (dev HEAD 937479551): <c>DialogueManager.confirmationDialogTip</c> - the
/// field that used to make this prefab look shared beyond the Start scene - serialized a fileID
/// (1583025568552138434) that does not correspond to any object in this prefab file at all (a dangling
/// reference to a deleted component, not a live dependency), <c>DialogueManager</c> itself only ever
/// exists as a scene-placed GameObject in <c>level_00_account_loginLocal.unity</c> (never
/// <c>level_00_start.unity</c>, where this prefab's instance lives), and
/// <c>DialogueManager.ShowConfirmationDialog()</c> is only ever invoked by the local-account removal
/// flow (<c>LocalAccount.cs</c>/<c>UserAccountManager.cs</c>), never for tips. That combination makes
/// the dangling reference permanently unreachable, not merely rare - see
/// <see cref="Level5ProjectValidator.CollectTipDialogueTextRenderingContractErrors"/>'s doc comment for
/// where that field was removed accordingly.
///
/// Button ownership: <c>UnityEventCallState</c> is <c>Off = 0, EditorAndRuntime = 1, RuntimeOnly = 2</c>
/// (verified against this project's own Unity 6000.5.7f1 via a throwaway reflection probe, not assumed) -
/// so this prefab's <c>m_CallState: 2</c> persistent OnClick entries were <c>RuntimeOnly</c>, i.e. ACTIVE,
/// not "Off"/inert as an earlier draft of this comment incorrectly claimed. next_button's persistent call
/// had a null <c>m_Target</c> and empty <c>m_MethodName</c>, so it was inert regardless of call state -
/// invoking a persistent call with a null target is a silent no-op. cancel_button's persistent call,
/// however, had a valid target (this prefab's own <see cref="StartScreenTipDialogueManager"/> instance)
/// and a valid method name (<c>CancelButtonOnClick</c>), and was genuinely live: before this migration,
/// clicking Close ran <c>CancelButtonOnClick()</c> TWICE per click - once via this Inspector-configured
/// persistent call, once via <see cref="StartScreenTipDialogueManager"/>'s own Awake-time
/// <c>GameObject.Find("cancel_button").onClick.AddListener(CancelButtonOnClick)</c>. That duplicate
/// invocation had no visible symptom only because every side effect it triggers is idempotent
/// (<c>result = CANCEL</c> re-assigns the same value, <c>EventSystem.SetSelectedGameObject</c> and
/// <c>Destroy(gameObject)</c> are both safe to call twice) - it was a real double-registration bug, not a
/// harmless no-op by design. This migration fixes it by clearing both persistent entries and making
/// <see cref="StartScreenTipDialogueManager"/>'s OnEnable/OnDisable the single, exclusive place either
/// button is wired.
/// </summary>
internal static class TipDialogueTextMeshProMigration
{
    internal const string PrefabPath = "Assets/Resources/Prefabs/misc/confirm_tip.prefab";
    private const string NeonPixelFontAssetPath = "Assets/Fonts/TMP/Neon Pixel-7 SDF.asset";

    private const string HeaderGameObjectName = "header";
    private const string TipGameObjectName = "tip_text";
    private const string NextButtonGameObjectName = "next_button";
    private const string CloseButtonGameObjectName = "cancel_button";

    [MenuItem("Level5/Migrate Confirm Tip Dialogue Text")]
    public static void MigrateFromMenu()
    {
        Migrate();
    }

    /// <summary>
    /// Idempotent: a second run against an already-migrated prefab finds zero legacy Text and reports
    /// "nothing to do" without touching the asset, matching every other AUD-092 single-prefab migration.
    /// </summary>
    public static bool Migrate()
    {
        const string LogPrefix = "TipDialogueTextMeshProMigration.Migrate";

        if (AssetDatabase.FindAssets("t:TMP_Settings").Length == 0)
        {
            Debug.LogError(LogPrefix + ": TMP Essential Resources are not present. Run Level5/Import TMP Essential Resources first, then re-run this.");
            return false;
        }

        TMP_FontAsset font = MenuTextConversion.EnsureNeonPixelFontAsset();
        if (font == null)
        {
            Debug.LogError(LogPrefix + ": could not create/load the Neon Pixel-7 SDF font asset; aborting.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            List<string> errors = new List<string>();

            StartScreenTipDialogueManager manager = root.GetComponentInChildren<StartScreenTipDialogueManager>(true);
            if (manager == null)
            {
                errors.Add(PrefabPath + " : StartScreenTipDialogueManager component is missing.");
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }

            if (root.GetComponentsInChildren<Text>(true).Length == 0 && root.GetComponentInChildren<TipDialogueUiObjects>(true) != null)
            {
                Debug.Log(LogPrefix + ": " + PrefabPath + " has no legacy Text remaining; nothing to do.");
                return true;
            }

            Dictionary<string, Transform> childrenByName = MenuTextConversion.IndexChildrenByName(root);
            Text headerText = ResolveNamedText(childrenByName, HeaderGameObjectName, errors);
            Text tipText = ResolveNamedText(childrenByName, TipGameObjectName, errors);
            Button nextButton = ResolveNamedButton(childrenByName, NextButtonGameObjectName, errors);
            Button closeButton = ResolveNamedButton(childrenByName, CloseButtonGameObjectName, errors);
            if (errors.Count > 0)
            {
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }

            Text nextButtonText = nextButton.GetComponent<Text>();
            Text closeButtonText = closeButton.GetComponent<Text>();
            if (nextButtonText == null)
            {
                errors.Add(PrefabPath + " -> " + NextButtonGameObjectName + " : has no legacy Text label.");
            }
            if (closeButtonText == null)
            {
                errors.Add(PrefabPath + " -> " + CloseButtonGameObjectName + " : has no legacy Text label.");
            }
            if (errors.Count > 0)
            {
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }

            // Safety gate: nothing other than the two Buttons' own targetGraphic may reference these
            // four Text components before they are destroyed - StartScreenTipDialogueManager no longer
            // declares tipText/headerText fields at all (removed as part of this same phase), so if this
            // finds anything it is a genuinely unknown consumer this migration does not know how to
            // preserve, not the manager's own already-handled mapping.
            HashSet<Object> textSet = new HashSet<Object> { headerText, tipText, nextButtonText, closeButtonText };
            List<string> unsupportedConsumers = new List<string>();
            MenuTextConversion.CollectUnsupportedConsumers(root, textSet, unsupportedConsumers);
            if (unsupportedConsumers.Count > 0)
            {
                errors.AddRange(unsupportedConsumers);
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }

            TextMeshProUGUI headerTmp = MenuTextConversion.ConvertSingleText(root, headerText, font);
            TextMeshProUGUI tipTmp = MenuTextConversion.ConvertSingleText(root, tipText, font);
            TextMeshProUGUI nextButtonTmp = MenuTextConversion.ConvertSingleText(root, nextButtonText, font);
            TextMeshProUGUI closeButtonTmp = MenuTextConversion.ConvertSingleText(root, closeButtonText, font);
            if (headerTmp == null || tipTmp == null || nextButtonTmp == null || closeButtonTmp == null)
            {
                errors.Add(PrefabPath + " : one or more Text -> TextMeshProUGUI conversions failed.");
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }

            foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (MenuTextConversion.IsPartOfNestedPrefabInstance(selectable.gameObject, root))
                {
                    continue;
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
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }

            TipDialogueUiObjects ui = manager.gameObject.GetComponent<TipDialogueUiObjects>();
            if (ui == null)
            {
                ui = manager.gameObject.AddComponent<TipDialogueUiObjects>();
            }

            SerializedObject serializedUi = new SerializedObject(ui);
            serializedUi.FindProperty("header").objectReferenceValue = headerTmp;
            serializedUi.FindProperty("tip").objectReferenceValue = tipTmp;
            serializedUi.FindProperty("nextButton").objectReferenceValue = nextButton;
            serializedUi.FindProperty("closeButton").objectReferenceValue = closeButton;
            serializedUi.ApplyModifiedProperties();

            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty uiProperty = serializedManager.FindProperty("ui");
            if (uiProperty == null)
            {
                errors.Add(PrefabPath + " : StartScreenTipDialogueManager has no 'ui' field - update the class before re-running this migration.");
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }
            uiProperty.objectReferenceValue = ui;
            serializedManager.ApplyModifiedProperties();

            ClearPersistentOnClickListeners(nextButton);
            ClearPersistentOnClickListeners(closeButton);

            List<string> missingUiFields = new List<string>();
            if (!ui.Validate(missingUiFields))
            {
                errors.Add(PrefabPath + " : TipDialogueUiObjects failed to resolve after wiring - " + string.Join(", ", missingUiFields));
                MenuTextConversion.LogAbort(LogPrefix, errors);
                return false;
            }

            MenuTextConversion.PersistLooseUnderlayMaterials(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log(LogPrefix + " complete: converted 4 Text component(s), added TipDialogueUiObjects, cleared 2 persistent OnClick listener(s) (code now owns both buttons exclusively).");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Text ResolveNamedText(Dictionary<string, Transform> childrenByName, string name, List<string> errors)
    {
        if (!childrenByName.TryGetValue(name, out Transform transform))
        {
            errors.Add(PrefabPath + " : expected GameObject '" + name + "' was not found.");
            return null;
        }

        Text text = transform.GetComponent<Text>();
        if (text == null)
        {
            errors.Add(PrefabPath + " -> " + name + " : has no legacy Text component.");
        }

        return text;
    }

    private static Button ResolveNamedButton(Dictionary<string, Transform> childrenByName, string name, List<string> errors)
    {
        if (!childrenByName.TryGetValue(name, out Transform transform))
        {
            errors.Add(PrefabPath + " : expected GameObject '" + name + "' was not found.");
            return null;
        }

        Button button = transform.GetComponent<Button>();
        if (button == null)
        {
            errors.Add(PrefabPath + " -> " + name + " : has no Button component.");
        }

        return button;
    }

    private static void ClearPersistentOnClickListeners(Button button)
    {
        SerializedObject serializedButton = new SerializedObject(button);
        SerializedProperty calls = serializedButton.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.ClearArray();
        serializedButton.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------------------------------------
    // Permanent contract (backs Level5ProjectValidator.CollectTipDialogueTextRenderingContractErrors)
    // ---------------------------------------------------------------------------------------------

    public static List<string> CollectContractErrors()
    {
        List<string> errors = new List<string>();

        GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefabRoot == null)
        {
            errors.Add(PrefabPath + " : could not load confirm_tip prefab asset.");
            return errors;
        }

        List<Text> ownedLegacyTexts = new List<Text>();
        List<Text> nestedLegacyTexts = new List<Text>();
        MenuTextConversion.PartitionByNestedPrefabInstance(
            prefabRoot.GetComponentsInChildren<Text>(true), prefabRoot, ownedLegacyTexts, nestedLegacyTexts);
        if (ownedLegacyTexts.Count > 0)
        {
            errors.Add(PrefabPath + " : " + ownedLegacyTexts.Count + " legacy Text component(s) remain (expected 0).");
        }

        TMP_FontAsset neonPixel = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NeonPixelFontAssetPath);
        TextMeshProUGUI[] tmpComponents = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        if (tmpComponents.Length != 4)
        {
            errors.Add(PrefabPath + " : expected exactly 4 TextMeshProUGUI component(s), found " + tmpComponents.Length + ".");
        }

        foreach (TextMeshProUGUI tmp in tmpComponents)
        {
            if (neonPixel == null || tmp.font != neonPixel)
            {
                errors.Add(
                    PrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(tmp.gameObject, prefabRoot)
                        + " : does not use the shared Neon Pixel-7 SDF font asset.");
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
                    PrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(selectable.gameObject, prefabRoot) + " : "
                        + selectable.GetType().Name + " has a null targetGraphic.");
            }
        }

        StartScreenTipDialogueManager[] managers = prefabRoot.GetComponentsInChildren<StartScreenTipDialogueManager>(true);
        if (managers.Length != 1)
        {
            errors.Add(PrefabPath + " : expected exactly 1 StartScreenTipDialogueManager, found " + managers.Length + ".");
        }

        TipDialogueUiObjects[] uiViews = prefabRoot.GetComponentsInChildren<TipDialogueUiObjects>(true);
        if (uiViews.Length != 1)
        {
            errors.Add(PrefabPath + " : expected exactly 1 TipDialogueUiObjects, found " + uiViews.Length + ".");
        }
        else
        {
            List<string> missing = new List<string>();
            uiViews[0].Validate(missing);
            foreach (string field in missing)
            {
                errors.Add(PrefabPath + " : " + field + " is not resolved.");
            }
        }

        errors.AddRange(CollectLegacyFieldSchemaErrors());
        errors.AddRange(CollectPersistentListenerErrors(prefabRoot));

        return errors;
    }

    /// <summary>
    /// <see cref="StartScreenTipDialogueManager"/> must carry no field assignable to legacy
    /// <see cref="Text"/>/<see cref="Button"/> at all - it must resolve UI only through its single
    /// <see cref="TipDialogueUiObjects"/> reference.
    /// </summary>
    private static List<string> CollectLegacyFieldSchemaErrors()
    {
        List<string> errors = new List<string>();
        foreach (FieldInfo field in typeof(StartScreenTipDialogueManager).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (typeof(Text).IsAssignableFrom(field.FieldType) || typeof(Button).IsAssignableFrom(field.FieldType))
            {
                errors.Add(
                    "StartScreenTipDialogueManager." + field.Name
                        + " is still typed as a legacy UI reference; it must resolve UI only through TipDialogueUiObjects.");
            }
        }

        return errors;
    }

    /// <summary>
    /// No Button in the prefab may carry a persistent OnClick listener - code owns both buttons via
    /// <see cref="StartScreenTipDialogueManager"/>'s OnEnable/OnDisable.
    /// </summary>
    private static List<string> CollectPersistentListenerErrors(GameObject prefabRoot)
    {
        List<string> errors = new List<string>();
        foreach (Button button in prefabRoot.GetComponentsInChildren<Button>(true))
        {
            int persistentCount = button.onClick.GetPersistentEventCount();
            if (persistentCount > 0)
            {
                errors.Add(
                    PrefabPath + " -> " + MenuTextConversion.BuildHierarchyPath(button.gameObject, prefabRoot)
                        + " : Button.onClick still carries " + persistentCount
                        + " persistent listener(s); this dialogue's buttons must be wired exclusively by StartScreenTipDialogueManager's code.");
            }
        }

        return errors;
    }
}
