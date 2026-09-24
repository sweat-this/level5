
using Assets.Scripts.database;
using Assets.Scripts.restapi;
using Level5.BackendV2;
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

    // Online results use Backend V2's server-authoritative cursor contract, not a page/offset -
    // see docs/backend-v2-client.md and the leaderboard cutover notes. onlinePagination owns
    // page number, cursor and next-cursor together so they cannot drift apart the way three
    // separate fields could.
    readonly OnlineLeaderboardPaginationState onlinePagination = new OnlineLeaderboardPaginationState();

    /// <summary>Which leaderboard source (local SQLite or online Backend V2) the currently
    /// displayed high-score rows represent. Only the local/online mode or page controls change
    /// this - a filter toggle refreshes whichever source is already current without switching it,
    /// and this is never inferred from EventSystem selection (a filter button becoming selected
    /// must not appear to switch the displayed source).</summary>
    private enum StatsDisplaySource
    {
        Local,
        Online
    }

    private StatsDisplaySource currentDisplaySource = StatsDisplaySource.Local;

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
        onlinePagination.Reset();

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
        // The online leaderboard is not requested here (issue: Backend V2 leaderboard cutover) -
        // opening Stats must not contact Backend V2, or show a signed-out failure, merely because
        // the scene loaded. The first online request happens only when the player actually
        // selects the online leaderboard (changeHighScoreDataDisplayOnline).
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
            RefreshCurrentSourceAfterFilterChange();
        });
    }

    private void ToggleHardcoreFilter()
    {
        RunStatsAction(() =>
        {
            changeSelectedHardcoreOption();
            initializeHardcoreOptionDisplay();
            RefreshCurrentSourceAfterFilterChange();
        });
    }

    private void ToggleEnemiesFilter()
    {
        RunStatsAction(() =>
        {
            changeSelectedEnemiesOption();
            initializeEnemyOptionDisplay();
            RefreshCurrentSourceAfterFilterChange();
        });
    }

    private void ToggleSniperFilter()
    {
        RunStatsAction(() =>
        {
            changeSelectedSniperOption();
            initializeSniperOptionDisplay();
            RefreshCurrentSourceAfterFilterChange();
        });
    }

    /// <summary>
    /// A filter change always invalidates online pagination (the cursor is scoped to the exact
    /// mode/filter combination it was issued for - see OnlineLeaderboardPaginationState), even
    /// while viewing the local table. Only the table currently being viewed is refreshed - a
    /// filter change never fetches the source that is not on screen.
    /// </summary>
    private void RefreshCurrentSourceAfterFilterChange()
    {
        onlinePagination.Reset();
        if (currentDisplaySource == StatsDisplaySource.Online)
        {
            changeHighScoreDataDisplayOnline();
        }
        else
        {
            changeHighScoreDataDisplay();
        }
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
            // changeSelectedMode already resets onlinePagination (its cursor is scoped to the
            // mode it was issued for).
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

    /// <summary>
    /// Writes an online (Backend V2) row's presentation values directly into the display row at
    /// <paramref name="index"/>. Unlike <see cref="SetHighScoreRow"/>, there is no intermediate
    /// <see cref="StatsTableHighScoreRow"/> source instance - <see cref="LeaderboardEntryDto"/> is
    /// never turned into one, per the row-mapping requirement to prefer direct binding of
    /// presentation values over a throwaway GameObject/component.
    /// </summary>
    private void SetHighScoreRowValues(int index, LeaderboardRowPresentation presentation)
    {
        if (highScoreRowsObjectsList == null || index < 0 || index >= highScoreRowsObjectsList.Count
            || presentation == null)
        {
            return;
        }

        GameObject rowObject = highScoreRowsObjectsList[index];
        if (rowObject == null)
        {
            return;
        }

        StatsTableHighScoreRow row = rowObject.GetComponent<StatsTableHighScoreRow>();
        if (row == null)
        {
            return;
        }

        row.UserName = presentation.UserName;
        row.Score = presentation.Score;
        row.Character = presentation.Character;
        row.Level = presentation.Level;
        row.Date = presentation.Date;
        row.HardcoreEnabled = presentation.HardcoreEnabled;
        row.Bind();
    }

    private static string ResolveCharacterDisplayName(int characterId)
    {
        return LoadedData.instance != null
            ? LoadedData.instance.getSelectedCharacterProfile(characterId)?.PlayerDisplayName
            : null;
    }

    private static string ResolveLevelDisplayName(int levelId)
    {
        return LoadedData.instance != null && LoadedData.instance.LevelCatalog != null
            ? LoadedData.instance.LevelCatalog.FindByLevelId(levelId)?.LevelDisplayName
            : null;
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
        // currentModeSelectedIndex is shared between the local and online tables, so any change
        // here invalidates online pagination regardless of which control triggered it (local mode
        // button, online mode button, or a touch swipe on either) - the cursor is scoped to the
        // mode it was issued for and must never be reused once that mode changes.
        onlinePagination.Reset();

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
        currentDisplaySource = StatsDisplaySource.Local;
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
        currentDisplaySource = StatsDisplaySource.Online;
        StartCoroutine(ChangeHighScoreDataDisplayOnlineCoroutine(++onlineRequestVersion));
    }

    /// <summary>
    /// Backend V2 online leaderboard read (issue: leaderboard client cutover). Local SQLite,
    /// StatsPaging and all-time stats are untouched by this method.
    /// </summary>
    private IEnumerator ChangeHighScoreDataDisplayOnlineCoroutine(int requestVersion)
    {
        if (modesList == null || currentModeSelectedIndex < 0 || currentModeSelectedIndex >= modesList.Count)
        {
            yield break;
        }

        int modeId = modesList[currentModeSelectedIndex].modeSelectedId;

        // Never leave rows from a previous mode/filter/page visible under the newly selected
        // scope, whether this request succeeds, fails, or never goes out at all.
        ClearHighScoreRows(0);
        numOnlineResults = 0;

        // BackendV2SessionStore.IsAuthenticated is the only valid evidence of a Backend V2
        // session - never APIHelper.HasSession/GameOptions.userid/userName, which prove only a
        // V1 session or a local account selection. V1 and V2 authentication are independent.
        if (!BackendV2SessionStore.IsAuthenticated)
        {
            onlinePagination.Reset();
            modeSelectButtonOnlineText.text = "sign in required";
            initializeOnlinePageNumberDisplay();
            yield break;
        }

        modeSelectButtonOnlineText.text = "loading...";

        OnlineLeaderboardFilterQuery filters = OnlineLeaderboardFilterTranslator.Translate(
            hardcoreEnabled, trafficEnabled, enemiesEnabled, sniperEnabled);

        ApiResponse<LeaderboardPageDto> response = null;
        yield return BackendV2Runtime.Leaderboards.GetPage(
            modeId,
            StatsPaging.ResultsPerPage,
            onlinePagination.Cursor,
            filters.Hardcore,
            filters.Traffic,
            filters.Enemies,
            filters.Sniper,
            value => response = value);

        if (requestVersion != onlineRequestVersion)
        {
            yield break;
        }

        if (response == null || !response.Success)
        {
            // Covers both an unsupported-mode 400 (server code "unsupported_leaderboard_mode")
            // and a rejected/invalid cursor 400 - either way the query as scoped is unusable, so
            // pagination resets rather than compounding the failure on the next "next page" press.
            // A network/timeout/server/auth failure leaves pagination alone, since the same page
            // is worth retrying once transient trouble clears.
            modeSelectButtonOnlineText.text = "leaderboard unavailable";
            if (response != null && response.ErrorKind == ApiErrorKind.Validation)
            {
                onlinePagination.Reset();
            }

            initializeOnlinePageNumberDisplay();
            yield break;
        }

        LeaderboardPageDto page = response.Value;
        List<LeaderboardEntryDto> items = page != null && page.Items != null
            ? page.Items
            : new List<LeaderboardEntryDto>();
        // NextCursor is opaque - stored and forwarded verbatim on the next page, never decoded.
        onlinePagination.RecordPageResult(page != null ? page.NextCursor : null);
        numOnlineResults = items.Count;

        int displayedRows = Math.Min(items.Count, highScoreRowsObjectsList.Count);
        for (int i = 0; i < displayedRows; i++)
        {
            LeaderboardRowPresentation presentation = LeaderboardEntryPresentationMapper.Map(
                items[i], page.Metric, ResolveCharacterDisplayName, ResolveLevelDisplayName);
            SetHighScoreRowValues(i, presentation);
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
        // No fabricated "/ M" denominator online - the server never reports a total result count
        // (issue: leaderboard client cutover). StatsPaging.DisplayLabel remains local-only.
        pageNumberOnlineSelectButtonText.text = onlinePagination.DisplayLabel();
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
        // Advances using the server's last NextCursor, wrapping to the first page when there is
        // none - see OnlineLeaderboardPaginationState. This forward-cycling behavior replaces the
        // old total-count-based StatsPaging.NextPage wrap for online results only; local paging is
        // unchanged.
        onlinePagination.AdvancePage();
        initializeOnlinePageNumberDisplay();
        changeHighScoreDataDisplayOnline();
    }
    public void decreaseOnlineResultsPageNumber()
    {
        // No live caller navigates backward through online results (forward-only cursor
        // pagination wired to a single "next" control everywhere it is used - confirmed by a
        // repository-wide search). The opaque server cursor carries no reverse-navigation
        // information, so unlike the local/SQLite previous-page wrap, the only page always safely
        // reachable here without walking the entire server-side result set is the first one.
        onlinePagination.Reset();
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

    public int OnlineResultsPageNumber => onlinePagination.PageNumber;

    /// <summary>
    /// Restarts online pagination (page 0, no cursor) - the only thing a caller outside this class
    /// ever needs to do to online paging directly. Replaces a former public settable
    /// OnlineResultsPageNumber property whose setter silently ignored its own assigned value and
    /// always reset regardless - a real int-typed setter would have implied normal store semantics
    /// that cursor pagination cannot actually support (there is no way to jump to an arbitrary
    /// page N without walking the server's cursor chain). changeSelectedMode already calls this
    /// itself for every mode change, so a caller that also changes mode right after this (as
    /// TouchInputStatsScreenController does) is calling it redundantly but harmlessly.
    /// </summary>
    public void ResetOnlinePagination()
    {
        onlinePagination.Reset();
    }
}
