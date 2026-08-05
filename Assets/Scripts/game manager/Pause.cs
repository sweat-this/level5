
using Assets.Scripts.database;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Pause : MonoBehaviour
{
    private const float DatabaseWaitTimeoutSeconds = 8f;
    // main flag
    [SerializeField]
    private bool paused;
    [SerializeField]
    private bool startOnPause = false;

    //fade texture to obscure background
    [SerializeField]
    private Image fadeTexture;

    // ui text
    [SerializeField]
    private Text loadSceneText;
    private Text loadStartScreenText;
    private Text cancelMenuText;
    private Text quitGameText;

    // pause options
    private Text toggleCameraText;
    private Text toggleUiStatsText;
    private Text toggleMaxStatsText;
    private Text toggleFpsText;

    const string toggleCameraName = "toggle_camera";
    const string toggleUiStatsName = "toggle_stats";
    const string toggleMaxStatsName = "toggle_max_stats";
    const string toggleFpsName = "toggle_fps";

    //ui buttons
    private Button loadSceneButton;
    private Button loadStartScreenButton;
    private Button cancelMenuButton;
    private Button quitGameButton;

    private AudioSource[] allAudioSources;
    private GameObject currentHighlightedButton;

    private GameObject maxStatsObject;
    private GameObject toggleFpsObject;
    private GameObject toggleUiStatsObject;
    private GameObject footer;
    private bool pauseMenuNavigationEnabled;
    private ProgressionService progressionService;
    private string freePlayProgressionResultId;

    public static Pause instance;

    void Awake()
    {
        instance = this;
        progressionService = new ProgressionService();
        freePlayProgressionResultId = MatchSession.EnsureCurrentMatch();
#if !UNITY_ANDROID
        if (!GameOptions.battleRoyalEnabled && !GameOptions.cageMatchEnabled)
        {
            startOnPause = true;
            paused = true;
            Time.timeScale = 0;
        }

#endif
        //startOnPause = false;
        paused = startOnPause;
        footer = GameObject.Find("footer");

        fadeTexture = GameObject.Find("fade_texture").GetComponent<Image>();
        //text
        loadSceneText = GameObject.Find("load_scene").GetComponent<Text>();
        cancelMenuText = GameObject.Find("cancel_menu").GetComponent<Text>();
        loadStartScreenText = GameObject.Find("load_start").GetComponent<Text>();
        quitGameText = GameObject.Find("quit_game").GetComponent<Text>();
        //buttons
        loadSceneButton = GameObject.Find("load_scene").GetComponent<Button>();
        loadStartScreenButton = GameObject.Find("load_start").GetComponent<Button>();
        cancelMenuButton = GameObject.Find("cancel_menu").GetComponent<Button>();
        quitGameButton = GameObject.Find("quit_game").GetComponent<Button>();

        //toggleCameraText = GameObject.Find(toggleCameraName).GetComponent<Text>();
        toggleUiStatsText = GameObject.Find(toggleUiStatsName).GetComponent<Text>();
        toggleMaxStatsText = GameObject.Find(toggleMaxStatsName).GetComponent<Text>();

        toggleFpsText = GameObject.Find(toggleFpsName).GetComponent<Text>();

//#if UNITY_ANDROID && !UNITY_EDITOR
//            controlsDesktopObject.SetActive(false);
//            controlsMobileObject.SetActive(true);
//#endif

#if UNITY_STANDALONE || UNITY_EDITOR
            //controlsDesktopObject.SetActive(true);
            //controlsMobileObject.SetActive(false);
            disableMobileOnlyPauseOptions();
#endif

        //}

        EventSystem.current.firstSelectedGameObject = loadSceneButton.gameObject;
        // init current button
        currentHighlightedButton = EventSystem.current.firstSelectedGameObject.gameObject;
        //disable joystick if active
    }

    private void Start()
    {
        // if game active, disable pause
        if (Time.timeScale == 1f)
        {
            setBackgroundFade(false);
            setPauseScreen(false);
        }
        if (startOnPause)
        {
            GameObject.Find("footer").SetActive(false);
        }
    }

    private void OnDisable()
    {
        DisablePauseMenuNavigation();
    }

    // Update is called once per frame
    void Update()
    {
        //pause ESC, submit, cancel
        if (//GameLevelManager.instance.Controls.UINavigation.Submit.triggered||
             GameLevelManager.instance.Controls.Player.cancel.triggered
            //|| GameLevelManager.Instance.Controls.Player.esc.triggered
            && !startOnPause
            && !GameLevelManager.instance.GameOver)
        {
            paused = TogglePause();
        }
        if(startOnPause && GameLevelManager.instance.Controls.Player.submit.triggered)
            //&& !GameOptions.battleRoyalEnabled 
            //&& !GameOptions.cageMatchEnabled)
        {
            StartGame();
        }
        // ===================== pause checks =======================
        if ((Time.timeScale == 0 && !paused) || (Time.timeScale == 1 && paused))
        {
            
            TogglePause();
        }
        //==========================================================
        // if paused, show pause menu
        if (paused)
        {

            // check for some button not selected
            //*this is a hack but it works patch for v3.0.1 : clicking mouse causing game to crash
            if (EventSystem.current.currentSelectedGameObject == null)
            {
                EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject); // + "_description";
            }
            currentHighlightedButton = EventSystem.current.currentSelectedGameObject; // + "_description";
            currentHighlightedButton.GetComponent<Button>().OnSelect(null);
            currentHighlightedButton.GetComponent<Button>().Select();

            // ================== pause menu options ==============================================================
            // reload scene
            if (currentHighlightedButton.name.Equals(loadSceneButton.name)
                && PlayerControlsProvider.MenuSubmitTriggered
                && ( GameOptions.gameModeSelectedId != 26 ))
            {
                reloadScene();
            }
            //load start screen
            if (currentHighlightedButton.name.Equals(loadStartScreenButton.name)
                && PlayerControlsProvider.MenuSubmitTriggered)
            {
                StartCoroutine(loadstartScreen());
            }
            // cancel
            bool gameOver = GameLevelManager.instance != null && GameLevelManager.instance.GameOver;
            if (currentHighlightedButton.name.Equals(cancelMenuButton.name)
                && PlayerControlsProvider.MenuSubmitTriggered
                && !gameOver)
            {
                TogglePause();
            }
            // quit
            if (currentHighlightedButton.name.Equals(quitGameButton.name)
                && PlayerControlsProvider.MenuSubmitTriggered)
            {
                StartCoroutine(Quit());
            }
        }
    }

    public void StartGame()
    {
        startOnPause = false;
        GameObject go = GameObject.Find("paused_start");
        go.SetActive(false);
        paused = TogglePause();
    }

    public void disableMobileOnlyPauseOptions()
    {
        // mobile buttons
        maxStatsObject = GameObject.Find(toggleMaxStatsName).gameObject;
        toggleFpsObject = GameObject.Find(toggleFpsName).gameObject;
        toggleUiStatsObject = GameObject.Find(toggleUiStatsName).gameObject;

        maxStatsObject.SetActive(false);
        toggleFpsObject.SetActive(false);
        toggleUiStatsObject.SetActive(false);
    }

    public IEnumerator Quit()
    {
        // update all time stats
        if (DBConnector.instance != null &&
           (GameOptions.gameModeSelectedName.ToLower().Contains("free") || GameOptions.gameModeSelectedId == 99))
        {
            updateFreePlayStats();
        }
        yield return WaitForDatabaseUnlock();
        QuitApplication();
    }

    public IEnumerator loadstartScreen()
    {
        // update all time stats
        if (DBConnector.instance != null &&
           (GameOptions.gameModeSelectedName.ToLower().Contains("free") || GameOptions.gameModeSelectedId == 99))
        {
            updateFreePlayStats();
        }
        if (DBConnector.instance != null)
        {
            yield return WaitForDatabaseUnlock();
            // load screen should be first scene in build
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        }
        else
        {
            // load screen should be first scene in build
            SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
        }
    }

    public void reloadScene()
    {
        // update all time stats
        if (DBConnector.instance != null
            && (GameOptions.gameModeSelectedName.ToLower().Contains("free") || GameOptions.gameModeSelectedId == 99))
        {
            updateFreePlayStats();
            //make sure new high scores (if any) are loaded
            PlayerData.instance.loadStatsFromDatabase();
        }
        // check if game still paused. on reload, game should be active
        if (paused)
        {
            TogglePause();
        }
        // load highscores before loading scene
        if (PlayerData.instance != null)
        {
            try
            {
                PlayerData.instance.loadStatsFromDatabase();
            }
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                return;
            }
        }
        MatchSession.BeginNewMatch();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void updateFreePlayStats()
    {
        //set time played to stopped
        GameRules.instance.setTimePlayed();
        // save free play stats
        // convert basketball stats to high score model
        HighScoreModel dBHighScoreModel = new HighScoreModel();
        HighScoreModel dBHighScoreModelTemp = new HighScoreModel();
        dBHighScoreModelTemp = dBHighScoreModel.convertBasketBallStatsToModel(GameLevelManager.instance.players);

        bool scoreSaved = DBConnector.instance.savePlayerGameStats(dBHighScoreModelTemp);
        if (!scoreSaved)
        {
            PendingMatchPersistenceStore.QueueScore(dBHighScoreModelTemp);
        }
        // update all time stats
        bool allTimeSaved = DBConnector.instance.savePlayerAllTimeStats(BasketBall.instance.GameStats);
        if (!allTimeSaved)
        {
            PendingMatchPersistenceStore.QueueAllTime(freePlayProgressionResultId, BasketBall.instance.GameStats);
        }
        if (progressionService == null)
        {
            progressionService = new ProgressionService();
        }

        MatchProgressionResult result = new MatchProgressionResult(
            freePlayProgressionResultId,
            GameOptions.characterId,
            BasketBall.instance.GameStats.ExperienceGained);
        progressionService.ApplyMatchResult(result);
    }

    private IEnumerator WaitForDatabaseUnlock()
    {
        float deadline = Time.realtimeSinceStartup + DatabaseWaitTimeoutSeconds;
        while (DBHelper.instance != null
            && DBHelper.instance.DatabaseLocked
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (DBHelper.instance != null && DBHelper.instance.DatabaseLocked)
        {
            Debug.LogWarning("Pause timed out waiting for the local database; continuing navigation.");
        }
    }

    private void setPauseScreen(bool value)
    {
        //// if ui stats enables, trn off
        //if (BasketBall.instance.UiStatsEnabled && paused)
        //{
        //    BasketBall.instance.toggleUiStats();
        //}

        loadSceneText.enabled = value;
        loadStartScreenText.enabled = value;
        quitGameText.enabled = value;
        cancelMenuText.enabled = value;

        loadSceneButton.enabled = value;
        loadStartScreenButton.enabled = value;
        cancelMenuButton.enabled = value;
        quitGameButton.enabled = value;
        //controlsObject.SetActive(value);
        //toggleCameraText.enabled = value;
        toggleFpsText.enabled = value;
        toggleMaxStatsText.enabled = value;
        toggleUiStatsText.enabled = value;
    }

    public bool TogglePause()
    {
        //Debug.Log("toggle pause");
        if (Time.timeScale == 0f)
        {
            //gameManager.instance.backgroundFade.SetActive(false);
            footer.SetActive(true);
            paused = false;
            Time.timeScale = 1f;
            setBackgroundFade(false);
            setPauseScreen(false);
            DisablePauseMenuNavigation();
            resumeAllAudio();

            if (GameLevelManager.instance.Joystick != null)
            {
                GameLevelManager.instance.Joystick.enabled = true;
            }
            return false;
        }
        else
        {
            //gameManager.instance.backgroundFade.SetActive(true);
            paused = true;
            Time.timeScale = 0f;
            pauseAllAudio();
            setBackgroundFade(true);
            setPauseScreen(true);
            EnablePauseMenuNavigation();

            if (GameLevelManager.instance.Joystick != null)
            {
                GameLevelManager.instance.Joystick.enabled = false;
            }
            return true;
        }
    }

    public void setTimeScaleToActive()
    {
        Time.timeScale = 1f;
    }

    public void setBackgroundFade(bool value)
    {
        fadeTexture.enabled = value;
    }

    public bool Paused
    {
        get => paused; set => paused = value;
    }
    public Button LoadSceneButton { get => loadSceneButton; set => loadSceneButton = value; }
    public Button LoadStartScreenButton { get => loadStartScreenButton; set => loadStartScreenButton = value; }
    public Button CancelMenuButton { get => cancelMenuButton; set => cancelMenuButton = value; }
    public Button QuitGameButton { get => quitGameButton; set => quitGameButton = value; }

    public static string ToggleCameraName => toggleCameraName;

    public static string ToggleUiStatsName => toggleUiStatsName;

    public static string ToggleMaxStatsName => toggleMaxStatsName;

    public static string ToggleFpsName => toggleFpsName;

    public bool StartOnPause { get => startOnPause; set => startOnPause = value; }

    private string getCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }

    void pauseAllAudio()
    {
        allAudioSources = FindObjectsByType<AudioSource>();

        foreach (AudioSource audioS in allAudioSources)
        {
            //audioS.Stop();
            audioS.Pause();
        }
    }

    void resumeAllAudio()
    {
        allAudioSources = FindObjectsByType<AudioSource>();

        foreach (AudioSource audioS in allAudioSources)
        {
            //audioS.Stop();
            audioS.UnPause();
        }
    }

    private void QuitApplication()
    {
        Application.Quit();
    }

    private void EnablePauseMenuNavigation()
    {
        if (pauseMenuNavigationEnabled)
        {
            return;
        }

        PlayerControlsProvider.EnableMenuMaps();
        pauseMenuNavigationEnabled = true;
    }

    private void DisablePauseMenuNavigation()
    {
        if (!pauseMenuNavigationEnabled)
        {
            return;
        }

        PlayerControlsProvider.DisableMenuMaps();
        pauseMenuNavigationEnabled = false;
    }

}
