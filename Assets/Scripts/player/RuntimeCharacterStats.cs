using System;

[Serializable]
public class RuntimeCharacterStats
{
    public string characterId;
    public int legacyPlayerId;
    public string displayName;
    public int experience;
    public int level;
    public int pointsSpent;
    public bool unlocked;
    public CharacterStats stats;
}
