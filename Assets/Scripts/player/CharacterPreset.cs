using UnityEngine;
using System.Collections.Generic;

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
    [SerializeField] private bool upgradesEnabled = true;
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
    public bool UpgradesEnabled => upgradesEnabled;
    public CharacterStats BaseStats => baseStats;
    public CharacterStats MinStats => minStats;
    public CharacterStats MaxStats => maxStats;
    public CharacterStats UpgradeStep => upgradeStep;

    public bool Validate(List<string> issues)
    {
        bool valid = true;

        if (string.IsNullOrWhiteSpace(characterId))
        {
            issues?.Add(name + " is missing characterId.");
            valid = false;
        }

        if (legacyPlayerId <= 0)
        {
            issues?.Add(name + " has invalid legacyPlayerId.");
            valid = false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            issues?.Add(name + " is missing displayName.");
            valid = false;
        }

        if (baseStats == null)
        {
            issues?.Add(name + " is missing baseStats.");
            valid = false;
        }

        if (maxStats == null)
        {
            issues?.Add(name + " is missing maxStats.");
            valid = false;
        }

        if (upgradesEnabled && upgradeStep == null)
        {
            issues?.Add(name + " is missing upgradeStep.");
            valid = false;
        }

        if (baseStats != null && maxStats != null && !StatsWithinMax(baseStats, maxStats))
        {
            issues?.Add(name + " has baseStats above maxStats.");
            valid = false;
        }

        if (minStats != null && baseStats != null && !StatsWithinMax(minStats, baseStats))
        {
            issues?.Add(name + " has minStats above baseStats.");
            valid = false;
        }

        if (minStats != null && maxStats != null && !StatsWithinMax(minStats, maxStats))
        {
            issues?.Add(name + " has minStats above maxStats.");
            valid = false;
        }

        if (upgradeStep != null && HasNegativeStats(upgradeStep))
        {
            issues?.Add(name + " has negative upgradeStep values.");
            valid = false;
        }

        return valid;
    }

    private static bool StatsWithinMax(CharacterStats value, CharacterStats max)
    {
        return value.accuracy2Pt <= max.accuracy2Pt
            && value.accuracy3Pt <= max.accuracy3Pt
            && value.accuracy4Pt <= max.accuracy4Pt
            && value.accuracy7Pt <= max.accuracy7Pt
            && value.jumpForce <= max.jumpForce
            && value.speed <= max.speed
            && value.runSpeed <= max.runSpeed
            && value.runSpeedHasBall <= max.runSpeedHasBall
            && value.range <= max.range
            && value.release <= max.release
            && value.luck <= max.luck
            && value.shootAngle <= max.shootAngle;
    }

    private static bool HasNegativeStats(CharacterStats value)
    {
        return value.accuracy2Pt < 0
            || value.accuracy3Pt < 0
            || value.accuracy4Pt < 0
            || value.accuracy7Pt < 0
            || value.jumpForce < 0
            || value.speed < 0
            || value.runSpeed < 0
            || value.runSpeedHasBall < 0
            || value.range < 0
            || value.release < 0
            || value.luck < 0
            || value.shootAngle < 0;
    }
}
