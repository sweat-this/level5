using System;
using System.Collections.Generic;

[Serializable]
public class CharacterProgressSave
{
    public int version = 1;
    public string userId;
    public string selectedCharacterId;
    public string migratedFrom;
    public List<PlayerCharacterProgress> characters = new List<PlayerCharacterProgress>();
}
