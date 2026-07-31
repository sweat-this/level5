using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Level5/Character Preset Catalog", fileName = "CharacterPresetCatalog")]
public class CharacterPresetCatalog : ScriptableObject
{
    [SerializeField] private List<CharacterPreset> presets = new List<CharacterPreset>();

    public IReadOnlyList<CharacterPreset> Presets => presets;

    public bool Validate(List<string> issues)
    {
        bool valid = true;
        HashSet<string> characterIds = new HashSet<string>();
        HashSet<int> legacyIds = new HashSet<int>();

        for (int i = 0; i < presets.Count; i++)
        {
            CharacterPreset preset = presets[i];
            if (preset == null)
            {
                issues?.Add(name + " has an empty preset slot at index " + i + ".");
                valid = false;
                continue;
            }

            valid &= preset.Validate(issues);

            if (!string.IsNullOrEmpty(preset.CharacterId) && !characterIds.Add(preset.CharacterId))
            {
                issues?.Add(name + " has duplicate characterId " + preset.CharacterId + ".");
                valid = false;
            }

            if (preset.LegacyPlayerId > 0 && !legacyIds.Add(preset.LegacyPlayerId))
            {
                issues?.Add(name + " has duplicate legacyPlayerId " + preset.LegacyPlayerId + ".");
                valid = false;
            }
        }

        return valid;
    }

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
