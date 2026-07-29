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
    PlayerControls controls;
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

    bool buttonPressed;
    [SerializeField]
    bool emailAddressIsValid;
    [SerializeField]
    bool userNameIsValid;

    private void OnEnable()
    {
        controls.Player.Enable();
        controls.UINavigation.Enable();
        controls.Other.Enable();
        //controls.PlayerTouch.Enable();
    }
    private void OnDisable()
    {
        controls.Player.Disable();
        controls.UINavigation.Disable();
        controls.Other.Disable();
        //controls.PlayerTouch.Disable();
    }

    void Awake()
    {
        // mapped controls
        controls = new PlayerControls();
        apiConnector = UnityEngine.Object.FindAnyObjectByType<APIConnector>();
    }

    private void Start()
    {
        // if current scene is NOT account scene (with links to login scenes)
        if (!SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_00_account))
        {

            usernameInputField = GameObject.Find("UserNameInputField").GetComponent<InputField>();
            passwordInputField = GameObject.Find("PasswordInputField").GetComponent<InputField>();
            if (SceneManager.GetActiveScene().name.Equals(Constants.SCENE_NAME_level_00_account_createNew))
            {
                emailInputField = GameObject.Find("EmailInputField").GetComponent<InputField>();
                firstNameInputField = GameObject.Find("FirstNameInputField").GetComponent<InputField>();
                lastNameInputField = GameObject.Find("LastNameInputField").GetComponent<InputField>();
            }
            messageDisplay = GameObject.Find("messageDisplay").GetComponent<Text>();
            messageDisplay.text = "";
        }
    }

    void Update()
    {
        // check for some button not selected
        if (EventSystem.current != null)
        {
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject); // + "_description";
            }
        }

        if (controls.UINavigation.Submit.triggered && !buttonPressed)
        {
            buttonPressed = true;

            //check email
            if (EventSystem.current.currentSelectedGameObject.name.Equals(checkEmailButtonName))
            {
                EventSystem.current.SetSelectedGameObject(emailAddressTextButtonObject);
            }
            // check user name
            if (EventSystem.current.currentSelectedGameObject.name.Equals(checkUserNameButtonName))
            {
                EventSystem.current.SetSelectedGameObject(userNameTextButtonObject);
            }
            // enter email field
            if (EventSystem.current.currentSelectedGameObject.name.Equals(emailAddressInputFieldName))
            {
                EventSystem.current.SetSelectedGameObject(checkEmailButtonObject);
            }
            // enter username field
            if (EventSystem.current.currentSelectedGameObject.name.Equals(userNameInputFieldName))
            {
                EventSystem.current.SetSelectedGameObject(checkUserNameButtonObject);
            }
            // enter password field
            if (EventSystem.current.currentSelectedGameObject.name.Equals(passwordInputFieldName))
            {
                EventSystem.current.SetSelectedGameObject(passwordTextButtonObject);
            }
            // enter first name field
            if (EventSystem.current.currentSelectedGameObject.name.Equals(firstNameInputFieldName))
            {
                EventSystem.current.SetSelectedGameObject(firstNameTextButtonObject);
            }
            // enter last name field
            if (EventSystem.current.currentSelectedGameObject.name.Equals(lastNameInputFieldName))
            {
                EventSystem.current.SetSelectedGameObject(lastNameTextButtonObject);
            }
            // footer
            // main menu
            if (EventSystem.current.currentSelectedGameObject.name.Equals(mainMenuButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_start);
            }
            //stats
            if (EventSystem.current.currentSelectedGameObject.name.Equals(statsMenuButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_stats);
            }
            //progression
            if (EventSystem.current.currentSelectedGameObject.name.Equals(progressionMenuButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_progression);
            }
            //credits
            if (EventSystem.current.currentSelectedGameObject.name.Equals(creditsMenuButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_credits);
            }
            // create new
            if (EventSystem.current.currentSelectedGameObject.name.Equals(createNewButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_createNew);
            }
            // login existing
            if (EventSystem.current.currentSelectedGameObject.name.Equals(loginExistingButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_loginExisting);
            }
            // login local
            if (EventSystem.current.currentSelectedGameObject.name.Equals(loginLocalButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account_loginLocal);
            }
            // account
            if (EventSystem.current.currentSelectedGameObject.name.Equals(accountMenuButtonName))
            {
                SceneManager.LoadSceneAsync(Constants.SCENE_NAME_level_00_account);
            }
            buttonPressed = false;
        }
    }

    public void checkEmailAddressFormat()
    {
        if (!UtilityFunctions.IsValidEmail(emailInput))
        {
            emailAddressIsValid = false;
        }
        if (string.IsNullOrWhiteSpace(emailInput))
        {
            emailAddressIsValid = false;
        }
        else
        {
            emailAddressIsValid = true;
        }
        messageDisplay.text = getCheckEmailAddress();
    }

    public string getCheckEmailAddress()
    {
        messageDisplay.text = "";
        errorMessageUserName = "";
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

        //setErrorMessage();
        //messageDisplay.text = errorMessageEmail;
        if (string.IsNullOrEmpty(errorMessageUserName))
        {
            messageDisplay.text = errorMessageEmail;
        }
        else
        {
            messageDisplay.text = errorMessageUserName + errorMessageEmail;
        }
        return errorMessageEmail;
    }

    public void checkUserName()
    {
        // check if user already exists or null
        if (string.IsNullOrEmpty(userNameInput))
        {
            userNameIsValid = false;
        }
        if (APIHelper.UserNameExists(userNameInput))
        {
            userNameIsValid = false;
        }
        else
        {
            userNameIsValid = true;
        }
        messageDisplay.text = getCheckUserName();
    }

    public string getCheckUserName()
    {
        messageDisplay.text = "";
        errorMessageUserName = "";
        errorMessageEmail = "";

        // check if user already exists or null
        if (string.IsNullOrEmpty(userNameInput))
        {
            errorMessageUserName += "\nusername is empty or contains whitespace";
        }
        if (APIHelper.UserNameExists(userNameInput))
        {
            errorMessageUserName += "\nusername already exists";
        }
        if (!APIHelper.UserNameExists(userNameInput) && !string.IsNullOrWhiteSpace(userNameInput))
        {
            errorMessageUserName += "\nusername does not exist";
        }

        if (string.IsNullOrEmpty(errorMessageEmail))
        {
            messageDisplay.text = errorMessageUserName;
        }
        else
        {
            messageDisplay.text = errorMessageEmail + errorMessageUserName;
        }
        return errorMessageUserName;
    }

    public void createUser()
    {
        checkEmailAddressFormat();
        checkUserName();
        messageDisplay.text = getCheckEmailAddress() + getCheckUserName();

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
    public void LoginUser(string username)
    {
        StartCoroutine(LoginUserCoroutine(username));
    }

    private IEnumerator LoginUserCoroutine()
    {
        float startTime;
        float timeout = 10.0f;

        checkUserName();
        messageDisplay.text = getCheckUserName();
        UserModel user = APIHelper.GetUserByUserName(userNameInput);

        // 10 second time out for all internet calls is a good idea
        startTime = Time.time;

        yield return new WaitUntil(() => user != null || (Time.time > startTime + timeout));
        yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
        yield return new WaitUntil(() => !APIHelper.ApiLocked);

        StartCoroutine(APIHelper.PostToken(user));
        startTime = Time.time;

        // add 10 second timeout
        yield return new WaitUntil(() => APIHelper.BearerToken != null || (Time.time > startTime + timeout));

        // if local user doesnt exists, insert locally
        if (!DBHelper.instance.localUserExists(user))
        {
            DBHelper.instance.DatabaseLocked = false;
            // created on api, insert to local db
            DBHelper.instance.InsertUser(user);
        }
    }

    private IEnumerator LoginUserCoroutine(string username)
    {
        float startTime;
        float timeout = 10.0f;

        checkUserName();
        messageDisplay.text = getCheckUserName();
        UserModel user = APIHelper.GetUserByUserName(username);

        // 10 second time out for all internet calls is a good idea
        startTime = Time.time;

        yield return new WaitUntil(() => user != null || (Time.time > startTime + timeout));
        yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
        yield return new WaitUntil(() => !APIHelper.ApiLocked);

        StartCoroutine(APIHelper.PostToken(user));
        startTime = Time.time;

        // add 10 second timeout
        yield return new WaitUntil(() => APIHelper.BearerToken != null || (Time.time > startTime + timeout));

        // if local user doesnt exists, insert locally
        if (!DBHelper.instance.localUserExists(user))
        {
            DBHelper.instance.DatabaseLocked = false;
            // created on api, insert to local db
            DBHelper.instance.InsertUser(user);
        }
    }

    public void readEmailAddressInput(string s)
    {
        emailInput = emailInputField.text;
        Debug.Log(emailInput);
    }

    public void readUsernameInput(string s)
    {
        userNameInput = usernameInputField.text;
    }

    public void readPasswordInput(string s)
    {
        passwordInput = passwordInputField.text;
    }

    public void readFirstNameInput(string s)
    {
        firstNameInput = firstNameInputField.text;
    }
    public void readLastNameInput(string s)
    {
        lastNameInput = lastNameInputField.text;
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
