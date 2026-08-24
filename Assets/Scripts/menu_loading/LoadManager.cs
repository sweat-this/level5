
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadManager : MonoBehaviour
{
    private const float DatabaseReadyTimeoutSeconds = 8f;
    [SerializeField]
    public string currentHighlightedButton;

    [SerializeField]
    private CharacterPresetCatalog characterPresetCatalog;

    //list of all shooter profiles with player data
    [SerializeField]
    private List<CharacterProfile> playerSelectedData;
    public List<CharacterProfile> PlayerSelectedData { get => playerSelectedData; }

    [SerializeField]
    private List<CharacterProfile> cpuPlayerSelectedData;
    // list off cheerleader profile data
    [SerializeField]
    private List<CheerleaderProfile> cheerleaderSelectedData;
    public List<CheerleaderProfile> CheerleaderSelectedData { get => cheerleaderSelectedData; }
    // list off level  data
    [SerializeField]
    private List<LevelSelected> levelSelectedData;
    public List<LevelSelected> LevelSelectedData { get => levelSelectedData; }
    private LevelCatalog levelCatalog;
    public LevelCatalog LevelCatalog { get => levelCatalog; }

    //mode selected objects
    [SerializeField]
    private List<StartScreenModeSelected> modeSelectedData;
    public List<StartScreenModeSelected> ModeSelectedData { get => modeSelectedData; }
    public List<CharacterProfile> CpuPlayerSelectedData { get => cpuPlayerSelectedData; set => cpuPlayerSelectedData = value; }

    bool CharacterProfileTableExists = false;

    [SerializeField] internal bool playerDataLoaded = false;
    [SerializeField] internal bool cpuPlayerDataLoaded = false;
    [SerializeField] internal bool cheerleaderDataLoaded = false;
    [SerializeField] internal bool levelDataLoaded = false;
    [SerializeField] internal bool modeDataLoaded = false;
    [SerializeField] private bool persistenceReady;
    public bool PersistenceReady => persistenceReady;
    private bool sceneLoadRequested;
    private Coroutine loadRoutine;
    private bool failureMessageShown;

    private bool CheerleaderProfileTableExists;

    public static LoadManager instance;

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
    }

    private void OnDisable()
    {
        PlayerControlsProvider.DisableMenuMaps();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        LoadAllData();
    }

    private void Update()
    {
        // load start screen
        if (!sceneLoadRequested && LoadedData.instance != null && LoadedData.instance.DataLoaded)
        {
            sceneLoadRequested = true;
            // this is all confusing
            if (String.IsNullOrEmpty(GameOptions.previousSceneName))
            {
                SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
            }
            // go back to update manager
            else
            {
                SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
            }
        }

        if (LoadedData.instance != null && LoadedData.instance.LoadFailed)
        {
            ShowLoadFailure();
            if (PlayerControlsProvider.Controls.UINavigation.Submit.triggered)
            {
                failureMessageShown = false;
                LoadAllData();
                LoadedData.instance.Retry();
            }
            else if (PlayerControlsProvider.Controls.UINavigation.Cancel.triggered)
            {
                SceneManager.LoadScene(Constants.SCENE_NAME_level_00_account);
            }
        }
    }

    private void ShowLoadFailure()
    {
        if (failureMessageShown)
        {
            return;
        }

        failureMessageShown = true;
        GameObject messageObject = GameObject.Find("messageDisplay");
        if (messageObject != null && messageObject.TryGetComponent(out Text messageText))
        {
            messageText.text = "Game data could not be loaded. Submit to retry or cancel to return.";
        }
    }

    public void LoadAllData()
    {
        if (loadRoutine != null)
        {
            StopCoroutine(loadRoutine);
        }

        loadRoutine = StartCoroutine(LoadAllDataCoroutine());
    }

    private IEnumerator LoadAllDataCoroutine()
    {
        ResetLoadState();
        float deadline = Time.realtimeSinceStartup + DatabaseReadyTimeoutSeconds;
        while (!IsDatabaseReady() && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        bool databaseReady = IsDatabaseReady();
        if (!databaseReady)
        {
            Debug.LogWarning("The local database was not ready in time. Loading default catalogs.");
        }

        // AUD-085 code-review follow-up: DBConnector.tableExists() now reads DBHelper's shared
        // connection but (deliberately - see the note on tableExists() itself) never acquires
        // DatabaseLocked, since a "the connection is busy" answer would be indistinguishable from
        // a real "table doesn't exist" to every caller and could trigger unwanted re-seeding.
        // Waiting here instead, once, before the try block (a yield cannot appear inside a
        // try/catch), keeps the whole synchronous block below - which nothing yields inside of,
        // so nothing else can interleave once the wait clears - from ever running concurrently
        // with a locked operation elsewhere.
        if (databaseReady)
        {
            yield return new WaitUntil(() => !DBHelper.instance.DatabaseLocked);
        }

        try
        {
            if (databaseReady)
            {
                bool characterTableExists = DBConnector.instance.tableExists(Constants.LOCAL_DATABASE_tableName_characterProfile);
                if (characterTableExists)
                {
                    databaseReady = DBHelper.instance.EnsureCharacterProfilesForAccount(
                        CharacterProgressAccountId.GetCurrent());
                }

                CharacterProfileTableExists = databaseReady
                    && characterTableExists
                    && DBHelper.instance.HasCharacterProfilesForAccount(CharacterProgressAccountId.GetCurrent());
                CheerleaderProfileTableExists = DBConnector.instance.tableExists(Constants.LOCAL_DATABASE_tableName_cheerleaderProfile)
                    && !DBHelper.instance.isTableEmpty(Constants.LOCAL_DATABASE_tableName_cheerleaderProfile);

                if (databaseReady)
                {
                    new ProgressionService().RepairPendingJsonProjections();
                    PendingMatchPersistenceStore.Repair();
                }
            }

            playerSelectedData = CharacterProfileTableExists
                ? loadPlayerSelectDataList()
                : loadDefaultPlayerShooterProfiles();
            cheerleaderSelectedData = CheerleaderProfileTableExists
                ? loadCheerleaderSelectDataList()
                : loadDefaultCheerleaderProfiles();
            cpuPlayerSelectedData = loadCpuSelectDataList();
            levelSelectedData = loadLevelSelectDataList();
            levelCatalog = LevelCatalog.FromLevelSelected(levelSelectedData);
            modeSelectedData = loadModeSelectDataList();
        }
        catch (Exception exception)
        {
            Debug.LogError("Catalog loading failed: " + exception);
            TryLoadFallbackData(out _);
        }

        if (databaseReady && !CharacterProfileTableExists && playerSelectedData != null && playerSelectedData.Count > 0)
        {
            yield return SeedCharacterTable(playerSelectedData);
        }
        if (databaseReady && !CheerleaderProfileTableExists && cheerleaderSelectedData != null && cheerleaderSelectedData.Count > 0)
        {
            yield return SeedCheerleaderTable(cheerleaderSelectedData);
        }

        persistenceReady = true;
        loadRoutine = null;
    }

    private void ResetLoadState()
    {
        playerDataLoaded = false;
        cpuPlayerDataLoaded = false;
        cheerleaderDataLoaded = false;
        levelDataLoaded = false;
        modeDataLoaded = false;
        persistenceReady = false;
        CharacterProfileTableExists = false;
        CheerleaderProfileTableExists = false;
    }

    public bool TryLoadFallbackData(out string error)
    {
        try
        {
            playerSelectedData = loadDefaultPlayerShooterProfiles();
            cpuPlayerSelectedData = loadCpuSelectDataList();
            cheerleaderSelectedData = loadDefaultCheerleaderProfiles();
            levelSelectedData = loadLevelSelectDataList();
            levelCatalog = LevelCatalog.FromLevelSelected(levelSelectedData);
            modeSelectedData = loadModeSelectDataList();

            bool complete = playerDataLoaded
                && cpuPlayerDataLoaded
                && cheerleaderDataLoaded
                && levelDataLoaded
                && modeDataLoaded;
            error = complete ? string.Empty : "One or more required default catalogs were empty.";
            return complete;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            Debug.LogError("Default catalog loading failed: " + exception);
            return false;
        }
    }

    private IEnumerator SeedCharacterTable(List<CharacterProfile> defaults)
    {
        yield return DBConnector.instance.CreateTableCharacterProfile();
        yield return DBHelper.instance.InsertCharacterProfile(defaults);
    }

    private IEnumerator SeedCheerleaderTable(List<CheerleaderProfile> defaults)
    {
        yield return DBConnector.instance.CreateTableCheerleaderProfile();
        yield return DBHelper.instance.InsertCheerleaderProfile(defaults);
    }

    private static bool IsDatabaseReady()
    {
        return DBConnector.instance != null
            && DBHelper.instance != null
            && DBConnector.instance.DatabaseCreated;
    }

    private List<CharacterProfile> loadPlayerSelectDataList()
    {
        List<CharacterProfileRecord> dbShootStatsList = DBHelper.instance.getCharacterProfileStats(GameOptions.userid)
            ?? new List<CharacterProfileRecord>();
        // AUD-012: purely diagnostic - it only logs when the preset catalog and the database
        // disagree. Compiled out of release builds so the production load path carries no
        // dependency on Assets/Scripts/Dev.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CharacterProgressParityLogger.LogMismatchWarnings(characterPresetCatalog, dbShootStatsList);
#endif
        List<CharacterProfile> shooterList = new List<CharacterProfile>();

        string path = "Prefabs/menu_start/player_selected_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            /*
             * if prefab is not in DB, insert
             * ex. create new character, need to auto insert into db
             */
            if (obj == null || !obj.TryGetComponent(out CharacterProfile temp))
            {
                Debug.LogError("A player selection prefab is missing CharacterProfile.");
                continue;
            }

            // if character not in database, but prefab exists -- insert into DB and add to list
            if (!dbShootStatsList.Any(item => item.PlayerId == temp.PlayerId))
            {
                //isLocked = true;
                // get default profile for chracter to be inserted
                string defaultPath = "Prefabs/menu_start/default_shooter_profiles/player_selected_" + temp.PlayerObjectName;
                GameObject defaultObject = Resources.Load<GameObject>(defaultPath);
                CharacterProfile defaultTemp = defaultObject != null
                    ? defaultObject.GetComponent<CharacterProfile>()
                    : null;
                if (defaultTemp != null)
                {
                    DBHelper.instance.InsertCharacterProfile(defaultTemp);
                }
                else
                {
                    Debug.LogWarning("No default progression prefab exists for player " + temp.PlayerId + ".");
                }
                // add to current list to be loaded
                dbShootStatsList.Add(CharacterProfileRecord.FromProfile(temp));
            }

            CharacterProfileRecord dbStats = dbShootStatsList.Find(x => x.PlayerId == temp.PlayerId);
            if (dbStats == null)
            {
                Debug.LogWarning("No progression record exists for player " + temp.PlayerId + ". Using prefab defaults.");
                shooterList.Add(temp);
                continue;
            }

            // load stats from DB, but load portrait from prefab
            temp.Accuracy2Pt = dbStats.Accuracy2Pt;
            temp.Accuracy3Pt = dbStats.Accuracy3Pt;
            temp.Accuracy4Pt = dbStats.Accuracy4Pt;
            temp.Accuracy7Pt = dbStats.Accuracy7Pt;
            temp.Speed = dbStats.Speed;
            temp.RunSpeed = dbStats.RunSpeed;
            temp.RunSpeedHasBall = dbStats.RunSpeedHasBall;
            temp.Luck = dbStats.Luck;
            temp.ShootAngle = dbStats.ShootAngle;
            temp.Experience = dbStats.Experience;
            temp.Level = CharacterLevel.FromExperience(temp.Experience);
            //temp.PointsUsed = (int)(temp.Accuracy3Pt - 70) + (int)(temp.Accuracy4Pt - 70) + (int)(temp.Accuracy7Pt - 70);
            temp.Range = dbStats.Range;
            temp.Release = dbStats.Release;
            temp.IsLocked = dbStats.IsLocked;

            temp.PointsUsed = getPointsUsed(temp);
            temp.PointsAvailable = getPointsAvailable(temp);

            shooterList.Add(temp);

        }
        // sort list by  character id
        shooterList.Sort(sortByPlayerId);

        playerDataLoaded = shooterList.Count > 0;
        return shooterList;
    }

    private int getPointsUsed(CharacterProfile temp)
    {

       int pointsUsed = ((int)temp.Accuracy3Pt + (int)temp.Accuracy4Pt + (int)temp.Accuracy7Pt) - 210;
       int pointsUsedRange = (temp.Range - 25) / 5;
       if (pointsUsed >= 90)
       {
            //pointsUsed = (temp.Level - (pointsUsedRange - pointsUsed));
            pointsUsed = pointsUsedRange;
            //Debug.Log("pointsUsed : " + pointsUsed);
            //Debug.Log("----------------range should be : " + (((temp.Level - 90) * 5) +(90 * 5)+25));
        }

       return pointsUsed;
    }
    private int getPointsAvailable(CharacterProfile temp)
    {
        int pointsAvailable;
        pointsAvailable = temp.PointsUsed >= 0 ? (temp.Level - temp.PointsUsed) : -(temp.Level - temp.PointsUsed);

        return pointsAvailable;
    }
        private List<CharacterProfile> loadCpuSelectDataList()
    {
        List<CharacterProfile> shooterList = new List<CharacterProfile>();

        string path = "Prefabs/menu_start/cpu_players_selected_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];
        foreach (GameObject obj in objects)
        {
            if (obj == null || !obj.TryGetComponent(out CharacterProfile temp))
            {
                Debug.LogError("A CPU selection prefab is missing CharacterProfile.");
                continue;
            }

            // Legacy CPU id 0 is the authored "no CPU here" record - not a real character. It
            // never gets CPU baseline stats; it exists only so the menu has something to render
            // for an inactive CPU slot. This assumes id 0 is never reused for a real CPU selection
            // entry - true today (confirmed by the parity audit in #69/#70) but not guaranteed by
            // anything the type system enforces, so do not "fix" a real character's id to 0 to
            // match a runtime prefab that misauthors it that way (see #70).
            if (temp.PlayerId == 0)
            {
                shooterList.Add(temp);
                continue;
            }

            // Catalog membership is what makes this entry a CPU, not the authored isCpu flag -
            // that flag drifted stale before (issue #69) and left a real CPU initialized from raw
            // serialized shooter stats instead of a computed baseline. Every real entry in this
            // catalog is a CPU; isCpu = false here is an authoring defect to surface, not a
            // legitimately different kind of entry to skip initializing.
            if (!temp.isCpu)
            {
                Debug.LogError($"CPU selection prefab '{temp.PlayerObjectName}' (playerId {temp.PlayerId}) does not author isCpu = true.");
            }

            temp.InitializeCpuBaselineStats();
            shooterList.Add(temp);
        }
        // sort list by  character id
        shooterList.Sort(sortByPlayerId);

        cpuPlayerDataLoaded = shooterList.Count > 0;

        return shooterList;
    }


    private List<CheerleaderProfile> loadDefaultCheerleaderProfiles()
    {
        List<CheerleaderProfile> cheerList = new List<CheerleaderProfile>();

        string path = "Prefabs/menu_start/cheerleader_default_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            if (obj != null && obj.TryGetComponent(out CheerleaderProfile temp))
            {
                cheerList.Add(temp);
            }
            else
            {
                Debug.LogError("A default cheerleader prefab is missing CheerleaderProfile.");
            }
        }
        // sort list by  character id
        cheerList.Sort(sortByCheerleaderId);

        //Debug.Log("***************************  cheerList.Count : " + cheerList.Count + "   objects.Length : " + objects.Length);

        cheerleaderDataLoaded = cheerList.Count > 0;
        return cheerList;
    }

    private List<CharacterProfile> loadDefaultPlayerShooterProfiles()
    {
        List<CharacterProfile> shooterList = new List<CharacterProfile>();

        string path = "Prefabs/menu_start/default_shooter_profiles";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            if (obj != null && obj.TryGetComponent(out CharacterProfile temp))
            {
                shooterList.Add(temp);
            }
            else
            {
                Debug.LogError("A default player prefab is missing CharacterProfile.");
            }
        }
        // sort list by  character id
        shooterList.Sort(sortByPlayerId);

        playerDataLoaded = shooterList.Count > 0;

        return shooterList;
    }

    private List<CheerleaderProfile> loadCheerleaderSelectDataList()
    {
        List<CheerleaderProfileRecord> dbCheerList = DBHelper.instance.getCheerleaderProfileStats();
        List<CheerleaderProfile> cheerList = new List<CheerleaderProfile>();

        string path = "Prefabs/menu_start/cheerleader_selected_object";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            if (obj == null || !obj.TryGetComponent(out CheerleaderProfile temp))
            {
                Debug.LogError("A cheerleader selection prefab is missing CheerleaderProfile.");
                continue;
            }
            cheerList.Add(temp);
            // need to create a copy to keep prefab from changing

            //// if character not in database, but prefab exists -- insert into DB and add to list
            //if (!dbCheerList.Any(item => item.CheerleaderId == temp.CheerleaderId))
            //{
            //    //isLocked = true;
            //    // get default profile for chracter to be inserted
            //    string defaultPath = "Prefabs/menu_start/cheerleader_default_objects/cheerleader_selected_"
            //        + temp.CheerleaderId.ToString("00") + "_" + temp.CheerleaderObjectName;

            //    Debug.Log("defaultPath : " + defaultPath);

            //    CheerleaderProfile defaultTemp = Resources.Load<GameObject>(defaultPath).GetComponent<CheerleaderProfile>();
            //    //// insert to DB
            //    //StartCoroutine(InsertNewCheerleaderToDB(defaultTemp));
            //    // add to current list to be loaded
            //    dbCheerList.Add(temp);
            //}

            //// load stats from DB, but load portrait from prefab
            //temp.CheerleaderId = dbCheerList.Find(x => x.CheerleaderId == temp.CheerleaderId).CheerleaderId;
            //temp.CheerleaderDisplayName = dbCheerList.Find(x => x.CheerleaderId == temp.CheerleaderId).CheerleaderDisplayName;
            //temp.CheerleaderObjectName = dbCheerList.Find(x => x.CheerleaderId == temp.CheerleaderId).CheerleaderObjectName;
            //temp.UnlockCharacterText = dbCheerList.Find(x => x.CheerleaderId == temp.CheerleaderId).UnlockCharacterText;

            //temp.IsLocked = dbCheerList.Find(x => x.CheerleaderId == temp.CheerleaderId).IsLocked;
            /*
             * Portrait should already be loaded from prefab
             */

            //cheerList.Add(temp);
            //if (!temp.IsLocked)
            //{
            //    Debug.Log("--------------------------------- ADD CHEER DB DATA TO PREFAB--------------------------");
            //    Debug.Log("cheer unlock text :: " + temp.UnlockCharacterText + "  islocked :: " + temp.IsLocked);
            //}
        }
        // sort list by  character id
        cheerList.Sort(sortByCheerleaderId);

        cheerleaderDataLoaded = cheerList.Count > 0;

        return cheerList;
    }

    private List<LevelSelected> loadLevelSelectDataList()
    {
        List<LevelSelected> levelList = new List<LevelSelected>();

        string path = "Prefabs/menu_start/level_selected_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];
        int counter = 0;
        foreach (GameObject obj in objects)
        {
            if (obj == null || !obj.TryGetComponent(out LevelSelected temp))
            {
                Debug.LogError("A level selection prefab is missing LevelSelected.");
                continue;
            }
            if(temp.IsSelectable)
            {
                levelList.Add((LevelSelected)temp);
                counter++;
            }
        }

        // sort list by  level id
        levelList.Sort(sortByLevelId);

        levelDataLoaded = levelList.Count > 0 && levelList.Count == counter;

        return levelList;
    }

    private List<StartScreenModeSelected> loadModeSelectDataList()
    {
        List<StartScreenModeSelected> modeList = new List<StartScreenModeSelected>();

        string path = "Prefabs/menu_start/mode_selected_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            if (obj != null && obj.TryGetComponent(out StartScreenModeSelected temp))
            {
                modeList.Add(temp);
            }
            else
            {
                Debug.LogError("A mode selection prefab is missing StartScreenModeSelected.");
            }
        }
        // sort list by  mode id
        modeList.Sort(sortByModeId);

        modeDataLoaded = modeList.Count > 0;

        return modeList;
    }

    static int sortByPlayerId(CharacterProfile p1, CharacterProfile p2)
    {
        return p1.PlayerId.CompareTo(p2.PlayerId);
    }

    static int sortByCheerleaderId(CheerleaderProfile p1, CheerleaderProfile p2)
    {
        return p1.CheerleaderId.CompareTo(p2.CheerleaderId);
    }

    static int sortByLevelId(LevelSelected l1, LevelSelected l2)
    {
        return l1.LevelId.CompareTo(l2.LevelId);
    }

    static int sortByModeId(StartScreenModeSelected m1, StartScreenModeSelected m2)
    {
        return m1.ModeId.CompareTo(m2.ModeId);
    }


    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        GameObject messageObject = GameObject.Find("messageDisplay");
        if (messageObject != null && messageObject.TryGetComponent(out Text messageText))
        {
            messageText.text = "";
        }
    }
}

