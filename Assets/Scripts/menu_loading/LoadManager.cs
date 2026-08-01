
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadManager : MonoBehaviour
{
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
    bool CharacterProfileTableCreated = false;

    [SerializeField] internal bool playerDataLoaded = false;
    [SerializeField] internal bool cpuPlayerDataLoaded = false;
    [SerializeField] internal bool cheerleaderDataLoaded = false;
    [SerializeField] internal bool levelDataLoaded = false;
    [SerializeField] internal bool modeDataLoaded = false;
    private bool sceneLoadRequested;

    private bool CheerleaderProfileTableExists;
    private bool CheerleaderProfileTableCreated;

    public static LoadManager instance;

    void Awake()
    {
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
    }

    public void LoadAllData()
    {
        //yield return new WaitUntil(() => GameOptions.architectureInfoLoaded == true);
        //Debug.Log("LoadAllData : architectureInfoLoaded : " + GameOptions.architectureInfoLoaded);
        StartCoroutine(verifyCharacterProfileTable());
        StartCoroutine(verifyCheerleaderProfileTable());
        StartCoroutine(LoadGameData());
    }

    IEnumerator verifyCharacterProfileTable()
    {
        yield return new WaitUntil(IsDatabaseReady);

        // if CharacterProfile table does exist
        if (DBConnector.instance.tableExists("CharacterProfile")
            && !DBHelper.instance.isTableEmpty("CharacterProfile"))
        {
            CharacterProfileTableExists = true;
        }
        // if CharacterProfile table doesnt exist, create table
        else
        {
            // drop table just in case of error
            StartCoroutine(DBConnector.instance.dropDatabaseTable("CharacterProfile"));
            //create table
            StartCoroutine(DBConnector.instance.CreateTableCharacterProfile());
            CharacterProfileTableCreated = true;
        }
    }

    IEnumerator verifyCheerleaderProfileTable()
    {
        yield return new WaitUntil(IsDatabaseReady);

        if (DBConnector.instance.tableExists("CheerleaderProfile")
            && !DBHelper.instance.isTableEmpty("CheerleaderProfile"))
        {
            CheerleaderProfileTableExists = true;
        }
        // if CharacterProfile table doesnt exist, create table
        else
        {
            // drop table just in case of error
            StartCoroutine(DBConnector.instance.dropDatabaseTable("CheerleaderProfile"));
            //create table
            StartCoroutine( DBConnector.instance.CreateTableCheerleaderProfile());
            CheerleaderProfileTableCreated = true;
        }

    }


    IEnumerator LoadGameData()
    {
        yield return new WaitUntil(IsDatabaseReady);

        // insert default player profiles + table did not already exits
        if (CharacterProfileTableCreated && !CharacterProfileTableExists)
        {
            playerSelectedData = loadDefaultPlayerShooterProfiles();
            StartCoroutine(DBHelper.instance.InsertCharacterProfile(playerSelectedData));
        }
        //table already exists + does NOT require default records
        if (!CharacterProfileTableCreated && CharacterProfileTableExists)
        {
            playerSelectedData = loadPlayerSelectDataList();
        }
        // =============================================================================
        // insert default cheerleader profiles + table did not already exits
        if (CheerleaderProfileTableCreated && !CheerleaderProfileTableExists)
        {
            // load default data from prefabs
            cheerleaderSelectedData = loadDefaultCheerleaderProfiles();
            // insert default into DB
            StartCoroutine(DBHelper.instance.InsertCheerleaderProfile(cheerleaderSelectedData));
        }

        //table already exists + does NOT require default records
        if (!CheerleaderProfileTableCreated && CheerleaderProfileTableExists)
        {
            cheerleaderSelectedData = loadCheerleaderSelectDataList();
        }
        //cheerleaderSelectedData = loadDefaultCheerleaderProfiles();
        cpuPlayerSelectedData = loadCpuSelectDataList();
        levelSelectedData = loadLevelSelectDataList();
        levelCatalog = LevelCatalog.FromLevelSelected(levelSelectedData);
        modeSelectedData = loadModeSelectDataList();
    }

    IEnumerator InsertNewCharacterToDB(CharacterProfile character)
    {
        yield return new WaitUntil(IsDatabaseUnlocked);
        DBHelper.instance.InsertCharacterProfile(character);
    }

    IEnumerator InsertNewCheerleaderToDB(CheerleaderProfile cheerleader)
    {
        yield return new WaitUntil(IsDatabaseUnlocked);
        DBHelper.instance.InsertCheerleaderProfile(cheerleader);

        Debug.Log("cheerleader record inserted");
    }

    private static bool IsDatabaseReady()
    {
        return DBConnector.instance != null
            && DBHelper.instance != null
            && DBConnector.instance.DatabaseCreated;
    }

    private static bool IsDatabaseUnlocked()
    {
        return DBHelper.instance != null && !DBHelper.instance.DatabaseLocked;
    }

    private List<CharacterProfile> loadPlayerSelectDataList()
    {
        List<CharacterProfileRecord> dbShootStatsList = DBHelper.instance.getCharacterProfileStats(GameOptions.userid);
        CharacterProgressParityLogger.LogMismatchWarnings(characterPresetCatalog, dbShootStatsList);
        List<CharacterProfile> shooterList = new List<CharacterProfile>();

        string path = "Prefabs/menu_start/player_selected_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            /*
             * if prefab is not in DB, insert
             * ex. create new character, need to auto insert into db
             */
            CharacterProfile temp = obj.GetComponent<CharacterProfile>();

            // if character not in database, but prefab exists -- insert into DB and add to list
            if (!dbShootStatsList.Any(item => item.PlayerId == temp.PlayerId))
            {
                //isLocked = true;
                // get default profile for chracter to be inserted
                string defaultPath = "Prefabs/menu_start/default_shooter_profiles/player_selected_" + temp.PlayerObjectName;
                CharacterProfile defaultTemp = Resources.Load<GameObject>(defaultPath).GetComponent<CharacterProfile>();
                // insert to DB
                StartCoroutine(InsertNewCharacterToDB(defaultTemp));
                // add to current list to be loaded
                dbShootStatsList.Add(CharacterProfileRecord.FromProfile(temp));
            }

            CharacterProfileRecord dbStats = dbShootStatsList.Find(x => x.PlayerId == temp.PlayerId);

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
            temp.Level = temp.Experience / 3000;
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

        if (shooterList.Count == objects.Length)
        {
            playerDataLoaded = true;
        }
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
            CharacterProfile temp = obj.GetComponent<CharacterProfile>();
            if (temp.isCpu)
            {
                temp.intializeCpuShooterStats();
            }
            shooterList.Add(temp);
        }
        // sort list by  character id
        shooterList.Sort(sortByPlayerId);

        if (shooterList.Count == objects.Length)
        {
            cpuPlayerDataLoaded = true;
        }

        return shooterList;
    }


    private List<CheerleaderProfile> loadDefaultCheerleaderProfiles()
    {
        List<CheerleaderProfile> cheerList = new List<CheerleaderProfile>();

        string path = "Prefabs/menu_start/cheerleader_default_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            CheerleaderProfile temp = obj.GetComponent<CheerleaderProfile>();
            cheerList.Add(temp);
        }
        // sort list by  character id
        cheerList.Sort(sortByCheerleaderId);

        //Debug.Log("***************************  cheerList.Count : " + cheerList.Count + "   objects.Length : " + objects.Length);

        if (cheerList.Count == objects.Length)
        {
            cheerleaderDataLoaded = true;
        }
        return cheerList;
    }

    private List<CharacterProfile> loadDefaultPlayerShooterProfiles()
    {
        List<CharacterProfile> shooterList = new List<CharacterProfile>();

        string path = "Prefabs/menu_start/default_shooter_profiles";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            CharacterProfile temp = obj.GetComponent<CharacterProfile>();
            shooterList.Add(temp);
        }
        // sort list by  character id
        shooterList.Sort(sortByPlayerId);

        if (shooterList.Count == objects.Length)
        {
            playerDataLoaded = true;
        }

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
            CheerleaderProfile temp = obj.GetComponent<CheerleaderProfile>();
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

        if (cheerList.Count == objects.Length)
        {
            cheerleaderDataLoaded = true;
        }

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
            LevelSelected temp = obj.GetComponent<LevelSelected>();
            if(temp.IsSelectable)
            {
                levelList.Add((LevelSelected)temp);
                counter++;
            }
        }

        // sort list by  level id
        levelList.Sort(sortByLevelId);

        if (levelList.Count == counter)
        {
            levelDataLoaded = true;
        }

        return levelList;
    }

    private List<StartScreenModeSelected> loadModeSelectDataList()
    {
        List<StartScreenModeSelected> modeList = new List<StartScreenModeSelected>();

        string path = "Prefabs/menu_start/mode_selected_objects";
        GameObject[] objects = Resources.LoadAll<GameObject>(path) as GameObject[];

        foreach (GameObject obj in objects)
        {
            StartScreenModeSelected temp = obj.GetComponent<StartScreenModeSelected>();
            modeList.Add(temp);
        }
        // sort list by  mode id
        modeList.Sort(sortByModeId);

        if (modeList.Count == objects.Length)
        {
            modeDataLoaded = true;
        }

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
        Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
        messageText.text = "";
    }
}

