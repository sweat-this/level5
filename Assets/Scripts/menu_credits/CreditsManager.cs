using Assets.Scripts.Models;
using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CreditsManager : MonoBehaviour
{
    [SerializeField]
    public string currentHighlightedButton;

    [SerializeField] private CreditsUiObjects ui;
    [SerializeField] private MenuFooterUiObjects footer;

    //version text
    private Text versionText;

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

    GameObject submitReportButtonObject;
    [SerializeField]
    string reportInput;
    InputField reportInputField;

    Button mainMenuButton;
    Button statsMenuButton;
    Button optionsButton;
    Button optionsMenuButton;
    Button creditsMenuButton;
    Button progressionMenuButton;
    Button accountMenuButton;
    Button quitButton;
    Button submitReportButton;

    bool buttonPressed = false;

    private EventTrigger reportInputSubmitTrigger;
    private EventTrigger.Entry reportInputSubmitEntry;
    private bool initialized;

    public static CreditsManager instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

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

        List<string> missing = new List<string>();
        if (!ValidateMenuUi(missing))
        {
            Debug.LogError(
                "CreditsManager is missing required serialized UI references and will be disabled: "
                    + string.Join(", ", missing.ToArray()),
                this);
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

    /// <summary>
    /// True once <see cref="ui"/>/<see cref="footer"/> carry every reference this screen needs.
    /// creditsButton/progressionButton/accountButton are not required: they do not exist on
    /// creditsManager.prefab today (confirmed against the asset), so those three footer callbacks
    /// have always been unreachable from this screen - preserved as-is, not treated as a wiring gap.
    /// </summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        if (ui == null)
        {
            missing.Add("CreditsManager.ui");
        }
        else
        {
            ui.Validate(missing);
        }

        if (footer == null)
        {
            missing.Add("CreditsManager.footer");
        }
        else
        {
            footer.Validate(
                missing,
                (footer.StartOrPlayButton, "startOrPlayButton"),
                (footer.StatsButton, "statsButton"),
                (footer.QuitButton, "quitButton"));
        }

        return missing.Count == 0;
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

    /// <summary>
    /// Copies references out of the serialized <see cref="ui"/>/<see cref="footer"/> views, which
    /// <see cref="ValidateMenuUi"/> has already confirmed are complete. Replaces the
    /// <c>GameObject.Find(name)</c> chain this used to fall back to (AUD-103).
    /// </summary>
    private void ResolveUiReferences()
    {
        reportInputField = ui.ReportInputField;
        submitReportButton = ui.SubmitReportButton;
        submitReportButtonObject = ui.SubmitReportButtonObject;
        optionsButton = ui.OptionsButton;

        mainMenuButton = footer.StartOrPlayButton;
        statsMenuButton = footer.StatsButton;
        optionsMenuButton = footer.OptionsButton;
        creditsMenuButton = footer.CreditsButton;
        progressionMenuButton = footer.ProgressionButton;
        accountMenuButton = footer.AccountButton;
        quitButton = footer.QuitButton;
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
        UiSelectionAdapter.RegisterButton(submitReportButton, SubmitReportIfAllowed);
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

    /// <summary>
    /// "messageDisplay" is not part of the credits screen - it comes from the loading scene's
    /// prefab, same as the identical chain AUD-110 already fixed in StartManager - so it is resolved
    /// through <see cref="SceneObjects"/>, which reports the missing name instead of an unguarded
    /// <c>GameObject.Find(...).GetComponent&lt;Text&gt;()</c>.
    /// </summary>
    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Text messageText = SceneObjects.Find<Text>("messageDisplay", this);
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

        yield return APIHelper.PostReport(userReportModel, reportInputField);

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
