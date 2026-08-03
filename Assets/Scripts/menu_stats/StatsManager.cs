
using Assets.Scripts.database;
using Assets.Scripts.restapi;
using System;
using System.Collections.Generic;
using System.Linq;
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

    // tag find high score rows that are instantiated
    const string highScoreRowTag = "high_score_row";
    const int ResultsPerPage = 10;
    //const string mainMenuSceneName = "level_00_start";

    GameObject allTimeTableObject;
    GameObject highScoreTableObject;
    [SerializeField]
    Text modeSelectButtonText;
    [SerializeField]
    Text modeSelectButtonHardcoreText;
    [SerializeField]
    Text modeSelectButtonOnlineText;
    [SerializeField]
    Text pageNumberLocalSelectButtonText;
    [SerializeField]
    Text pageNumberOnlineSelectButtonText;

    [SerializeField] Button modeSelectButton;
    [SerializeField] Button modeSelectOnlineButton;
    [SerializeField] Button allTimeSelectButton;
    [SerializeField] Button mainMenuButton;
    [SerializeField] Button pageNumberLocalButton;
    [SerializeField] Button pageNumberOnlineButton;
    [SerializeField] Button trafficOptionButton;
    [SerializeField] Button hardcoreOptionButton;
    [SerializeField] Button enemiesOptionButton;
    [SerializeField] Button sniperOptionButton;

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
    [SerializeField]
    private Text trafficSelectOptionText;
    [SerializeField]
    private Text hardcoreSelectOptionText;
    [SerializeField]
    private Text enemySelectOptionText;
    [SerializeField]
    private Text sniperSelectOptionText;
    [SerializeField]
    private Text submittedHighscoresText;
    [SerializeField]
    private Text numUnsubmittedHighscoresText;

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
    [SerializeField]
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

    PlayerControls controls;
    private bool initialized;

    public static StatsManager instance;

    // for input system
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
        controls = PlayerControlsProvider.Controls;

        // table objects
        highScoreTableObject = GameObject.Find(highScoreTableName);
        allTimeTableObject = GameObject.Find(allTimeTableName);

        // parent object where rows will be instantiated
        // ex. usage Instantiate(prefab, position, quaternion, parent object);
        highScoresRowsObject = GameObject.Find(highScoresRowsName);

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
            try
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
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                return;
            }
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
        for (int i = 0; i < initialRowCount; i++)
        {
            StatsTableHighScoreRow row = highScoreRowPrefab.GetComponent<StatsTableHighScoreRow>();
            StatsTableHighScoreRow source = i < highScoreRowsDataList.Count ? highScoreRowsDataList[i] : null;
            CopyHighScoreRow(row, source);
            // instantiate row on necessary table object
            Instantiate(highScoreRowPrefab, highScoresRowsObject.transform.position, Quaternion.identity, highScoresRowsObject.transform);
        }
        // list of row onjects that contain the Text displays
        highScoreRowsObjectsList = GameObject.FindGameObjectsWithTag(highScoreRowTag).ToList();

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

    private void ResolveButtonReferences()
    {
        modeSelectButton = ResolveButton(modeSelectButton, modeSelectButtonName);
        modeSelectOnlineButton = ResolveButton(modeSelectOnlineButton, modeSelectButtonOnlineName);
        allTimeSelectButton = ResolveButton(allTimeSelectButton, alltimeSelectButtonName);
        mainMenuButton = ResolveButton(mainMenuButton, mainMenuButtonName);
        pageNumberLocalButton = ResolveButton(pageNumberLocalButton, pageNumberLocalButtonName);
        pageNumberOnlineButton = ResolveButton(pageNumberOnlineButton, pageNumberOnlineButtonName);
        trafficOptionButton = ResolveButton(trafficOptionButton, trafficOptionButtonName);
        hardcoreOptionButton = ResolveButton(hardcoreOptionButton, hardcoreOptionButtonName);
        enemiesOptionButton = ResolveButton(enemiesOptionButton, enemiesOptionButtonName);
        sniperOptionButton = ResolveButton(sniperOptionButton, sniperOptionButtonName);
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

    private void RegisterButtonCallbacks()
    {
        RegisterRequiredButtonCallback(mainMenuButton, LoadStartMenu);
    }

    private void UnregisterButtonCallbacks()
    {
        UiSelectionAdapter.UnregisterButton(mainMenuButton, LoadStartMenu);
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

    private void HandleSelectedStatsControl()
    {
        if (buttonPressed || string.IsNullOrEmpty(currentHighlightedButton))
        {
            return;
        }

        if (controls.UINavigation.Up.triggered || controls.UINavigation.Down.triggered)
        {
            HandleVerticalOptionInput();
        }

        if (controls.UINavigation.Left.triggered)
        {
            HandleLeftInput();
        }

        if (controls.UINavigation.Right.triggered)
        {
            HandleRightInput();
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

    private void HandleVerticalOptionInput()
    {
        if (currentHighlightedButton.Equals(trafficSelectValueName))
        {
            ToggleTrafficFilter();
        }
        if (currentHighlightedButton.Equals(hardcoreSelectValueName))
        {
            ToggleHardcoreFilter();
        }
        if (currentHighlightedButton.Equals(enemySelectValueName))
        {
            ToggleEnemiesFilter();
        }
        if (currentHighlightedButton.Equals(sniperSelectValueName))
        {
            ToggleSniperFilter();
        }
    }

    private void HandleLeftInput()
    {
        if (currentHighlightedButton.Equals(modeSelectButtonName))
        {
            ChangeLocalModeLeft();
        }
        if (currentHighlightedButton.Equals(modeSelectButtonOnlineName))
        {
            ChangeOnlineModeLeft();
        }
        if (currentHighlightedButton.Equals(pageNumberLocalButtonName))
        {
            DecreaseLocalPage();
        }
        if (currentHighlightedButton.Equals(pageNumberOnlineButtonName))
        {
            DecreaseOnlinePage();
        }
    }

    private void HandleRightInput()
    {
        if (currentHighlightedButton.Equals(modeSelectButtonName))
        {
            ChangeLocalModeRight();
        }
        if (currentHighlightedButton.Equals(modeSelectButtonOnlineName))
        {
            ChangeOnlineModeRight();
        }
        if (currentHighlightedButton.Equals(pageNumberLocalButtonName))
        {
            IncreaseLocalPage();
        }
        if (currentHighlightedButton.Equals(pageNumberOnlineButtonName))
        {
            IncreaseOnlinePage();
        }
    }

    private void RunStatsAction(Action action)
    {
        if (buttonPressed || action == null)
        {
            return;
        }

        buttonPressed = true;
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

    private void ChangeLocalModeLeft()
    {
        ChangeLocalMode("left");
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

    private void ChangeOnlineModeLeft()
    {
        ChangeOnlineMode("left");
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

    private void DecreaseLocalPage()
    {
        RunStatsAction(decreaseLocalResultsPageNumber);
    }

    private void IncreaseOnlinePage()
    {
        RunStatsAction(increaseOnlineResultsPageNumber);
    }

    private void DecreaseOnlinePage()
    {
        RunStatsAction(decreaseOnlineResultsPageNumber);
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

        StatsTableHighScoreRow row = highScoreRowsObjectsList[index].GetComponent<StatsTableHighScoreRow>();
        CopyHighScoreRow(row, source);
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
        //if (!String.IsNullOrEmpty(GameOptions.userName) && GameOptions.userid != 0)
        //{
            try
            {
                DBHelper.instance.DatabaseLocked = true;
                // get unsubmitted scores
                unsubmittedHighScores = DBHelper.instance.getUnsubmittedHighScoreFromDatabase();
                numUnsubmittedHighscores = unsubmittedHighScores.Count();
                // if count > 0,  set appropriate text
                if (numUnsubmittedHighscores > 0)
                {
                    //Debug.Log("if");
                    submittedHighscoresText.text = "submit scores";
                    numUnsubmittedHighscoresText.text = "+" + numUnsubmittedHighscores.ToString();
                    APIHelper.PostUnsubmittedHighscores(unsubmittedHighScores);
                }
                // if none, set appropriate text
                if (numUnsubmittedHighscores == 0)
                {
                    //Debug.Log("if");
                    submittedHighscoresText.text = "no scores to submit";
                    numUnsubmittedHighscoresText.text = "";
                }

            }
            catch (Exception e)
            {
                DBHelper.instance.DatabaseLocked = false;
                Debug.Log("ERROR : " + e);
            }
        //}
        //getUnsubmittedHighscores();
        DBHelper.instance.DatabaseLocked = false;
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_stats);
    }

    private void getUnsubmittedHighscores()
    {
        try
        {
            DBHelper.instance.DatabaseLocked = true;
            // get unsubmitted scores
            unsubmittedHighScores = DBHelper.instance.getUnsubmittedHighScoreFromDatabase();
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
            DBHelper.instance.DatabaseLocked = false;
        }
        catch (Exception e)
        {
            DBHelper.instance.DatabaseLocked = false;
            Debug.Log("ERROR : " + e);
        }
    }


    public void changeHighScoreDataDisplay()
    {
        if (GameObject.FindGameObjectWithTag("database") != null)
        {
            try
            {
                DBHelper.instance.DatabaseLocked = true;
                // counts number entries returned.
                int index = 0;
                // get highscore field from mode prefab
                string field = modesList[currentModeSelectedIndex].modeSelectedHighScoreField;
                // get new list of scores based on currently selected game mode
                highScoreRowsDataList
                    = DBHelper.instance.getListOfHighScoreRowsFromTableByModeIdAndField(field,
                    modesList[currentModeSelectedIndex].modeSelectedId,
                    hardcoreEnabled,
                    trafficEnabled,
                    enemiesEnabled,
                    sniperEnabled,
                    localResultsPageNumber);

                // get # of results for pageination display
                numLocalResults = DBHelper.instance.getNumberOfResults(field, modesList[currentModeSelectedIndex].modeSelectedId, hardcoreEnabled, localResultsPageNumber);

                if (highScoreRowsDataList == null)
                {
                    highScoreRowsDataList = new List<StatsTableHighScoreRow>();
                }

                int rowCount = Math.Min(highScoreRowsDataList.Count, highScoreRowsObjectsList.Count);

                // updates row with new data
                for (int i = 0; i < rowCount; i++)
                {
                    SetHighScoreRow(i, highScoreRowsDataList[i]);
                    index++;
                }
                // empty out rows if scores do not exist or there isnt at least 10
                ClearHighScoreRows(index);
                initializeLocalPageNumberDisplay();
                DBHelper.instance.DatabaseLocked = false;
            }
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                DBHelper.instance.DatabaseLocked = false;
                return;
            }
        }
        modeSelectButtonText.text = modesList[currentModeSelectedIndex].modeSelectedName;
    }

    public void changeHighScoreDataDisplayOnline()
    {
        // if not free play
        if (GameObject.Find("restapi") != null)
        {
            try
            {
                // counts number entries returned.
                int index = 0;
                int modeid = modesList[currentModeSelectedIndex].modeSelectedId;
                // get highscore field from mode prefab
                string field = modesList[currentModeSelectedIndex].modeSelectedHighScoreField;

                List<StatsTableHighScoreRow> highScoreRowList = new List<StatsTableHighScoreRow>();

                // # of results for pagination
                numOnlineResults = APIHelper.GetHighscoreCountByModeid(modeid,
                    Convert.ToInt32(hardcoreEnabled),
                    Convert.ToInt32(trafficEnabled),
                    Convert.ToInt32(enemiesEnabled),
                    Convert.ToInt32(sniperEnabled));

                //Debug.Log("numOnlineResults : " + numOnlineResults);
                //Debug.Log("-Convert.ToInt32(hardcoreEnabled) : " + Convert.ToInt32(hardcoreEnabled));
                //Debug.Log("-Convert.ToInt32(traffEnabled) : " + Convert.ToInt32(trafficEnabled));
                //Debug.Log("-Convert.ToInt32(enemyEnabled) : " + Convert.ToInt32(enemiesEnabled));
                //Debug.Log("-Convert.ToInt32(sniperEnabled) : " + Convert.ToInt32(sniperEnabled));
                // scores got display
                highScoreRowList = APIHelper.GetHighscoreByModeid(modeid,
                    Convert.ToInt32(hardcoreEnabled),
                    Convert.ToInt32(trafficEnabled),
                    Convert.ToInt32(enemiesEnabled),
                    Convert.ToInt32(sniperEnabled),
                    onlineResultsPageNumber,
                    ResultsPerPage) ?? new List<StatsTableHighScoreRow>();

                //Debug.Log("numOnlineResults : " + numOnlineResults);

                int rowCount = Math.Min(highScoreRowList.Count, highScoreRowsObjectsList.Count);
                //if modeid = free play, zero it out
                if (modeid != 99)
                {
                    // updates row with new data
                    for (int i = 0; i < rowCount; i++)
                    {
                        SetHighScoreRow(i, highScoreRowList[i]);
                        index++;
                    }
                }
                else
                {
                    index = 0;
                }
                // empty out rows if scores do not exist or there isnt at least 10
                ClearHighScoreRows(index);
                initializeOnlinePageNumberDisplay();
            }
            catch (Exception e)
            {
                Debug.Log("ERROR : " + e);
                //onlineLoaded = false;
                return;
            }
        }
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
        int numPages;
        if ((numLocalResults % 10) == 0)
        {
            numPages = numLocalResults / 10;
        }
        else
        {
            numPages = (numLocalResults / 10) + 1;
        }

        pageNumberLocalSelectButtonText.text = "page " + (localResultsPageNumber + 1) + " / " + numPages;
    }
    public void initializeOnlinePageNumberDisplay()
    {
        int numPages;
        if ((numOnlineResults % 10) == 0)
        {
            numPages = numOnlineResults / 10;
        }
        else
        {
            numPages = (numOnlineResults / 10) + 1;
        }

        if (numPages > 0)
        {
            pageNumberOnlineSelectButtonText.text = "page " + (onlineResultsPageNumber + 1) + " / " + numPages;
        }
        else
        {
            pageNumberOnlineSelectButtonText.text = "page " + (onlineResultsPageNumber) + " / " + numPages;
        }
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
        int numPages;
        if ((numLocalResults % 10) == 0)
        {
            numPages = numLocalResults / 10;
        }
        else
        {
            numPages = (numLocalResults / 10) + 1;
        }
        // if can increase page number of results, do so
        if ((localResultsPageNumber + 2) <= numPages)
        {
            localResultsPageNumber++;
            initializeLocalPageNumberDisplay();
        }
        else
        {
            localResultsPageNumber = 0;
            initializeLocalPageNumberDisplay();
        }
        changeHighScoreDataDisplay();
    }
    public void decreaseLocalResultsPageNumber()
    {
        int numPages;
        if ((numLocalResults % 10) == 0)
        {
            numPages = numLocalResults / 10;
        }
        else
        {
            numPages = (numLocalResults / 10) + 1;
        }
        // if can increase page number of results, do so
        if ((localResultsPageNumber - 1) >= 0)
        {
            localResultsPageNumber--;
            initializeLocalPageNumberDisplay();
        }
        else
        {
            localResultsPageNumber = numPages - 1;
            initializeLocalPageNumberDisplay();
        }
        changeHighScoreDataDisplay();
    }

    public void increaseOnlineResultsPageNumber()
    {
        int numPages;
        if ((numOnlineResults % 10) == 0)
        {
            numPages = numOnlineResults / 10;
        }
        else
        {
            numPages = (numOnlineResults / 10) + 1;
        }
        // if can increase page number of results, do so
        if ((onlineResultsPageNumber + 2) <= numPages)
        {
            onlineResultsPageNumber++;
            initializeOnlinePageNumberDisplay();
        }
        else
        {
            onlineResultsPageNumber = 0;
            initializeOnlinePageNumberDisplay();
        }
        changeHighScoreDataDisplayOnline();
    }
    public void decreaseOnlineResultsPageNumber()
    {
        int numPages;
        if ((numOnlineResults % 10) == 0)
        {
            numPages = numOnlineResults / 10;
        }
        else
        {
            numPages = (numOnlineResults / 10) + 1;
        }
        // if can increase page number of results, do so
        if ((onlineResultsPageNumber - 1) >= 0)
        {
            onlineResultsPageNumber--;
            initializeOnlinePageNumberDisplay();
        }
        else
        {
            onlineResultsPageNumber = numPages - 1;
            initializeOnlinePageNumberDisplay();
        }
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
