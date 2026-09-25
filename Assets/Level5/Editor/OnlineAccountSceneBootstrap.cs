using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-off generator for <c>Assets/Scenes/level_00_account_online.unity</c>. Starts from
/// <c>level_00_account_loginExisting.unity</c> (closest existing template: Canvas/EventSystem/TMP
/// styling, footer, username/password fields, message text, all already themed the same as every
/// other account screen) and reshapes it into the Backend V2 online-account screen: strips
/// <see cref="AccountManager"/>/<see cref="AccountLoginUiObjects"/>, keeps the shared
/// infrastructure (Camera, EventSystem, touch-input scaffolding, restapi/database/PlatformCheck
/// singletons, <see cref="MenuFooterUiObjects"/>), and builds a signed-out panel (sign-in fields plus
/// a cloned register block) and a signed-in panel (display name/tag/sign-out/retry) wired to a new
/// <see cref="OnlineAccountUiObjects"/>/<see cref="OnlineAccountController"/>.
///
/// Re-run via Tools/Backend V2/Generate Online Account Scene if the scene file is ever deleted or
/// needs regenerating; it is idempotent (overwrites the existing scene, and only adds itself to
/// Build Settings once). It never touches the source template scene - it opens it, edits the
/// in-memory copy, then Save-As's to the new path.
/// </summary>
public static class OnlineAccountSceneBootstrap
{
    private const string SourceScenePath = "Assets/Scenes/level_00_account_loginExisting.unity";
    private const string ScenePath = "Assets/Scenes/level_00_account_online.unity";

    [MenuItem("Tools/Backend V2/Generate Online Account Scene")]
    public static void GenerateScene()
    {
        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);

        GameObject root = GameObject.Find("LoginExistingAccountManager");
        if (root == null)
        {
            Debug.LogError(
                "OnlineAccountSceneBootstrap: could not find LoginExistingAccountManager in " + SourceScenePath);
            return;
        }

        root.name = "OnlineAccountScreen";
        Object.DestroyImmediate(root.GetComponent<AccountManager>());
        Object.DestroyImmediate(root.GetComponent<AccountLoginUiObjects>());

        Transform canvas = root.transform.Find("Canvas");
        Transform instructions = canvas.Find("instructions");
        TMP_Text instructionsText = instructions.Find("login").GetComponent<TMP_Text>();
        Transform messageDisplay = canvas.Find("messageDisplay");
        Transform verticalFields = canvas.Find("verticalFields");
        Transform userNameField = verticalFields.Find("userNameField");
        Transform passwordField = verticalFields.Find("passwordField");
        TMP_InputField signInUsername = userNameField.Find("UserNameInputField").GetComponent<TMP_InputField>();
        TMP_InputField signInPassword = passwordField.Find("PasswordInputField").GetComponent<TMP_InputField>();
        Transform connect = canvas.Find("Connect");
        Button signInButton = connect.Find("login/loginButton").GetComponent<Button>();

        // The online-account screen never calls a username-existence endpoint before login/register
        // (issue requirement) - the pre-login "check username" button has no role here.
        Transform checkUserName = userNameField.Find("checkUserName");
        if (checkUserName != null)
        {
            Object.DestroyImmediate(checkUserName.gameObject);
        }

        instructionsText.text = "Sign In";
        signInButton.GetComponentInChildren<TMP_Text>().text = "Sign In";

        // --- Signed-out panel: the existing sign-in fields, plus a cloned register block. ---
        RectTransform signedOutPanel = CreateFullStretchPanel("SignedOutPanel", canvas);
        AddStackingLayout(signedOutPanel);
        instructions.SetParent(signedOutPanel, false);
        messageDisplay.SetParent(signedOutPanel, false);
        verticalFields.SetParent(signedOutPanel, false);
        connect.SetParent(signedOutPanel, false);

        messageDisplay.name = "signInError";
        messageDisplay.GetComponent<TMP_Text>().text = string.Empty;

        GameObject registerFields = Object.Instantiate(verticalFields.gameObject, signedOutPanel);
        registerFields.name = "registerFields";
        Transform registerUserNameField = registerFields.transform.Find("userNameField");
        Transform registerPasswordField = registerFields.transform.Find("passwordField");
        TMP_InputField registerUsername =
            registerUserNameField.Find("UserNameInputField").GetComponent<TMP_InputField>();
        TMP_InputField registerPassword =
            registerPasswordField.Find("PasswordInputField").GetComponent<TMP_InputField>();
        registerUsername.text = string.Empty;
        registerPassword.text = string.Empty;

        GameObject displayNameField = Object.Instantiate(registerUserNameField.gameObject, registerFields.transform);
        displayNameField.name = "displayNameField";
        TMP_InputField displayNameInput =
            displayNameField.transform.Find("UserNameInputField").GetComponent<TMP_InputField>();
        displayNameInput.text = string.Empty;
        displayNameInput.gameObject.name = "DisplayNameInputField";
        displayNameField.transform.Find("userNameText").GetComponent<TMP_Text>().text = "Display Name";
        SetPlaceholder(displayNameInput, "Display Name");

        GameObject registerConnect = Object.Instantiate(connect.gameObject, signedOutPanel);
        registerConnect.name = "RegisterConnect";
        Button registerButton = registerConnect.transform.Find("login/loginButton").GetComponent<Button>();
        registerButton.gameObject.name = "registerButton";
        registerButton.GetComponentInChildren<TMP_Text>().text = "Register";

        GameObject registerErrorGo = Object.Instantiate(messageDisplay.gameObject, signedOutPanel);
        registerErrorGo.name = "registerError";
        TMP_Text registerError = registerErrorGo.GetComponent<TMP_Text>();
        registerError.text = string.Empty;

        // --- Signed-in panel: current identity plus Sign Out / Retry. ---
        RectTransform signedInPanel = CreateFullStretchPanel("SignedInPanel", canvas);
        AddStackingLayout(signedInPanel);

        TMP_Text displayNameText = CloneMessageText(messageDisplay.gameObject, signedInPanel, "displayNameText");
        TMP_Text playerTagText = CloneMessageText(messageDisplay.gameObject, signedInPanel, "playerTagText");
        TMP_Text signedInStatus = CloneMessageText(messageDisplay.gameObject, signedInPanel, "signedInStatus");

        GameObject signOutConnect = Object.Instantiate(connect.gameObject, signedInPanel);
        signOutConnect.name = "SignOutConnect";
        Button signOutButton = signOutConnect.transform.Find("login/loginButton").GetComponent<Button>();
        signOutButton.gameObject.name = "signOutButton";
        signOutButton.GetComponentInChildren<TMP_Text>().text = "Sign Out";

        GameObject retryConnect = Object.Instantiate(connect.gameObject, signedInPanel);
        retryConnect.name = "RetryConnect";
        Button retryButton = retryConnect.transform.Find("login/loginButton").GetComponent<Button>();
        retryButton.gameObject.name = "retryProfileButton";
        retryButton.GetComponentInChildren<TMP_Text>().text = "Retry";

        signedInPanel.gameObject.SetActive(false);

        // --- Wire OnlineAccountUiObjects. ---
        OnlineAccountUiObjects onlineUi = root.AddComponent<OnlineAccountUiObjects>();
        SerializedObject uiSo = new SerializedObject(onlineUi);
        SetRef(uiSo, "signedOutPanel", signedOutPanel.gameObject);
        SetRef(uiSo, "signedInPanel", signedInPanel.gameObject);
        SetRef(uiSo, "signInUsernameInputField", signInUsername);
        SetRef(uiSo, "signInPasswordInputField", signInPassword);
        SetRef(uiSo, "signInButton", signInButton);
        SetRef(uiSo, "signInError", messageDisplay.GetComponent<TMP_Text>());
        SetRef(uiSo, "registerUsernameInputField", registerUsername);
        SetRef(uiSo, "registerPasswordInputField", registerPassword);
        SetRef(uiSo, "registerDisplayNameInputField", displayNameInput);
        SetRef(uiSo, "registerButton", registerButton);
        SetRef(uiSo, "registerError", registerError);
        SetRef(uiSo, "displayNameText", displayNameText);
        SetRef(uiSo, "playerTagText", playerTagText);
        SetRef(uiSo, "signOutButton", signOutButton);
        SetRef(uiSo, "signedInStatus", signedInStatus);
        SetRef(uiSo, "retryProfileButton", retryButton);
        uiSo.ApplyModifiedPropertiesWithoutUndo();

        // --- Wire OnlineAccountController. ---
        MenuFooterUiObjects footer = root.GetComponent<MenuFooterUiObjects>();
        OnlineAccountController controller = root.AddComponent<OnlineAccountController>();
        SerializedObject ctrlSo = new SerializedObject(controller);
        SetRef(ctrlSo, "onlineUi", onlineUi);
        SetRef(ctrlSo, "footer", footer);
        ctrlSo.ApplyModifiedPropertiesWithoutUndo();

        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
        {
            Debug.LogError("OnlineAccountSceneBootstrap: failed to save " + ScenePath);
            return;
        }

        AddSceneToBuildSettings(ScenePath);
        Debug.Log("OnlineAccountSceneBootstrap: generated " + ScenePath);
    }

    private static RectTransform CreateFullStretchPanel(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        return rt;
    }

    /// <summary>Stacks a panel's direct children top-to-bottom by sibling order, without resizing
    /// them - matches the convention every nested group in this scene family already uses
    /// (<c>account_links</c>, <c>verticalFields</c>, <c>menu_footer</c>). Without this, each cloned
    /// element (built via <c>Object.Instantiate(source, panel)</c>, which keeps the source's local
    /// RectTransform values) would render at exactly its source's original position - the register
    /// block on top of the sign-in block, and the signed-in panel's texts/buttons on top of each
    /// other.</summary>
    private static void AddStackingLayout(RectTransform panel)
    {
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.spacing = 24f;
        layout.padding = new RectOffset(0, 0, 40, 40);
    }

    private static TMP_Text CloneMessageText(GameObject source, Transform parent, string name)
    {
        GameObject clone = Object.Instantiate(source, parent);
        clone.name = name;
        TMP_Text text = clone.GetComponent<TMP_Text>();
        text.text = string.Empty;
        return text;
    }

    private static void SetPlaceholder(TMP_InputField field, string text)
    {
        if (field.placeholder is TMP_Text placeholder)
        {
            placeholder.text = text;
        }
    }

    private static void SetRef(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogError("OnlineAccountSceneBootstrap: missing serialized property " + propertyName);
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

        EditorBuildSettingsScene[] updated = existing
            .Append(new EditorBuildSettingsScene(path, true))
            .ToArray();
        EditorBuildSettings.scenes = updated;
    }
}
