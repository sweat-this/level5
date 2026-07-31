using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Level5/Character Preset Catalog", fileName = "CharacterPresetCatalog")]
public class CharacterPresetCatalog : ScriptableObject
{
    [SerializeField] private List<CharacterPreset> presets = new List<CharacterPreset>();

    public IReadOnlyList<CharacterPreset> Presets => presets;

    public CharacterPreset FindByCharacterId(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
        {
            return null;
        }

        return presets.Find(preset => preset != null && preset.CharacterId == characterId);
    }

    public CharacterPreset FindByLegacyPlayerId(int legacyPlayerId)
    {
        return presets.Find(preset => preset != null && preset.LegacyPlayerId == legacyPlayerId);
    }

    public CharacterProgressSave CreateDefaultProgress(string userId)
    {
        CharacterProgressSave save = new CharacterProgressSave
        {
            userId = userId
        };

        foreach (CharacterPreset preset in presets)
        {
            if (preset == null)
            {
                continue;
            }

            save.characters.Add(new PlayerCharacterProgress
            {
                characterId = preset.CharacterId,
                legacyPlayerId = preset.LegacyPlayerId,
                unlocked = preset.UnlockedByDefault,
                experience = 0,
                level = 0,
                upgrades = new CharacterUpgradeLevels()
            });
        }

        return save;
    }
}
