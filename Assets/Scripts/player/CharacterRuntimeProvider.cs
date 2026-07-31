using System.Collections.Generic;
using UnityEngine;

public class CharacterRuntimeProvider : MonoBehaviour
{
    [SerializeField] private CharacterPresetCatalog catalog;
    [SerializeField] private bool logDataSource;

    private bool catalogValidationCached;
    private bool catalogIsValid;
    private bool catalogValidationIssuesLogged;
    private List<string> catalogValidationIssues = new List<string>();
    private string cachedAccountId;
    private CharacterProgressSave cachedProgressSave;

    public void RefreshCache()
    {
        catalogValidationCached = false;
        catalogIsValid = false;
        catalogValidationIssuesLogged = false;
        catalogValidationIssues.Clear();
        cachedAccountId = null;
        cachedProgressSave = null;
    }

    public bool TryBuildRuntimeStats(CharacterProfile legacyProfile, out RuntimeCharacterStats runtimeStats)
    {
        runtimeStats = null;

        if (legacyProfile == null)
        {
            return false;
        }

        if (catalog == null)
        {
            LogSource(legacyProfile, "legacy-db", "missing catalog");
            return false;
        }

        if (!ValidateCatalog())
        {
            if (!catalogValidationIssuesLogged)
            {
                catalogValidationIssuesLogged = true;
                foreach (string issue in catalogValidationIssues)
                {
                    Debug.LogError(issue);
                }
            }

            LogSource(legacyProfile, "legacy-db", "invalid catalog");
            return false;
        }

        CharacterPreset preset = catalog.FindByLegacyPlayerId(legacyProfile.PlayerId);
        if (preset == null)
        {
            LogSource(legacyProfile, "legacy-db", "missing preset");
            return false;
        }

        if (!TryGetProgressSave(out CharacterProgressSave save))
        {
            LogSource(legacyProfile, "legacy-db", "missing progress save");
            return false;
        }

        PlayerCharacterProgress progress = save.characters.Find(item => item.characterId == preset.CharacterId);
        if (progress == null)
        {
            LogSource(legacyProfile, "legacy-db", "missing progress");
            return false;
        }

        runtimeStats = CharacterProgressResolver.BuildRuntimeStats(preset, progress);
        LogSource(legacyProfile, "preset-progress", "resolved");
        return true;
    }

    public bool TryApplyRuntimeStats(CharacterProfile profile)
    {
        if (!TryBuildRuntimeStats(profile, out RuntimeCharacterStats runtimeStats))
        {
            return false;
        }

        CharacterProfileStatMapper.Apply(profile, runtimeStats);
        return true;
    }

    private bool ValidateCatalog()
    {
        if (catalogValidationCached)
        {
            return catalogIsValid;
        }

        catalogValidationCached = true;
        catalogValidationIssues.Clear();
        catalogIsValid = catalog.Validate(catalogValidationIssues);
        return catalogIsValid;
    }

    private bool TryGetProgressSave(out CharacterProgressSave progressSave)
    {
        string accountId = CharacterProgressAccountId.GetCurrent();
        if (cachedProgressSave != null && cachedAccountId == accountId)
        {
            progressSave = cachedProgressSave;
            return true;
        }

        if (!CharacterProgressStore.TryLoadExisting(accountId, out CharacterProgressSave save))
        {
            cachedAccountId = accountId;
            cachedProgressSave = null;
            progressSave = null;
            return false;
        }

        cachedAccountId = accountId;
        cachedProgressSave = save;
        progressSave = cachedProgressSave;
        return progressSave != null;
    }

    private void LogSource(CharacterProfile profile, string source, string reason)
    {
        if (!logDataSource)
        {
            return;
        }

        string characterName = profile == null ? "unknown" : profile.PlayerDisplayName;
        Debug.Log("CharacterRuntimeProvider: " + characterName + " using " + source + " (" + reason + ").");
    }
}
