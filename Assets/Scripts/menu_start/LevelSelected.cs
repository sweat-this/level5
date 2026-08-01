using UnityEngine;
using UnityEngine.UI;

public class LevelSelected : MonoBehaviour
{
    [SerializeField] private int levelId;
    [SerializeField] private string levelDisplayName;
    [SerializeField] private string levelInfo;
    [SerializeField] private string levelObjectName;
    [SerializeField] private string levelDescription;
    [SerializeField] private bool levelRequiresTimeOfDay;
    [SerializeField] private bool levelHasTraffic;
    [SerializeField] private bool levelHasWeather;
    [SerializeField] private bool levelHasSevenPointers;
    [SerializeField] private bool isFightingLevel;
    [SerializeField] private bool isShootingLevel;
    [SerializeField] private bool isBattleRoyalLevel;
    [SerializeField] private bool isCageMatchLevel;
    [SerializeField] private bool customCamera;
    [SerializeField] private bool isSelectable;
    [SerializeField] private bool isLocked;
    [SerializeField] private GameObject cpuPlayer;
    private Sprite cpuPlayerWinImage;
    private Sprite cpuPlayerLoseImage;

    public string LevelDescription
    {
        get => levelDescription;
        set => levelDescription = value;
    }

    public int LevelId
    {
        get => levelId;
        set => levelId = value;
    }

    public string LevelDisplayName
    {
        get => levelDisplayName;
        set => levelDisplayName = value;
    }

    public string LevelObjectName
    {
        get => levelObjectName;
        set => levelObjectName = value;
    }
    public bool LevelRequiresTimeOfDay { get => levelRequiresTimeOfDay; set => levelRequiresTimeOfDay = value; }
    public bool LevelHasTraffic { get => levelHasTraffic; set => levelHasTraffic = value; }
    public bool LevelHasWeather { get => levelHasWeather; set => levelHasWeather = value; }
    public bool IsFightingLevel { get => isFightingLevel; }
    public bool IsShootingLevel { get => isShootingLevel;  }
    public bool IsBattleRoyalLevel { get => isBattleRoyalLevel; }
    public bool IsCageMatchLevel { get => isCageMatchLevel;  }
    public bool CustomCamera { get => customCamera; set => customCamera = value; }
    public bool LevelHasSevenPointers { get => levelHasSevenPointers; set => levelHasSevenPointers = value; }
    public string LevelInfo { get => levelInfo; set => levelInfo = value; }
    public GameObject CpuPlayer { get => cpuPlayer; set => cpuPlayer = value; }
    public Sprite CpuPlayerWinImage { get => cpuPlayer.GetComponent<CharacterProfile>().winPortrait; set => cpuPlayerWinImage = value; }
    public Sprite CpuPlayerLoseImage { get => cpuPlayer.GetComponent<CharacterProfile>().losePortrait; set => cpuPlayerLoseImage = value; }
    public bool IsSelectable { get => isSelectable; }
    public bool IsLocked { get => isLocked; }
}
