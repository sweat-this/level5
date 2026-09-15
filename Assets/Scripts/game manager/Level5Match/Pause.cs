
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

    [SerializeField] private PauseUiObjects ui;

    //fade texture to obscure background
    private Image fadeTexture;

    // ui text
    private Text loadSceneText;
    private Text loadStartScreenText;
    private Text cancelMenuText;
    private Text quitGameText;

    // pause options
    private Text toggleCameraText;
    private Text toggleUiStatsText;
    private Text toggleMaxStatsText;
    private Text toggleFpsText;

    // kept for TouchInputController's name-selected dispatch (out of scope: legacy touch
    // controller deletion is gated on device verification)
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
    private string freePlayProgressionResultId;

    public static Pause instance;

    /// <summary>AUD-012 Phase 2b Slice 63 dependency-cut fields - see <see cref="BindGameLevelManagerContext"/>.</summary>
    private Func<bool> hasGameLevelManagerReader;
    private Func<bool> cancelTriggeredReader;
    private Func<bool> submitTriggeredReader;
    private Func<bool> gameOverReader;
    private Action<bool> setJoystickEnabled;

    /// <summary>
    /// AUD-012 Phase 2b Slice 63: replaces this class's former direct <c>GameLevelManager.instance</c>/
    /// <c>GameRules.instance</c> reads, its last two loose <c>Assembly-CSharp</c> integration points
    /// (`DBConnector`/`PlayerData`/`DBHelper` remained at the time - a separate, deliberately deferred
    /// persistence blocker Slice 67 has since cut, see <see cref="BindPersistenceContext"/>). Called once
    /// from <c>GameLevelManager.Start()</c>, the same composition point that already binds
    /// <see cref="Timer"/>'s equivalent context - every delegate here resolves
    /// <c>GameLevelManager.instance</c>/<c>GameRules.instance</c> fresh on every call, matching every
    /// other adapter in this migration. None of the five are null-guarded at their call sites below,
    /// preserving every former unconditional dereference exactly - a <see cref="Pause"/> built without
    /// this binding (any direct-construction test) fails exactly as the former direct reads did when
    /// their singleton was absent.
    ///
    /// Slice 67 retired this call's original eighth/seventh/sixth parameters
    /// (<c>allParticipantsReader</c>, <c>primaryPlayerReader</c>, <c>setTimePlayed</c>): the whole Free
    /// Play persistence operation that consumed them moved wholesale into
    /// <see cref="BindPersistenceContext"/>'s <c>persistFreePlayStats</c>, so nothing in this class reads
    /// them anymore.
    /// </summary>
    public void BindGameLevelManagerContext(
        Func<bool> hasGameLevelManagerReader,
        Func<bool> cancelTriggeredReader,
        Func<bool> submitTriggeredReader,
        Func<bool> gameOverReader,
        Action<bool> setJoystickEnabled)
    {
        this.hasGameLevelManagerReader = hasGameLevelManagerReader;
        this.cancelTriggeredReader = cancelTriggeredReader;
        this.submitTriggeredReader = submitTriggeredReader;
        this.gameOverReader = gameOverReader;
        this.setJoystickEnabled = setJoystickEnabled;
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 67 dependency-cut fields - see <see cref="BindPersistenceContext"/>.
    ///
    /// Default to safe no-ops rather than null, unlike every other delegate field on this class. The
    /// former direct reads these replace (<c>DBConnector.instance != null</c>, etc.) were null-safe
    /// checks against a global static, independent of any scene's <c>GameLevelManager</c> - so a scene
    /// that authors its own inline <c>Pause</c> without a composed <c>GameLevelManager</c> (found in
    /// <c>minigame_racing.unity</c>, which uses its own <c>RacingGameManager</c> instead) previously hit
    /// these checks safely regardless. <see cref="BindPersistenceContext"/> is only ever called from
    /// <c>GameLevelManager.Start()</c>, so without one in the scene these fields are never bound; an
    /// unconditional dereference (the shape <see cref="BindGameLevelManagerContext"/>'s fields correctly
    /// use, since their former direct reads - <c>GameLevelManager.instance.Controls...</c> - already threw
    /// under the same condition) would turn a scene that used to work into a `NullReferenceException` on
    /// the pause menu's Quit/reload/load-start-screen actions.
    /// </summary>
    private Func<bool> hasDatabaseReader = () => false;
    private Func<bool> databaseLockedReader = () => false;
    private Func<bool> hasPlayerDataReader = () => false;
    private Action reloadPlayerData = () => { };
    private Action<string> persistFreePlayStats = resultId => { };

    /// <summary>
    /// AUD-012 Phase 2b Slice 67: replaces this class's former direct <c>DBConnector</c>/<c>DBHelper</c>/
    /// <c>PlayerData</c>/<c>HighScoreModel</c>/<c>PendingMatchPersistenceStore</c>/<c>ProgressionService</c>
    /// reads - the persistence-layer blocker Slice 63 explicitly deferred, the same kind of cut Slice 64
    /// made for <c>MatchHudPresenter</c> - so <see cref="Pause"/> can move out of <c>Assembly-CSharp</c>.
    /// Called once from <c>GameLevelManager.Start()</c>, alongside but separate from
    /// <see cref="BindGameLevelManagerContext"/>: that binds game-manager-cycle state, this binds the
    /// persistence-layer ownership boundary - a different concern, so a different bind call, mirroring
    /// <c>MatchHudPresenter.BindPersistenceContext</c>'s split.
    ///
    /// <paramref name="hasDatabaseReader"/> replaces every former <c>DBConnector.instance != null</c>
    /// check. <paramref name="databaseLockedReader"/> replaces the former
    /// <c>DBHelper.instance != null &amp;&amp; DBHelper.instance.DatabaseLocked</c> compound read as one
    /// delegate - the "DBHelper exists" half lives inside the adapter now, not as a second guard here.
    /// <paramref name="hasPlayerDataReader"/> replaces the former <c>PlayerData.instance != null</c>
    /// check at <see cref="reloadScene"/>'s second, already-guarded reload. <paramref name="reloadPlayerData"/>
    /// wraps the exact prior bare <c>PlayerData.instance.loadStatsFromDatabase()</c> dereference -
    /// deliberately not null-guarded inside the delegate itself, so <see cref="reloadScene"/>'s first,
    /// former-unguarded reload and its second, former-guarded reload keep their former distinct guarding
    /// at their own call sites rather than being deduplicated. <paramref name="persistFreePlayStats"/>
    /// wraps the entire former <see cref="updateFreePlayStats"/> operation - <c>GameRules.setTimePlayed</c>,
    /// the <c>HighScoreModel</c> conversion, the <c>DBConnector</c> score/all-time saves and their
    /// <c>PendingMatchPersistenceStore</c> queue fallbacks, and the <c>ProgressionService.ApplyMatchResult</c>
    /// call - moved wholesale into one <c>GameLevelManager</c> production adapter, in the same order,
    /// receiving this instance's own captured <see cref="freePlayProgressionResultId"/>.
    /// </summary>
    public void BindPersistenceContext(
        Func<bool> hasDatabaseReader,
        Func<bool> databaseLockedReader,
        Func<bool> hasPlayerDataReader,
        Action reloadPlayerData,
        Action<string> persistFreePlayStats)
    {
        this.hasDatabaseReader = hasDatabaseReader;
        this.databaseLockedReader = databaseLockedReader;
        this.hasPlayerDataReader = hasPlayerDataReader;
        this.reloadPlayerData = reloadPlayerData;
        this.persistFreePlayStats = persistFreePlayStats;
    }

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

    /// <summary>
    /// True once <see cref="ui"/> carries every reference this screen needs. <see cref="PauseUiObjects.Footer"/>
    /// is intentionally not required here - Pause only shows/hides it and already treats a missing
    /// footer as non-fatal, which this preserves. Callable from editor tooling as a pure check - it
    /// only reads an already-serialized reference.
    /// </summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        if (ui == null)
        {
            missing.Add("Pause.ui");
            return false;
        }

        ui.Validate(missing);
        return missing.Count == 0;
    }

    void Awake()
    {
        instance = this;
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

        // resolved from the serialized view, which ValidateMenuUi below has already confirmed is
        // complete for everything except the footer - Pause only shows/hides it, so a missing one
        // is logged but not fatal, same as before this migration (AUD-103).
        footer = ui != null ? ui.Footer : null;

        List<string> missing = new List<string>();
        if (!ValidateMenuUi(missing))
        {
            // a half-wired pause menu cannot be driven safely, and Update would throw on
            // every frame trying. fail loudly once, with the names, and stay out of the way.
            Debug.LogError(
                "Pause is disabled because it is missing required serialized UI references: "
                + string.Join(", ", missing.ToArray()),
                this);
            SceneTransition.RestoreTimeScale();
            enabled = false;
            return;
        }

        fadeTexture = ui.FadeTexture;
        //text
        loadSceneText = ui.LoadSceneText;
        cancelMenuText = ui.CancelMenuText;
        loadStartScreenText = ui.LoadStartScreenText;
        quitGameText = ui.QuitGameText;
        //buttons
        loadSceneButton = ui.LoadSceneButton;
        loadStartScreenButton = ui.LoadStartScreenButton;
        cancelMenuButton = ui.CancelMenuButton;
        quitGameButton = ui.QuitGameButton;

        //toggleCameraText = GameObject.Find(toggleCameraName).GetComponent<Text>();
        toggleUiStatsText = ui.ToggleUiStatsText;
        toggleMaxStatsText = ui.ToggleMaxStatsText;
        toggleFpsText = ui.ToggleFpsText;

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
        UiSelectionAdapter.EnsureInputSystemUiModule();
        RegisterPauseButtonCallbacks();
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

    private void OnEnable()
    {
        // Pause reads Controls.Player.submit (dismiss the start-on-pause screen) and
        // Controls.Player.cancel (toggle the pause menu) off the shared PlayerControls instance -
        // but nothing enabled that map. GameLevelManager only calls EnableOther(), and real player
        // input runs on the separate per-player instances from AcquireGameplayControls, which enable
        // Player on themselves. The only EnableGameplayMaps() caller in the project is
        // SniperCameraController, so outside sniper levels both of these actions were permanently
        // dead: every level started paused and the prompt could not be dismissed.
        //
        // Pause is a user of that map, so it acquires it for its own lifetime. The provider
        // ref-counts, so this composes with SniperCameraController rather than fighting it.
        PlayerControlsProvider.EnableGameplayMaps();

        // symmetric with OnDisable, so re-enabling the component does not leave the pause buttons
        // inert (the asymmetry AUD-102 found on the menu screens)
        if (loadSceneButton != null)
        {
            RegisterPauseButtonCallbacks();
        }
    }

    private void OnDisable()
    {
        UnregisterPauseButtonCallbacks();
        DisablePauseMenuNavigation();
        PlayerControlsProvider.DisableGameplayMaps();
    }

    // Update is called once per frame
    void Update()
    {
        //pause ESC, submit, cancel
        if (//GameLevelManager.instance.Controls.UINavigation.Submit.triggered||
             cancelTriggeredReader()
            //|| GameLevelManager.Instance.Controls.Player.esc.triggered
            && !startOnPause
            && !gameOverReader())
        {
            paused = TogglePause();
        }
        if(startOnPause && submitTriggeredReader())
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
        // if paused, keep a selection so navigation and submit always have a target
        //
        // This block used to also call OnSelect(null) and Select() on the selected button every
        // frame (AUD-099), which restarted the Selectable state transition on every frame the game
        // was paused and took selection ownership away from the EventSystem, and it dispatched the
        // four pause actions by comparing the selected object's name against each button under a
        // polled Submit (AUD-098) - which is why clicking a pause button did nothing. The actions
        // are registered on Button.onClick in RegisterPauseButtonCallbacks now.
        if (paused)
        {
            // check for some button not selected
            //*this is a hack but it works patch for v3.0.1 : clicking mouse causing game to crash
            UiSelectionAdapter.EnsureSelected(
                EventSystem.current != null ? EventSystem.current.firstSelectedGameObject : null);
            currentHighlightedButton = UiSelectionAdapter.CurrentSelected;
        }
    }

    /// <summary>
    /// Wires the four pause actions to their buttons so pointer, touch, keyboard and gamepad all
    /// reach them through the one route (AUD-098).
    ///
    /// Each handler checks both <c>paused</c> and <c>startOnPause</c>. `paused` alone is not enough:
    /// at the start-on-pause prompt `paused` is already true, and Start() skips
    /// <c>setPauseScreen(false)</c> because it only runs at timeScale 1 - so the buttons are still
    /// enabled and interactable, Update forces selection onto <c>load_scene</c>, and the UI module
    /// enables Submit independently of PlayerControlsProvider's counter. Without the second check a
    /// Submit or a mouse click at the "press start" prompt reloads the scene.
    /// </summary>
    private void RegisterPauseButtonCallbacks()
    {
        UiSelectionAdapter.RegisterButton(loadSceneButton, PressReloadScene);
        UiSelectionAdapter.RegisterButton(loadStartScreenButton, PressLoadStartScreen);
        UiSelectionAdapter.RegisterButton(cancelMenuButton, PressCancelMenu);
        UiSelectionAdapter.RegisterButton(quitGameButton, PressQuitGame);
    }

    private void UnregisterPauseButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(loadSceneButton, PressReloadScene);
        UiSelectionAdapter.UnregisterButton(loadStartScreenButton, PressLoadStartScreen);
        UiSelectionAdapter.UnregisterButton(cancelMenuButton, PressCancelMenu);
        UiSelectionAdapter.UnregisterButton(quitGameButton, PressQuitGame);
    }

    private void PressReloadScene()
    {
        // mode 26 has no reload, same guard the polled dispatch carried
        if (!paused || startOnPause || MatchRuntime.RawModeId == 26)
        {
            return;
        }

        reloadScene();
    }

    private void PressLoadStartScreen()
    {
        if (!paused || startOnPause)
        {
            return;
        }

        StartCoroutine(loadstartScreen());
    }

    private void PressCancelMenu()
    {
        bool gameOver = hasGameLevelManagerReader() && gameOverReader();
        if (!paused || startOnPause || gameOver)
        {
            return;
        }

        TogglePause();
    }

    private void PressQuitGame()
    {
        if (!paused || startOnPause)
        {
            return;
        }

        StartCoroutine(Quit());
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
        // mobile buttons - same objects toggleMaxStatsText/toggleFpsText/toggleUiStatsText already
        // reference, fetched here as plain GameObjects to SetActive(false)
        maxStatsObject = ui != null ? ui.ToggleMaxStatsObject : null;
        toggleFpsObject = ui != null ? ui.ToggleFpsObject : null;
        toggleUiStatsObject = ui != null ? ui.ToggleUiStatsObject : null;

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
        if (hasDatabaseReader() &&
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
        if (hasDatabaseReader() &&
           (MatchRuntime.ModeDisplayName.ToLower().Contains("free") || MatchRuntime.RawModeId == 99))
        {
            updateFreePlayStats();
        }
        if (hasDatabaseReader())
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
        if (hasDatabaseReader()
            && (MatchRuntime.ModeDisplayName.ToLower().Contains("free") || MatchRuntime.RawModeId == 99))
        {
            updateFreePlayStats();
            //make sure new high scores (if any) are loaded
            reloadPlayerData();
        }
        // check if game still paused. on reload, game should be active
        if (paused)
        {
            TogglePause();
        }
        // load highscores before loading scene
        if (hasPlayerDataReader())
        {
            try
            {
                reloadPlayerData();
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
        persistFreePlayStats(freePlayProgressionResultId);
    }

    private IEnumerator WaitForDatabaseUnlock()
    {
        float deadline = Time.realtimeSinceStartup + DatabaseWaitTimeoutSeconds;
        while (databaseLockedReader() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (databaseLockedReader())
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

            setJoystickEnabled(true);
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

            setJoystickEnabled(false);
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
