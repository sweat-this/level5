using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Progression;

/// <summary>
/// Builds an <see cref="UnlockSnapshot"/> from the current account's SQLite-backed
/// <see cref="CharacterProfile"/> lists the menu already loaded. A character absent from both the
/// primary and CPU rosters defaults locked - see docs/persistence-boundaries.md.
///
/// This replaces the old <c>UnlockService</c>, which answered the same question but recomputed it
/// (including a filesystem read) on every single call instead of once per refresh, and had no
/// production caller.
///
/// <paramref name="cpuProfiles"/> never overrides an id <paramref name="primaryProfiles"/> already
/// answered. <c>LoadManager.loadCpuSelectDataList</c> never sets <c>CharacterProfile.IsLocked</c>
/// from SQLite the way it does for the primary roster (`loadPlayerSelectDataList`), so a CPU-list
/// profile's lock flag defaults to false regardless of the account's real progress. Character ids
/// commonly appear in both rosters (the same character can be your primary pick and a CPU
/// opponent), so letting the CPU pass win would silently report a locked character as unlocked.
///
/// Level unlock has no JSON-backed account entitlement yet (see issue #39): a level's authored
/// <see cref="LevelDefinition.Locked"/> flag is the only source until durable level progress is
/// introduced, which this deliberately does not do without established completion semantics to
/// build it from.
/// </summary>
public static class UnlockSnapshotBuilder
{
    public static UnlockSnapshot Build(
        IReadOnlyList<CharacterProfile> primaryProfiles,
        IReadOnlyList<CharacterProfile> cpuProfiles,
        LevelDefinitionCatalog levelCatalog)
    {
        Dictionary<int, bool> characters = new Dictionary<int, bool>();
        AddProfiles(characters, primaryProfiles, overwrite: true);
        AddProfiles(characters, cpuProfiles, overwrite: false);

        Dictionary<int, bool> levels = new Dictionary<int, bool>();
        if (levelCatalog != null)
        {
            foreach (LevelDefinition level in levelCatalog.Definitions)
            {
                if (level != null)
                {
                    levels[level.LevelId] = !level.Locked;
                }
            }
        }

        return new UnlockSnapshot(characters, levels);
    }

    private static void AddProfiles(Dictionary<int, bool> characters, IReadOnlyList<CharacterProfile> profiles, bool overwrite)
    {
        if (profiles == null)
        {
            return;
        }

        foreach (CharacterProfile profile in profiles)
        {
            if (profile == null)
            {
                continue;
            }

            if (!overwrite && characters.ContainsKey(profile.PlayerId))
            {
                continue;
            }

            characters[profile.PlayerId] = !profile.IsLocked;
        }
    }
}
