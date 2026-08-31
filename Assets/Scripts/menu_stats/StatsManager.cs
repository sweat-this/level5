
using Assets.Scripts.database;
using Assets.Scripts.restapi;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StatsManager : MonoBehaviour
{
    [SerializeField]
    private string currentHighlightedButton;
    [SerializeField]
    private string previousHighlightedButton;

    [SerializeField] private StatsUiObjects ui;

    const string modeSelectButtonName = "mode_select_name";
    const string modeSelectButtonOnlineName = "mode_select_name_online";
    const string alltimeSelectButtonName = "all_time_select";
    const string mainMenuButtonName = "main_menu";
    const string pageNumberLocalButtonName = "page_number_local";
    const string pageNumberOnlineButtonName = "page_number_online";
    //options button names
    const string hardcoreOptionButtonName = "hardcore_value_button";
    const string trafficOptionButtonName = "traffic_value_button";
    const string enemiesOptionButtonName = "enemies_value_button";
    const string sniperOptionButtonName = "sniper_value_button";

    // table names
    const string highScoreTableName = "high_scores_table";
    const string allTimeTableName = "all_time_table";

    // page size lives in StatsPaging so the row count, the page arithmetic, and the SQL LIMIT
    // cannot drift apart
    const int ResultsPerPage = StatsPaging.ResultsPerPage;
    //const string mainMenuSceneName = "level_00_start";

    // RequiredSceneObjectNames retired: Level5ProjectValidator now asserts this screen's contract
    // through ValidateMenuUi/CollectMenuUiObjectContractErrors instead of a name list (AUD-103).

    GameObject allTimeTableObject;
    GameObject highScoreTableObject;
    // AUD-092 Phase 2: these used to be [SerializeField] Text fields on StatsManager itself. They are
    // now resolved from the serialized ui view in ResolveButtonReferences, matching the Button fields
    // below (AUD-103).
    TextMeshProUGUI modeSelectButtonText;
    TextMeshProUGUI modeSelectButtonHardcoreText;
    TextMeshProUGUI modeSelectButtonOnlineText;
    TextMeshProUGUI pageNumberLocalSelectButtonText;
    TextMeshProUGUI pageNumberOnlineSelectButtonText;

    Button modeSelectButton;
    Button modeSelectOnlineButton;
    Button allTimeSelectButton;
    Button mainMenuButton;
    Button pageNumberLocalButton;
    Button pageNumberOnlineButton;
    Button trafficOptionButton;
    Button hardcoreOptionButton;
    Button enemiesOptionButton;
    Button sniperOptionButton;

    // list of high score rows
    [SerializeField]
    List<StatsTableHighScoreRow> highScoreRowsDataList;
    //list of high score row objects
    [SerializeField]
    List<GameObject> highScoreRowsObjectsList;
    // list of modes
    List<mode> modesList;
    //list of unsubmitted highscores
    [SerializeField]
    List<HighScoreModel> unsubmittedHighScores;
    [SerializeField]
    int numUnsubmittedHighscores;

    [SerializeField]
    private bool trafficEnabled;
    [SerializeField]
    private bool hardcoreEnabled;
    [SerializeField]
    private bool enemiesEnabled;
    [SerializeField]
    private bool sniperEnabled;

    //selectable option text
    private TextMeshProUGUI trafficSelectOptionText;
    private TextMeshProUGUI hardcoreSelectOptionText;
    private TextMeshProUGUI enemySelectOptionText;
    private TextMeshProUGUI sniperSelectOptionText;
    private TextMeshProUGUI submittedHighscoresText;
    private TextMeshProUGUI numUnsubmittedHighscoresText;

    int defaultModeSelectedIndex;
    int currentModeSelectedIndex;

    // high score results pagination
    [SerializeField]
    int localResultsPageNumber;
    [SerializeField]
    int onlineResultsPageNumber;

    // high score rows
    const string highScoreRowPrefabPath = "Prefabs/stats/highScoreRow";
    const string highScoresRowsName = "high_scores_rows";
    GameObject highScoresRowsObject;
    [SerializeField]
    GameObject highScoreRowPrefab;
    [SerializeField]
    bool localLoaded;
    [SerializeField]
    bool onlineLoaded;

    bool buttonPressed;

    private const string trafficSelectValueName = "traffic_value_button";
    private const string hardcoreSelectValueName = "hardcore_value_button";
    private const string enemySelectValueName = "enemies_value_button";
    private const string sniperSelectValueName = "sniper_value_button";

    public int numLocalResults;
    public int numOnlineResults;

    private bool initialized;
    private int onlineRequestVersion;
    private int lastActionFrame = -1;

    public static StatsManager instance;

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

    // for input system
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

    // store data for each row
    public class mode
    {
        public int modeSelectedId;
        public string modeSelectedName;
        public string modeSelectedHighScoreField;

        // constructor
        public mode(int modeid, string modeName, string field)
        {
            modeSelectedId = modeid;
            modeSelectedName = modeName;
            modeSelectedHighScoreField = field;
        }
    }

    void Awake()
    {
        instance = this;

        List<string> missing = new List<string>();
        if (!ValidateMenuUi(missing))
        {
            Debug.LogError(
                "StatsManager is missing required serialized UI references and will be disabled: "
                    + string.Join(", ", missing.ToArray()),
                this);
            enabled = false;
            return;
        }

        // table objects
        highScoreTableObject = ui.HighScoreTableObject;
        allTimeTableObject = ui.AllTimeTableObject;

        // parent object where rows will be instantiated
        // ex. usage Instantiate(prefab, position, quaternion, parent object);
        highScoresRowsObject = ui.HighScoresRowsObject;

        // get mode ids and display names. mode ids will be used for queries to display data
        modesList = getModeSelectDataList();

        defaultModeSelectedIndex = 0;
        currentModeSelectedIndex = defaultModeSelectedIndex;

        // row prefab to be instantiated
        highScoreRowPrefab = Resources.Load(highScoreRowPrefabPath) as GameObject;

        // get mode id of default game mode
        string field = modesList[defaultModeSelectedIndex].modeSelectedHighScoreField;

        // get data for default mode to be displayed
        if (GameObject.FindGameObjectWithTag("database") != null)
        {
            // get default high score list + num results
            highScoreRowsDataList =
            DBHelper.instance.getListOfHighScoreRowsFromTableByModeIdAndField(field,
                modesList[defaultModeSelectedIndex].modeSelectedId,
                hardcoreEnabled,
                trafficEnabled,
                enemiesEnabled,
                sniperEnabled,
                localResultsPageNumber);
        }
    }

    private void Start()
    {
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        UiSelectionAdapter.EnsureInputSystemUiModule();
        ResolveButtonReferences();

        // default page number value, start on first page
        localResultsPageNumber = 0;
        onlineResultsPageNumber = 0;

        AnaylticsManager.MenuStatsLoaded();

        // create rows dor data display
        if (highScoreRowsDataList == null)
        {
            highScoreRowsDataList = new List<StatsTableHighScoreRow>();
        }

        int initialRowCount = Mathf.Max(ResultsPerPage, highScoreRowsDataList.Count);

        // AUD-106: the row values used to be written into the shared Resources prefab and then
        // copied out by instantiating it. That mutated the asset itself - in the editor it dirtied
        // highScoreRow.prefab on disk - and it is the same defect class as AUD-020. Instantiate
        // first, then write into the instance.
        //
        // AUD-107: the list is now built in creation order. It used to be re-derived from
        // GameObject.FindGameObjectsWithTag, which guarantees no ordering and skips inactive
        // objects, while the rows are indexed positionally against a ranked query result - so the
        // leaderboard order on screen did not have to be the order the rows came back in.
        highScoreRowsObjectsList = new List<GameObject>(initialRowCount);
        for (int i = 0; i < initialRowCount; i++)
        {
            // same placement as before: the row parents into high_scores_rows, whose layout group
            // owns the final position
            GameObject rowObject = Instantiate(
                highScoreRowPrefab,
                highScoresRowsObject.transform.position,
                Quaternion.identity,
                highScoresRowsObject.transform);
            highScoreRowsObjectsList.Add(rowObject);

            StatsTableHighScoreRow source = i < highScoreRowsDataList.Count ? highScoreRowsDataList[i] : null;
            SetHighScoreRow(i, source);
        }

        // default table view
        if (!highScoreTableObject.activeSelf)
        {
            highScoreTableObject.SetActive(true);
        }
        if (allTimeTableObject.activeSelf)
        {
            allTimeTableObject.SetActive(false);
        }

        initializeTrafficOptionDisplay();
        initializeHardcoreOptionDisplay();
        initializeEnemyOptionDisplay();
        initializeSniperOptionDisplay();

        initializeLocalPageNumberDisplay();
        initializeOnlinePageNumberDisplay();

        changeHighScoreDataDisplay();
        changeHighScoreDataDisplayOnline();
        getUnsubmittedHighscores();
        //submitUnsubmittedScores();
        RegisterButtonCallbacks();
        UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        initialized = true;
    }

    /// <summary>
    /// Copies references out of the serialized <see cref="ui"/> view, which
    /// <see cref="ValidateMenuUi"/> has already confirmed is complete. Replaces the
    /// <c>GameObject.Find(name)</c> chain this used to fall back to (AUD-103).
    /// </summary>
    private void ResolveButtonReferences()
    {
        modeSelectButton = ui.ModeSelectButton;
        modeSelectOnlineButton = ui.ModeSelectOnlineButton;
        allTimeSelectButton = ui.AllTimeSelectButton;
        mainMenuButton = ui.MainMenuButton;
        pageNumberLocalButton = ui.PageNumberLocalButton;
        pageNumberOnlineButton = ui.PageNumberOnlineButton;
        trafficOptionButton = ui.TrafficOptionButton;
        hardcoreOptionButton = ui.HardcoreOptionButton;
        enemiesOptionButton = ui.EnemiesOptionButton;
        sniperOptionButton = ui.SniperOptionButton;

        modeSelectButtonText = ui.ModeSelectText;
        modeSelectButtonHardcoreText = ui.ModeSelectHardcoreText;
        modeSelectButtonOnlineText = ui.ModeSelectOnlineText;
        pageNumberLocalSelectButtonText = ui.PageNumberLocalText;
        pageNumberOnlineSelectButtonText = ui.PageNumberOnlineText;
        trafficSelectOptionText = ui.TrafficOptionValueText;
        hardcoreSelectOptionText = ui.HardcoreOptionValueText;
        enemySelectOptionText = ui.EnemiesOptionValueText;
        sniperSelectOptionText = ui.SniperOptionValueText;
        submittedHighscoresText = ui.SubmittedHighscoresText;
        numUnsubmittedHighscoresText = ui.NumUnsubmittedHighscoresText;
    }

    /// <summary>
    /// True once <see cref="ui"/> carries every reference this screen needs. Callable from editor
    /// tooling as a pure check - it only reads an already-serialized reference.
    /// </summary>
    public bool ValidateMenuUi(List<string> missing)
    {
        if (ui == null)
        {
            missing.Add("StatsManager.ui");
            return false;
        }

        ui.Validate(missing);
        return missing.Count == 0;
    }

    /// <summary>
    /// Every stats control that changes a value now has an onClick route (AUD-096, AUD-098).
    ///
    /// Mode and page step forward only. Both wrap - <see cref="StatsPaging.NextPage"/> and
    /// <see cref="changeSelectedMode"/> cycle - so every page and every mode is still reachable
    /// with one control, which is how the start menu's option buttons already behave. What is gone
    /// is stepping backwards with Left, which was never available to mouse or touch anyway.
    /// </summary>
    private void RegisterButtonCallbacks()
    {
        UiSelectionAdapter.RegisterButton(mainMenuButton, LoadStartMenu);
        UiSelectionAdapter.RegisterButton(modeSelectButton, ChangeLocalModeRight);
        UiSelectionAdapter.RegisterButton(modeSelectOnlineButton, ChangeOnlineModeRight);
        UiSelectionAdapter.RegisterButton(pageNumberLocalButton, IncreaseLocalPage);
        UiSelectionAdapter.RegisterButton(pageNumberOnlineButton, IncreaseOnlinePage);
        UiSelectionAdapter.RegisterButton(allTimeSelectButton, ShowAllTimeTable);
        UiSelectionAdapter.RegisterButton(trafficOptionButton, ToggleTrafficFilter);
        UiSelectionAdapter.RegisterButton(hardcoreOptionButton, ToggleHardcoreFilter);
        UiSelectionAdapter.RegisterButton(enemiesOptionButton, ToggleEnemiesFilter);
        UiSelectionAdapter.RegisterButton(sniperOptionButton, ToggleSniperFilter);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(mainMenuButton, LoadStartMenu);
        UiSelectionAdapter.UnregisterButton(modeSelectButton, ChangeLocalModeRight);
        UiSelectionAdapter.UnregisterButton(modeSelectOnlineButton, ChangeOnlineModeRight);
        UiSelectionAdapter.UnregisterButton(pageNumberLocalButton, IncreaseLocalPage);
        UiSelectionAdapter.UnregisterButton(pageNumberOnlineButton, IncreaseOnlinePage);
        UiSelectionAdapter.UnregisterButton(allTimeSelectButton, ShowAllTimeTable);
        UiSelectionAdapter.UnregisterButton(trafficOptionButton, ToggleTrafficFilter);
        UiSelectionAdapter.UnregisterButton(hardcoreOptionButton, ToggleHardcoreFilter);
        UiSelectionAdapter.UnregisterButton(enemiesOptionButton, ToggleEnemiesFilter);
        UiSelectionAdapter.UnregisterButton(sniperOptionButton, ToggleSniperFilter);
    }

    private GameObject GetDefaultSelectedButton()
    {
        if (EventSystem.current != null && EventSystem.current.firstSelectedGameObject != null)
        {
            return EventSystem.current.firstSelectedGameObject;
        }

        if (modeSelectButton != null)
        {
            return modeSelectButton.gameObject;
        }

        return mainMenuButton != null ? mainMenuButton.gameObject : null;
    }


    // Update is called once per frame
    void Update()
    {
        GameObject selectedObject = UiSelectionAdapter.EnsureSelected(GetDefaultSelectedButton());
        if (selectedObject == null)
        {
            return;
        }

        currentHighlightedButton = selectedObject.name;

        HandleSelectedStatsControl();
        previousHighlightedButton = currentHighlightedButton;
    }

    /// <summary>
    /// Shows the table that matches the current selection.
    ///
    /// This used to also poll <c>UINavigation.Up/Down/Left/Right</c> and change the mode, page and
    /// filter values from here (AUD-096). The InputSystemUIInputModule consumes the same press to
    /// move selection, so one Left press both moved the selection and stepped the page - and each
    /// step runs a synchronous SQLite query. Unlike StartManager and ProgressionManager this screen
    /// had no per-frame guard, so it was the one actually double-actuating. Value changes now
    /// arrive through Button.onClick only; see RegisterButtonCallbacks.
    /// </summary>
    private void HandleSelectedStatsControl()
    {
        if (buttonPressed || string.IsNullOrEmpty(currentHighlightedButton))
        {
            return;
        }

        if (currentHighlightedButton.Equals(modeSelectButtonName))
        {
            ShowHighScoreTable();
            if (previousHighlightedButton != modeSelectButtonName)
            {
                changeHighScoreDataDisplay();
            }

            if (modeSelectButtonText != null)
            {
                modeSelectButtonText.text = modesList[currentModeSelectedIndex].modeSelectedName;
            }
        }
        if (currentHighlightedButton.Equals(modeSelectButtonOnlineName))
        {
            ShowHighScoreTable();
            if (previousHighlightedButton != modeSelectButtonOnlineName)
            {
                changeHighScoreDataDisplayOnline();
            }
        }
        if (currentHighlightedButton.Equals(alltimeSelectButtonName))
        {
            ShowAllTimeTable();
        }
        if (currentHighlightedButton.Equals(pageNumberLocalButtonName)
            || currentHighlightedButton.Equals(pageNumberOnlineButtonName))
        {
            ShowHighScoreTable();
        }
    }

    /// <summary>
    /// Guards a stats action against re-entry and against running twice in one frame.
    ///
    /// The frame guard matches StartManager.RunCommand and ProgressionManager.RunProgressionAction.
    /// This screen was the one menu that had no frame guard at all (AUD-096), so a control reachable
    /// from more than one route could step twice on a single press.
    /// </summary>
    private void RunStatsAction(Action action)
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
        finally
        {
            buttonPressed = false;
        }
    }

    private void ToggleTrafficFilter()
    {
        RunStatsAction(() =>
        {
            changeSelectedTrafficOption();
            initializeTrafficOptionDisplay();
            changeHighScoreDataDisplay();
        });
    }

    private void ToggleHardcoreFilter()
    {
        RunStatsAction(() =>
        {
            changeSelectedHardcoreOption();
            initializeHardcoreOptionDisplay();
            changeHighScoreDataDisplay();
        });
    }

    private void ToggleEnemiesFilter()
    {
        RunStatsAction(() =>
        {
            changeSelectedEnemiesOption();
            initializeEnemyOptionDisplay();
            changeHighScoreDataDisplay();
        });
    }

    private void ToggleSniperFilter()
    {
        RunStatsAction(() =>
        {
            changeSelectedSniperOption();
            initializeSniperOptionDisplay();
            changeHighScoreDataDisplay();
        });
    }

    private void ChangeLocalModeRight()
    {
        ChangeLocalMode("right");
    }

    private void ChangeLocalMode(string direction)
    {
        RunStatsAction(() =>
        {
            previousHighlightedButton = currentHighlightedButton;
            localResultsPageNumber = 0;
            changeSelectedMode(direction);
            changeHighScoreDataDisplay();
        });
    }

    private void ChangeOnlineModeRight()
    {
        ChangeOnlineMode("right");
    }

    private void ChangeOnlineMode(string direction)
    {
        RunStatsAction(() =>
        {
            onlineResultsPageNumber = 0;
            previousHighlightedButton = currentHighlightedButton;
            changeSelectedMode(direction);
            changeHighScoreDataDisplayOnline();
        });
    }

    private void IncreaseLocalPage()
    {
        RunStatsAction(increaseLocalResultsPageNumber);
    }

    private void IncreaseOnlinePage()
    {
        RunStatsAction(increaseOnlineResultsPageNumber);
    }

    private void LoadStartMenu()
    {
        loadMainMenu(Constants.SCENE_NAME_level_00_start);
    }

    private void ShowHighScoreTable()
    {
        if (highScoreTableObject != null && !highScoreTableObject.activeSelf)
        {
            highScoreTableObject.SetActive(true);
        }
        if (allTimeTableObject != null && allTimeTableObject.activeSelf)
        {
            allTimeTableObject.SetActive(false);
        }
    }

    private void ShowAllTimeTable()
    {
        if (highScoreTableObject != null && highScoreTableObject.activeSelf)
        {
            highScoreTableObject.SetActive(false);
        }
        if (allTimeTableObject != null && !allTimeTableObject.activeSelf)
        {
            allTimeTableObject.SetActive(true);
        }
    }

    public static void navigateUp()
    {
        EventSystem.current.SetSelectedGameObject(EventSystem.current.currentSelectedGameObject
            .GetComponent<Button>().FindSelectableOnUp().gameObject);
    }

    public static void navigateDown()
    {
        Debug.Log("navigate down");
        EventSystem.current.SetSelectedGameObject(EventSystem.current.currentSelectedGameObject
            .GetComponent<Button>().FindSelectableOnDown().gameObject);
    }

    public void loadMainMenu(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    private List<mode> getModeSelectDataList()
    {
        List<mode> tempList = new List<mode>();

        string path = "Prefabs/menu_start/mode_selected_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];
        //Debug.Log(objects.Length);

        foreach (GameObject obj in objects)
        {
            StartScreenModeSelected temp = obj.GetComponent<StartScreenModeSelected>();
            //// add to list
            //if (!temp.ModeDisplayName.ToLower().Contains("free") 
            //    && !temp.ModeDisplayName.ToLower().Contains("arcade")) // exclude freeplay
            //{
            //    tempList.Add(new mode(temp.ModeId, temp.ModeDisplayName, temp.HighScoreField));

            //}
            if (temp.ModeId != 98)
            {
                tempList.Add(new mode(temp.ModeId, temp.ModeDisplayName, temp.HighScoreField));
            }
        }

        // sort list by  level id
        tempList.Sort(sortByModeId);

        return tempList;
    }

    static int sortByModeId(mode m1, mode m2)
    {
        return m1.modeSelectedId.CompareTo(m2.modeSelectedId);
    }

    private void SetHighScoreRow(int index, StatsTableHighScoreRow source)
    {
        if (highScoreRowsObjectsList == null || index < 0 || index >= highScoreRowsObjectsList.Count)
        {
            return;
        }

        GameObject rowObject = highScoreRowsObjectsList[index];
        if (rowObject == null)
        {
            return;
        }

        StatsTableHighScoreRow row = rowObject.GetComponent<StatsTableHighScoreRow>();
        CopyHighScoreRow(row, source);
        if (row != null)
        {
            // the row used to push these into its Text components from Update every frame (AUD-108)
            row.Bind();
        }
    }

    private void ClearHighScoreRows(int startIndex)
    {
        if (highScoreRowsObjectsList == null)
        {
            return;
        }

        for (int i = Mathf.Max(0, startIndex); i < highScoreRowsObjectsList.Count; i++)
        {
            SetHighScoreRow(i, null);
        }
    }

    private static void CopyHighScoreRow(StatsTableHighScoreRow row, StatsTableHighScoreRow source)
    {
        if (row == null)
        {
            return;
        }

        if (source == null)
        {
            row.UserName = "";
            row.Score = "";
            row.Character = "";
            row.Level = "";
            row.Date = "";
            row.HardcoreEnabled = "";
            return;
        }

        row.UserName = source.UserName;
        row.Score = source.Score;
        row.Character = source.Character;
        row.Level = source.Level;
        row.Date = source.Date;
        row.HardcoreEnabled = source.HardcoreEnabled;
    }

    public void changeSelectedMode(string direction)
    {
        // left option || decrement
        if (direction.ToLower().Equals("left"))
        {
            // if default index (first in list), go to end of list
            if (currentModeSelectedIndex == 0)
            {
                currentModeSelectedIndex = modesList.Count - 1;
            }
            else
            {
                // if not first index, decrement
                currentModeSelectedIndex--;
            }
        }

        // right option || increment
        if (direction.ToLower().Equals("right"))
        {
            // if default index (first in list
            if (currentModeSelectedIndex == modesList.Count - 1)
            {
                currentModeSelectedIndex = 0;
            }
            else
            {
                //if not first index, increment
                currentModeSelectedIndex++;
            }
        }
    }

    public void submitUnsubmittedScores()
    {
        StartCoroutine(SubmitUnsubmittedScoresCoroutine());
    }

    private IEnumerator SubmitUnsubmittedScoresCoroutine()
    {
        // PostUnsubmittedHighscores stamps each score with GameOptions.userid/userName, which are
        // set by picking a local account or by the offline guest fallback - neither proves a
        // session. Without a token the request carries no Authorization header, so it would fail
        // server-side with nothing here to explain why.
        if (!APIHelper.HasSession)
        {
            submittedHighscoresText.text = "sign in to submit";
            yield break;
        }

        // getUnsubmittedHighScoreFromDatabase already owns SQLite recovery internally and signals
        // failure by returning null rather than throwing (DBHelper.cs).
        List<HighScoreModel> unsubmitted = DBHelper.instance.getUnsubmittedHighScoreFromDatabase();
        if (unsubmitted == null)
        {
            Debug.LogError("Could not read unsubmitted scores from the local database.");
            submittedHighscoresText.text = "scores unavailable";
            yield break;
        }

        unsubmittedHighScores = unsubmitted;

        numUnsubmittedHighscores = unsubmittedHighScores.Count;
        if (numUnsubmittedHighscores == 0)
        {
            submittedHighscoresText.text = "no scores to submit";
            numUnsubmittedHighscoresText.text = string.Empty;
            yield break;
        }

        submittedHighscoresText.text = "submitting...";
        ApiResult<int> result = null;
        yield return APIHelper.PostUnsubmittedHighscores(unsubmittedHighScores, value => result = value);
        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        bool submitted = result != null && result.Success;
        submittedHighscoresText.text = submitted ? "scores submitted" : "submission failed";
        numUnsubmittedHighscoresText.text = submitted ? string.Empty : "+" + numUnsubmittedHighscores;
    }

    private void getUnsubmittedHighscores()
    {
        List<HighScoreModel> unsubmitted;
        DBHelper.instance.DatabaseLocked = true;
        try
        {
            // get unsubmitted scores
            unsubmitted = DBHelper.instance.getUnsubmittedHighScoreFromDatabase();
        }
        finally
        {
            DBHelper.instance.DatabaseLocked = false;
        }

        // getUnsubmittedHighScoreFromDatabase already owns SQLite recovery internally and signals
        // failure by returning null rather than throwing (DBHelper.cs).
        if (unsubmitted == null)
        {
            Debug.LogError("Could not read unsubmitted scores from the local database.");
            return;
        }

        unsubmittedHighScores = unsubmitted;
        numUnsubmittedHighscores = unsubmittedHighScores.Count;

        // if count > 0,  set appropriate text
        if (numUnsubmittedHighscores > 0)
        {
            submittedHighscoresText.text = "submit scores";
            numUnsubmittedHighscoresText.text = "+" + numUnsubmittedHighscores.ToString();
        }
        // if none, set appropriate text
        if (numUnsubmittedHighscores == 0)
        {
            submittedHighscoresText.text = "no scores to submit";
            numUnsubmittedHighscoresText.text = "";
        }
    }


    public void changeHighScoreDataDisplay()
    {
        if (GameObject.FindGameObjectWithTag("database") != null)
        {
            // get highscore field/mode from mode prefab - a defect here is an invalid
            // currentModeSelectedIndex, not a database failure, so it must surface normally
            // rather than being reported as an unavailable database.
            string field = modesList[currentModeSelectedIndex].modeSelectedHighScoreField;
            int modeId = modesList[currentModeSelectedIndex].modeSelectedId;

            DBHelper.instance.DatabaseLocked = true;
            try
            {
                // get new list of scores based on currently selected game mode
                highScoreRowsDataList
                    = DBHelper.instance.getListOfHighScoreRowsFromTableByModeIdAndField(field,
                    modeId,
                    hardcoreEnabled,
                    trafficEnabled,
                    enemiesEnabled,
                    sniperEnabled,
                    localResultsPageNumber);

                // get # of results for pageination display
                // same four filters the rows query above used, so the page count describes the
                // set actually being paged
                numLocalResults = DBHelper.instance.getNumberOfResults(
                    field,
                    modeId,
                    hardcoreEnabled,
                    trafficEnabled,
                    enemiesEnabled,
                    sniperEnabled);
            }
            finally
            {
                DBHelper.instance.DatabaseLocked = false;
            }

            if (highScoreRowsDataList == null)
            {
                highScoreRowsDataList = new List<StatsTableHighScoreRow>();
            }

            int rowCount = Math.Min(highScoreRowsDataList.Count, highScoreRowsObjectsList.Count);

            // updates row with new data
            for (int i = 0; i < rowCount; i++)
            {
                SetHighScoreRow(i, highScoreRowsDataList[i]);
            }
            // empty out rows if scores do not exist or there isnt at least 10
            ClearHighScoreRows(rowCount);
            initializeLocalPageNumberDisplay();
        }
        modeSelectButtonText.text = modesList[currentModeSelectedIndex].modeSelectedName;
    }

    public void changeHighScoreDataDisplayOnline()
    {
        StartCoroutine(ChangeHighScoreDataDisplayOnlineCoroutine(++onlineRequestVersion));
    }

    private IEnumerator ChangeHighScoreDataDisplayOnlineCoroutine(int requestVersion)
    {
        if (modesList == null || currentModeSelectedIndex < 0 || currentModeSelectedIndex >= modesList.Count)
        {
            yield break;
        }

        int modeId = modesList[currentModeSelectedIndex].modeSelectedId;
        int hardcore = Convert.ToInt32(hardcoreEnabled);
        int traffic = Convert.ToInt32(trafficEnabled);
        int enemies = Convert.ToInt32(enemiesEnabled);
        int sniper = Convert.ToInt32(sniperEnabled);

        ApiResult<int> countResult = null;
        yield return APIHelper.GetHighscoreCountByModeid(
            modeId, hardcore, traffic, enemies, sniper, value => countResult = value);
        if (requestVersion != onlineRequestVersion)
        {
            yield break;
        }

        ApiResult<List<StatsTableHighScoreRow>> rowsResult = null;
        yield return APIHelper.GetHighscoreByModeid(
            modeId,
            hardcore,
            traffic,
            enemies,
            sniper,
            onlineResultsPageNumber,
            ResultsPerPage,
            value => rowsResult = value);
        if (requestVersion != onlineRequestVersion)
        {
            yield break;
        }

        // AUD-078: same null-result guard UserAccountManager.LoginGuestCoroutine already uses after
        // the identical APIHelper callback pattern.
        numOnlineResults = countResult != null && countResult.Success ? countResult.Value : 0;
        List<StatsTableHighScoreRow> rows = rowsResult != null && rowsResult.Success && rowsResult.Value != null
            ? rowsResult.Value
            : new List<StatsTableHighScoreRow>();
        int displayedRows = modeId == 99 ? 0 : Math.Min(rows.Count, highScoreRowsObjectsList.Count);
        for (int i = 0; i < displayedRows; i++)
        {
            SetHighScoreRow(i, rows[i]);
        }

        ClearHighScoreRows(displayedRows);
        initializeOnlinePageNumberDisplay();
        modeSelectButtonOnlineText.text = modesList[currentModeSelectedIndex].modeSelectedName;
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
        if (sniperEnabled)
        {
            sniperSelectOptionText.text = "ON";
        }
        if (!sniperEnabled)
        {
            sniperSelectOptionText.text = "OFF";
        }
    }

    public void initializeLocalPageNumberDisplay()
    {
        pageNumberLocalSelectButtonText.text =
            StatsPaging.DisplayLabel(localResultsPageNumber, numLocalResults);
    }
    public void initializeOnlinePageNumberDisplay()
    {
        pageNumberOnlineSelectButtonText.text =
            StatsPaging.DisplayLabel(onlineResultsPageNumber, numOnlineResults);
    }

    public void changeSelectedTrafficOption()
    {
        trafficEnabled = !trafficEnabled;
    }

    public void changeSelectedEnemiesOption()
    {
        enemiesEnabled = !enemiesEnabled;
    }

    public void changeSelectedHardcoreOption()
    {
        hardcoreEnabled = !hardcoreEnabled;
    }
    public void changeSelectedSniperOption()
    {
        sniperEnabled = !sniperEnabled;
    }

    public void increaseLocalResultsPageNumber()
    {
        localResultsPageNumber = StatsPaging.NextPage(localResultsPageNumber, numLocalResults);
        initializeLocalPageNumberDisplay();
        changeHighScoreDataDisplay();
    }
    public void decreaseLocalResultsPageNumber()
    {
        // wraps within a valid page range. this used to land on numPages - 1, which is -1 when
        // there are no results at all.
        localResultsPageNumber = StatsPaging.PreviousPage(localResultsPageNumber, numLocalResults);
        initializeLocalPageNumberDisplay();
        changeHighScoreDataDisplay();
    }

    public void increaseOnlineResultsPageNumber()
    {
        onlineResultsPageNumber = StatsPaging.NextPage(onlineResultsPageNumber, numOnlineResults);
        initializeOnlinePageNumberDisplay();
        changeHighScoreDataDisplayOnline();
    }
    public void decreaseOnlineResultsPageNumber()
    {
        // wraps within a valid page range. this used to land on numPages - 1, which is -1 when
        // there are no results at all.
        onlineResultsPageNumber = StatsPaging.PreviousPage(onlineResultsPageNumber, numOnlineResults);
        initializeOnlinePageNumberDisplay();
        changeHighScoreDataDisplayOnline();
    }



    public static string ModeSelectButtonName => modeSelectButtonName;
    public static string AlltimeSelectButtonName => alltimeSelectButtonName;
    public static string MainMenuButtonName => mainMenuButtonName;
    public static string PageNumberLocalButtonName => pageNumberLocalButtonName;

    public static string PageNumberOnlineButtonName => pageNumberOnlineButtonName;
    public static string ModeSelectButtonOnlineName => modeSelectButtonOnlineName;

    public static string HardcoreOptionButtonName => hardcoreOptionButtonName;
    public static string TrafficOptionButtonName => trafficOptionButtonName;
    public static string EnemiesOptionButtonName => enemiesOptionButtonName;
    public static string SniperOptionButtonName => sniperOptionButtonName;

    public string PreviousHighlightedButton { get => previousHighlightedButton; set => previousHighlightedButton = value; }
    public string CurrentHighlightedButton { get => currentHighlightedButton; set => currentHighlightedButton = value; }
    public int LocalResultsPageNumber { get => localResultsPageNumber; set => localResultsPageNumber = value; }
    public int OnlineResultsPageNumber { get => onlineResultsPageNumber; set => onlineResultsPageNumber = value; }
}
