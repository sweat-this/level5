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
    private LevelCatalog levelCatalog;
    //mode selected objects
    [SerializeField]
    private List<StartScreenModeSelected> modeSelectedData;

    [SerializeField] private bool dataLoaded;

    [SerializeField] private float timeoutMax = 12f;
    [SerializeField] private bool loadFailed;
    [SerializeField] private string loadError;
    private Coroutine loadCoroutine;

    public static LoadedData instance;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
    private void Start()
    {
        Retry();
    }

    public void Retry()
    {
        if (loadCoroutine != null)
        {
            StopCoroutine(loadCoroutine);
        }

        dataLoaded = false;
        loadFailed = false;
        loadError = string.Empty;
        loadCoroutine = StartCoroutine(LoadStartScreenData());
    }

    IEnumerator LoadStartScreenData()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            float deadline = Time.realtimeSinceStartup + timeoutMax;
            while (!ManagerReportsReady() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (LoadManager.instance != null)
            {
                CopyFromManager(LoadManager.instance);
            }

            if (HasAllRequiredData())
            {
                dataLoaded = true;
                loadCoroutine = null;
                yield break;
            }

            if (LoadManager.instance != null
                && LoadManager.instance.TryLoadFallbackData(out string fallbackError))
            {
                CopyFromManager(LoadManager.instance);
                dataLoaded = HasAllRequiredData();
                if (dataLoaded)
                {
                    loadCoroutine = null;
                    yield break;
                }

                loadError = fallbackError;
            }

            if (attempt == 0 && LoadManager.instance != null)
            {
                LoadManager.instance.LoadAllData();
            }
        }

        loadFailed = true;
        loadCoroutine = null;
        loadError = string.IsNullOrEmpty(loadError)
            ? "Required game catalogs could not be loaded."
            : loadError;
        Debug.LogError(loadError);
    }

    private static bool ManagerReportsReady()
    {
        return LoadManager.instance != null
            && LoadManager.instance.playerDataLoaded
            && LoadManager.instance.cpuPlayerDataLoaded
            && LoadManager.instance.cheerleaderDataLoaded
            && LoadManager.instance.levelDataLoaded
            && LoadManager.instance.modeDataLoaded
            && LoadManager.instance.PersistenceReady;
    }

    private void CopyFromManager(LoadManager manager)
    {
        playerSelectedData = manager.PlayerSelectedData;
        cpuPlayerSelectedData = manager.CpuPlayerSelectedData;
        cheerleaderSelectedData = manager.CheerleaderSelectedData;
        levelSelectedData = manager.LevelSelectedData;
        levelCatalog = manager.LevelCatalog;
        modeSelectedData = manager.ModeSelectedData;
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
            && modeSelectedData.Count > 0
            && LoadManager.instance != null
            && LoadManager.instance.PersistenceReady;
    }

    public CharacterProfile getSelectedCharacterProfile(int charid)
    {
        return playerSelectedData?.Find(x => x.PlayerId == charid);
    }

    public List<CharacterProfile> PlayerSelectedData { get => playerSelectedData; }
    public List<CheerleaderProfile> CheerleaderSelectedData { get => cheerleaderSelectedData; }
    public List<LevelSelected> LevelSelectedData { get => levelSelectedData; }
    public LevelCatalog LevelCatalog { get => levelCatalog; }
    public List<StartScreenModeSelected> ModeSelectedData { get => modeSelectedData; }
    public bool DataLoaded { get => dataLoaded; }
    public bool LoadFailed => loadFailed;
    public string LoadError => loadError;
    public List<CharacterProfile> CpuPlayerSelectedData { get => cpuPlayerSelectedData; set => cpuPlayerSelectedData = value; }
}
