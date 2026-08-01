using System.Collections.Generic;

public class LevelCatalog
{
    private readonly List<LevelPreset> presets;

    public LevelCatalog(IEnumerable<LevelPreset> presets)
    {
        this.presets = presets == null ? new List<LevelPreset>() : new List<LevelPreset>(presets);
    }

    public IReadOnlyList<LevelPreset> Presets => presets;

    public LevelPreset FindByLevelId(int levelId)
    {
        return presets.Find(level => level != null && level.LevelId == levelId);
    }

    public LevelPreset FindByObjectName(string levelObjectName)
    {
        if (string.IsNullOrEmpty(levelObjectName))
        {
            return null;
        }

        return presets.Find(level => level != null && level.LevelObjectName == levelObjectName);
    }

    public static LevelCatalog FromLevelSelected(IEnumerable<LevelSelected> levels)
    {
        List<LevelPreset> levelPresets = new List<LevelPreset>();
        if (levels == null)
        {
            return new LevelCatalog(levelPresets);
        }

        foreach (LevelSelected level in levels)
        {
            LevelPreset preset = LevelPreset.FromLevelSelected(level);
            if (preset != null)
            {
                levelPresets.Add(preset);
            }
        }

        return new LevelCatalog(levelPresets);
    }
}
