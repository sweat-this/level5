using Assets.Scripts.database;
using Assets.Scripts.restapi;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AccountManager : MonoBehaviour
{
    // Exactly one of these two is assigned per scene - level_00_account uses hubUi,
    // level_00_account_createNew uses createUi. level_00_account_loginLocal has no AccountManager at
    // all. level_00_account_loginExisting is retired from production (no player-facing navigation
    // reaches it and it ships in no build) but its scene file and AccountLoginUiObjects component are
    // kept as an editor-only template for OnlineAccountSceneBootstrap, which strips both before using
    // it - AccountManager itself no longer needs a variant for that screen.
    [SerializeField] private AccountHubUiObjects hubUi;
    [SerializeField] private AccountCreateUiObjects createUi;
    [SerializeField] private MenuFooterUiObjects footer;

    TMP_Text messageDisplay;

    // kept for TouchInputAccountScreenController's name-selected dispatch (out of scope: legacy
    // touch controller deletion is gated on device verification)
    const string mainMenuButtonName = "press_start";
    const string statsMenuButtonName = "stats_menu";
    const string progressionMenuButtonName = "update_menu";
    const string creditsMenuButtonName = "credits_menu";
    const string accountMenuButtonName = "account_menu";
    const string createNewButtonName = "createNew";
    const string loginLocalButtonName = "loginLocal";
    const string onlineAccountButtonName = "onlineAccount";

    string profileNameInput;

    TMP_InputField usernameInputField;

    Button mainMenuButton;
    Button statsMenuButton;
    Button progressionMenuButton;
    Button creditsMenuButton;
    Button accountMenuButton;
    Button createNewButton;
    Button loginLocalButton;
    Button onlineAccountButton;
    Button createAccountButton;

    // AUD-092 Phase 5B section 13: narrow in-flight guard so a rapid double Create activation cannot
    // start a second concurrent flow. Reset in a coroutine-wrapping try/finally so every exit path -
    // success, an early `yield break`, or an exception - clears it and allows retry.
    bool isCreatingAccount;

    private bool initialized;

    private void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
        if (initialized)
        {
            RegisterButtonCallbacks();
            RegisterInputSubmitCallbacks();
        }
    }
    private void OnDisable()
    {
        UnregisterButtonCallbacks();
        UnregisterInputSubmitCallbacks();
        PlayerControlsProvider.DisableMenuMaps();
    }

    void Awake()
    {
    }

    private void Start()
    {
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        List<string> missing = new List<string>();
        if (!ValidateMenuUi(missing))
        {
            Debug.LogError(
                "AccountManager is missing required serialized UI references and will be disabled: "
                    + string.Join(", ", missing.ToArray()),
                this);
            enabled = false;
            return;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();
        ResolveUiReferences();

        SetMessage("");

        RegisterButtonCallbacks();
        RegisterInputSubmitCallbacks();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        initialized = true;
    }

    void Update()
    {
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
    }

    /// <summary>
    /// True once exactly one of <see cref="hubUi"/>/<see cref="createUi"/>, plus <see cref="footer"/>,
    /// carry every reference the corresponding scene needs. Callable from editor tooling as a pure
    /// check - it only reads already-serialized references.
    /// </summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        int assigned = (hubUi != null ? 1 : 0) + (createUi != null ? 1 : 0);
        if (assigned != 1)
        {
            missing.Add("AccountManager.hubUi/createUi (exactly one must be assigned)");
            return false;
        }

        if (footer == null)
        {
            missing.Add("AccountManager.footer");
            return false;
        }

        if (hubUi != null)
        {
            hubUi.Validate(missing);
            footer.Validate(
                missing,
                (footer.StartOrPlayButton, "startOrPlayButton"),
                (footer.StatsButton, "statsButton"),
                (footer.OptionsButton, "optionsButton"),
                (footer.CreditsButton, "creditsButton"),
                (footer.ProgressionButton, "progressionButton"),
                (footer.AccountButton, "accountButton"),
                (footer.QuitButton, "quitButton"));
        }
        else
        {
            createUi.Validate(missing);
            footer.Validate(missing, (footer.AccountButton, "accountButton"));
        }

        return missing.Count == 0;
    }

    /// <summary>
    /// Copies references out of whichever screen-specific view <see cref="ValidateMenuUi"/> has
    /// already confirmed is assigned and complete. Fields the current screen's variant does not use
    /// stay null.
    /// </summary>
    private void ResolveUiReferences()
    {
        if (hubUi != null)
        {
            createNewButton = hubUi.CreateNewButton;
            loginLocalButton = hubUi.LoginLocalButton;
            onlineAccountButton = hubUi.OnlineAccountButton;
        }
        else
        {
            usernameInputField = createUi.UsernameInputField;
            messageDisplay = createUi.MessageDisplay;
            createAccountButton = createUi.CreateAccountButton;
        }

        mainMenuButton = footer.StartOrPlayButton;
        statsMenuButton = footer.StatsButton;
        progressionMenuButton = footer.ProgressionButton;
        creditsMenuButton = footer.CreditsButton;
        accountMenuButton = footer.AccountButton;
    }

    private void RegisterButtonCallbacks()
    {
        UiSelectionAdapter.RegisterButton(mainMenuButton, LoadStartMenu);
        UiSelectionAdapter.RegisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.RegisterButton(progressionMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.RegisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.RegisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.RegisterButton(createNewButton, LoadCreateNewAccount);
        UiSelectionAdapter.RegisterButton(loginLocalButton, LoadLoginLocal);
        UiSelectionAdapter.RegisterButton(onlineAccountButton, LoadOnlineAccount);
        UiSelectionAdapter.RegisterButton(createAccountButton, OnCreateAccountButtonClicked);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(mainMenuButton, LoadStartMenu);
        UiSelectionAdapter.UnregisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.UnregisterButton(progressionMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.UnregisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.UnregisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.UnregisterButton(createNewButton, LoadCreateNewAccount);
        UiSelectionAdapter.UnregisterButton(loginLocalButton, LoadLoginLocal);
        UiSelectionAdapter.UnregisterButton(onlineAccountButton, LoadOnlineAccount);
        UiSelectionAdapter.UnregisterButton(createAccountButton, OnCreateAccountButtonClicked);
    }

    private void OnCreateAccountButtonClicked()
    {
        createUser();
    }

    /// <summary>
    /// AUD-092 Phase 5B: native <see cref="TMP_InputField.onSubmit"/> registration, mirroring
    /// <c>CreditsManager.RegisterReportInputSubmit</c>'s idiom - fires on the UI Submit action while
    /// the field is focused and only moves EventSystem selection, never invokes an account operation
    /// itself. Uses a fixed, named instance method (not a lambda) so <c>RemoveListener</c> before
    /// <c>AddListener</c> actually matches the previous registration and repeated Enable/Disable
    /// cannot accumulate listeners.
    /// </summary>
    private void RegisterInputSubmitCallbacks()
    {
        RegisterInputSubmitCallback(usernameInputField, OnProfileNameInputSubmit);
    }

    private void UnregisterInputSubmitCallbacks()
    {
        UnregisterInputSubmitCallback(usernameInputField, OnProfileNameInputSubmit);
    }

    private static void RegisterInputSubmitCallback(TMP_InputField inputField, UnityAction<string> handler)
    {
        if (inputField == null)
        {
            return;
        }

        inputField.onSubmit.RemoveListener(handler);
        inputField.onSubmit.AddListener(handler);
    }

    private static void UnregisterInputSubmitCallback(TMP_InputField inputField, UnityAction<string> handler)
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(handler);
        }
    }

    private void OnProfileNameInputSubmit(string submittedText)
    {
        UiSelectionAdapter.TrySelect(createAccountButton != null ? createAccountButton.gameObject : null);
    }

    private GameObject GetDefaultSelectedButton()
    {
        if (EventSystem.current != null && EventSystem.current.firstSelectedGameObject != null)
        {
            return EventSystem.current.firstSelectedGameObject;
        }

        if (createNewButton != null)
        {
            return createNewButton.gameObject;
        }

        if (usernameInputField != null)
        {
            return usernameInputField.gameObject;
        }

        return mainMenuButton != null ? mainMenuButton.gameObject : null;
    }

    private void LoadStartMenu()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_start);
    }

    private void LoadStatsMenu()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_stats);
    }

    private void LoadProgressionMenu()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_progression);
    }

    private void LoadCreditsMenu()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_credits);
    }

    private void LoadAccountMenu()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account);
    }

    private void LoadCreateNewAccount()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_createNew);
    }

    private void LoadLoginLocal()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_loginLocal);
    }

    private void LoadOnlineAccount()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_online);
    }

    private void SetMessage(string message)
    {
        if (messageDisplay != null)
        {
            messageDisplay.text = message;
        }
    }

    private void RefreshInputValues()
    {
        if (usernameInputField != null)
        {
            profileNameInput = usernameInputField.text;
        }
    }

    /// <summary>
    /// AUD-092 Phase 5B section 13: guarded so a rapid double activation (double click, or a stray
    /// duplicate Submit/click while the first flow is still in progress) cannot start a second
    /// concurrent create flow. <see cref="isCreatingAccount"/> is cleared in
    /// <see cref="CreateUserCoroutineGuarded"/>'s <c>finally</c> on every exit path, so a failed
    /// attempt can always be retried.
    /// </summary>
    public void createUser()
    {
        if (isCreatingAccount)
        {
            return;
        }

        isCreatingAccount = true;
        StartCoroutine(CreateUserCoroutineGuarded());
    }

    private System.Collections.IEnumerator CreateUserCoroutineGuarded()
    {
        try
        {
            yield return CreateUserCoroutine();
        }
        finally
        {
            isCreatingAccount = false;
        }
    }

    /// <summary>
    /// Local-only profile creation: no email, password, or name fields, no server round trip. The
    /// userid is allocated by <see cref="DBHelper.CreateLocalProfileCoroutine"/> itself (atomic with
    /// the insert), so a duplicate name comes back as a deterministic failure from that one call
    /// rather than a separate asynchronous preflight check. Yields the coroutine directly (both types
    /// compile into Assembly-CSharp, so the internal member is reachable) rather than firing the
    /// public <see cref="DBHelper.CreateLocalProfile"/> wrapper and polling a captured variable -
    /// matches how every other async DB/API call in this class already chains via <c>yield return</c>.
    /// </summary>
    private System.Collections.IEnumerator CreateUserCoroutine()
    {
        RefreshInputValues();
        if (string.IsNullOrWhiteSpace(profileNameInput))
        {
            SetMessage("Enter a profile name.");
            yield break;
        }

        if (DBHelper.instance == null)
        {
            SetMessage("Local accounts are unavailable.");
            yield break;
        }

        SetMessage("creating profile...");
        ApiResult<UserModel> createResult = null;
        yield return DBHelper.instance.CreateLocalProfileCoroutine(profileNameInput, value => createResult = value);

        if (createResult == null || !createResult.Success)
        {
            SetMessage(createResult != null ? createResult.Error : "Could not create the local profile.");
            yield break;
        }

        GameOptions.userid = createResult.Value.Userid;
        GameOptions.userName = createResult.Value.UserName;
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
    }

    public static string MainMenuButtonName => mainMenuButtonName;
    public static string StatsMenuButtonName => statsMenuButtonName;
    public static string ProgressionMenuButtonName => progressionMenuButtonName;
    public static string CreditsMenuButtonName => creditsMenuButtonName;
    public static string CreateNewButtonName => createNewButtonName;
    public static string LoginLocalButtonName => loginLocalButtonName;
    public static string OnlineAccountButtonName => onlineAccountButtonName;
    public static string AccountMenuButtonName => accountMenuButtonName;
}
