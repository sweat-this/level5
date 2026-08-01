public static class UnlockService
{
    public static bool IsCharacterUnlocked(int legacyPlayerId)
    {
        if (LoadedData.instance != null && LoadedData.instance.PlayerSelectedData != null)
        {
            CharacterProfile profile = LoadedData.instance.PlayerSelectedData.Find(character =>
                character != null && character.PlayerId == legacyPlayerId);
            if (profile != null)
            {
                return !profile.IsLocked;
            }
        }

        if (CharacterProgressStore.TryLoadExisting(CharacterProgressAccountId.GetCurrent(), out CharacterProgressSave save)
            && save.characters != null)
        {
            PlayerCharacterProgress progress = save.characters.Find(character =>
                character != null && character.legacyPlayerId == legacyPlayerId);
            if (progress != null)
            {
                return progress.unlocked;
            }
        }

        return false;
    }

    public static bool IsCharacterUnlocked(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return false;
        }

        if (LoadedData.instance != null && LoadedData.instance.PlayerSelectedData != null)
        {
            CharacterProfile profile = LoadedData.instance.PlayerSelectedData.Find(character =>
                character != null && character.PlayerObjectName == characterId);
            if (profile != null)
            {
                return !profile.IsLocked;
            }
        }

        if (CharacterProgressStore.TryLoadExisting(CharacterProgressAccountId.GetCurrent(), out CharacterProgressSave save)
            && save.characters != null)
        {
            PlayerCharacterProgress progress = save.characters.Find(character =>
                character != null && character.characterId == characterId);
            if (progress != null)
            {
                return progress.unlocked;
            }
        }

        return false;
    }

    public static bool IsLevelUnlocked(int levelId)
    {
        LevelPreset preset = LoadedData.instance?.LevelCatalog?.FindByLevelId(levelId);
        if (preset != null)
        {
            return !preset.IsLocked;
        }

        if (LoadedData.instance != null && LoadedData.instance.LevelSelectedData != null)
        {
            LevelSelected level = LoadedData.instance.LevelSelectedData.Find(item =>
                item != null && item.LevelId == levelId);
            if (level != null)
            {
                return !level.IsLocked;
            }
        }

        return false;
    }

    public static bool IsLevelUnlocked(string levelObjectName)
    {
        if (string.IsNullOrEmpty(levelObjectName))
        {
            return false;
        }

        LevelPreset preset = LoadedData.instance?.LevelCatalog?.FindByObjectName(levelObjectName);
        if (preset != null)
        {
            return !preset.IsLocked;
        }

        if (LoadedData.instance != null && LoadedData.instance.LevelSelectedData != null)
        {
            LevelSelected level = LoadedData.instance.LevelSelectedData.Find(item =>
                item != null && item.LevelObjectName == levelObjectName);
            if (level != null)
            {
                return !level.IsLocked;
            }
        }

        return false;
    }
}
