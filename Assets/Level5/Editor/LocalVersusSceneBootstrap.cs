using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-off authoring tools for the production Local Versus screen. The output is an ordinary,
/// serialized scene: nothing here runs in a build, and the screen it produces builds no UI at
/// runtime.
///
/// <see cref="GenerateScene"/> starts from <c>level_00_account_online.unity</c> (the closest
/// already-contract-compliant menu scene: Canvas at the 1920x1080 scaling contract, Camera, an
/// EventSystem carrying an Input System UI module, and the themed TMP input/button/text elements),
/// keeps the shared infrastructure, drops everything Backend V2 specific, and lays out the Local
/// Versus form. <see cref="AuthorStartScreenButton"/> adds the authored, serialized Local Versus
/// entry to the Start screen through <see cref="StartMenuUiObjects"/>.
///
/// Both are idempotent regenerators: rerun via the Tools/Local Versus menu if a scene is ever lost.
/// </summary>
public static class LocalVersusSceneBootstrap
{
    private const string TemplateScenePath = "Assets/Scenes/level_00_account_online.unity";
    private const string ScenePath = "Assets/Scenes/level_00_local_versus.unity";
    private const string StartScenePath = "Assets/Scenes/level_00_start.unity";

    private const float PanelWidth = 820f;

    // Compact enough for the series panel to hold its tallest state - a simultaneous series, which
    // adds Player 2's character selector - inside the 1080px canvas. Alternating play, which keeps the
    // single selector, simply has room to spare.
    private const float PanelButtonHeight = 64f;
    private const float PanelSpacing = 8f;
    private const float TurnMessageMinFontSize = 18f;
    private const float TurnMessageMaxFontSize = 30f;

    [MenuItem("Tools/Local Versus/Generate Local Versus Scene")]
    public static void GenerateScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TemplateScenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find("OnlineAccountScreen");
        if (root == null)
        {
            Debug.LogError("LocalVersusSceneBootstrap: OnlineAccountScreen not found in " + TemplateScenePath);
            return;
        }

        Transform canvas = root.transform.Find("Canvas");
        GameObject nameFieldTemplate = Detached(canvas.Find("SignedOutPanel/verticalFields/userNameField"));
        GameObject buttonTemplate = Detached(canvas.Find("SignedOutPanel/Connect/login/loginButton"));
        GameObject textTemplate = Detached(canvas.Find("SignedOutPanel/signInError"));

        // Everything online-account specific goes; the shared scene infrastructure stays.
        foreach (string name in new[] { "menu_footer", "SignedOutPanel", "SignedInPanel" })
        {
            Object.DestroyImmediate(canvas.Find(name).gameObject);
        }

        Object.DestroyImmediate(root.GetComponent<OnlineAccountController>());
        Object.DestroyImmediate(root.GetComponent<OnlineAccountUiObjects>());
        Object.DestroyImmediate(root.GetComponent<MenuFooterUiObjects>());
        foreach (string name in new[] { "[SerializedScene]", "touch_joystick", "restapi" })
        {
            GameObject extra = GameObject.Find(name);
            if (extra != null)
            {
                Object.DestroyImmediate(extra);
            }
        }

        root.name = "LocalVersusScreen";

        // ---- title
        TMP_Text title = MakeText(textTemplate, canvas, "title", "Local Versus", 72, TextAlignmentOptions.Center);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1600f, 110f));

        // ---- create panel (left)
        RectTransform createPanel = MakePanel(canvas, "CreatePanel", new Vector2(-450f, -230f));
        MakeText(textTemplate, createPanel, "createHeader", "New Series", 44, TextAlignmentOptions.MidlineLeft, 60f);
        TMP_InputField player1 = MakeNameField(nameFieldTemplate, createPanel, "player1Field", "Player 1");
        TMP_InputField player2 = MakeNameField(nameFieldTemplate, createPanel, "player2Field", "Player 2");
        Button modeButton = MakeButton(buttonTemplate, createPanel, "modeButton", "Mode", out TMP_Text modeText);
        Button rulesetButton = MakeButton(buttonTemplate, createPanel, "rulesetButton", "Ruleset", out TMP_Text rulesetText);
        Button formatButton = MakeButton(buttonTemplate, createPanel, "formatButton", "Format", out TMP_Text formatText);
        Button createButton = MakeButton(buttonTemplate, createPanel, "createButton", "Create Series", out _);
        TMP_Text createMessage = MakeText(textTemplate, createPanel, "createMessage", string.Empty, 30, TextAlignmentOptions.TopLeft, 80f);

        // ---- series panel (right)
        RectTransform seriesPanel = MakePanel(canvas, "SeriesPanel", new Vector2(450f, -230f));
        MakeText(textTemplate, seriesPanel, "seriesHeader", "Series", 44, TextAlignmentOptions.MidlineLeft, 60f);
        TMP_Text seriesList = MakeText(textTemplate, seriesPanel, "seriesList", string.Empty, 28, TextAlignmentOptions.TopLeft, 210f);
        Button seriesSelectButton = MakeButton(buttonTemplate, seriesPanel, "seriesSelectButton", "Series", out TMP_Text seriesSelectText);
        TMP_Text seriesDetail = MakeText(textTemplate, seriesPanel, "seriesDetail", string.Empty, 30, TextAlignmentOptions.TopLeft, 120f);
        Button characterButton = MakeButton(buttonTemplate, seriesPanel, "characterButton", "Character", out TMP_Text characterText);
        Button character2Button = MakeButton(buttonTemplate, seriesPanel, "character2Button", "Player 2", out TMP_Text character2Text);
        // Shown by the controller only while a simultaneous series is selected.
        character2Button.gameObject.SetActive(false);
        Button levelButton = MakeButton(buttonTemplate, seriesPanel, "levelButton", "Arena", out TMP_Text levelText);
        Button playTurnButton = MakeButton(buttonTemplate, seriesPanel, "playTurnButton", "Play Turn", out TMP_Text playTurnText);
        TMP_Text turnMessage = MakeText(textTemplate, seriesPanel, "turnMessage", string.Empty, 30, TextAlignmentOptions.TopLeft, 60f);
        FitTurnMessage(turnMessage);

        // ---- back
        Button backButton = MakeButton(buttonTemplate, canvas, "backButton", "Back", out _);
        Anchor((RectTransform)backButton.transform, new Vector2(0.5f, 0f), new Vector2(-450f, 70f), new Vector2(360f, 80f));
        Object.DestroyImmediate(backButton.GetComponent<LayoutElement>());
        backButton.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

        Object.DestroyImmediate(nameFieldTemplate);
        Object.DestroyImmediate(buttonTemplate);
        Object.DestroyImmediate(textTemplate);

        // ---- wire the view
        LocalVersusUiObjects ui = root.AddComponent<LocalVersusUiObjects>();
        SerializedObject uiSo = new SerializedObject(ui);
        SetRef(uiSo, "player1NameInputField", player1);
        SetRef(uiSo, "player2NameInputField", player2);
        SetRef(uiSo, "modeButton", modeButton);
        SetRef(uiSo, "modeText", modeText);
        SetRef(uiSo, "rulesetButton", rulesetButton);
        SetRef(uiSo, "rulesetText", rulesetText);
        SetRef(uiSo, "formatButton", formatButton);
        SetRef(uiSo, "formatText", formatText);
        SetRef(uiSo, "createButton", createButton);
        SetRef(uiSo, "createMessageText", createMessage);
        SetRef(uiSo, "seriesListText", seriesList);
        SetRef(uiSo, "seriesSelectButton", seriesSelectButton);
        SetRef(uiSo, "seriesSelectText", seriesSelectText);
        SetRef(uiSo, "seriesDetailText", seriesDetail);
        SetRef(uiSo, "characterButton", characterButton);
        SetRef(uiSo, "characterText", characterText);
        SetRef(uiSo, "character2Button", character2Button);
        SetRef(uiSo, "character2Text", character2Text);
        SetRef(uiSo, "levelButton", levelButton);
        SetRef(uiSo, "levelText", levelText);
        SetRef(uiSo, "playTurnButton", playTurnButton);
        SetRef(uiSo, "playTurnText", playTurnText);
        SetRef(uiSo, "turnMessageText", turnMessage);
        SetRef(uiSo, "backButton", backButton);
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        LocalVersusController controller = root.AddComponent<LocalVersusController>();
        SerializedObject controllerSo = new SerializedObject(controller);
        SetRef(controllerSo, "ui", ui);
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        // A deterministic first selection before the controller's own Start runs.
        EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            eventSystem.firstSelectedGameObject = createButton.gameObject;
        }

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("LocalVersusSceneBootstrap: failed to save " + ScenePath);
            return;
        }

        AddSceneToBuildSettings(ScenePath);
        Debug.Log("LocalVersusSceneBootstrap: generated " + ScenePath);
    }

    /// <summary>
    /// Upgrades the already-authored Local Versus scene in place with the simultaneous-play controls:
    /// a Mode selector at the top of the create panel and Player 2's character selector under Player
    /// 1's, each cloned from a sibling so it keeps that control's theming, and the panels' buttons
    /// compacted to <see cref="PanelButtonHeight"/> so the taller simultaneous state still fits the
    /// canvas. The same end state <see cref="GenerateScene"/> produces from scratch, reached without
    /// regenerating (and so re-identifying) every object in the scene. Idempotent.
    /// </summary>
    [MenuItem("Tools/Local Versus/Add Simultaneous Controls")]
    public static void AddSimultaneousControls()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        LocalVersusUiObjects ui = Object.FindAnyObjectByType<LocalVersusUiObjects>(FindObjectsInactive.Include);
        if (ui == null)
        {
            Debug.LogError("LocalVersusSceneBootstrap: no LocalVersusUiObjects in " + ScenePath);
            return;
        }

        SerializedObject uiSo = new SerializedObject(ui);
        // Part of the same end state as GenerateScene, so a scene upgraded in place gets it too.
        bool turnMessageFitted = FitTurnMessage(ui.TurnMessageText);

        if (uiSo.FindProperty("modeButton").objectReferenceValue != null
            && uiSo.FindProperty("character2Button").objectReferenceValue != null)
        {
            if (turnMessageFitted)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("LocalVersusSceneBootstrap: the scene already has the simultaneous controls; fitted its turn message.");
                return;
            }

            Debug.Log("LocalVersusSceneBootstrap: the scene already has the simultaneous controls.");
            return;
        }

        Transform createPanel = ui.RulesetButton.transform.parent;
        Transform seriesPanel = ui.CharacterButton.transform.parent;

        Button modeButton = CloneButton(ui.FormatButton, createPanel, "modeButton", "Mode", out TMP_Text modeText);
        modeButton.transform.SetSiblingIndex(ui.RulesetButton.transform.GetSiblingIndex());

        Button character2Button = CloneButton(ui.CharacterButton, seriesPanel, "character2Button", "Player 2", out TMP_Text character2Text);
        character2Button.transform.SetSiblingIndex(ui.CharacterButton.transform.GetSiblingIndex() + 1);

        // Shown by the controller only while a simultaneous series is selected.
        character2Button.gameObject.SetActive(false);

        foreach (Transform panel in new[] { createPanel, seriesPanel })
        {
            panel.GetComponent<VerticalLayoutGroup>().spacing = PanelSpacing;
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                SetPreferred(button.gameObject, PanelButtonHeight);
            }
        }

        SetRef(uiSo, "modeButton", modeButton);
        SetRef(uiSo, "modeText", modeText);
        SetRef(uiSo, "character2Button", character2Button);
        SetRef(uiSo, "character2Text", character2Text);
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("LocalVersusSceneBootstrap: failed to save " + ScenePath);
            return;
        }

        Debug.Log("LocalVersusSceneBootstrap: added the simultaneous controls to " + ScenePath);
    }

    private static Button CloneButton(Button template, Transform parent, string name, string label, out TMP_Text text)
    {
        GameObject go = Object.Instantiate(template.gameObject, parent);
        go.name = name;
        go.SetActive(true);
        text = go.GetComponent<TMP_Text>();
        text.text = label;
        return go.GetComponent<Button>();
    }

    [MenuItem("Tools/Local Versus/Author Start Screen Button")]
    public static void AuthorStartScreenButton()
    {
        Scene scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);

        StartMenuUiObjects startUi = Object.FindAnyObjectByType<StartMenuUiObjects>(FindObjectsInactive.Include);
        Transform footer = FindByName(scene, "footer");
        Transform account = footer == null ? null : footer.Find(StartMenuUiObjects.accountMenuButtonName);
        if (startUi == null || account == null)
        {
            Debug.LogError("LocalVersusSceneBootstrap: Start scene is missing StartMenuUiObjects or the footer account button.");
            return;
        }

        SerializedObject uiSo = new SerializedObject(startUi);
        SerializedProperty property = uiSo.FindProperty("localVersusButton");
        if (property.objectReferenceValue != null)
        {
            Debug.Log("LocalVersusSceneBootstrap: Start scene already has a Local Versus button.");
            return;
        }

        // The footer row is already at capacity: an eighth button shrinks every label in it
        // ("ACCOUN", "STAT"). So the entry is its own authored row directly under the footer,
        // styled from the account button, and reached with Up/Down from Start.
        GameObject clone = Object.Instantiate(account.gameObject, footer.parent);
        clone.name = "local_versus_menu";
        clone.GetComponentInChildren<TMP_Text>(true).text = "Local Versus";
        RectTransform footerRect = (RectTransform)footer;
        RectTransform rect = (RectTransform)clone.transform;
        rect.anchorMin = rect.anchorMax = footerRect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 70f);
        rect.anchoredPosition = new Vector2(footerRect.anchoredPosition.x, footerRect.anchoredPosition.y - 90f);
        clone.transform.SetSiblingIndex(footer.GetSiblingIndex() + 1);

        Button button = clone.GetComponent<Button>();
        Button start = footer.Find(StartMenuUiObjects.startButtonName).GetComponent<Button>();
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = start;
        navigation.selectOnDown = null;
        navigation.selectOnLeft = null;
        navigation.selectOnRight = null;
        button.navigation = navigation;

        Navigation startNavigation = start.navigation;
        if (startNavigation.mode == Navigation.Mode.Explicit)
        {
            startNavigation.selectOnDown = button;
            start.navigation = startNavigation;
        }

        property.objectReferenceValue = button;
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("LocalVersusSceneBootstrap: failed to save " + StartScenePath);
            return;
        }

        Debug.Log("LocalVersusSceneBootstrap: authored the Local Versus button into " + StartScenePath);
    }

    // ------------------------------------------------------------------ builders

    private static GameObject Detached(Transform source)
    {
        GameObject copy = Object.Instantiate(source.gameObject);
        copy.name = source.name;
        copy.transform.SetParent(null, false);
        return copy;
    }

    private static RectTransform MakePanel(Transform parent, string name, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(PanelWidth, 0f);

        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = PanelSpacing;

        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>
    /// A refused launch reports the device preflight's sentence ("Two local players need two gamepads, or a
    /// keyboard plus one gamepad. Detected: ..."), which is three lines at 30pt in a box that holds two, and
    /// the panel has no room to grow the box. Shrink to fit instead of truncating what the player must read.
    /// Returns whether anything changed.
    /// </summary>
    private static bool FitTurnMessage(TMP_Text turnMessage)
    {
        if (turnMessage.enableAutoSizing
            && Mathf.Approximately(turnMessage.fontSizeMin, TurnMessageMinFontSize)
            && Mathf.Approximately(turnMessage.fontSizeMax, TurnMessageMaxFontSize))
        {
            return false;
        }

        turnMessage.enableAutoSizing = true;
        turnMessage.fontSizeMin = TurnMessageMinFontSize;
        turnMessage.fontSizeMax = TurnMessageMaxFontSize;
        EditorUtility.SetDirty(turnMessage);
        return true;
    }

    private static TMP_Text MakeText(
        GameObject template,
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        float preferredHeight = 0f)
    {
        GameObject go = Object.Instantiate(template, parent);
        go.name = name;
        TMP_Text tmp = go.GetComponent<TMP_Text>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.enableAutoSizing = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Truncate;
        tmp.raycastTarget = false;

        if (preferredHeight > 0f)
        {
            SetPreferred(go, preferredHeight);
        }

        return tmp;
    }

    private static Button MakeButton(GameObject template, Transform parent, string name, string label, out TMP_Text text)
    {
        GameObject go = Object.Instantiate(template, parent);
        go.name = name;
        text = go.GetComponent<TMP_Text>();
        text.text = label;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = 34f;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferred(go, PanelButtonHeight);

        Button button = go.GetComponent<Button>();
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Automatic;
        button.navigation = navigation;
        return button;
    }

    private static TMP_InputField MakeNameField(GameObject template, Transform parent, string rowName, string label)
    {
        GameObject row = Object.Instantiate(template, parent);
        row.name = rowName;
        SetPreferred(row, 90f);

        Transform labelTransform = row.transform.Find("userNameText");
        Button stray = labelTransform.GetComponent<Button>();
        if (stray != null)
        {
            // The account screens' label carries a Button nothing listens to; here it would just be
            // one more dead stop in keyboard/gamepad navigation.
            Object.DestroyImmediate(stray);
        }

        TMP_Text labelText = labelTransform.GetComponent<TMP_Text>();
        labelText.text = label;
        ((RectTransform)labelTransform).sizeDelta = new Vector2(260f, 90f);

        TMP_InputField input = row.transform.Find("UserNameInputField").GetComponent<TMP_InputField>();
        input.gameObject.name = rowName.Replace("Field", "NameInputField");
        input.text = string.Empty;
        input.characterLimit = 16;
        // Selecting a field with the d-pad must not start editing: an active field swallows the d-pad,
        // stick and Cancel, so a gamepad player who moved onto it could not move off it. Click, Enter
        // and the gamepad's Submit still activate it deliberately.
        input.shouldActivateOnSelect = false;
        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = label;
        }

        ((RectTransform)input.transform).sizeDelta = new Vector2(520f, 90f);
        return input;
    }

    private static void SetPreferred(GameObject go, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.minHeight = height;
    }

    // ------------------------------------------------------------------ helpers

    private static Transform FindByName(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }
        }

        return null;
    }

    private static void SetRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError("LocalVersusSceneBootstrap: missing serialized property " + propertyName);
            return;
        }

        prop.objectReferenceValue = value;
    }

    private static void AddSceneToBuildSettings(string path)
    {
        EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
        if (existing.Any(s => s.path == path))
        {
            return;
        }

        EditorBuildSettings.scenes = existing.Append(new EditorBuildSettingsScene(path, true)).ToArray();
    }
}
