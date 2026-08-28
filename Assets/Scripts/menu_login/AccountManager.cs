using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AccountManager : MonoBehaviour
{
    // Exactly one of these three is assigned per scene - level_00_account uses hubUi,
    // level_00_account_createNew uses createUi, level_00_account_loginExisting uses loginUi. Their
    // required controls differ enough (inspected against the actual scenes) that one shared type
    // would need optional fields most screens leave null; level_00_account_loginLocal has no
    // AccountManager at all, so there is no fourth variant to wire.
    [SerializeField] private AccountHubUiObjects hubUi;
    [SerializeField] private AccountCreateUiObjects createUi;
    [SerializeField] private AccountLoginUiObjects loginUi;
    [SerializeField] private MenuFooterUiObjects footer;

    TMP_Text messageDisplay;
    string errorMessageEmail = "";
    string errorMessageUserName = "";

    // kept for TouchInputAccountScreenController's name-selected dispatch (out of scope: legacy
    // touch controller deletion is gated on device verification)
    const string mainMenuButtonName = "press_start";
    const string statsMenuButtonName = "stats_menu";
    const string progressionMenuButtonName = "update_menu";
    const string creditsMenuButtonName = "credits_menu";
    const string accountMenuButtonName = "account_menu";
    const string createNewButtonName = "createNew";
    const string loginExistingButtonName = "loginExisting";
    const string loginLocalButtonName = "loginLocal";

    string emailInput;
    string userNameInput;
    string passwordInput;
    string firstNameInput;
    string lastNameInput;

    InputField emailInputField;
    InputField usernameInputField;
    InputField passwordInputField;
    InputField firstNameInputField;
    InputField lastNameInputField;

    Button checkEmailButton;
    Button checkUserNameButton;
    Button mainMenuButton;
    Button statsMenuButton;
    Button progressionMenuButton;
    Button creditsMenuButton;
    Button accountMenuButton;
    Button createNewButton;
    Button loginExistingButton;
    Button loginLocalButton;

    // button objects
    GameObject emailAddressTextButtonObject;
    GameObject checkEmailButtonObject;
    GameObject userNameTextButtonObject;
    GameObject checkUserNameButtonObject;
    GameObject passwordTextButtonObject;
    GameObject firstNameTextButtonObject;
    GameObject lastNameTextButtonObject;

    [SerializeField]
    bool emailAddressIsValid;
    [SerializeField]
    bool userNameIsValid;

    private EventTrigger emailInputSubmitTrigger;
    private EventTrigger.Entry emailInputSubmitEntry;
    private EventTrigger usernameInputSubmitTrigger;
    private EventTrigger.Entry usernameInputSubmitEntry;
    private EventTrigger passwordInputSubmitTrigger;
    private EventTrigger.Entry passwordInputSubmitEntry;
    private EventTrigger firstNameInputSubmitTrigger;
    private EventTrigger.Entry firstNameInputSubmitEntry;
    private EventTrigger lastNameInputSubmitTrigger;
    private EventTrigger.Entry lastNameInputSubmitEntry;
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

        if (usernameInputField != null
            && string.IsNullOrWhiteSpace(usernameInputField.text)
            && !string.IsNullOrWhiteSpace(GameOptions.userName)
            && !GameOptions.userName.Equals(UserAccountManager.GuestUsername, StringComparison.OrdinalIgnoreCase))
        {
            usernameInputField.text = GameOptions.userName;
            userNameInput = GameOptions.userName;
        }

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
    /// True once exactly one of <see cref="hubUi"/>/<see cref="createUi"/>/<see cref="loginUi"/>,
    /// plus <see cref="footer"/>, carry every reference the corresponding scene needs. Callable from
    /// editor tooling as a pure check - it only reads already-serialized references.
    /// </summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        int assigned = (hubUi != null ? 1 : 0) + (createUi != null ? 1 : 0) + (loginUi != null ? 1 : 0);
        if (assigned != 1)
        {
            missing.Add("AccountManager.hubUi/createUi/loginUi (exactly one must be assigned)");
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
        else if (createUi != null)
        {
            createUi.Validate(missing);
            footer.Validate(missing, (footer.AccountButton, "accountButton"));
        }
        else
        {
            loginUi.Validate(missing);
            footer.Validate(missing, (footer.AccountButton, "accountButton"));
        }

        return missing.Count == 0;
    }

    /// <summary>
    /// Copies references out of whichever screen-specific view <see cref="ValidateMenuUi"/> has
    /// already confirmed is assigned and complete. Replaces the <c>GameObject.Find(name)</c> chain
    /// this used to fall back to (AUD-103); fields the current screen's variant does not use stay
    /// null, matching what the old lookup already returned for them.
    /// </summary>
    private void ResolveUiReferences()
    {
        if (hubUi != null)
        {
            createNewButton = hubUi.CreateNewButton;
            loginExistingButton = hubUi.LoginExistingButton;
            loginLocalButton = hubUi.LoginLocalButton;
        }
        else if (createUi != null)
        {
            emailInputField = createUi.EmailInputField;
            usernameInputField = createUi.UsernameInputField;
            passwordInputField = createUi.PasswordInputField;
            firstNameInputField = createUi.FirstNameInputField;
            lastNameInputField = createUi.LastNameInputField;
            messageDisplay = createUi.MessageDisplay;
            checkEmailButton = createUi.CheckEmailButton;
            checkUserNameButton = createUi.CheckUserNameButton;
            emailAddressTextButtonObject = createUi.EmailAddressTargetObject;
            userNameTextButtonObject = createUi.UserNameTargetObject;
            passwordTextButtonObject = createUi.PasswordTargetObject;
            firstNameTextButtonObject = createUi.FirstNameTargetObject;
            lastNameTextButtonObject = createUi.LastNameTargetObject;
            checkEmailButtonObject = createUi.CheckEmailButtonObject;
            checkUserNameButtonObject = createUi.CheckUserNameButtonObject;
        }
        else
        {
            usernameInputField = loginUi.UsernameInputField;
            passwordInputField = loginUi.PasswordInputField;
            messageDisplay = loginUi.MessageDisplay;
            checkUserNameButton = loginUi.CheckUserNameButton;
            userNameTextButtonObject = loginUi.UserNameTargetObject;
            checkUserNameButtonObject = checkUserNameButton != null ? checkUserNameButton.gameObject : null;
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
        UiSelectionAdapter.RegisterButton(loginExistingButton, LoadLoginExisting);
        UiSelectionAdapter.RegisterButton(loginLocalButton, LoadLoginLocal);
        UiSelectionAdapter.RegisterButton(checkEmailButton, SelectEmailInput);
        UiSelectionAdapter.RegisterButton(checkUserNameButton, SelectUsernameInput);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(mainMenuButton, LoadStartMenu);
        UiSelectionAdapter.UnregisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.UnregisterButton(progressionMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.UnregisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.UnregisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.UnregisterButton(createNewButton, LoadCreateNewAccount);
        UiSelectionAdapter.UnregisterButton(loginExistingButton, LoadLoginExisting);
        UiSelectionAdapter.UnregisterButton(loginLocalButton, LoadLoginLocal);
        UiSelectionAdapter.UnregisterButton(checkEmailButton, SelectEmailInput);
        UiSelectionAdapter.UnregisterButton(checkUserNameButton, SelectUsernameInput);
    }

    private void RegisterInputSubmitCallbacks()
    {
        RegisterInputSubmitCallback(emailInputField, checkEmailButtonObject, ref emailInputSubmitTrigger, ref emailInputSubmitEntry);
        RegisterInputSubmitCallback(usernameInputField, checkUserNameButtonObject, ref usernameInputSubmitTrigger, ref usernameInputSubmitEntry);
        RegisterInputSubmitCallback(passwordInputField, passwordTextButtonObject, ref passwordInputSubmitTrigger, ref passwordInputSubmitEntry);
        RegisterInputSubmitCallback(firstNameInputField, firstNameTextButtonObject, ref firstNameInputSubmitTrigger, ref firstNameInputSubmitEntry);
        RegisterInputSubmitCallback(lastNameInputField, lastNameTextButtonObject, ref lastNameInputSubmitTrigger, ref lastNameInputSubmitEntry);
    }

    private void RegisterInputSubmitCallback(InputField inputField, GameObject targetObject, ref EventTrigger trigger, ref EventTrigger.Entry entry)
    {
        if (inputField == null || targetObject == null || entry != null)
        {
            return;
        }

        trigger = inputField.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = inputField.gameObject.AddComponent<EventTrigger>();
        }

        entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.Submit,
            callback = new EventTrigger.TriggerEvent()
        };
        entry.callback.AddListener((eventData) => UiSelectionAdapter.TrySelect(targetObject));
        trigger.triggers.Add(entry);
    }

    private void UnregisterInputSubmitCallbacks()
    {
        UnregisterInputSubmitCallback(ref emailInputSubmitTrigger, ref emailInputSubmitEntry);
        UnregisterInputSubmitCallback(ref usernameInputSubmitTrigger, ref usernameInputSubmitEntry);
        UnregisterInputSubmitCallback(ref passwordInputSubmitTrigger, ref passwordInputSubmitEntry);
        UnregisterInputSubmitCallback(ref firstNameInputSubmitTrigger, ref firstNameInputSubmitEntry);
        UnregisterInputSubmitCallback(ref lastNameInputSubmitTrigger, ref lastNameInputSubmitEntry);
    }

    private void UnregisterInputSubmitCallback(ref EventTrigger trigger, ref EventTrigger.Entry entry)
    {
        if (trigger != null && entry != null)
        {
            trigger.triggers.Remove(entry);
        }

        trigger = null;
        entry = null;
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

    private void SelectEmailInput()
    {
        UiSelectionAdapter.TrySelect(emailAddressTextButtonObject);
    }

    private void SelectUsernameInput()
    {
        UiSelectionAdapter.TrySelect(userNameTextButtonObject);
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

    private void LoadLoginExisting()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_loginExisting);
    }

    private void LoadLoginLocal()
    {
        SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_loginLocal);
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
        if (emailInputField != null)
        {
            emailInput = emailInputField.text;
        }
        if (usernameInputField != null)
        {
            userNameInput = usernameInputField.text;
        }
        if (passwordInputField != null)
        {
            passwordInput = passwordInputField.text;
        }
        if (firstNameInputField != null)
        {
            firstNameInput = firstNameInputField.text;
        }
        if (lastNameInputField != null)
        {
            lastNameInput = lastNameInputField.text;
        }
    }

    private string BuildCheckEmailAddressMessage()
    {
        errorMessageEmail = "";

        if (!UtilityFunctions.IsValidEmail(emailInput))
        {
            errorMessageEmail += "\nemail address is invalid format";
        }
        else
        {
            errorMessageEmail += "\nemail address is valid format";
        }
        if (string.IsNullOrWhiteSpace(emailInput))
        {
            errorMessageEmail += "\nemail address is empty or contains white space";
        }

        return errorMessageEmail;
    }

    private string BuildCheckUserNameMessage(bool userNameExists)
    {
        return BuildCheckUserNameMessage(userNameInput, userNameExists);
    }

    private string BuildCheckUserNameMessage(string username, bool userNameExists)
    {
        errorMessageUserName = "";

        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessageUserName += "\nusername is empty or contains whitespace";
        }
        if (userNameExists)
        {
            errorMessageUserName += "\nusername already exists";
        }
        if (!userNameExists && !string.IsNullOrWhiteSpace(username))
        {
            errorMessageUserName += "\nusername does not exist";
        }

        return errorMessageUserName;
    }

    private string BuildLoginUserNameMessage(string username, bool userNameExists)
    {
        errorMessageUserName = "";

        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessageUserName += "\nusername is empty or contains whitespace";
        }
        else if (!userNameExists)
        {
            errorMessageUserName += "\nusername does not exist";
        }

        return errorMessageUserName;
    }

    public void checkEmailAddressFormat()
    {
        RefreshInputValues();

        emailAddressIsValid = UtilityFunctions.IsValidEmail(emailInput)
            && !string.IsNullOrWhiteSpace(emailInput);
        SetMessage(BuildCheckEmailAddressMessage());
    }

    public string getCheckEmailAddress()
    {
        string message = BuildCheckEmailAddressMessage();
        SetMessage(message);
        return message;
    }

    public void checkUserName()
    {
        StartCoroutine(CheckUserNameCoroutine());
    }

    public string getCheckUserName()
    {
        checkUserName();
        return "checking username";
    }

    public void createUser()
    {
        StartCoroutine(CreateUserCoroutine());
    }

    private IEnumerator CheckUserNameCoroutine()
    {
        RefreshInputValues();
        if (string.IsNullOrWhiteSpace(userNameInput))
        {
            userNameIsValid = false;
            SetMessage(BuildCheckUserNameMessage(false));
            yield break;
        }

        SetMessage("checking username...");
        ApiResult<bool> result = null;
        yield return APIHelper.UserNameExists(userNameInput, value => result = value);
        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        if (result == null || !result.Success)
        {
            userNameIsValid = false;
            SetMessage(result != null ? result.Error : "Could not check the username.");
            yield break;
        }

        userNameIsValid = !result.Value;
        SetMessage(BuildCheckUserNameMessage(result.Value));
    }

    private IEnumerator CreateUserCoroutine()
    {
        RefreshInputValues();
        emailAddressIsValid = UtilityFunctions.IsValidEmail(emailInput)
            && !string.IsNullOrWhiteSpace(emailInput);
        if (!emailAddressIsValid || string.IsNullOrWhiteSpace(userNameInput) || string.IsNullOrEmpty(passwordInput))
        {
            string passwordMessage = string.IsNullOrEmpty(passwordInput) ? "\npassword is required" : string.Empty;
            SetMessage(BuildCheckEmailAddressMessage() + passwordMessage);
            yield break;
        }

        SetMessage("checking username...");
        ApiResult<bool> existsResult = null;
        yield return APIHelper.UserNameExists(userNameInput, value => existsResult = value);
        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        if (existsResult == null || !existsResult.Success)
        {
            SetMessage(existsResult != null ? existsResult.Error : "Could not check the username.");
            yield break;
        }

        userNameIsValid = !existsResult.Value;
        if (!userNameIsValid)
        {
            SetMessage(BuildCheckUserNameMessage(true));
            yield break;
        }

        UserModel newUser = new UserModel
        {
            Email = emailInput,
            UserName = userNameInput,
            Password = passwordInput,
            FirstName = firstNameInput,
            LastName = lastNameInput,
            IpAddress = string.Empty,
            SignUpDate = DateTime.UtcNow.ToString("o"),
            LastLogin = DateTime.UtcNow.ToString("o")
        };

        SetMessage("creating account...");
        ApiResult<UserModel> createResult = null;
        yield return APIHelper.PostUser(newUser, value => createResult = value);
        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        if (createResult == null || !createResult.Success)
        {
            SetMessage(createResult != null
                ? (createResult.StatusCode == 409 ? "username already exists" : createResult.Error)
                : "Could not create the account.");
            yield break;
        }

        newUser.Userid = createResult.Value != null ? createResult.Value.Userid : newUser.Userid;
        yield return LoginUserCoroutine(newUser.UserName, newUser.Password, newUser);
    }

    public void LoginUser()
    {
        StartCoroutine(LoginUserCoroutine());
    }
    public void LoginUser(string username, string password)
    {
        StartCoroutine(LoginUserCoroutine(username, password));
    }

    private IEnumerator LoginUserCoroutine()
    {
        RefreshInputValues();
        yield return LoginUserCoroutine(userNameInput, passwordInput, null);
    }

    private IEnumerator LoginUserCoroutine(string username, string password)
    {
        yield return LoginUserCoroutine(username, password, null);
    }

    private IEnumerator LoginUserCoroutine(string username, string password, UserModel knownUser)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            SetMessage("enter a username and password");
            yield break;
        }

        SetMessage("signing in...");
        UserModel loginUser = knownUser;
        if (loginUser == null)
        {
            ApiResult<UserModel> userResult = null;
            yield return APIHelper.GetUserByUserName(username, value => userResult = value);
            // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses
            // after the identical APIHelper callback pattern.
            if (userResult == null || !userResult.Success || userResult.Value == null)
            {
                SetMessage(userResult != null
                    ? (userResult.StatusCode == 404 ? "username does not exist" : userResult.Error)
                    : "Could not sign in.");
                yield break;
            }

            loginUser = userResult.Value;
        }

        loginUser.Password = password;
        ApiResult<string> tokenResult = null;
        yield return APIHelper.PostToken(loginUser, value => tokenResult = value, false);
        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        if (tokenResult == null || !tokenResult.Success)
        {
            SetMessage(tokenResult != null ? tokenResult.Error : "Could not sign in.");
            yield break;
        }

        yield return InsertLocalUserIfMissing(loginUser);
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
    }

    private static bool IsDatabaseUnlocked()
    {
        return DBHelper.instance != null && !DBHelper.instance.DatabaseLocked;
    }

    private static IEnumerator InsertLocalUserIfMissing(UserModel user)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (DBHelper.instance != null
            && DBHelper.instance.DatabaseLocked
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (DBHelper.instance == null
            || DBHelper.instance.DatabaseLocked
            || user == null
            || DBHelper.instance.localUserExists(user))
        {
            yield break;
        }

        DBHelper.instance.InsertUser(user);
    }

    public void readEmailAddressInput(string s)
    {
        emailInput = emailInputField == null ? s : emailInputField.text;
    }

    public void readUsernameInput(string s)
    {
        userNameInput = usernameInputField == null ? s : usernameInputField.text;
    }

    public void readPasswordInput(string s)
    {
        passwordInput = passwordInputField == null ? s : passwordInputField.text;
    }

    public void readFirstNameInput(string s)
    {
        firstNameInput = firstNameInputField == null ? s : firstNameInputField.text;
    }
    public void readLastNameInput(string s)
    {
        lastNameInput = lastNameInputField == null ? s : lastNameInputField.text;
    }

    public static string MainMenuButtonName => mainMenuButtonName;
    public static string StatsMenuButtonName => statsMenuButtonName;
    public static string ProgressionMenuButtonName => progressionMenuButtonName;
    public static string CreditsMenuButtonName => creditsMenuButtonName;
    public static string CreateNewButtonName => createNewButtonName;
    public static string LoginExistingButtonName => loginExistingButtonName;
    public static string LoginLocalButtonName => loginLocalButtonName;
    public static string AccountMenuButtonName => accountMenuButtonName;
}
