
using Assets.Scripts.database;
using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Level5.Core.Match;

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
    const string footerName = "footer";
    const string fadeTextureName = "fade_texture";
    const string loadSceneName = "load_scene";
    const string loadStartName = "load_start";
    const string cancelMenuName = "cancel_menu";
    const string quitGameName = "quit_game";

    /// <summary>
    /// Pause-menu objects every gameplay scene must provide. Level5ProjectValidator asserts
    /// these exist at build time so a rename fails the build instead of the play session.
    /// </summary>
    public static readonly string[] RequiredPauseObjectNames =
    {
        footerName,
        fadeTextureName,
        loadSceneName,
        loadStartName,
        cancelMenuName,
        quitGameName,
        toggleUiStatsName,
        toggleMaxStatsName,
        toggleFpsName
    };

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

    void Awake()
    {
        instance = this;
        progressionService = new ProgressionService();
        freePlayProgressionResultId = MatchSession.EnsureCurrentMatch();
#if !UNITY_ANDROID
        if (!MatchRuntime.Rules.IsBattleRoyal && !MatchRuntime.Rules.IsCageMatch)
        {
            startOnPause = true;
            paused = true;
            Time.timeScale = 0;
        }

#endif
        //startOnPause = false;
        paused = startOnPause;
        // the footer is only shown/hidden, so a missing one is logged but not fatal
        footer = SceneObjects.Find(footerName, this);

        // resolved one at a time and collected, so a missing pause-menu object is reported by
        // name instead of throwing partway through Awake and leaving Update to NRE every frame
        List<string> missing = new List<string>();
        fadeTexture = SceneObjects.Find<Image>(fadeTextureName, missing, this);
        //text
        loadSceneText = SceneObjects.Find<Text>(loadSceneName, missing, this);
        cancelMenuText = SceneObjects.Find<Text>(cancelMenuName, missing, this);
        loadStartScreenText = SceneObjects.Find<Text>(loadStartName, missing, this);
        quitGameText = SceneObjects.Find<Text>(quitGameName, missing, this);
        //buttons
        loadSceneButton = SceneObjects.Find<Button>(loadSceneName, missing, this);
        loadStartScreenButton = SceneObjects.Find<Button>(loadStartName, missing, this);
        cancelMenuButton = SceneObjects.Find<Button>(cancelMenuName, missing, this);
        quitGameButton = SceneObjects.Find<Button>(quitGameName, missing, this);

        //toggleCameraText = GameObject.Find(toggleCameraName).GetComponent<Text>();
        toggleUiStatsText = SceneObjects.Find<Text>(toggleUiStatsName, missing, this);
        toggleMaxStatsText = SceneObjects.Find<Text>(toggleMaxStatsName, missing, this);

        toggleFpsText = SceneObjects.Find<Text>(toggleFpsName, missing, this);

        if (missing.Count > 0)
        {
            // a half-wired pause menu cannot be driven safely, and Update would throw on
            // every frame trying. fail loudly once, with the names, and stay out of the way.
            Debug.LogError(
                "Pause is disabled because this scene is missing required pause-menu objects: "
                + string.Join(", ", missing.ToArray()),
                this);
            SceneTransition.RestoreTimeScale();
            enabled = false;
            return;
        }

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
        if (startOnPause && footer != null)
        {
            footer.SetActive(false);
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
            //&& !MatchRuntime.Rules.IsBattleRoyal 
            //&& !MatchRuntime.Rules.IsCageMatch)
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
                && ( MatchRuntime.RawModeId != 26 ))
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
        if (go != null)
        {
            go.SetActive(false);
        }
        paused = TogglePause();
    }

    public void disableMobileOnlyPauseOptions()
    {
        // mobile buttons
        maxStatsObject = SceneObjects.Find(toggleMaxStatsName, this);
        toggleFpsObject = SceneObjects.Find(toggleFpsName, this);
        toggleUiStatsObject = SceneObjects.Find(toggleUiStatsName, this);

        if (maxStatsObject == null || toggleFpsObject == null || toggleUiStatsObject == null)
        {
            return;
        }

        maxStatsObject.SetActive(false);
        toggleFpsObject.SetActive(false);
        toggleUiStatsObject.SetActive(false);
    }

    public IEnumerator Quit()
    {
        // update all time stats
        if (DBConnector.instance != null &&
           (MatchRuntime.ModeDisplayName.ToLower().Contains("free") || MatchRuntime.RawModeId == 99))
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
           (MatchRuntime.ModeDisplayName.ToLower().Contains("free") || MatchRuntime.RawModeId == 99))
        {
            updateFreePlayStats();
        }
        if (DBConnector.instance != null)
        {
            yield return WaitForDatabaseUnlock();
            // load screen should be first scene in build
            SceneTransition.LoadScene(Constants.SCENE_NAME_level_00_loading);
        }
        else
        {
            // load screen should be first scene in build
            SceneTransition.LoadScene(Constants.SCENE_NAME_level_00_loading);
        }
    }

    public void reloadScene()
    {
        // update all time stats
        if (DBConnector.instance != null
            && (MatchRuntime.ModeDisplayName.ToLower().Contains("free") || MatchRuntime.RawModeId == 99))
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
        SceneTransition.LoadScene(SceneManager.GetActiveScene().name);
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
            MatchRuntime.PrimaryCharacterId,
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
            if (footer != null)
            {
                footer.SetActive(true);
            }
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

    // kept because scene UnityEvents may be wired to it; the scene-exit paths
    // restore time scale through SceneTransition rather than calling this.
    public void setTimeScaleToActive()
    {
        SceneTransition.RestoreTimeScale();
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
        // the editor keeps running after a play-mode stop, so leave time flowing behind us
        SceneTransition.RestoreTimeScale();
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
