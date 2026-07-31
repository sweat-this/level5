using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadedData : MonoBehaviour
{
    // load start screen data for players/ friend/ mode /level
    [SerializeField]
    private List<CharacterProfile> playerSelectedData;
    [SerializeField]
    private List<CharacterProfile> cpuPlayerSelectedData;
    // list off cheerleader profile data
    [SerializeField]
    private List<CheerleaderProfile> cheerleaderSelectedData;
    // list off level  data
    [SerializeField]
    private List<LevelSelected> levelSelectedData;
    //mode selected objects
    [SerializeField]
    private List<StartScreenModeSelected> modeSelectedData;

    [SerializeField] private bool dataLoaded;

    float timeoutStart;
    float timeoutEnd;
    float timeoutMax = 10;
    private bool loadTimeoutLogged;

    public static LoadedData instance;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        StartCoroutine(LoadStartScreenData());
    }

    private void Update()
    {
        if (!dataLoaded)
        {
            if (Time.time > timeoutEnd)
            {
                LogLoadTimeout();
            }
        }
    }

    IEnumerator LoadStartScreenData()
    {
        timeoutStart = Time.time;
        timeoutEnd = Time.time + timeoutMax;

        yield return new WaitUntil(() => LoadManager.instance.playerDataLoaded);
        playerSelectedData = LoadManager.instance.PlayerSelectedData;

        yield return new WaitUntil(() => LoadManager.instance.cpuPlayerDataLoaded);
        cpuPlayerSelectedData = LoadManager.instance.CpuPlayerSelectedData;

        yield return new WaitUntil(() => LoadManager.instance.cheerleaderDataLoaded);
        cheerleaderSelectedData = LoadManager.instance.CheerleaderSelectedData;

        yield return new WaitUntil(() => LoadManager.instance.levelDataLoaded);
        levelSelectedData = LoadManager.instance.LevelSelectedData;

        yield return new WaitUntil(() => LoadManager.instance.modeDataLoaded);
        modeSelectedData = LoadManager.instance.ModeSelectedData;


        if (HasAllRequiredData())
        {
            dataLoaded = true;
        }
    }

    private bool HasAllRequiredData()
    {
        return playerSelectedData != null
            && playerSelectedData.Count > 0
            && cpuPlayerSelectedData != null
            && cpuPlayerSelectedData.Count > 0
            && cheerleaderSelectedData != null
            && cheerleaderSelectedData.Count > 0
            && levelSelectedData != null
            && levelSelectedData.Count > 0
            && modeSelectedData != null
            && modeSelectedData.Count > 0;
    }

    private void LogLoadTimeout()
    {
        if (loadTimeoutLogged)
        {
            return;
        }

        loadTimeoutLogged = true;
        Debug.LogError("Loading timed out before required start-screen data was ready. "
            + "player=" + GetCount(playerSelectedData)
            + ", cpu=" + GetCount(cpuPlayerSelectedData)
            + ", cheerleader=" + GetCount(cheerleaderSelectedData)
            + ", level=" + GetCount(levelSelectedData)
            + ", mode=" + GetCount(modeSelectedData));
    }

    private static int GetCount<T>(List<T> values)
    {
        return values == null ? -1 : values.Count;
    }

    public CharacterProfile getSelectedCharacterProfile(int charid)
    {
        CharacterProfile temp = new CharacterProfile();
        //CharacterProfile temp = gameObject.AddComponent<CharacterProfile>();

        temp = playerSelectedData.Find(x => x.PlayerId == charid);

        return temp;
    }

    public List<CharacterProfile> PlayerSelectedData { get => playerSelectedData; }
    public List<CheerleaderProfile> CheerleaderSelectedData { get => cheerleaderSelectedData; }
    public List<LevelSelected> LevelSelectedData { get => levelSelectedData; }
    public List<StartScreenModeSelected> ModeSelectedData { get => modeSelectedData; }
    public bool DataLoaded { get => dataLoaded; }
    public List<CharacterProfile> CpuPlayerSelectedData { get => cpuPlayerSelectedData; set => cpuPlayerSelectedData = value; }
}
