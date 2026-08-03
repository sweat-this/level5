using Assets.Scripts.Models;
using Assets.Scripts.restapi;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreditsManager : MonoBehaviour
{
    [SerializeField]
    public string currentHighlightedButton;

    //version text
    private Text versionText;

    private const string inputFieldButtonName = "ReportInputField";
    private const string submitReportButtonName = "submit_report";

    private const string webLinkMusic = "https://www.instagram.com/stustumaru/";
    private const string webLinkDevProgress = "https://www.instagram.com/patrickcharlez/";
    private const string webLinkDevWebSite = "http://sweatthis.com/highscores/";
    private const string webLinkGooglePLay = "https://play.google.com/store/apps/details?id=com.level5.level5";
    private const string webLinkItchIo = "https://skeleton-district.itch.io/level-5";
    private const string webLinkBugReportEmail = "mailto:levelfivegames@gmail.com?subject=BugReport";
    private const string webLinkNftCollection = "https://opensea.io/collection/level5dudes";
    private const string webLinkNftAirdrop = "http://www.skeletondistrict.com/nftairdrop/";

    //footer object names
    private const string mainMenuButtonName = "press_start";
    private const string statsMenuButtonName = "stats_menu";
    private const string optionsButtonName = "options";
    private const string quitButtonName = "quit_game";
    private const string optionsMenuButtonName = "options_menu";
    private const string creditsMenuButtonName = "credits_menu";
    private const string progressionMenuButtonName = "update_menu";
    private const string accountMenuButtonName = "account_menu";

    [SerializeField]
    private GameObject submitReportButtonObject;
    [SerializeField]
    string reportInput;
    [SerializeField]
    InputField reportInputField;

    [SerializeField] Button mainMenuButton;
    [SerializeField] Button statsMenuButton;
    [SerializeField] Button optionsButton;
    [SerializeField] Button optionsMenuButton;
    [SerializeField] Button creditsMenuButton;
    [SerializeField] Button progressionMenuButton;
    [SerializeField] Button accountMenuButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button submitReportButton;

    bool buttonPressed = false;

    private EventTrigger reportInputSubmitTrigger;
    private EventTrigger.Entry reportInputSubmitEntry;
    private bool initialized;

    public static CreditsManager instance;

    private void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
        if (initialized)
        {
            RegisterButtonCallbacks();
            RegisterReportInputSubmit();
        }
    }
    private void OnDisable()
    {
        UnregisterButtonCallbacks();
        UnregisterReportInputSubmit();
        PlayerControlsProvider.DisableMenuMaps();
    }

    void Awake()
    {
        instance = this;
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
        RegisterButtonCallbacks();
        RegisterReportInputSubmit();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        initialized = true;
    }

    private void Update()
    {
        GameObject selectedObject = UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        if (selectedObject == null)
        {
            return;
        }

        currentHighlightedButton = selectedObject.name;
    }

    private void ResolveUiReferences()
    {
        reportInputField = ResolveInputField(reportInputField, inputFieldButtonName);
        submitReportButton = ResolveButton(submitReportButton, submitReportButtonName);
        if (submitReportButton == null && submitReportButtonObject != null)
        {
            submitReportButton = submitReportButtonObject.GetComponent<Button>();
        }

        submitReportButtonObject = submitReportButton != null ? submitReportButton.gameObject : submitReportButtonObject;

        mainMenuButton = ResolveButton(mainMenuButton, mainMenuButtonName);
        statsMenuButton = ResolveButton(statsMenuButton, statsMenuButtonName);
        optionsButton = ResolveButton(optionsButton, optionsButtonName);
        optionsMenuButton = ResolveButton(optionsMenuButton, optionsMenuButtonName);
        creditsMenuButton = ResolveButton(creditsMenuButton, creditsMenuButtonName);
        progressionMenuButton = ResolveButton(progressionMenuButton, progressionMenuButtonName);
        accountMenuButton = ResolveButton(accountMenuButton, accountMenuButtonName);
        quitButton = ResolveButton(quitButton, quitButtonName);
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

    private void RegisterButtonCallbacks()
    {
        UiSelectionAdapter.RegisterButton(mainMenuButton, loadStartMenu);
        UiSelectionAdapter.RegisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.RegisterButton(optionsButton, LoadOptionsMenu);
        UiSelectionAdapter.RegisterButton(optionsMenuButton, LoadOptionsMenu);
        UiSelectionAdapter.RegisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.RegisterButton(progressionMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.RegisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.RegisterButton(quitButton, QuitGame);
        RegisterRequiredButtonCallback(submitReportButton, SubmitReportIfAllowed);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(mainMenuButton, loadStartMenu);
        UiSelectionAdapter.UnregisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.UnregisterButton(optionsButton, LoadOptionsMenu);
        UiSelectionAdapter.UnregisterButton(optionsMenuButton, LoadOptionsMenu);
        UiSelectionAdapter.UnregisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.UnregisterButton(progressionMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.UnregisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.UnregisterButton(quitButton, QuitGame);
        UiSelectionAdapter.UnregisterButton(submitReportButton, SubmitReportIfAllowed);
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

    private void RegisterReportInputSubmit()
    {
        if (reportInputField == null || submitReportButtonObject == null || reportInputSubmitEntry != null)
        {
            return;
        }

        reportInputSubmitTrigger = reportInputField.GetComponent<EventTrigger>();
        if (reportInputSubmitTrigger == null)
        {
            reportInputSubmitTrigger = reportInputField.gameObject.AddComponent<EventTrigger>();
        }

        reportInputSubmitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.Submit,
            callback = new EventTrigger.TriggerEvent()
        };
        reportInputSubmitEntry.callback.AddListener(SelectSubmitReportButton);
        reportInputSubmitTrigger.triggers.Add(reportInputSubmitEntry);
    }

    private void UnregisterReportInputSubmit()
    {
        if (reportInputSubmitTrigger != null && reportInputSubmitEntry != null)
        {
            reportInputSubmitTrigger.triggers.Remove(reportInputSubmitEntry);
        }

        reportInputSubmitEntry = null;
        reportInputSubmitTrigger = null;
    }

    private void SelectSubmitReportButton(BaseEventData eventData)
    {
        UiSelectionAdapter.TrySelect(submitReportButtonObject);
    }

    private GameObject GetDefaultSelectedButton()
    {
        if (EventSystem.current != null && EventSystem.current.firstSelectedGameObject != null)
        {
            return EventSystem.current.firstSelectedGameObject;
        }

        return mainMenuButton != null ? mainMenuButton.gameObject : null;
    }

    // ============================  footer options activate - load scene/stats/quit/etc ==============================

    public void loadStartMenu()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
    }

    private void LoadStatsMenu()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_stats);
    }

    private void LoadOptionsMenu()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_options);
    }

    private void LoadCreditsMenu()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_credits);
    }

    private void LoadProgressionMenu()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_progression);
    }

    private void LoadAccountMenu()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_account);
    }

    private void QuitGame()
    {
        Application.Quit();
    }


    // ============================  message display ==============================
    // used in this context to display if item is locked

    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        GameObject messageObject = GameObject.Find("messageDisplay");
        Text messageText = messageObject == null ? null : messageObject.GetComponent<Text>();
        if (messageText != null)
        {
            messageText.text = "";
        }
    }

    public void readReportInput(string s)
    {
        reportInput = reportInputField == null ? s : reportInputField.text;
    }

    public void OpenMusicSite()
    {
        Application.OpenURL(webLinkMusic);
    }
    public void OpenDevProgressSite()
    {
        Application.OpenURL(webLinkDevProgress);
    }
    public void OpenDevWebSite()
    {
        Application.OpenURL(webLinkDevWebSite);
    }
    public void OpenGooglePlaySite()
    {
        Application.OpenURL(webLinkGooglePLay);
    }
    public void OpenItchIoSite()
    {
        Application.OpenURL(webLinkItchIo);
    }
    public void OpenBugReportEmail()
    {
        Application.OpenURL(webLinkBugReportEmail);
    }
    public void OpenNftCollection()
    {
        Application.OpenURL(webLinkNftCollection);
    }
    public void OpenNftAirdrop()
    {
        Application.OpenURL(webLinkNftAirdrop);
    }
    private IEnumerator SubmitReportCoroutine()
    {
        UserReportModel userReportModel = new UserReportModel();
        userReportModel.Report = reportInput;
        if (reportInputField == null)
        {
            buttonPressed = false;
            yield break;
        }

        StartCoroutine(APIHelper.PostReport(userReportModel, reportInputField));

        yield return new WaitUntil(() => !APIHelper.ApiLocked);

        buttonPressed = false;
    }
    private void SubmitReport()
    {
        if (reportInputField != null)
        {
            reportInput = reportInputField.text;
        }

        buttonPressed = true;
        StartCoroutine(SubmitReportCoroutine());
    }

    private void SubmitReportIfAllowed()
    {
        if (!APIHelper.ApiLocked && !buttonPressed)
        {
            SubmitReport();
        }
    }

    public static string MainMenuButtonName => mainMenuButtonName;
    public static string StatsMenuButtonName => statsMenuButtonName;
    public static string OptionsButtonName => optionsButtonName;
    public static string QuitButtonName => quitButtonName;
    public static string OptionsMenuButtonName => optionsMenuButtonName;
    public static string CreditsMenuButtonName => creditsMenuButtonName;
    public static string ProgressionMenuButtonName => progressionMenuButtonName;
    public static string AccountMenuButtonName => accountMenuButtonName;
}
