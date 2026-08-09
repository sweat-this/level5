public static class UnlockService
{
    /// <summary>
    /// The level catalog on a <see cref="LoadedData"/> that is actually alive, or null.
    ///
    /// This exists so the call sites do not write <c>LoadedData.instance?.LevelCatalog</c>. The
    /// null-conditional operator compiles to a plain reference null check, which does not go
    /// through Unity's overloaded <c>==</c>, so a destroyed <c>LoadedData</c> would not be seen as
    /// null and the member access would run on it anyway. Comparing with <c>!=</c> first is what
    /// asks Unity the question properly; the <c>?.</c> after this call is safe because
    /// <c>LevelCatalog</c> is a plain object rather than a UnityEngine.Object.
    /// </summary>
    private static LevelCatalog LevelCatalogOf(LoadedData loadedData)
    {
        return loadedData != null ? loadedData.LevelCatalog : null;
    }

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
        // Deliberately not `LoadedData.instance?.` - see LevelCatalogOf.
        LevelPreset preset = LevelCatalogOf(LoadedData.instance)?.FindByLevelId(levelId);
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

        LevelPreset preset = LevelCatalogOf(LoadedData.instance)?.FindByObjectName(levelObjectName);
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
