using Assets.Scripts.restapi;
using Assets.Scripts.Utility;
using Level5.Core.Match;
using Level5.Core.PlayerSelection;
using Level5.Core.Progression;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartManager : MonoBehaviour
{
    private const float DataWaitTimeoutSeconds = 12f;

    /// <summary>The level the campaign always starts on, whatever the menu had selected.</summary>
    private const int CampaignFirstLevelId = 1;

    [SerializeField]
    public string currentHighlightedButton;
    //list of all shooter profiles with player data
    [SerializeField]
    private List<CharacterProfile> playerSelectedData;
    [SerializeField]
    private List<CharacterProfile> cpuPlayerSelectedData;
    // list off friend profile data
    [SerializeField]
    private List<CheerleaderProfile> friendSelectedData;
    // list off level  data
    [SerializeField]
    private List<LevelSelected> levelSelectedData;
    //mode selected objects
    [SerializeField]
    private List<StartScreenModeSelected> modeSelectedData;

    //private Text playerSelectUnlockText;

    [SerializeField]
    private Text friendSelectUnlockText;

    // option select buttons, this will be disabled with touch input
    Button numPlayersSelectButton;
    Button levelSelectButton;
    Button trafficSelectButton;
    Button hardcoreSelectButton;
    Button enemySelectButton;
    Button sniperSelectButton;
    Button difficultySelectButton;
    Button obstacleSelectButton;
    Button playerSelectButton;
    Button friendSelectButton;
    Button modeSelectButton;
    Button cpu1OptionButton;
    Button cpu2OptionButton;
    Button cpu3OptionButton;
    [SerializeField] Button startButton;
    [SerializeField] Button statsMenuButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button optionsMenuButton;
    [SerializeField] Button creditsMenuButton;
    [SerializeField] Button updateMenuButton;
    [SerializeField] Button accountMenuButton;

    // player select rendering/state - owned by playerSelectCoordinator, not this class.
    // cpuSlotButtonObjects is the one array CPU slot 0/1/2 map to: TouchInputStartScreenController
    // resolves a slot by GameObject reference against it, and IndexOfCpuButtonName below resolves
    // a slot by name against the same objects' names - so there is one place that knows which
    // button is which slot, not a name array and a reference array kept in sync by hand.
    private PlayerSelectView playerSelectView;
    private GameObject[] cpuSlotButtonObjects = Array.Empty<GameObject>();

    // num player select display
    private Text numPlayersSelectOptionText;

    // level select display
    private Text levelSelectOptionText;
    private Text levelSelectOptionDescriptionText;

    //friend selected display
    private Text friendSelectOptionText;
    private Image friendSelectOptionImage;

    //mode selected display
    private Text modeSelectOptionText;
    private Text modeSelectOptionNameText;
    private Text ModeSelectOptionDescriptionText;

    //selectable option text
    [SerializeField]
    private Text trafficSelectOptionText;
    [SerializeField]
    private Text hardcoreSelectOptionText;
    [SerializeField]
    private Text enemySelectOptionText;
    [SerializeField]
    private Text sniperSelectOptionText;
    [SerializeField]
    private Text difficultySelectOptionText;
    [SerializeField]
    private Text difficultySelectOptionDescriptionText;
    [SerializeField]
    private Text obstacleSelectOptionText;

    //version text
    private Text versionText;
    private Text latestVersionText;
    private Text userNameText;

    //const object names
    public const string startButtonName = "press_start";
    public const string statsMenuButtonName = "stats_menu";
    public const string quitButtonName = "quit_game";
    public const string optionsMenuButtonName = "options_menu";
    public const string creditsMenuButtonName = "credits_menu";
    public const string updateMenuButtonName = "update_menu";
    public const string accountMenuButtonName = "account_menu";
    public const string updatePointsAvailable = "update_points_available";

    /// <summary>Shared message line, owned by the loading scene's prefab rather than this scene.</summary>
    private const string MessageDisplayObjectName = "messageDisplay";

    /// <summary>
    /// The footer command buttons this manager still resolves by name. Level5ProjectValidator
    /// asserts they exist in any scene carrying a StartManager, so a rename fails the build rather
    /// than the play session - the same contract GameRules, Pause and ProgressionManager carry
    /// (AUD-028, AUD-047, AUD-112).
    /// </summary>
    public static readonly string[] RequiredSceneObjectNames =
    {
        startButtonName,
        statsMenuButtonName,
        quitButtonName,
        optionsMenuButtonName,
        creditsMenuButtonName,
        updateMenuButtonName,
        accountMenuButtonName
    };

    public const string playerSelectButtonName = "player_select";
    public const string playerSelectOptionButtonName = "player_selected_name";
    public const string playerSelectStatsObjectName = "player_selected_stats_numbers";
    public const string playerSelectImageObjectName = "player_selected_image";
    //public const string playerSelectUnlockObjectName = "player_selected_unlock";
    //public const string playerSelectIsLockedObjectName = "player_selected_lock_texture";
    public const string playerSelectStatsCategoryName = "player_selected_stats_category";

    public const string playerProgressionName = "player_progression";
    public const string playerProgressionStatsName = "player_progression_stats";

    public const string cpuSelectButtonName = "cpu_select";
    public const string cpuSelectOptionButtonName = "cpu_selected_name";
    //friend objects
    public const string friendSelectButtonName = "friend_select";
    public const string friendSelectOptionButtonName = "friend_selected_name";
    public const string friendSelectImageObjectName = "friend_selected_image";
    public const string friendSelectUnlockObjectName = "friend_selected_unlock";
    public const string friendSelectIsLockedObjectName = "friend_selected_lock_texture";

    //level objects
    public const string levelSelectButtonName = "level_select";
    public const string levelSelectOptionButtonName = "level_selected_name";

    //level objects
    public const string numPlayersSelectButtonName = "num_players_select";
    public const string numPlayersSelectOptionButtonName = "num_players_selected_name";

    //mode objects
    public const string modeSelectButtonName = "mode_select";
    public const string modeSelectOptionButtonName = "mode_selected_name";
    public const string modeSelectDescriptionObjectName = "mode_selected_description";

    //traffic objects
    public const string trafficSelectButtonName = "traffic_select";
    public const string trafficSelectOptionName = "traffic_select_option";

    //hardcore mode
    public const string hardcoreSelectButtonName = "hardcore_select";
    public const string hardcoreSelectOptionName = "hardcore_select_option";

    //hardcore mode
    public const string enemySelectButtonName = "enemy_select";
    public const string enemySelectOptionName = "enemy_select_option";

    //sniper
    public const string sniperSelectButtonName = "sniper_select";
    public const string sniperSelectOptionName = "sniper_select_option";
    //difficulty
    public const string difficultySelectButtonName = "difficulty_select";
    public const string difficultySelectOptionName = "difficulty_select_option";
    public const string difficultySelectDescriptionName = "difficulty_selected_description";
    //obstacle
    public const string obstacleSelectButtonName = "obstacle_select";
    public const string obstacleSelectOptionName = "obstacle_select_option";
    //options
    public const string optionsSelectButtonName = "options_select";
    public const string optionsSelectOptionName = "options_selected_name";

    public const string Cpu1SelectOptionName = "cpu1_button";
    public const string Cpu2SelectOptionName = "cpu2_button";
    public const string Cpu3SelectOptionName = "cpu3_button";

    /// <summary>
    /// Everything the player has picked, except player/CPU selection. The menu changes this and
    /// nothing else; the match configuration is built from it once, when start is pressed.
    /// </summary>
    private readonly StartMenuSelectionState selection = new StartMenuSelectionState();

    /// <summary>
    /// The player-selection subsystem: roster draft, cycling, launch validation and rendering.
    /// <see cref="StartManager"/> no longer owns selected human/CPU indices or player-select render
    /// methods - it composes this and the rest of the menu.
    /// </summary>
    private readonly PlayerSelectCoordinator playerSelectCoordinator = new PlayerSelectCoordinator();

    /// <summary>
    /// The authoritative account unlock answer for the current session, built once the loaded
    /// profile/level data is available and rebuilt whenever <see cref="InitializeDisplay"/> reruns.
    /// Cycling, player-select projection and launch validation all read this same snapshot instead
    /// of each deciding unlock state independently.
    ///
    /// Starts null rather than <see cref="UnlockSnapshot.Empty"/> deliberately: null means "not
    /// gated yet" to <see cref="LevelEligibility"/>/<see cref="MatchConfigurationBuilder"/> (the
    /// previous, permissive mode/arena-only behavior), where <c>Empty</c> would mean "every level
    /// and character is locked". Some cycling entry points
    /// (<c>TouchInputStartScreenController</c>'s swipe handlers call <see cref="changeSelectedLevelUp"/>/
    /// <see cref="changeSelectedModeUp"/> directly) do not go through <see cref="HasLoadedGameSetup"/>,
    /// so an input during the brief window between the level catalog being ready and this session's
    /// real snapshot being built must not appear to freeze the menu.
    /// </summary>
    private UnlockSnapshot unlockSnapshot;

    /// <summary>Read by the touch input controller to identify the primary select control by reference, not by name.</summary>
    public PlayerSelectCoordinator PlayerSelect => playerSelectCoordinator;

    public GameObject PrimarySelectButtonObject => playerSelectButton != null ? playerSelectButton.gameObject : null;

    public IReadOnlyList<GameObject> CpuSlotButtonObjects => cpuSlotButtonObjects;

    private bool trafficEnabled { get => selection.TrafficEnabled; set => selection.TrafficEnabled = value; }
    private bool hardcoreEnabled { get => selection.HardcoreEnabled; set => selection.HardcoreEnabled = value; }
    private bool enemiesEnabled { get => selection.EnemiesEnabled; set => selection.EnemiesEnabled = value; }
    private bool obstaclesEnabled { get => selection.ObstaclesEnabled; set => selection.ObstaclesEnabled = value; }
    private int levelSelectedIndex { get => selection.LevelIndex; set => selection.LevelIndex = value; }
    private int modeSelectedIndex { get => selection.ModeIndex; set => selection.ModeIndex = value; }
    private int friendSelectedIndex { get => selection.FriendIndex; set => selection.FriendIndex = value; }

    /// <summary>Read by the touch input controller, so it stays public.</summary>
    public int difficultySelected
    {
        get => MatchDifficulties.ToInt(selection.Difficulty);
        set => selection.Difficulty = MatchDifficulties.FromInt(value);
    }

    private bool sniperBulletEnabled => selection.Sniper == SniperMode.Bullet;
    private bool sniperLaserEnabled => selection.Sniper == SniperMode.Laser;
    private bool sniperBulleAutoEnabled => selection.Sniper == SniperMode.MachineGun;

    /// <summary>The compatibility service and builder for the catalogs this menu is showing.</summary>
    private GameModeCompatibility Compatibility => MatchCatalogs.Compatibility;

    /// <summary>
    /// The current modifiers, in the shape player select needs to evaluate character capability.
    /// Passed to <see cref="PlayerSelectCoordinator.SetMatchContext"/> whenever mode or the
    /// enemies-only modifier changes, so cycling and required-CPU reconciliation see the same
    /// context the launch builder will.
    /// </summary>
    private MatchModifiers CurrentModifiers()
    {
        return new MatchModifiers(
            difficulty: selection.Difficulty,
            trafficRequested: selection.TrafficEnabled,
            enemiesRequested: selection.EnemiesEnabled,
            obstaclesRequested: selection.ObstaclesEnabled,
            sniper: selection.Sniper,
            hardcoreRequested: selection.HardcoreEnabled);
    }

    //private int numOfPlayers; //testing with 1

    [SerializeField]
    public static StartManager instance;

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

    bool buttonPressed = false;
    bool dataLoaded = false;
    bool experienceRefreshed = false;
    bool initialized = false;
    int lastCommandFrame = -1;
    int lastLoadGameFrame = -1;
    int lastOptionFrame = -1;

    private void OnEnable()
    {
        PlayerControlsProvider.EnableMenuMaps();
        if (initialized)
        {
            RegisterButtonCallbacks();
        }
    }
    private void OnDisable()
    {
        UnregisterButtonCallbacks();
        PlayerControlsProvider.DisableMenuMaps();
    }

    //private Text gameModeSelectText;
    void Awake()
    {
        instance = this;
        GameOptions.gameModeHasBeenSelected = false;
        StartCoroutine(getLoadedData());
        // find all button / text / etc and assign to variables
        StartCoroutine(GetUiObjectReferences());

        //default index for selected configuration
        selection.LoadPersistedPreferences();

        // update experience and levels
        // recommended here because experience will be gained after every game played
        StartCoroutine(UpdateLevelAndExperienceFromDatabase());
    }

    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(InitializeStartMenu());
    }

    private IEnumerator InitializeStartMenu()
    {
        //UtilityFunctions.GetCurrentDeviceHour();
        yield return WaitForCondition(() => EventSystem.current != null);
        if (EventSystem.current == null)
        {
            Debug.LogError("StartManager could not find an EventSystem for the start menu.");
            enabled = false;
            yield break;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();
        ResolveCommandButtonReferences();
        RegisterButtonCallbacks();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        StartCoroutine(InitializeDisplay());
        StartCoroutine(SetVersion());
        AnaylticsManager.MenuStartLoaded();
        initialized = true;
    }

    /// <summary>
    /// Keeps a valid selection and repaints the tab that matches it.
    ///
    /// This used to be wrapped in `#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_EDITOR_OSX`, so the
    /// start menu ran a materially different interaction model on device than in the editor. Inside
    /// it, ShouldUseManualMenuInput gated a second navigation and option-cycling implementation that
    /// polled UINavigation directly (AUD-097): it stayed off until the player pressed Submit on a
    /// control, then latched on for that control - and while latched, Up/Down both moved the
    /// selection (via the InputSystemUIInputModule, which consumes the same press) and changed the
    /// value. Nothing showed the player which mode a control was in.
    ///
    /// Everything that block reached is registered on Button.onClick in RegisterButtonCallbacks and
    /// runs through RunOptionAction/RunCommand, so it is now available to pointer, touch, keyboard
    /// and gamepad on every platform through one route. Cycling steps forward only; every catalog
    /// wraps, so nothing became unreachable.
    ///
    /// The display refresh below also ran every frame, and ran the level-select and mode-select
    /// blocks twice per frame because they were pasted in twice (AUD-109). It now runs when the
    /// selection actually changes.
    /// </summary>
    void Update()
    {
        GameObject selectedObject = UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        if (selectedObject == null)
        {
            return;
        }

        string selectedName = selectedObject.name;
        if (selectedName != currentHighlightedButton)
        {
            currentHighlightedButton = selectedName;
            RefreshDisplayForSelection();
        }

        // Submit fallback. Removing the old polled block (AUD-097) left the InputSystemUIInputModule
        // as the only route to the selected button, so anything that stops the module resolving its
        // submit action - a scene whose EventSystem never got one configured, the StandaloneInputModule
        // fallback path - leaves press_start unreachable with no way to start a game.
        //
        // This is safe to keep alongside the module because every action it can reach goes through
        // RunCommand/RunOptionAction, which drop a second call in the same frame via
        // lastCommandFrame/lastOptionFrame. That guard is what made the original double-invoke a
        // non-issue, and it is what makes this belt-and-braces rather than a double actuation.
        if (PlayerControlsProvider.MenuSubmitTriggered)
        {
            UiSelectionAdapter.TryInvokeSelectedButton(GetDefaultSelectedButton());
        }

        // Change-driven: this is cheap when nothing changed and only rebuilds text/portraits when
        // selection, focus or mode context actually did.
        playerSelectCoordinator.RenderIfNeeded();
    }

    private void RefreshDisplayForSelection()
    {
        if (string.IsNullOrEmpty(currentHighlightedButton))
        {
            return;
        }

        if ((currentHighlightedButton.Equals(numPlayersSelectButtonName) || currentHighlightedButton.Equals(numPlayersSelectOptionButtonName))
            && dataLoaded)
        {
            initializeNumPlayersDisplay();
        }
        // if player highlighted, focus the primary select control so its stats stay current
        if ((currentHighlightedButton.Equals(playerSelectButtonName) || currentHighlightedButton.Equals(playerSelectOptionButtonName))
            && dataLoaded)
        {
            FocusPlayersTab();
        }
        if (currentHighlightedButton.Equals(friendSelectButtonName) || currentHighlightedButton.Equals(FriendSelectOptionButtonName))
        {
            initializefriendDisplay();
        }
        if (currentHighlightedButton.Equals(levelSelectButtonName) || currentHighlightedButton.Equals(levelSelectOptionButtonName))
        {
            initializeLevelDisplay();
        }
        if (currentHighlightedButton.Equals(modeSelectButtonName) || currentHighlightedButton.Equals(modeSelectOptionButtonName))
        {
            initializeModeDisplay();
        }
        if (currentHighlightedButton.Equals(cpuSelectButtonName) || currentHighlightedButton.Equals(cpuSelectOptionButtonName))
        {
            initializeCpuDisplay();
        }
        FocusHighlightedCpuSlot();
        if (currentHighlightedButton.Equals(optionsSelectButtonName) || currentHighlightedButton.Equals(optionsSelectOptionName))
        {
            initializeOptionsDisplay();
        }
    }

    /// <summary>
    /// Activates the players tab and focuses the primary select control. Wrapped in try/catch like
    /// the render method this replaced (initializePlayerDisplay) - it runs from Update() every
    /// frame the control is highlighted, outside of RunOptionAction's own exception handling.
    /// </summary>
    private void FocusPlayersTab()
    {
        try
        {
            disableMenuObjects("players_tab");
            enableMenuObjects("players_tab");
            playerSelectCoordinator.FocusPrimary();
        }
        catch (Exception e)
        {
            Debug.LogError("ERROR : " + e);
        }
    }

    /// <summary>Focuses whichever CPU slot is currently highlighted, if any, so its stats become the shown ones.</summary>
    private void FocusHighlightedCpuSlot()
    {
        int slot = IndexOfCpuButtonName(currentHighlightedButton);
        if (slot >= 0)
        {
            playerSelectCoordinator.FocusCpu(slot);
        }
    }

    /// <summary>
    /// Routes an up/down navigation trigger on a CPU slot control to the one cycling command,
    /// instead of three near-identical methods keyed to Cpu1Index/Cpu2Index/Cpu3Index.
    /// </summary>
    private void CycleHighlightedCpuSlot(string highlightedButton, int step)
    {
        int slot = IndexOfCpuButtonName(highlightedButton);
        if (slot < 0)
        {
            return;
        }

        if (step > 0)
        {
            playerSelectCoordinator.SelectNextCpu(slot);
        }
        else
        {
            playerSelectCoordinator.SelectPreviousCpu(slot);
        }
    }

    private int IndexOfCpuButtonName(string buttonName)
    {
        for (int i = 0; i < cpuSlotButtonObjects.Length; i++)
        {
            if (cpuSlotButtonObjects[i] != null && cpuSlotButtonObjects[i].name.Equals(buttonName))
            {
                return i;
            }
        }

        return -1;
    }

    private void ResolveCommandButtonReferences()
    {
        startButton = ResolveButton(startButton, startButtonName);
        statsMenuButton = ResolveButton(statsMenuButton, statsMenuButtonName);
        quitButton = ResolveButton(quitButton, quitButtonName);
        optionsMenuButton = ResolveButton(optionsMenuButton, optionsMenuButtonName);
        creditsMenuButton = ResolveButton(creditsMenuButton, creditsMenuButtonName);
        updateMenuButton = ResolveButton(updateMenuButton, updateMenuButtonName);
        accountMenuButton = ResolveButton(accountMenuButton, accountMenuButtonName);
    }

    private Button ResolveButton(Button button, string buttonName)
    {
        if (button != null && button.gameObject.scene.IsValid())
        {
            return button;
        }

        // AUD-103: the fallback here used to be Resources.FindObjectsOfTypeAll<Button>(), which
        // walks every Button loaded in the process - prefab assets and hidden objects included -
        // to find one by name. GameObject.Find still cannot see inactive objects, so a missing
        // result is reported by name rather than silently returning null.
        GameObject buttonObject = GameObject.Find(buttonName);
        Button activeButton = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
        if (activeButton == null)
        {
            Debug.LogWarning(
                "StartManager could not resolve the '" + buttonName + "' button in this scene.",
                this);
        }

        return activeButton;
    }

    private Button GetButton(GameObject buttonObject)
    {
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private void RegisterButtonCallbacks()
    {
        UiSelectionAdapter.RegisterButton(startButton, StartGame);
        UiSelectionAdapter.RegisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.RegisterButton(quitButton, QuitGame);
        UiSelectionAdapter.RegisterButton(updateMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.RegisterButton(optionsMenuButton, LoadOptionsMenu);
        UiSelectionAdapter.RegisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.RegisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.RegisterButton(playerSelectButton, SelectNextPlayer);
        UiSelectionAdapter.RegisterButton(friendSelectButton, SelectNextFriend);
        UiSelectionAdapter.RegisterButton(levelSelectButton, SelectNextLevel);
        UiSelectionAdapter.RegisterButton(modeSelectButton, SelectNextMode);
        UiSelectionAdapter.RegisterButton(trafficSelectButton, ToggleTrafficOption);
        UiSelectionAdapter.RegisterButton(hardcoreSelectButton, ToggleHardcoreOption);
        UiSelectionAdapter.RegisterButton(enemySelectButton, ToggleEnemiesOption);
        UiSelectionAdapter.RegisterButton(sniperSelectButton, ToggleSniperOption);
        UiSelectionAdapter.RegisterButton(difficultySelectButton, CycleDifficultyOption);
        UiSelectionAdapter.RegisterButton(obstacleSelectButton, ToggleObstacleOption);
        UiSelectionAdapter.RegisterButton(cpu1OptionButton, CycleCpu1Option);
        UiSelectionAdapter.RegisterButton(cpu2OptionButton, CycleCpu2Option);
        UiSelectionAdapter.RegisterButton(cpu3OptionButton, CycleCpu3Option);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(startButton, StartGame);
        UiSelectionAdapter.UnregisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.UnregisterButton(quitButton, QuitGame);
        UiSelectionAdapter.UnregisterButton(updateMenuButton, LoadProgressionMenu);
        UiSelectionAdapter.UnregisterButton(optionsMenuButton, LoadOptionsMenu);
        UiSelectionAdapter.UnregisterButton(creditsMenuButton, LoadCreditsMenu);
        UiSelectionAdapter.UnregisterButton(accountMenuButton, LoadAccountMenu);
        UiSelectionAdapter.UnregisterButton(playerSelectButton, SelectNextPlayer);
        UiSelectionAdapter.UnregisterButton(friendSelectButton, SelectNextFriend);
        UiSelectionAdapter.UnregisterButton(levelSelectButton, SelectNextLevel);
        UiSelectionAdapter.UnregisterButton(modeSelectButton, SelectNextMode);
        UiSelectionAdapter.UnregisterButton(trafficSelectButton, ToggleTrafficOption);
        UiSelectionAdapter.UnregisterButton(hardcoreSelectButton, ToggleHardcoreOption);
        UiSelectionAdapter.UnregisterButton(enemySelectButton, ToggleEnemiesOption);
        UiSelectionAdapter.UnregisterButton(sniperSelectButton, ToggleSniperOption);
        UiSelectionAdapter.UnregisterButton(difficultySelectButton, CycleDifficultyOption);
        UiSelectionAdapter.UnregisterButton(obstacleSelectButton, ToggleObstacleOption);
        UiSelectionAdapter.UnregisterButton(cpu1OptionButton, CycleCpu1Option);
        UiSelectionAdapter.UnregisterButton(cpu2OptionButton, CycleCpu2Option);
        UiSelectionAdapter.UnregisterButton(cpu3OptionButton, CycleCpu3Option);
    }

    private GameObject GetDefaultSelectedButton()
    {
        if (EventSystem.current != null && EventSystem.current.firstSelectedGameObject != null)
        {
            return EventSystem.current.firstSelectedGameObject;
        }

        return startButton != null ? startButton.gameObject : null;
    }

    private static IEnumerator WaitForCondition(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + DataWaitTimeoutSeconds;
        while (!condition() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
    }

    private static void ReturnToLoadingScene()
    {
        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene == Constants.SCENE_NAME_level_00_loading)
        {
            return;
        }

        GameOptions.previousSceneName = activeScene;
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_loading);
    }

    /// <summary>
    /// Names the first unmet precondition for starting a match, or null when the menu is ready.
    ///
    /// <see cref="HasLoadedGameSetup"/> is a single bool over eighteen conditions, and when it is
    /// false the player gets "press start does nothing" - the launch is refused and the menu bounces
    /// to the loading scene, which comes straight back. Anyone diagnosing that had no way to know
    /// which of the eighteen failed. This says so.
    /// </summary>
    private string DescribeMissingGameSetup()
    {
        if (!dataLoaded) { return "menu data has not finished loading (dataLoaded)"; }
        if (playerSelectedData == null || playerSelectedData.Count == 0) { return "no player profiles (playerSelectedData)"; }
        if (playerSelectCoordinator.CurrentPrimary == null) { return "no primary character selected (playerSelectCoordinator.CurrentPrimary) - player select has not initialised yet"; }
        if (cpuPlayerSelectedData == null || cpuPlayerSelectedData.Count == 0) { return "no CPU profiles (cpuPlayerSelectedData)"; }
        if (friendSelectedData == null || friendSelectedData.Count == 0) { return "no cheerleader profiles (friendSelectedData)"; }
        if (friendSelectedIndex < 0 || friendSelectedIndex >= friendSelectedData.Count) { return "cheerleader index " + friendSelectedIndex + " is outside 0.." + (friendSelectedData.Count - 1); }
        if (levelSelectedData == null || levelSelectedData.Count == 0) { return "no levels (levelSelectedData)"; }
        if (levelSelectedIndex < 0 || levelSelectedIndex >= levelSelectedData.Count) { return "level index " + levelSelectedIndex + " is outside 0.." + (levelSelectedData.Count - 1); }
        if (modeSelectedData == null || modeSelectedData.Count == 0) { return "no game modes (modeSelectedData)"; }
        if (modeSelectedIndex < 0 || modeSelectedIndex >= modeSelectedData.Count) { return "mode index " + modeSelectedIndex + " is outside 0.." + (modeSelectedData.Count - 1); }
        if (!MatchCatalogs.IsReady) { return "match catalogs were never built (MatchCatalogs.IsReady)"; }

        return null;
    }

    private bool HasLoadedGameSetup()
    {
        return dataLoaded
            && playerSelectedData != null
            && playerSelectedData.Count > 0
            && playerSelectCoordinator.CurrentPrimary != null
            && cpuPlayerSelectedData != null
            && cpuPlayerSelectedData.Count > 0
            && friendSelectedData != null
            && friendSelectedData.Count > 0
            && friendSelectedIndex >= 0
            && friendSelectedIndex < friendSelectedData.Count
            && levelSelectedData != null
            && levelSelectedData.Count > 0
            && levelSelectedIndex >= 0
            && levelSelectedIndex < levelSelectedData.Count
            && modeSelectedData != null
            && modeSelectedData.Count > 0
            && modeSelectedIndex >= 0
            && modeSelectedIndex < modeSelectedData.Count
            // the menu draws from the catalogs, so an empty catalog means there is nothing to show
            // or launch even when the raw prefab lists arrived
            && MatchCatalogs.IsReady;
    }

    private bool HasLoadedStartUi()
    {
        return StartMenuUiObjects.instance != null
            && playerSelectView != null
            && friendSelectOptionText != null
            && friendSelectOptionImage != null
            && levelSelectOptionText != null
            && modeSelectOptionText != null
            && trafficSelectOptionText != null
            && hardcoreSelectOptionText != null
            && enemySelectOptionText != null
            && sniperSelectOptionText != null
            && difficultySelectOptionText != null
            && obstacleSelectOptionText != null;
    }

    private void RunCommand(Action action)
    {
        if (buttonPressed || action == null || lastCommandFrame == Time.frameCount)
        {
            return;
        }

        buttonPressed = true;
        lastCommandFrame = Time.frameCount;
        try
        {
            action();
        }
        catch (Exception e)
        {
            Debug.LogError("ERROR : " + e);
        }
        finally
        {
            buttonPressed = false;
        }
    }

    private void RunOptionAction(Action action)
    {
        if (buttonPressed || action == null || lastOptionFrame == Time.frameCount || !HasLoadedStartUi())
        {
            return;
        }

        buttonPressed = true;
        lastOptionFrame = Time.frameCount;
        try
        {
            action();

            // Button clicks reach player select through this wrapper (SelectNextPlayer,
            // CycleCpu1-3Option, ...). Update() drives the same call on desktop/editor every
            // frame, but Update() is compiled out on device builds, so a raw UI Button tap there
            // would otherwise change selection without ever repainting it. Cheap no-op when
            // nothing changed.
            playerSelectCoordinator.RenderIfNeeded();
        }
        catch (Exception e)
        {
            Debug.LogError("ERROR : " + e);
        }
        finally
        {
            buttonPressed = false;
        }
    }

    public void StartGame()
    {
        RunCommand(() =>
        {
            string missing = DescribeMissingGameSetup();
            if (missing != null)
            {
                Debug.LogWarning(
                    "StartManager cannot start a game: " + missing
                        + ". Returning to loading scene.",
                    this);
                ReturnToLoadingScene();
                return;
            }

            loadGame();
        });
    }

    public void LoadStatsMenu()
    {
        RunCommand(() => loadMenu(Constants.SCENE_NAME_level_00_stats));
    }

    public void LoadProgressionMenu()
    {
        RunCommand(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            playerSelectCoordinator.PersistSessionPreferences();
            loadMenu(Constants.SCENE_NAME_level_00_progression);
        });
    }

    public void LoadOptionsMenu()
    {
        RunCommand(() => loadMenu(Constants.SCENE_NAME_level_00_options));
    }

    public void LoadCreditsMenu()
    {
        RunCommand(() => loadMenu(Constants.SCENE_NAME_level_00_credits));
    }

    public void LoadAccountMenu()
    {
        RunCommand(() => loadMenu(Constants.SCENE_NAME_level_00_account));
    }

    public void QuitGame()
    {
        RunCommand(Application.Quit);
    }

    public void SelectNextPlayer()
    {
        RunOptionAction(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            disableMenuObjects("players_tab");
            enableMenuObjects("players_tab");
            playerSelectCoordinator.SelectNextPrimary();
        });
    }

    public void SelectNextFriend()
    {
        RunOptionAction(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            changeSelectedfriendDown();
            initializefriendDisplay();
        });
    }

    public void SelectNextLevel()
    {
        RunOptionAction(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            changeSelectedLevelDown();
            initializeLevelDisplay();
        });
    }

    public void SelectNextMode()
    {
        RunOptionAction(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            changeSelectedModeDown();
            initializeModeDisplay();
        });
    }

    public void ToggleTrafficOption()
    {
        RunOptionAction(() =>
        {
            changeSelectedTrafficOption();
            initializeTrafficOptionDisplay();
        });
    }

    public void ToggleHardcoreOption()
    {
        RunOptionAction(() =>
        {
            changeSelectedHardcoreOption();
            initializeHardcoreOptionDisplay();
        });
    }

    public void ToggleEnemiesOption()
    {
        RunOptionAction(() =>
        {
            changeSelectedEnemiesOption();
            initializeEnemyOptionDisplay();
        });
    }

    public void ToggleSniperOption()
    {
        RunOptionAction(() =>
        {
            changeSelectedSniperOption();
            initializeSniperOptionDisplay();
        });
    }

    public void CycleDifficultyOption()
    {
        RunOptionAction(() =>
        {
            changeSelectedDifficultyOption(difficultySelected);
            initializeDifficultyOptionDisplay();
        });
    }

    public void ToggleObstacleOption()
    {
        RunOptionAction(() =>
        {
            changeSelectedObstacleOption();
            initializeObstacleOptionDisplay();
        });
    }

    public void CycleCpu1Option()
    {
        RunOptionAction(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            playerSelectCoordinator.SelectNextCpu(0);
        });
    }

    public void CycleCpu2Option()
    {
        RunOptionAction(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            playerSelectCoordinator.SelectNextCpu(1);
        });
    }

    public void CycleCpu3Option()
    {
        RunOptionAction(() =>
        {
            if (!HasLoadedGameSetup())
            {
                return;
            }

            playerSelectCoordinator.SelectNextCpu(2);
        });
    }

    IEnumerator UpdateLevelAndExperienceFromDatabase()
    {
        yield return WaitForCondition(() => dataLoaded);
        if (!dataLoaded || DBHelper.instance == null)
        {
            experienceRefreshed = true;
            yield break;
        }

        foreach (CharacterProfile s in playerSelectedData)
        {
            s.Experience = DBHelper.instance.getIntValueFromTableByFieldAndCharId("CharacterProfile", "experience", s.PlayerId);
            s.Level = DBHelper.instance.getIntValueFromTableByFieldAndCharId("CharacterProfile", "level", s.PlayerId);
        }

        // Player select projects Level/Clutch from these values once, when it initializes - not on
        // every render like the old view did - so this refresh has to land before that happens.
        experienceRefreshed = true;
    }

    IEnumerator getLoadedData()
    {
        if (LoadedData.instance != null)
        {
            yield return WaitForCondition(() => LoadedData.instance != null
                && !LoadedData.instance.LoadFailed
                && LoadedData.instance.PlayerSelectedData != null
                && LoadedData.instance.CpuPlayerSelectedData != null
                && LoadedData.instance.CheerleaderSelectedData != null
                && LoadedData.instance.LevelSelectedData != null
                && LoadedData.instance.ModeSelectedData != null);

            if (LoadedData.instance == null
                || LoadedData.instance.LoadFailed
                || LoadedData.instance.PlayerSelectedData == null
                || LoadedData.instance.CpuPlayerSelectedData == null
                || LoadedData.instance.CheerleaderSelectedData == null
                || LoadedData.instance.LevelSelectedData == null
                || LoadedData.instance.ModeSelectedData == null)
            {
                ReturnToLoadingScene();
                yield break;
            }

            playerSelectedData = LoadedData.instance.PlayerSelectedData;
            cpuPlayerSelectedData = LoadedData.instance.CpuPlayerSelectedData;
            friendSelectedData = LoadedData.instance.CheerleaderSelectedData;
            levelSelectedData = LoadedData.instance.LevelSelectedData;
            modeSelectedData = LoadedData.instance.ModeSelectedData;

            if (playerSelectedData != null
                && friendSelectedData != null
                && levelSelectedData != null
                && modeSelectedData != null)
            {
                // The mode/level catalogs the compatibility service and the launch builder work
                // from. Built here, from the same authored prefabs the menu lists, so the menu can
                // never offer a combination the launch validation would then refuse.
                MatchCatalogs.EnsureBuilt(modeSelectedData, levelSelectedData);
                dataLoaded = true;
            }
        }
        else
        {
            ReturnToLoadingScene();
        }
    }

    IEnumerator InitializeDisplay()
    {
        yield return WaitForCondition(() => dataLoaded && experienceRefreshed);
        if (!dataLoaded)
        {
            yield break;
        }

        // Built here, from the same profile/level data the rest of this method projects into the
        // menu, so player select and level cycling/launch cannot see a different unlock answer for
        // the same session.
        unlockSnapshot = UnlockSnapshotBuilder.Build(playerSelectedData, cpuPlayerSelectedData, Compatibility.Levels);

        playerSelectCoordinator.Initialize(playerSelectedData, cpuPlayerSelectedData, playerSelectView, unlockSnapshot);
        playerSelectCoordinator.SetMatchContext(selection.CurrentMode(Compatibility), CurrentModifiers());
        playerSelectCoordinator.RenderIfNeeded();

        // display default data
        initializeNumPlayersDisplay();
        initializefriendDisplay();
        initializeCpuDisplay();
        initializeLevelDisplay();

        initializeModeDisplay();
        initializeEnemyOptionDisplay();
        initializeTrafficOptionDisplay();
        initializeHardcoreOptionDisplay();
        initializeSniperOptionDisplay();
        initializeDifficultyOptionDisplay();
        initializeObstacleOptionDisplay();
    }

    public void initializeCpuDisplay()
    {
        //Debug.Log("initializeCpuDisplay");
        disableMenuObjects("cpu_tab");
        enableMenuObjects("cpu_tab");

        // The old CPU panel always repainted the shared stats readout from the last CPU slot
        // (setCpuPlayer1, then 2, then 3, each overwriting the same text) whenever the tab
        // activated, so it was never blank on entry. Defaulting focus to the last slot here
        // preserves that instead of leaving the panel empty until a specific slot is highlighted.
        playerSelectCoordinator.FocusCpu(PlayerSelectionState.CpuSlotCount - 1);
    }

    private IEnumerator SetVersion()
    {
        yield return WaitForCondition(() => StartMenuUiObjects.instance != null);
        if (StartMenuUiObjects.instance == null)
        {
            yield break;
        }

        //Debug.Log(GameOptions.userName);
        if (APIHelper.BearerToken != null && !string.IsNullOrEmpty(GameOptions.userName))
        {
            userNameText.text = "username : " + GameOptions.userName + " connected";
        }
        if (APIHelper.BearerToken == null || string.IsNullOrEmpty(GameOptions.userName))
        {
            userNameText.text = "username : " + GameOptions.userName + " disconnected";
        }
        versionText.text = "current version : " + Application.version;
        ApiResult<string> versionResult = null;
        yield return APIHelper.GetLatestBuildVersion(result => versionResult = result);
        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        latestVersionText.text = versionResult != null && versionResult.Success
            ? "latest version: " + versionResult.Value.Trim().Trim('"')
            : "latest version: unavailable";
    }

    // ============================  get UI buttons / text references ==============================
    private IEnumerator GetUiObjectReferences()
    {
        yield return WaitForCondition(() => StartMenuUiObjects.instance != null);
        if (StartMenuUiObjects.instance == null)
        {
            Debug.LogError("StartManager could not resolve StartMenuUiObjects.");
            enabled = false;
            yield break;
        }

        //buttons to disable for touch input

        levelSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_level_selected_name_button;
        trafficSelectButton = StartMenuUiObjects.instance.column2_options_tab_traffic_select_button;
        hardcoreSelectButton = StartMenuUiObjects.instance.column2_options_tab_hardcore_select_button;
        enemySelectButton = StartMenuUiObjects.instance.column2_options_tab_enemy_select_button;
        sniperSelectButton = StartMenuUiObjects.instance.column2_options_tab_sniper_select_button;
        difficultySelectButton = StartMenuUiObjects.instance.column2_options_tab_difficulty_select_button;
        obstacleSelectButton = StartMenuUiObjects.instance.column2_options_tab_obstacles_select_button;
        playerSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_player_select_name_button;
        friendSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_friend_selected_name_button;
        modeSelectButton = StartMenuUiObjects.instance.column1_subgroup_column2_mode_selected_name_button;
        cpu1OptionButton = GetButton(StartMenuUiObjects.instance.column4_cpu1_button);
        cpu2OptionButton = GetButton(StartMenuUiObjects.instance.column4_cpu2_button);
        cpu3OptionButton = GetButton(StartMenuUiObjects.instance.column4_cpu3_button);

        cpuSlotButtonObjects = new[]
        {
            StartMenuUiObjects.instance.column4_cpu1_button,
            StartMenuUiObjects.instance.column4_cpu2_button,
            StartMenuUiObjects.instance.column4_cpu3_button,
        };

        // Every reference below already exists as a serialized StartMenuUiObjects field; wrapping
        // them in PlayerSelectView adds no new serialized state to the scene.
        playerSelectView = new PlayerSelectView(
            StartMenuUiObjects.instance.column1_subgroup_column2_player_select_name_text,
            StartMenuUiObjects.instance.column2_players_tab_player_selected_image,
            StartMenuUiObjects.instance.column2_players_tab_lock,
            StartMenuUiObjects.instance.column3_player_selected_stats_numbers_text,
            StartMenuUiObjects.instance.column3_player_selected_progression_stats_text,
            StartMenuUiObjects.instance.column3_player_selected_progression_update_points_text,
            StartMenuUiObjects.instance.column1_subgroup_column2_num_players_selected_name_text,
            StartMenuUiObjects.instance.column4_cpu_selected_stats_numbers_text,
            new[]
            {
                new CpuSlotBinding(
                    StartMenuUiObjects.instance.column4_cpu1_button,
                    StartMenuUiObjects.instance.column4_cpu1_image,
                    StartMenuUiObjects.instance.column4_cpu1_name_text),
                new CpuSlotBinding(
                    StartMenuUiObjects.instance.column4_cpu2_button,
                    StartMenuUiObjects.instance.column4_cpu2_image,
                    StartMenuUiObjects.instance.column4_cpu2_name_text),
                new CpuSlotBinding(
                    StartMenuUiObjects.instance.column4_cpu3_button,
                    StartMenuUiObjects.instance.column4_cpu3_image,
                    StartMenuUiObjects.instance.column4_cpu3_name_text),
            });

        // friend object with lock texture and unlock text
        friendSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_friend_selected_name_text;
        friendSelectOptionImage = StartMenuUiObjects.instance.column2_friend_tab_friend_selected_image;

        // level object with selected level text
        levelSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_level_selected_name_text;

        // options selection text
        trafficSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_traffic_select_option_text;
        hardcoreSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_hardcore_select_option_text;
        enemySelectOptionText = StartMenuUiObjects.instance.column2_options_tab_enemy_select_option_text;
        sniperSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_sniper_select_option_text;
        difficultySelectOptionText = StartMenuUiObjects.instance.column2_options_tab_difficulty_select_option_text;
        obstacleSelectOptionText = StartMenuUiObjects.instance.column2_options_tab_obstacle_select_option_text;

        //version
        versionText = StartMenuUiObjects.instance.header_version;
        latestVersionText = StartMenuUiObjects.instance.header_latestVersion;
        userNameText = StartMenuUiObjects.instance.header_username;

        ResolveCommandButtonReferences();
        RegisterButtonCallbacks();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
    }

    public void disableButtonsNotUsedForTouchInput()
    {
        levelSelectButton.enabled = false;
        trafficSelectButton.enabled = false;
        playerSelectButton.enabled = false;
        friendSelectButton.enabled = false;
        modeSelectButton.enabled = false;
    }

    public void enableButtonsNotUsedForTouchInput()
    {
        Debug.Log("enable buttons");
        levelSelectButton.enabled = true;
        trafficSelectButton.enabled = true;
        playerSelectButton.enabled = true;
        friendSelectButton.enabled = true;
        modeSelectButton.enabled = true;
    }

    public void changeSelectedTrafficOption()
    {
        trafficEnabled = !trafficEnabled;
    }

    public void changeSelectedHardcoreOption()
    {
        hardcoreEnabled = !hardcoreEnabled;
    }

    public void changeSelectedEnemiesOption()
    {
        enemiesEnabled = !enemiesEnabled;

        // Enemies-only changes which characters can play (fighter vs shooter), so player select
        // needs to know immediately - both to keep cycling correct and to reconcile a required
        // CPU opponent under the new context.
        playerSelectCoordinator.SetMatchContext(selection.CurrentMode(Compatibility), CurrentModifiers());
    }

    public void changeSelectedSniperOption()
    {
        selection.CycleSniper();
    }

    public void changeSelectedObstacleOption()
    {
        obstaclesEnabled = !obstaclesEnabled;
    }

    /// <summary>
    /// The parameter is ignored; the selection model owns the current difficulty. It stays on the
    /// signature because the touch input controller calls it with a value.
    /// </summary>
    public void changeSelectedDifficultyOption(int currentDifficulty)
    {
        selection.CycleDifficulty();
    }

    // ============================  Initialize displays ==============================

    public void initializeTrafficOptionDisplay()
    {
        if (trafficEnabled)
        {
            trafficSelectOptionText.text = "ON";
        }
        if (!trafficEnabled)
        {
            trafficSelectOptionText.text = "OFF";
        }
    }

    public void initializeHardcoreOptionDisplay()
    {
        if (hardcoreEnabled)
        {
            hardcoreSelectOptionText.text = "ON";
        }
        if (!hardcoreEnabled)
        {
            hardcoreSelectOptionText.text = "OFF";
        }
    }
    public void initializeEnemyOptionDisplay()
    {
        if (enemiesEnabled)
        {
            enemySelectOptionText.text = "ON";
        }
        if (!enemiesEnabled)
        {
            enemySelectOptionText.text = "OFF";
        }
    }
    public void initializeSniperOptionDisplay()
    {
        if (sniperBulletEnabled)
        {
            sniperSelectOptionText.text = "Bullet";
        }
        if (sniperBulleAutoEnabled)
        {
            sniperSelectOptionText.text = "Machine Gun";
        }
        if (sniperLaserEnabled)
        {
            sniperSelectOptionText.text = "Laser";
        }
        if (!sniperBulletEnabled && !sniperLaserEnabled && !sniperBulleAutoEnabled)
        {
            sniperSelectOptionText.text = "OFF";
        }
    }
    public void initializeDifficultyOptionDisplay()
    {
        difficultySelectOptionDescriptionText = StartMenuUiObjects.instance.column2_options_tab_difficulty_select_description_text;
        if (difficultySelected == 0)
        {
            difficultySelectOptionText.text = "easy";
            difficultySelectOptionDescriptionText.text = "max stats | 0.5x experience";
        }
        if (difficultySelected == 1)
        {
            difficultySelectOptionText.text = "normal";
            difficultySelectOptionDescriptionText.text = "basic stats | 1x experience";
        }
        if (difficultySelected == 2)
        {
            difficultySelectOptionText.text = "hardcore";
            difficultySelectOptionDescriptionText.text = "basic stats | 1.5x experience";
        }
    }
    public void initializeObstacleOptionDisplay()
    {
        //Debug.Log(obstaclesEnabled + " : " + obstacleSelectOptionText.text);
        if (obstaclesEnabled)
        {
            obstacleSelectOptionText.text = "ON";
        }
        if (!obstaclesEnabled)
        {
            obstacleSelectOptionText.text = "OFF";
        }
        //Debug.Log(obstaclesEnabled + " : " + obstacleSelectOptionText.text);
    }

    public void initializeLevelDisplay()
    {
        disableMenuObjects("level_tab");
        enableMenuObjects("level_tab");

        LevelDefinition level = selection.CurrentLevel(Compatibility);
        if (level == null)
        {
            return;
        }

        // NOTE : add level column 2 refs to change text/descr
        // add descritpion to startmenu levle objects
        StartMenuUiObjects.instance.column1_subgroup_column2_level_selected_name_text.text
            = StartMenuUiObjects.instance.column2_level_tab_level_selected_name.text
            = level.DisplayName;
        StartMenuUiObjects.instance.column2_level_tab_level_selected_info.text = level.Info;
    }

    public void initializeNumPlayersDisplay()
    {
        numPlayersSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_num_players_selected_name_text;
        numPlayersSelectOptionText.text = playerSelectCoordinator.ParticipantCount.ToString();
    }

    public void initializefriendDisplay()
    {
        try
        {
            disableMenuObjects("friend_tab");
            enableMenuObjects("friend_tab");
            //Debug.Log(friendSelectedData[friendSelectedIndex].CheerleaderDisplayName);
            //Debug.Log(friendSelectedData[friendSelectedIndex].bonus3Accuracy);
            //Debug.Log(friendSelectedIndex);

            friendSelectOptionText.text = friendSelectedData[friendSelectedIndex].CheerleaderDisplayName;
            friendSelectOptionImage.sprite = friendSelectedData[friendSelectedIndex].CheerleaderPortrait;

            if (friendSelectedIndex > 0)
            {
                StartMenuUiObjects.instance.column3_friend_selected_stats_numbers_text.text = // friendSelectedData[friendSelectedIndex].Accuracy2Pt.ToString("F0") + "\n"
                   "+" +  friendSelectedData[friendSelectedIndex].bonus3Accuracy.ToString("F0") + "\n"
                    + "+" +  friendSelectedData[friendSelectedIndex].bonus4Accuracy.ToString("F0") + "\n"
                    + "+" +  friendSelectedData[friendSelectedIndex].bonus7Accuracy.ToString("F0") + "\n"
                    + "+" + friendSelectedData[friendSelectedIndex].bonusRelease.ToString("F0") + "\n"
                    + "+" + friendSelectedData[friendSelectedIndex].bonusRange.ToString("F0") + "\n"
                    + "\n"
                    //+ "+" + (playerSelectedData[playerSelectedIndex].calculateSpeedToPercent() + friendSelectedData[friendSelectedIndex].bonusSpeed).ToString("F0") + "\n"
                    + "\n"
                    + "+" + friendSelectedData[friendSelectedIndex].bonusLuck.ToString("F0") + "\n"
                    + "+" +  friendSelectedData[friendSelectedIndex].bonusClutch.ToString("F0");
            }
            else { StartMenuUiObjects.instance.column3_friend_selected_stats_numbers_text.text = "";  }

            // AUD-110: this used to re-resolve the label with
            // GameObject.Find(...).GetComponent<Text>() on every refresh, unguarded, inside a catch
            // that only logs - so a rename or an inactive object made the friend display fail
            // silently. GetUiObjectReferences already holds this reference.
            if (friendSelectOptionText != null)
            {
                friendSelectOptionText.text = friendSelectedData[friendSelectedIndex].CheerleaderDisplayName;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("ERROR : " + e);
            return;
        }
    }
    public void initializeOptionsDisplay()
    {
        try
        {
            //Debug.Log("initializeOptionsDisplay");
            disableMenuObjects("options_tab");
            enableMenuObjects("options_tab");
        }
        catch (Exception e)
        {
            Debug.LogError("ERROR : " + e);
            return;
        }
    }

    public void initializeModeDisplay()
    {
        disableMenuObjects("mode_tab");
        enableMenuObjects("mode_tab");

        GameModeDefinition mode = selection.CurrentMode(Compatibility);
        if (mode == null)
        {
            return;
        }

        modeSelectOptionText = StartMenuUiObjects.instance.column1_subgroup_column2_mode_selected_name_text;
        modeSelectOptionText.text = mode.DisplayName;

        modeSelectOptionNameText = StartMenuUiObjects.instance.column2_mode_tab_mode_selected_name;
        modeSelectOptionNameText.text = mode.DisplayName;

        ModeSelectOptionDescriptionText = StartMenuUiObjects.instance.column2_mode_tab_mode_selected_description;
        ModeSelectOptionDescriptionText.text = mode.Description;
    }
    /// <summary>
    /// Shows or hides one tab object, tolerating an unwired reference.
    ///
    /// These were direct <c>StartMenuUiObjects.instance.field.SetActive(...)</c> calls. Ninety-six of
    /// StartMenuUiObjects' serialized references are unassigned on the prefab, and Unity throws
    /// UnassignedReferenceException on member access rather than returning null - so hitting one
    /// killed the caller. From <see cref="InitializeDisplay"/> that aborted the coroutine at
    /// initializeCpuDisplay, and every display after it - level, mode, enemy, traffic, hardcore,
    /// sniper, difficulty, obstacle - was never initialised at all. Naming the field makes the
    /// missing wiring actionable instead of a stack trace that only points at SetActive.
    /// </summary>
    private static void SetTabObjectActive(GameObject target, string fieldName, bool active)
    {
        // Unity's == overload reports an unassigned serialized reference as null, so this catches it
        // without touching the member that throws.
        if (target == null)
        {
            Debug.LogWarning(
                "StartMenuUiObjects." + fieldName + " is not assigned on the prefab, so the start"
                    + " menu cannot show or hide it.");
            return;
        }

        target.SetActive(active);
    }

    void disableMenuObjects(string activeMenu)
    {
        if (!activeMenu.ToLower().Equals("players_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_players_tab, "column2_players_tab", false);
            SetTabObjectActive(StartMenuUiObjects.instance.column3, "column3", false);
            SetTabObjectActive(StartMenuUiObjects.instance.column2_players_tab, "column2_players_tab", false);
        }
        if (!activeMenu.ToLower().Equals("cpu_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column4, "column4", false);
            SetTabObjectActive(StartMenuUiObjects.instance.column2, "column2", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column1_subgroup_column2, "column1_subgroup_column2", true);
        }
        if (!activeMenu.ToLower().Equals("friend_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_friend_tab, "column2_friend_tab", false);
            SetTabObjectActive(StartMenuUiObjects.instance.column3, "column3", false);
            SetTabObjectActive(StartMenuUiObjects.instance.column3_friend_selected_stats_numbers, "column3_friend_selected_stats_numbers", false);
        }
        if (!activeMenu.ToLower().Equals("level_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_level_tab, "column2_level_tab", false);
        }
        if (!activeMenu.ToLower().Equals("mode_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_mode_tab, "column2_mode_tab", false);
        }
        if (!activeMenu.ToLower().Equals("options_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_options_tab, "column2_options_tab", false);
        }
        if (activeMenu.ToLower().Equals("cpu_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2, "column2", false);
            SetTabObjectActive(StartMenuUiObjects.instance.column1_subgroup_column2, "column1_subgroup_column2", false);
        }
    }
    void enableMenuObjects(string activeMenu)
    {
        if (activeMenu.ToLower().Equals("players_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column1_subgroup_column2, "column1_subgroup_column2", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column2, "column2", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column2_players_tab, "column2_players_tab", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column3, "column3", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column3_player_stats, "column3_player_stats", true);
            // Lock overlay is owned by PlayerSelectView now; it renders whenever selection changes,
            // not only when this tab activates.
        }

        if (activeMenu.ToLower().Equals("cpu_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column4, "column4", true);
            // CPU slot portraits/names are kept current by playerSelectCoordinator.RenderIfNeeded(),
            // not refreshed here.
        }

        if (activeMenu.ToLower().Equals("friend_tab"))
        {
            //SetTabObjectActive(StartMenuUiObjects.instance.column1_subgroup_column2, "column1_subgroup_column2", true);
            //SetTabObjectActive(StartMenuUiObjects.instance.column2, "column2", true);
            //SetTabObjectActive(StartMenuUiObjects.instance.column2_players_tab, "column2_players_tab", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column3, "column3", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column3_player_stats, "column3_player_stats", true);

            SetTabObjectActive(StartMenuUiObjects.instance.column2_friend_tab, "column2_friend_tab", true);
            //SetTabObjectActive(StartMenuUiObjects.instance.column3, "column3", true);
            SetTabObjectActive(StartMenuUiObjects.instance.column3_friend_selected_stats_numbers, "column3_friend_selected_stats_numbers", true);
        }
        if (activeMenu.ToLower().Equals("level_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_level_tab, "column2_level_tab", true);
        }
        if (activeMenu.ToLower().Equals("mode_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_mode_tab, "column2_mode_tab", true);

        }
        if (activeMenu.ToLower().Equals("options_tab"))
        {
            SetTabObjectActive(StartMenuUiObjects.instance.column2_options_tab, "column2_options_tab", true);
        }
    }

    // Player-select rendering (name/portrait/stats/progression/lock) is owned by PlayerSelectView,
    // driven by playerSelectCoordinator.RenderIfNeeded() - not this class.

    // ============================  footer options activate - load scene/stats/quit/etc ==============================
    // ============================  footer options activate - load scene/stats/quit/etc ==============================

    /// <summary>
    /// The launch path.
    ///
    /// Gather the selection into a request, build and validate the configuration, make it the
    /// active match, push the legacy fields for the systems that have not migrated, then load the
    /// scene. An invalid combination stops here with a reason instead of loading a scene that
    /// cannot be played - the menu filters as a convenience, but this is what decides.
    /// </summary>
    public void loadGame()
    {
        if (lastLoadGameFrame == Time.frameCount || !HasLoadedGameSetup())
        {
            return;
        }

        lastLoadGameFrame = Time.frameCount;
        forceCampaignStartLevel();

        if (!playerSelectCoordinator.TryBuildRoster(out PlayerRoster roster, out string playerSelectError))
        {
            ShowLaunchError(playerSelectError);
            return;
        }

        MatchRequest request = selection.BuildRequest(Compatibility, roster, friendSelectedData);

        if (request == null)
        {
            Debug.LogError("StartManager could not build a match request from the current selection.");
            return;
        }

        MatchBuildResult result = MatchCatalogs.Builder.Build(request, unlockSnapshot);
        if (!result.Succeeded)
        {
            ShowLaunchError(result.Validation);
            return;
        }

        MatchConfiguration configuration = result.Configuration;

        // The new configuration is authoritative from here. The bridge is the only thing that
        // writes the old globals, and it only ever writes - nothing reads back into the match.
        ActiveMatch.Begin(configuration);
        LegacyGameOptionsBridge.Apply(configuration);

        selection.SavePersistedPreferences();
        playerSelectCoordinator.PersistSessionPreferences();
        applyNonMatchLaunchState(configuration);

        SceneManager.LoadScene(resolveSceneName(configuration));
    }

    /// <summary>
    /// The campaign always begins at its first level, whatever the menu had selected.
    ///
    /// This has to move the selection itself, not just the scene: the campaign reads
    /// <c>levelSelectedIndex</c> afterwards to pick the opponent and to know which round it is on,
    /// so a scene override alone would start the run at the right place and then advance from the
    /// wrong one.
    /// </summary>
    private void forceCampaignStartLevel()
    {
        GameModeDefinition mode = selection.CurrentMode(Compatibility);
        if (mode == null || mode.Id != GameModeId.BeatThaComputahs)
        {
            return;
        }

        int firstCampaignLevel = -1;
        for (int index = 0; index < Compatibility.Levels.Count; index++)
        {
            if (Compatibility.Levels.Definitions[index].LevelId == CampaignFirstLevelId)
            {
                firstCampaignLevel = index;
                break;
            }
        }

        if (firstCampaignLevel >= 0)
        {
            selection.LevelIndex = firstCampaignLevel;
        }
    }

    /// <summary>
    /// Which scene a configuration loads. Normally the arena's own scene; the campaign mode is the
    /// one launch-time special case, and it stays here rather than in the domain model until it has
    /// characterization coverage.
    /// </summary>
    private static string resolveSceneName(MatchConfiguration configuration)
    {
        return configuration.ModeId == GameModeId.BeatThaComputahs
            ? Constants.SCENE_NAME_level_01_scrapyard
            : configuration.SceneName;
    }

    private void ShowLaunchError(ValidationResult validation)
    {
        ShowLaunchError(validation == null ? "unknown" : validation.ToString());
    }

    private void ShowLaunchError(string reasons)
    {
        Debug.LogError("This match cannot be started: " + reasons);
        if (ModeSelectOptionDescriptionText != null)
        {
            ModeSelectOptionDescriptionText.text = reasons;
        }
    }

    /// <summary>
    /// The launch-time state that is not part of the match configuration: application metadata,
    /// the campaign level list, the end-round portraits and the progression snapshot. These belong
    /// to their own owners (plan phase 11) and are only gathered here because they are gathered at
    /// the same moment.
    /// </summary>
    private void applyNonMatchLaunchState(MatchConfiguration configuration)
    {
        GameOptions.applicationVersion = Application.version;
        GameOptions.operatingSystemVersion = SystemInfo.operatingSystem;
        GameOptions.levelsList = PlayerData.instance.LevelsList;

        // End-round portraits and the progression snapshot are player-specific launch details;
        // player select resolves them from the selected character without this class indexing
        // playerSelectedData directly.
        playerSelectCoordinator.ApplyLaunchSideEffects(configuration);

        // Reset continues for this fresh run - numberOfContinues is a static field that only
        // ever gets decremented during play, so without this a player who exhausted continues in
        // an earlier campaign attempt would start every later attempt (in the same session) with
        // zero continues left, silently.
        EndRoundData.numberOfContinues = configuration.Rules.Hardcore ? 0 : EndRoundData.DefaultContinues;

        // AUD-068: same shape as numberOfContinues above. tipDialogueLoadedOnStart only ever gets
        // set true (by StartScreenTipDialogueManager/StartScreenCpuSelectManager, once either has
        // shown), never reset, so without this it would show at most once per application process
        // instead of once per pass through the start flow. Reset here (launch time, not mid-flow)
        // so it's correct the *next* time a player re-enters the start flow, without disturbing
        // whichever of those two screens already ran during the flow that is launching right now.
        GameOptions.tipDialogueLoadedOnStart = false;

        // load hardcore mode highscores (for ui display) for game mode if hardcore mode enabled
        PlayerData.instance.loadStatsFromDatabase();
    }

    public void loadMenu(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    // ============================  message display ==============================
    // used in this context to display if item is locked

    /// <summary>
    /// Clears the shared message line after a delay.
    ///
    /// This used to be `GameObject.Find("messageDisplay").GetComponent&lt;Text&gt;()` with no guard
    /// on either step (AUD-110), and `messageDisplay` is not part of the start scene - it comes from
    /// the loading scene's prefab - so the chain throws whenever the start menu is entered without
    /// it. Resolved through <see cref="SceneObjects"/>, which reports the missing name instead.
    /// </summary>
    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Text messageText = SceneObjects.Find<Text>(MessageDisplayObjectName, this);
        if (messageText != null)
        {
            messageText.text = "";
        }
    }

    // ============================  navigation functions ==============================

    // The player count is not something the menu sets any more - it is how many participants the
    // CPU picks add up to. changeSelectedNumPlayersUp/Down are gone with the field they wrote.

    // ============================  selection cycling ==============================
    // Every one of these used to recurse until it found a valid entry, and used to publish the
    // result straight into GameOptions. Now they step the selection model, which asks the
    // compatibility service and walks the catalog a bounded number of times, and nothing outside
    // the menu changes until start is pressed.

    // Primary character cycling is playerSelectCoordinator.SelectNextPrimary()/SelectPreviousPrimary() now.

    public void changeSelectedfriendUp()
    {
        selection.CycleFriend(friendSelectedData.Count, -1);
    }

    public void changeSelectedfriendDown()
    {
        selection.CycleFriend(friendSelectedData.Count, 1);
    }

    public void changeSelectedLevelUp()
    {
        selection.CycleLevel(Compatibility, -1, unlockSnapshot);
        initializeLevelDisplay();
        initializeModeDisplay();
    }

    public void changeSelectedLevelDown()
    {
        selection.CycleLevel(Compatibility, 1, unlockSnapshot);
        initializeLevelDisplay();
        initializeModeDisplay();
    }

    public void changeSelectedModeUp()
    {
        selection.CycleMode(Compatibility, -1, unlockSnapshot);
        initializeModeDisplay();
        initializeLevelDisplay();
        playerSelectCoordinator.SetMatchContext(selection.CurrentMode(Compatibility), CurrentModifiers());
    }

    public void changeSelectedModeDown()
    {
        selection.CycleMode(Compatibility, 1, unlockSnapshot);
        initializeModeDisplay();
        initializeLevelDisplay();
        playerSelectCoordinator.SetMatchContext(selection.CurrentMode(Compatibility), CurrentModifiers());
    }

    // ============================  public var references  ==============================
    // dont think some of these are used, keep an eye on this on refactor
    // button names
    public static string FriendSelectOptionButtonName => friendSelectOptionButtonName;
    public static string SniperSelectOptionName => sniperSelectOptionName;
    public static string ObstacleSelectOptionName => obstacleSelectOptionName;
}
