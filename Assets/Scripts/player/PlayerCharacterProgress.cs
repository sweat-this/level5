using System;

[Serializable]
public class PlayerCharacterProgress
{
    public string characterId;
    public int legacyPlayerId;
    public bool unlocked;
    public int experience;
    public int level;
    public CharacterUpgradeLevels upgrades = new CharacterUpgradeLevels();
    public string lastModifiedUtc;

    public int PointsSpent => upgrades == null ? 0 : upgrades.TotalSpent;
}
