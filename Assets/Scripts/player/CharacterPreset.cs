using UnityEngine;

[CreateAssetMenu(menuName = "Level5/Character Preset", fileName = "CharacterPreset")]
public class CharacterPreset : ScriptableObject
{
    [SerializeField] private string characterId;
    [SerializeField] private int legacyPlayerId;
    [SerializeField] private string displayName;
    [SerializeField] private string objectName;
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private Sprite portrait;
    [SerializeField] private Sprite winPortrait;
    [SerializeField] private Sprite losePortrait;
    [SerializeField] private bool unlockedByDefault;
    [SerializeField] private bool isShooter;
    [SerializeField] private bool isFighter;
    [SerializeField] private bool isCpu;
    [SerializeField] private CharacterStats baseStats;
    [SerializeField] private CharacterStats minStats;
    [SerializeField] private CharacterStats maxStats;
    [SerializeField] private CharacterStats upgradeStep;

    public string CharacterId => characterId;
    public int LegacyPlayerId => legacyPlayerId;
    public string DisplayName => displayName;
    public string ObjectName => objectName;
    public GameObject CharacterPrefab => characterPrefab;
    public Sprite Portrait => portrait;
    public Sprite WinPortrait => winPortrait;
    public Sprite LosePortrait => losePortrait;
    public bool UnlockedByDefault => unlockedByDefault;
    public bool IsShooter => isShooter;
    public bool IsFighter => isFighter;
    public bool IsCpu => isCpu;
    public CharacterStats BaseStats => baseStats;
    public CharacterStats MinStats => minStats;
    public CharacterStats MaxStats => maxStats;
    public CharacterStats UpgradeStep => upgradeStep;
}
