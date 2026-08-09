using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ProgressionManager : MonoBehaviour
{
    private const float DataWaitTimeoutSeconds = 12f;
    [SerializeField]
    public string currentHighlightedButton;
    // option select buttons, this will be disabled with touch input
    [SerializeField] Button playerSelectButton;
    [SerializeField] Button startButton;
    [SerializeField] Button statsMenuButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button playerSelectOptionButton;
    [SerializeField] Button progression3AccuracyButton;
    [SerializeField] Button progression4AccuracyButton;
    [SerializeField] Button progression7AccuracyButton;
    [SerializeField] Button confirmButton;
    [SerializeField] Button cancelButton;
    [SerializeField] Button saveButton;
    [SerializeField] Button resetButton;

    //list of all shooter profiles with player data
    private List<CharacterProfile> playerSelectedData;

    // list off cheerleader profile data
    private List<CheerleaderProfile> cheerleaderSelectedData;

    //player selected display
    private Text playerSelectOptionText;
    private Image playerSelectOptionImage;
    private Text playerProgressionStatsText;
    private Text playerProgressionUpdatePointsText;
    private Text progression3Accuracy;
    private Text progression4Accuracy;
    private Text progression7Accuracy;
    private Text progressionRange;
    private Text progressionRelease;
    private Text progressionSpeed;
    private Text progressionJump;
    private Text progressionLuck;

    private Text bonusReleaseText;
    private Text bonusRangeText;
    private Text bonusLuckText;

    private Text addTo3Text;
    private Text addTo4Text;
    private Text addTo7Text;

    //const object names
    private const string startButtonName = "press_start";
    private const string statsMenuButtonName = "stats_menu";
    private const string quitButtonName = "quit_game";
    //private const string optionsMenuButtonName = "options_menu";

    // button names
    private const string playerSelectButtonName = "player_select_button";
    private const string playerSelectOptionButtonName = "player_selected_name";
    //private const string playerSelectStatsObjectName = "player_selected_stats_numbers";
    private const string playerSelectImageObjectName = "player_selected_image";
    //private const string playerSelectStatsCategoryName = "player_selected_stats_category";
    //private const string playerBonusName = "current_player_bonus";

    //private const string playerProgressionName = "player_progression";
    private const string playerProgressionStatsName = "player_progression_stats";
    private const string playerProgressionPointsAvailableName = "player_points_available";

    private const string progression3AccuracyName = "3accuracyButton";
    private const string progression4AccuracyName = "4accuracyButton";
    private const string progression7AccuracyName = "7accuracyButton";

    private const string releaseBonusName = "release_bonus";
    private const string rangeBonusName = "range_bonus";
    private const string luckBonusName = "luck_bonus";

    private const string confirmButtonName = "confirm_button";
    private const string cancelButtonName = "cancel_button";
    private const string saveButtonName = "save_button";
    private const string resetButtonName = "reset_button";

    private const string confirmationDialogueBoxName = "confirm_update";
    private const string progression3AccuracyTextName = "3accuracy";
    private const string progression4AccuracyTextName = "4accuracy";
    private const string progression7AccuracyTextName = "7accuracy";
    private const string progressionRangeName = "range";
    private const string progressionReleaseName = "release";
    private const string progressionSpeedName = "speed";
    private const string progressionJumpName = "jump";
    private const string progressionLuckName = "luck";

    /// <summary>
    /// Objects this manager resolves by name. Level5ProjectValidator asserts they exist in any
    /// scene carrying a ProgressionManager, so a rename fails the build rather than the play
    /// session - the same contract GameRules and Pause carry (AUD-028, AUD-047).
    /// </summary>
    public static readonly string[] RequiredProgressionObjectNames =
    {
        playerSelectButtonName,
        playerSelectOptionButtonName,
        playerSelectImageObjectName,
        playerProgressionStatsName,
        playerProgressionPointsAvailableName,
        progression3AccuracyTextName,
        progression4AccuracyTextName,
        progression7AccuracyTextName,
        progressionRangeName,
        progressionReleaseName,
        progressionSpeedName,
        progressionJumpName,
        progressionLuckName,
        releaseBonusName,
        rangeBonusName,
        luckBonusName,
        progression3AccuracyName,
        progression4AccuracyName,
        progression7AccuracyName
    };


    private int playerSelectedIndex;

    private PlayerControls controls;
    public static ProgressionManager instance;

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

    ProgressionState progressionState;
    CharacterProgressionService progressionService;
    CharacterProgressDraft progressionDraft;
    // flags
    bool buttonPressed = false;
    bool dataLoaded = false;
    bool initialized = false;
    int lastActionFrame = -1;

    // confirm save dialogue
    bool confirmationDialogueBoxEnabled = false;
    GameObject confirmationDialogueBox;

    GameObject prevSelectedObject;

    // AUD-046: this used to be a serialized int with no initializer, which made it 0 unless
    // someone had set it in the inspector - and the level formula below divides by it. It is also
    // the tenth site of the XP curve AUD-036 was meant to collapse. CharacterLevel owns it now.
    public int ExperienceRequiredForNextLevel => CharacterLevel.ExperiencePerLevel;
    public static string PlayerSelectOptionButtonName => playerSelectOptionButtonName;
    public static string StartButtonName => startButtonName;
    public static string StatsMenuButtonName => statsMenuButtonName;
    public static string QuitButtonName => quitButtonName;
    public static string Progression3AccuracyName => progression3AccuracyName;
    public static string Progression4AccuracyName => progression4AccuracyName;
    public static string Progression7AccuracyName => progression7AccuracyName;
    public static string ConfirmButtonName => confirmButtonName;
    public static string CancelButtonName => cancelButtonName;
    public static string SaveButtonName => saveButtonName;
    public static string ResetButtonName => resetButtonName;
    public ProgressionState ProgressionState { get => progressionState; set => progressionState = value; }
    public bool DataLoaded { get => dataLoaded; }

    public enum UpdateType
    {
        Add,
        Subtract,
        Reset
    }

    private void OnEnable()
    {
        controls = PlayerControlsProvider.Controls;
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
        // disable confirmation dialogue
        confirmationDialogueBox = GameObject.Find(confirmationDialogueBoxName);
        if (confirmationDialogueBox != null)
        {
            confirmationDialogueBox.SetActive(confirmationDialogueBoxEnabled);
        }

        progressionState = GetComponent<ProgressionState>();
        if (progressionState == null)
        {
            Debug.LogError("ProgressionManager requires a ProgressionState on the same object.", this);
            enabled = false;
            return;
        }

        progressionService = new CharacterProgressionService();

        controls = PlayerControlsProvider.Controls;
        // find all button / text / etc and assign to variables. bail before starting any work if
        // the scene cannot satisfy the UI contract, rather than half-initializing (AUD-047)
        if (!getUiObjectReferences())
        {
            enabled = false;
            return;
        }

        // dont destroy on load / check for duplicate instance
        //destroyInstanceIfAlreadyExists();
        StartCoroutine(getLoadedData());

        //default index for player selected
        playerSelectedIndex = GameOptions.playerSelectedIndex;
    }

    // Start is called before the first frame update
    void Start()
    {
        //AnaylticsManager.MenuProgressionLoaded();
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();
        ResolveButtonReferences();
        RegisterButtonCallbacks();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        // default display
        StartCoroutine(InitializeDisplay());
        initialized = true;
    }

    // Update is called once per frame
    void Update()
    {
        GameObject selectedObject = UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        if (selectedObject == null)
        {
            return;
        }

        currentHighlightedButton = selectedObject.name; // + "_description";
        HandleSelectedProgressionControl(selectedObject);
    }

    private void HandleSelectedProgressionControl(GameObject selectedObject)
    {
        if (buttonPressed || string.IsNullOrEmpty(currentHighlightedButton))
        {
            return;
        }

        HandleSelectionMovement(selectedObject);
        HandleProgressionInput();
    }

    private void HandleSelectionMovement(GameObject selectedObject)
    {
        Button selectedButton = selectedObject.GetComponent<Button>();

        // right, go to change options
        if (controls.UINavigation.Right.triggered
            && selectedButton != null
            && selectedButton.FindSelectableOnRight() != null
            && currentHighlightedButton.Equals(playerSelectButtonName))
        {
            UiSelectionAdapter.TrySelect(selectedButton.FindSelectableOnRight().gameObject);
        }

        // left, return to option select
        if (controls.UINavigation.Left.triggered
            && selectedButton != null
            && currentHighlightedButton.Equals(playerSelectButtonName))
        {
            // check if button exists. if no selectable on left, throws null object exception
            if (selectedButton.FindSelectableOnLeft() != null)
            {
                UiSelectionAdapter.TrySelect(selectedButton.FindSelectableOnLeft().gameObject);
            }
        }
    }

    private void HandleProgressionInput()
    {
        // ================================== change options =============================================================
        // up, change options
        if (controls.UINavigation.Up.triggered && currentHighlightedButton.Equals(playerSelectOptionButtonName))
        {
            changePlayerUp();
        }
        // down, change option
        if (controls.UINavigation.Down.triggered && currentHighlightedButton.Equals(playerSelectOptionButtonName))
        {
            changePlayerDown();
        }
        // add a point to selected category
        if (!buttonPressed && dataLoaded
            && progressionState.PointsAvailable > 0
            && controls.UINavigation.Submit.triggered
            && IsProgressionStatButton(currentHighlightedButton))
        {
            addPoint();
        }
        // subtract a point
        if (!buttonPressed && dataLoaded
            && controls.UINavigation.Cancel.triggered
            && IsProgressionStatButton(currentHighlightedButton))
        {
            subtractPoint();
        }
    }

    private void ResolveButtonReferences()
    {
        startButton = ResolveButton(startButton, startButtonName);
        statsMenuButton = ResolveButton(statsMenuButton, statsMenuButtonName);
        quitButton = ResolveButton(quitButton, quitButtonName);
        playerSelectButton = ResolveButton(playerSelectButton, playerSelectButtonName);
        playerSelectOptionButton = ResolveButton(playerSelectOptionButton, playerSelectOptionButtonName);
        progression3AccuracyButton = ResolveButton(progression3AccuracyButton, progression3AccuracyName);
        progression4AccuracyButton = ResolveButton(progression4AccuracyButton, progression4AccuracyName);
        progression7AccuracyButton = ResolveButton(progression7AccuracyButton, progression7AccuracyName);
        confirmButton = ResolveButton(confirmButton, confirmButtonName);
        cancelButton = ResolveButton(cancelButton, cancelButtonName);
        saveButton = ResolveButton(saveButton, saveButtonName);
        resetButton = ResolveButton(resetButton, resetButtonName);
    }

    private Button ResolveButton(Button button, string buttonName)
    {
        if (button != null)
        {
            return button;
        }

        GameObject buttonObject = GameObject.Find(buttonName);
        if (buttonObject != null)
        {
            return buttonObject.GetComponent<Button>();
        }

        return FindButtonInInactiveChildren(buttonName);
    }

    private Button FindButtonInInactiveChildren(string buttonName)
    {
        if (confirmationDialogueBox == null)
        {
            return null;
        }

        Button[] buttons = confirmationDialogueBox.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.name.Equals(buttonName))
            {
                return button;
            }
        }

        return null;
    }

    private void RegisterButtonCallbacks()
    {
        RegisterRequiredButtonCallback(startButton, StartGame);
        RegisterRequiredButtonCallback(statsMenuButton, LoadStatsMenu);
        RegisterRequiredButtonCallback(quitButton, QuitGame);
        RegisterRequiredButtonCallback(saveButton, saveChanges);
        RegisterRequiredButtonCallback(resetButton, resetChanges);
        RegisterRequiredButtonCallback(confirmButton, confirmChanges);
        RegisterRequiredButtonCallback(cancelButton, cancelChanges);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(startButton, StartGame);
        UiSelectionAdapter.UnregisterButton(statsMenuButton, LoadStatsMenu);
        UiSelectionAdapter.UnregisterButton(quitButton, QuitGame);
        UiSelectionAdapter.UnregisterButton(saveButton, saveChanges);
        UiSelectionAdapter.UnregisterButton(resetButton, resetChanges);
        UiSelectionAdapter.UnregisterButton(confirmButton, confirmChanges);
        UiSelectionAdapter.UnregisterButton(cancelButton, cancelChanges);
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

    private GameObject GetDefaultSelectedButton()
    {
        if (EventSystem.current != null && EventSystem.current.firstSelectedGameObject != null)
        {
            return EventSystem.current.firstSelectedGameObject;
        }

        if (playerSelectOptionButton != null)
        {
            return playerSelectOptionButton.gameObject;
        }

        return startButton != null ? startButton.gameObject : null;
    }

    private bool IsProgressionStatButton(string selectedButtonName)
    {
        return selectedButtonName.Equals(progression3AccuracyName)
            || selectedButtonName.Equals(progression4AccuracyName)
            || selectedButtonName.Equals(progression7AccuracyName);
    }

    private void SelectProgressionButton(Button button, string buttonName)
    {
        Button targetButton = ResolveButton(button, buttonName);
        if (targetButton != null)
        {
            UiSelectionAdapter.TrySelect(targetButton.gameObject);
        }
    }

    private bool HasSelectedCharacterData()
    {
        return dataLoaded
            && playerSelectedData != null
            && playerSelectedData.Count > 0
            && playerSelectedIndex >= 0
            && playerSelectedIndex < playerSelectedData.Count;
    }

    private void RunProgressionAction(Action action)
    {
        if (buttonPressed || action == null || lastActionFrame == Time.frameCount)
        {
            return;
        }

        buttonPressed = true;
        lastActionFrame = Time.frameCount;
        try
        {
            action();
        }
        catch (Exception e)
        {
            Debug.Log("ERROR : " + e);
        }
        finally
        {
            buttonPressed = false;
        }
    }

    public void StartGame()
    {
        RunProgressionAction(() =>
        {
            ConfirmChangesInternal();
            loadScene(Constants.SCENE_NAME_level_00_start);
        });
    }

    public void LoadStatsMenu()
    {
        RunProgressionAction(() =>
        {
            ConfirmChangesInternal();
            loadScene(Constants.SCENE_NAME_level_00_stats);
        });
    }

    public void QuitGame()
    {
        RunProgressionAction(() =>
        {
            ConfirmChangesInternal();
            Application.Quit();
        });
    }

    public void changePlayerUp()
    {
        RunProgressionAction(() =>
        {
            if (!HasSelectedCharacterData())
            {
                return;
            }

            changeSelectedPlayerUp();
            initializePlayerDisplay();
        });
    }

    public void changePlayerDown()
    {
        RunProgressionAction(() =>
        {
            if (!HasSelectedCharacterData())
            {
                return;
            }

            changeSelectedPlayerDown();
            initializePlayerDisplay();
        });
    }

    public void addPoint()
    {
        RunProgressionAction(() =>
        {
            if (!HasSelectedCharacterData())
            {
                return;
            }

            if (currentHighlightedButton.Equals(progression3AccuracyName))
            {
                updateThreeAccuracy(UpdateType.Add);
            }
            if (currentHighlightedButton.Equals(progression4AccuracyName))
            {
                updateFourAccuracy(UpdateType.Add);
            }
            if (currentHighlightedButton.Equals(progression7AccuracyName))
            {
                updateSevenAccuracy(UpdateType.Add);
            }
            initializePlayerDisplay();
        });
    }

    public void subtractPoint()
    {
        RunProgressionAction(() =>
        {
            if (!HasSelectedCharacterData())
            {
                return;
            }

            if (currentHighlightedButton.Equals(progression3AccuracyName)
                && (progressionState.AddTo3 > 0 || progressionState.AddToRange > 0))
            {
                updateThreeAccuracy(UpdateType.Subtract);
            }
            if (currentHighlightedButton.Equals(progression4AccuracyName)
                && (progressionState.AddTo4 > 0 || progressionState.AddToRange > 0))
            {
                updateFourAccuracy(UpdateType.Subtract);
            }
            if (currentHighlightedButton.Equals(progression7AccuracyName)
                && (progressionState.AddTo7 > 0 || progressionState.AddToRange > 0))
            {
                updateSevenAccuracy(UpdateType.Subtract);
            }
            initializePlayerDisplay();
        });
    }

    public void saveChanges()
    {
        RunProgressionAction(() =>
        {
            SetConfirmationDialogueActive(true);
            SelectProgressionButton(confirmButton, confirmButtonName);
        });
    }

    public void cancelChanges()
    {
        RunProgressionAction(() =>
        {
            SetConfirmationDialogueActive(false);
            SelectProgressionButton(progression3AccuracyButton, progression3AccuracyName);
        });
    }

    // Awake null-checks confirmationDialogueBox but the three call sites that toggle it did not,
    // so a scene without the dialogue threw into RunProgressionAction's catch and the save silently
    // did nothing (AUD-047).
    private void SetConfirmationDialogueActive(bool active)
    {
        if (confirmationDialogueBox == null)
        {
            return;
        }

        confirmationDialogueBox.SetActive(active);
    }

    public void resetChanges()
    {
        RunProgressionAction(() =>
        {
            if (!HasSelectedCharacterData())
            {
                return;
            }

            ResetSelectedCharacterDraft();
            initializePlayerDisplay();

            SelectProgressionButton(progression3AccuracyButton, progression3AccuracyName);
        });
    }

    public void confirmChanges()
    {
        RunProgressionAction(ConfirmChangesInternal);
    }

    private void ConfirmChangesInternal()
    {
        if (!HasSelectedCharacterData())
        {
            return;
        }

        if (progressionDraft == null)
        {
            ResetSelectedCharacterDraft();
        }

        if (!progressionService.CommitDraft(progressionDraft, playerSelectedData[playerSelectedIndex]))
        {
            SelectProgressionButton(confirmButton, confirmButtonName);
            return;
        }

        // disable pop up
        SetConfirmationDialogueActive(false);
        // reset points
        ResetSelectedCharacterDraft();
        // display
        initializePlayerDisplay();
        // reset stats
        SelectProgressionButton(progression3AccuracyButton, progression3AccuracyName);
    }

    public void resetUpdatePoints()
    {
        if (!HasSelectedCharacterData())
        {
            return;
        }

        ResetSelectedCharacterDraft();
    }

    public void updateThreeAccuracy(UpdateType updateType)
    {
        if (!HasSelectedCharacterData())
        {
            return;
        }

        EnsureSelectedCharacterDraft();
        if (updateType == UpdateType.Add)
        {
            progressionService.TryAddAccuracy3(progressionDraft, progressionState);
        }
        else if (updateType == UpdateType.Subtract)
        {
            progressionService.TrySubtractAccuracy3(progressionDraft, progressionState);
        }

        SyncDraftToStateAndLabels();
        initializePlayerDisplay();
    }
    public void updateFourAccuracy(UpdateType updateType)
    {
        if (!HasSelectedCharacterData())
        {
            return;
        }

        EnsureSelectedCharacterDraft();
        if (updateType == UpdateType.Add)
        {
            progressionService.TryAddAccuracy4(progressionDraft, progressionState);
        }
        else if (updateType == UpdateType.Subtract)
        {
            progressionService.TrySubtractAccuracy4(progressionDraft, progressionState);
        }

        SyncDraftToStateAndLabels();
        initializePlayerDisplay();
    }
    public void updateSevenAccuracy(UpdateType updateType)
    {
        if (!HasSelectedCharacterData())
        {
            return;
        }

        EnsureSelectedCharacterDraft();
        if (updateType == UpdateType.Add)
        {
            progressionService.TryAddAccuracy7(progressionDraft, progressionState);
        }
        else if (updateType == UpdateType.Subtract)
        {
            progressionService.TrySubtractAccuracy7(progressionDraft, progressionState);
        }

        SyncDraftToStateAndLabels();
        initializePlayerDisplay();
    }


    //private void loadScene()
    //{
    //    throw new NotImplementedException();
    //}

    //IEnumerator UpdateLevelAndExperienceFromDatabase()
    //{
    //    yield return new WaitUntil(() => dataLoaded);

    //    foreach (CharacterProfile s in playerSelectedData)
    //    {
    //        s.Experience = DBHelper.instance.getIntValueFromTableByFieldAndCharId("CharacterProfile", "experience", s.PlayerId);
    //        s.Level = DBHelper.instance.getIntValueFromTableByFieldAndCharId("CharacterProfile", "level", s.PlayerId);
    //    }
    //}

    IEnumerator getLoadedData()
    {
        if (LoadedData.instance != null)
        {
            yield return WaitForCondition(() => LoadedData.instance != null
                && !LoadedData.instance.LoadFailed
                && LoadedData.instance.PlayerSelectedData != null
                && LoadedData.instance.CheerleaderSelectedData != null);

            if (LoadedData.instance == null
                || LoadedData.instance.LoadFailed
                || LoadedData.instance.PlayerSelectedData == null
                || LoadedData.instance.CheerleaderSelectedData == null)
            {
                ReturnToLoadingScene();
                yield break;
            }

            playerSelectedData = LoadedData.instance.PlayerSelectedData;
            cheerleaderSelectedData = LoadedData.instance.CheerleaderSelectedData;

            // GameOptions.playerSelectedIndex is written by StartManager against a roster that may
            // have had a different length. Every user-driven path goes through
            // HasSelectedCharacterData, but InitializeDisplay indexes the list directly, so clamp
            // once here rather than relying on the two lists always matching (AUD-047).
            if (playerSelectedData != null && playerSelectedIndex >= playerSelectedData.Count)
            {
                playerSelectedIndex = 0;
            }

            if (playerSelectedData != null
                && cheerleaderSelectedData != null)
            {
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
        //Debug.Log("------------------------------- start manager InitializeDisplay");
        yield return WaitForCondition(() => dataLoaded);
        if (!HasSelectedCharacterData())
        {
            yield break;
        }

        // display default data
        progressionState.clearState();
        InitializeSelectedCharacterDraft();
        // init display
        initializePlayerDisplay();

    }
    // ============================  get UI buttons / text references ==============================
    /// <summary>
    /// Resolves every UI object this manager drives, collecting the names that are missing rather
    /// than throwing on the first one. Returns false if any is absent.
    ///
    /// This was 17 consecutive `GameObject.Find(name).GetComponent&lt;T&gt;()` chains (AUD-047, the
    /// same shape AUD-028 fixed in GameRules and Pause). A single renamed object threw partway
    /// through Awake, which left `instance` published, `playerSelectedIndex` never read from
    /// GameOptions, and no message naming what was missing.
    /// </summary>
    private bool getUiObjectReferences()
    {
        List<string> missing = new List<string>();

        // buttons to disable for touch input
        playerSelectButton = SceneObjects.Find<Button>(playerSelectButtonName, missing, this);

        // player object with lock texture and unlock text
        playerSelectOptionText = SceneObjects.Find<Text>(playerSelectOptionButtonName, missing, this);
        playerSelectOptionImage = SceneObjects.Find<Image>(playerSelectImageObjectName, missing, this);
        playerProgressionStatsText = SceneObjects.Find<Text>(playerProgressionStatsName, missing, this);
        playerProgressionUpdatePointsText = SceneObjects.Find<Text>(playerProgressionPointsAvailableName, missing, this);

        progression3Accuracy = SceneObjects.Find<Text>(progression3AccuracyTextName, missing, this);
        progression4Accuracy = SceneObjects.Find<Text>(progression4AccuracyTextName, missing, this);
        progression7Accuracy = SceneObjects.Find<Text>(progression7AccuracyTextName, missing, this);
        progressionRange = SceneObjects.Find<Text>(progressionRangeName, missing, this);
        progressionRelease = SceneObjects.Find<Text>(progressionReleaseName, missing, this);
        progressionSpeed = SceneObjects.Find<Text>(progressionSpeedName, missing, this);
        progressionJump = SceneObjects.Find<Text>(progressionJumpName, missing, this);
        progressionLuck = SceneObjects.Find<Text>(progressionLuckName, missing, this);

        bonusReleaseText = SceneObjects.Find<Text>(releaseBonusName, missing, this);
        bonusRangeText = SceneObjects.Find<Text>(rangeBonusName, missing, this);
        bonusLuckText = SceneObjects.Find<Text>(luckBonusName, missing, this);

        addTo3Text = SceneObjects.Find<Text>(progression3AccuracyName, missing, this);
        addTo4Text = SceneObjects.Find<Text>(progression4AccuracyName, missing, this);
        addTo7Text = SceneObjects.Find<Text>(progression7AccuracyName, missing, this);

        if (missing.Count > 0)
        {
            Debug.LogError(
                "ProgressionManager is missing scene objects and will be disabled: "
                + string.Join(", ", missing.ToArray()),
                this);
            return false;
        }

        return true;
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

    public void disableButtonsNotUsedForTouchInput()
    {
        //Debug.Log("disable buttons for touch");
        //levelSelectButton.enabled = false;
        //trafficSelectButton.enabled = false;
        if (playerSelectButton != null)
        {
            playerSelectButton.enabled = false;
        }
        //CheerleaderSelectButton.enabled = false;
        //modeSelectButton.enabled = false;
    }


    public void initializePlayerDisplay()
    {
        try
        {
            // name and portrait
            playerSelectOptionText.text = playerSelectedData[playerSelectedIndex].PlayerDisplayName;
            playerSelectOptionImage.sprite = playerSelectedData[playerSelectedIndex].PlayerPortrait;

            // update text display static update stats (range, release, luck)
            //if (playerSelectedData[playerSelectedIndex].PointsAvailable > 0)  

            if (progressionState.PointsUsedThisSession > 0)
            {
                if (progressionState.Release < progressionState.MaxReleaseAccuraccy)
                {
                    bonusReleaseText.text = "+" + progressionState.AddToRelease;
                }
                else
                {
                    bonusReleaseText.text = "MAX";
                }
                if (progressionState.Luck < progressionState.MaxLuck)
                {
                    bonusLuckText.text = "+" + progressionState.AddToLuck;
                }
                else
                {
                    bonusLuckText.text = "MAX";
                }
                bonusRangeText.text = "+" + progressionState.AddToRange;
            }
            else
            {
                bonusReleaseText.text = "";
                bonusRangeText.text = "";
                bonusLuckText.text = "";
            }
            //// luck point only available every 3rd level
            //bonusLuckText.text = progressionState.AddToLuck == 0
            //    ? bonusLuckText.text = ""
            //    : "+" + progressionState.AddToLuck.ToString();

            // set text displays

            // these DO NOT have max limits
            progressionRange.text = progressionState.Range.ToString("F0") + " ft";
            progressionSpeed.text = playerSelectedData[playerSelectedIndex].calculateSpeedToPercent().ToString("F0");
            progressionJump.text = playerSelectedData[playerSelectedIndex].calculateJumpValueToPercent().ToString("F0");

            // these DO have max limits
            //release
            if (progressionState.Release < progressionState.MaxReleaseAccuraccy)
            {
                progressionRelease.text = progressionState.Release.ToString("F0");
            }
            else
            {
                progressionRelease.text = progressionState.Release.ToString("F0") + " MAX";
            }
            // luck
            if (progressionState.Luck < progressionState.MaxLuck)
            {
                progressionLuck.text = progressionState.Luck.ToString("F0");
            }
            else
            {
                progressionLuck.text = progressionState.Luck.ToString("F0") + " MAX";
            }
            // 3 accuracy
            if (progressionState.Accuracy3 < progressionState.MaxThreeAccuraccy)
            {
                progression3Accuracy.text = progressionState.Accuracy3.ToString("F0");
            }
            else
            {
                progression3Accuracy.text = progressionState.Accuracy3.ToString("F0") + " MAX";
            }
            // 4 accuracy
            if (progressionState.Accuracy4 < progressionState.MaxFourAccuraccy)
            {
                progression4Accuracy.text = progressionState.Accuracy4.ToString("F0");
            }
            // 7 accuracy
            else
            {
                progression4Accuracy.text = progressionState.Accuracy4.ToString("F0") + " MAX";
            }
            if (progressionState.Accuracy7 < progressionState.MaxSevenAccuraccy)
            {
                progression7Accuracy.text = progressionState.Accuracy7.ToString("F0");
            }
            else
            {
                progression7Accuracy.text = progressionState.Accuracy7.ToString("F0") + " MAX";
            }

            // get level by experience - same curve StartManager and DBHelper use (AUD-036/AUD-046)
            progressionState.Level = CharacterLevel.FromExperience(progressionState.Experience);
            int nextlvl = CharacterLevel.ExperienceToNextLevel(progressionState.Experience);
            // display lvl, exp, exp for next lvl
            playerProgressionStatsText.text = progressionState.Level.ToString("F0") + "\n"
                + progressionState.Experience.ToString("F0") + "\n"
                + nextlvl.ToString("F0") + "\n";
            playerProgressionUpdatePointsText.text = "points available : " + progressionState.PointsAvailable.ToString();
            // not sure what this is for but im not gonna touch it yet
            GameOptions.characterObjectName = playerSelectedData[playerSelectedIndex].PlayerObjectName;
        }
        catch (Exception e)
        {
            Debug.Log("ERROR : " + e);
            return;
        }
    }

    public void loadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    //// ============================  message display ==============================
    //// used in this context to display if item is locked

    //public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    //{
    //    yield return new WaitForSecondsRealtime(seconds);
    //    Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
    //    messageText.text = "";
    //}

    // ============================  navigation functions ==============================
    private void changeSelectedPlayerUp()
    {
        //progressionState.clearState();

        playerSelectedIndex =
            (playerSelectedIndex == 0
            ? playerSelectedData.Count - 1
            : playerSelectedIndex -= 1);

        InitializeSelectedCharacterDraft();
    }
    private void changeSelectedPlayerDown()
    {
        //progressionState.clearState();

        playerSelectedIndex =
            ((playerSelectedIndex == playerSelectedData.Count - 1)
            ? playerSelectedIndex = 0
            : playerSelectedIndex += 1);

        InitializeSelectedCharacterDraft();

    }

    private void EnsureSelectedCharacterDraft()
    {
        if (progressionDraft == null || progressionDraft.PlayerId != playerSelectedData[playerSelectedIndex].PlayerId)
        {
            InitializeSelectedCharacterDraft();
        }
    }

    private void InitializeSelectedCharacterDraft()
    {
        progressionDraft = progressionService.CreateDraft(playerSelectedData[playerSelectedIndex]);
        SyncDraftToStateAndLabels();
    }

    private void ResetSelectedCharacterDraft()
    {
        EnsureSelectedCharacterDraft();
        progressionService.ResetDraft(progressionDraft, playerSelectedData[playerSelectedIndex], progressionState);
        SyncDraftToStateAndLabels();
    }

    private void SyncDraftToStateAndLabels()
    {
        progressionService.ApplyDraftToState(progressionDraft, progressionState);
        addTo3Text.text = progressionState.AddTo3 > 0 ? "+" + progressionState.AddTo3 : "--";
        addTo4Text.text = progressionState.AddTo4 > 0 ? "+" + progressionState.AddTo4 : "--";
        addTo7Text.text = progressionState.AddTo7 > 0 ? "+" + progressionState.AddTo7 : "--";
    }
}
