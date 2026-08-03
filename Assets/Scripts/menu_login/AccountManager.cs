using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Net;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class AccountManager : MonoBehaviour
{
    Text messageDisplay;
    string errorMessageEmail = "";
    string errorMessageUserName = "";

    UserModel user;
    APIConnector apiConnector;
    //buttonobject names
    const string checkEmailButtonName = "checkEmail";
    const string checkUserNameButtonName = "checkUserName";
    const string loginNameButtonName = "login";
    const string createUserNameButtonName = "createUser";
    //input field object names
    const string emailAddressInputFieldName = "EmailInputField";
    const string userNameInputFieldName = "UserNameInputField";
    const string passwordInputFieldName = "PasswordInputField";
    const string firstNameInputFieldName = "FirstNameInputField";
    const string lastNameInputFieldName = "LastNameInputField";
    // scene link buttons
    const string createNewButtonName = "createNew";
    const string loginExistingButtonName = "loginExisting";
    const string loginLocalButtonName = "loginLocal";
    // footer button names
    const string mainMenuButtonName = "press_start";
    const string statsMenuButtonName = "stats_menu";
    const string progressionMenuButtonName = "update_menu";
    const string creditsMenuButtonName = "credits_menu";
    const string accountMenuButtonName = "account_menu";

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

    [SerializeField] Button checkEmailButton;
    [SerializeField] Button checkUserNameButton;
    [SerializeField] Button mainMenuButton;
    [SerializeField] Button statsMenuButton;
    [SerializeField] Button progressionMenuButton;
    [SerializeField] Button creditsMenuButton;
    [SerializeField] Button accountMenuButton;
    [SerializeField] Button createNewButton;
    [SerializeField] Button loginExistingButton;
    [SerializeField] Button loginLocalButton;

    // button objects
    [SerializeField]
    GameObject emailAddressTextButtonObject;
    [SerializeField]
    GameObject checkEmailButtonObject;
    [SerializeField]
    GameObject userNameTextButtonObject;
    [SerializeField]
    GameObject checkUserNameButtonObject;
    [SerializeField]
    GameObject passwordTextButtonObject;
    [SerializeField]
    GameObject firstNameTextButtonObject;
    [SerializeField]
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
        apiConnector = UnityEngine.Object.FindAnyObjectByType<APIConnector>();
    }

    private void Start()
    {
        if (EventSystem.current == null)
        {
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

    private void ResolveUiReferences()
    {
        emailInputField = ResolveInputField(emailInputField, emailAddressInputFieldName);
        usernameInputField = ResolveInputField(usernameInputField, userNameInputFieldName);
        passwordInputField = ResolveInputField(passwordInputField, passwordInputFieldName);
        firstNameInputField = ResolveInputField(firstNameInputField, firstNameInputFieldName);
        lastNameInputField = ResolveInputField(lastNameInputField, lastNameInputFieldName);

        messageDisplay = ResolveText(messageDisplay, "messageDisplay");

        emailAddressTextButtonObject = ResolveGameObject(emailAddressTextButtonObject, emailAddressInputFieldName);
        userNameTextButtonObject = ResolveGameObject(userNameTextButtonObject, userNameInputFieldName);
        passwordTextButtonObject = ResolveGameObject(passwordTextButtonObject, passwordInputFieldName);
        firstNameTextButtonObject = ResolveGameObject(firstNameTextButtonObject, firstNameInputFieldName);
        lastNameTextButtonObject = ResolveGameObject(lastNameTextButtonObject, lastNameInputFieldName);
        checkEmailButtonObject = ResolveGameObject(checkEmailButtonObject, checkEmailButtonName);
        checkUserNameButtonObject = ResolveGameObject(checkUserNameButtonObject, checkUserNameButtonName);

        checkEmailButton = ResolveButton(checkEmailButton, checkEmailButtonName);
        checkUserNameButton = ResolveButton(checkUserNameButton, checkUserNameButtonName);
        mainMenuButton = ResolveButton(mainMenuButton, mainMenuButtonName);
        statsMenuButton = ResolveButton(statsMenuButton, statsMenuButtonName);
        progressionMenuButton = ResolveButton(progressionMenuButton, progressionMenuButtonName);
        creditsMenuButton = ResolveButton(creditsMenuButton, creditsMenuButtonName);
        accountMenuButton = ResolveButton(accountMenuButton, accountMenuButtonName);
        createNewButton = ResolveButton(createNewButton, createNewButtonName);
        loginExistingButton = ResolveButton(loginExistingButton, loginExistingButtonName);
        loginLocalButton = ResolveButton(loginLocalButton, loginLocalButtonName);
    }

    private Button ResolveButton(Button button, string buttonName)
    {
        if (button != null)
        {
            return button;
        }

        GameObject buttonObject = GameObject.Find(buttonName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private InputField ResolveInputField(InputField inputField, string inputFieldName)
    {
        if (inputField != null)
        {
            return inputField;
        }

        GameObject inputFieldObject = GameObject.Find(inputFieldName);
        return inputFieldObject != null ? inputFieldObject.GetComponent<InputField>() : null;
    }

    private Text ResolveText(Text text, string objectName)
    {
        if (text != null)
        {
            return text;
        }

        GameObject textObject = GameObject.Find(objectName);
        return textObject != null ? textObject.GetComponent<Text>() : null;
    }

    private GameObject ResolveGameObject(GameObject gameObjectReference, string objectName)
    {
        return gameObjectReference != null ? gameObjectReference : GameObject.Find(objectName);
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
        RegisterRequiredButtonCallback(checkEmailButton, SelectEmailInput);
        RegisterRequiredButtonCallback(checkUserNameButton, SelectUsernameInput);
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

    private void RegisterRequiredButtonCallback(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null || action == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
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
        RefreshInputValues();

        bool userNameExists = APIHelper.UserNameExists(userNameInput);
        userNameIsValid = !string.IsNullOrWhiteSpace(userNameInput)
            && !userNameExists;
        SetMessage(BuildCheckUserNameMessage(userNameExists));
    }

    public string getCheckUserName()
    {
        string message = BuildCheckUserNameMessage(APIHelper.UserNameExists(userNameInput));
        SetMessage(message);
        return message;
    }

    public void createUser()
    {
        RefreshInputValues();
        bool userNameExists = APIHelper.UserNameExists(userNameInput);
        emailAddressIsValid = UtilityFunctions.IsValidEmail(emailInput)
            && !string.IsNullOrWhiteSpace(emailInput);
        userNameIsValid = !string.IsNullOrWhiteSpace(userNameInput)
            && !userNameExists;
        SetMessage(BuildCheckEmailAddressMessage() + BuildCheckUserNameMessage(userNameExists));

        if (userNameIsValid && emailAddressIsValid)
        {
            UserModel user = new UserModel();

            user.Email = emailInput;
            user.UserName = userNameInput;
            user.Password = passwordInput;
            user.FirstName = firstNameInput;
            user.LastName = lastNameInput;
            user.IpAddress = GetExternalIpAdress();
            user.SignUpDate = DateTime.Now.ToString();
            user.LastLogin = DateTime.Now.ToString();

            apiConnector.CreateNewUser(user);
        }
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
        float startTime;
        float timeout = 10.0f;

        RefreshInputValues();
        SetMessage(BuildLoginUserNameMessage(userNameInput, APIHelper.UserNameExists(userNameInput)));
        UserModel user = APIHelper.GetUserByUserName(userNameInput);

        // 10 second time out for all internet calls is a good idea
        startTime = Time.time;

        yield return new WaitUntil(() => user != null || (Time.time > startTime + timeout));
        if (user == null)
        {
            yield break;
        }

        yield return new WaitUntil(IsDatabaseUnlocked);
        yield return new WaitUntil(() => !APIHelper.ApiLocked);

        // the server never returns the real password (see UsersApiController.HideUserDetails) -
        // authentication is done with what the player actually typed, not whatever GetUserByUserName
        // handed back.
        user.Password = passwordInput;
        StartCoroutine(APIHelper.PostToken(user));
        startTime = Time.time;

        // add 10 second timeout
        yield return new WaitUntil(() => APIHelper.BearerToken != null || (Time.time > startTime + timeout));

        // if local user doesnt exists, insert locally
        InsertLocalUserIfMissing(user);
    }

    private IEnumerator LoginUserCoroutine(string username, string password)
    {
        float startTime;
        float timeout = 10.0f;

        RefreshInputValues();
        SetMessage(BuildLoginUserNameMessage(username, APIHelper.UserNameExists(username)));
        UserModel user = APIHelper.GetUserByUserName(username);

        // 10 second time out for all internet calls is a good idea
        startTime = Time.time;

        yield return new WaitUntil(() => user != null || (Time.time > startTime + timeout));
        if (user == null)
        {
            yield break;
        }

        yield return new WaitUntil(IsDatabaseUnlocked);
        yield return new WaitUntil(() => !APIHelper.ApiLocked);

        // the server never returns the real password (see UsersApiController.HideUserDetails) -
        // this overload is used for auto-login right after registration, so the caller passes
        // along the password that was just typed on the sign-up form.
        user.Password = password;
        StartCoroutine(APIHelper.PostToken(user));
        startTime = Time.time;

        // add 10 second timeout
        yield return new WaitUntil(() => APIHelper.BearerToken != null || (Time.time > startTime + timeout));

        // if local user doesnt exists, insert locally
        InsertLocalUserIfMissing(user);
    }

    private static bool IsDatabaseUnlocked()
    {
        return DBHelper.instance != null && !DBHelper.instance.DatabaseLocked;
    }

    private static void InsertLocalUserIfMissing(UserModel user)
    {
        if (DBHelper.instance == null || user == null || DBHelper.instance.localUserExists(user))
        {
            return;
        }

        DBHelper.instance.DatabaseLocked = false;
        // created on api, insert to local db
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

    public string GetExternalIpAdress()
    {
        string pubIp = new WebClient().DownloadString("https://api.ipify.org");
        return pubIp;
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
