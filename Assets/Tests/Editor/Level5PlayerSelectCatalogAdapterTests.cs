using System.Collections.Generic;
using System.Reflection;
using Level5.Core.PlayerSelection;
using Level5.Core.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit-mode tests for <see cref="PlayerSelectCatalogAdapter"/>: the projection from live
/// <see cref="CharacterProfile"/> data into the read-only selection catalog. Profiles are plain
/// <see cref="MonoBehaviour"/> instances created in memory - none of this touches persistence or a
/// loaded scene.
/// </summary>
public class Level5PlayerSelectCatalogAdapterTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private CharacterProfile MakeProfile(
        int playerId,
        string displayName,
        string objectName,
        int experience = 0,
        bool isShooter = true,
        bool isFighter = false,
        bool locked = false)
    {
        GameObject go = new GameObject("profile_" + playerId);
        spawned.Add(go);
        CharacterProfile profile = go.AddComponent<CharacterProfile>();
        profile.PlayerId = playerId;
        profile.PlayerDisplayName = displayName;
        profile.PlayerObjectName = objectName;
        profile.Experience = experience;
        profile.IsShooter = isShooter;
        profile.IsFighter = isFighter;

        // IsLocked has an internal setter (see CharacterProfile.cs); reflection is the only way to
        // drive it from an Editor-assembly test without loosening production accessibility for it.
        PropertyInfo property = typeof(CharacterProfile).GetProperty("IsLocked");
        property.SetValue(profile, locked);

        return profile;
    }

    /// <summary>An unlock snapshot that answers unlocked for exactly the given character ids.</summary>
    private static UnlockSnapshot Unlock(params int[] unlockedCharacterIds)
    {
        Dictionary<int, bool> characters = new Dictionary<int, bool>();
        foreach (int id in unlockedCharacterIds)
        {
            characters[id] = true;
        }

        return new UnlockSnapshot(characters, new Dictionary<int, bool>());
    }

    [Test]
    public void ProjectsProfileIdentityAndCapabilityIntoTheOption()
    {
        CharacterProfile profile = MakeProfile(7, "Hero", "hero_obj", isShooter: true, isFighter: true);

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new[] { profile }, new List<CharacterProfile>(), Unlock(7));

        Assert.That(catalog.PrimaryOptions.Count, Is.EqualTo(1));
        CharacterSelectOption option = catalog.PrimaryOptions[0];
        Assert.That(option.CharacterId, Is.EqualTo(7));
        Assert.That(option.DisplayName, Is.EqualTo("Hero"));
        Assert.That(option.ObjectName, Is.EqualTo("hero_obj"));
        Assert.That(option.IsShooter, Is.True);
        Assert.That(option.IsFighter, Is.True);
    }

    [Test]
    public void ASnapshotThatDoesNotListTheCharacterProjectsAsLocked()
    {
        CharacterProfile profile = MakeProfile(3, "Unknown To Snapshot", "unknown_obj");

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new[] { profile }, new List<CharacterProfile>(), Unlock());

        Assert.That(catalog.PrimaryOptions[0].IsUnlocked, Is.False);
    }

    [Test]
    public void TheSnapshotIsTheOnlyUnlockAuthorityNotCharacterProfileIsLocked()
    {
        // profile.IsLocked disagrees with the snapshot in both directions. If the adapter still
        // read CharacterProfile.IsLocked anywhere, at least one of these would fail - the boundary
        // this test protects is that PlayerSelectCatalogAdapter no longer decides unlock itself.
        CharacterProfile lockedInDbButUnlockedInSnapshot = MakeProfile(1, "A", "a_obj", locked: true);
        CharacterProfile unlockedInDbButLockedInSnapshot = MakeProfile(2, "B", "b_obj", locked: false);

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(
            new[] { lockedInDbButUnlockedInSnapshot, unlockedInDbButLockedInSnapshot },
            new List<CharacterProfile>(),
            Unlock(1));

        Assert.That(catalog.FindPrimary(1).IsUnlocked, Is.True, "the snapshot said 1 is unlocked");
        Assert.That(catalog.FindPrimary(2).IsUnlocked, Is.False, "the snapshot did not list 2 as unlocked");
    }

    [Test]
    public void EffectiveClutchMatchesTheExistingMinLevelOneHundredRule()
    {
        CharacterProfile under = MakeProfile(1, "Under", "under_obj", experience: CharacterLevel.ExperiencePerLevel * 40);
        CharacterProfile over = MakeProfile(2, "Over", "over_obj", experience: CharacterLevel.ExperiencePerLevel * 150);

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new[] { under, over }, new List<CharacterProfile>(), Unlock(1, 2));

        CharacterSelectOption underOption = catalog.FindPrimary(1);
        CharacterSelectOption overOption = catalog.FindPrimary(2);

        Assert.That(underOption.Stats.EffectiveClutch, Is.EqualTo(underOption.Stats.Level));
        Assert.That(overOption.Stats.EffectiveClutch, Is.EqualTo(100));
    }

    [Test]
    public void ProjectionDoesNotLeaveClutchUnsetForGameplayToReadLater()
    {
        // Gameplay reads CharacterProfile.Clutch directly at match launch
        // (CharacterProfile.intializeShooterStatsFromProfile). The old view wrote the effective
        // value there on every render; the adapter now does it once, at projection time.
        CharacterProfile profile = MakeProfile(1, "Hero", "hero_obj", experience: CharacterLevel.ExperiencePerLevel * 150);

        PlayerSelectCatalogAdapter.Project(new[] { profile }, new List<CharacterProfile>(), Unlock(1));

        Assert.That(profile.Clutch, Is.EqualTo(100));
    }

    [Test]
    public void PortraitsAreExposedSeparatelyFromTheSelectionOption()
    {
        CharacterProfile profile = MakeProfile(1, "Hero", "hero_obj");
        Sprite portrait = Sprite.Create(new Texture2D(1, 1), new Rect(0, 0, 1, 1), Vector2.zero);
        profile.PlayerPortrait = portrait;

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new[] { profile }, new List<CharacterProfile>(), Unlock(1));

        Assert.That(catalog.VisualsFor(1).Portrait, Is.EqualTo(portrait));
        Object.DestroyImmediate(portrait);
    }

    [Test]
    public void LegacyCpuNoneIsNotInTheSelectableCpuCatalog()
    {
        CharacterProfile none = MakeProfile(0, "none", "none_obj");
        CharacterProfile real = MakeProfile(1, "Real Cpu", "real_obj");

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new List<CharacterProfile>(), new[] { none, real }, Unlock(1));

        Assert.That(catalog.CpuOptions.Count, Is.EqualTo(1));
        Assert.That(catalog.CpuOptions[0].CharacterId, Is.EqualTo(1));
        Assert.That(catalog.CpuNoneDisplayName, Is.EqualTo("none"));
    }

    [Test]
    public void CatalogOrderMatchesTheSourceProfileListOrder()
    {
        CharacterProfile a = MakeProfile(3, "C", "c_obj");
        CharacterProfile b = MakeProfile(1, "A", "a_obj");
        CharacterProfile c = MakeProfile(2, "B", "b_obj");

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new[] { a, b, c }, new List<CharacterProfile>(), Unlock(1, 2, 3));

        Assert.That(catalog.PrimaryOptions[0].CharacterId, Is.EqualTo(3));
        Assert.That(catalog.PrimaryOptions[1].CharacterId, Is.EqualTo(1));
        Assert.That(catalog.PrimaryOptions[2].CharacterId, Is.EqualTo(2));
    }

    // ---- authored roster invariants (found while auditing #56's V2 setup) --------------------
    //
    // Found live: cpu_player_kamille and cpu_player_thom both authored playerId 4, and
    // cpu_player_zilla and cpu_player_woody both authored playerId 6. PlayerSelectCatalogAdapter's
    // visuals dictionary (`if (!visuals.ContainsKey(...))`) silently keeps only the first one added,
    // so the second character in load order rendered the first one's portrait in its CPU slot -
    // and PlayerSelectionController.RestoreCpuSlot/CharacterSelectOptions.Find, both keyed on this
    // same id, could resolve a remembered session slot to the wrong character. Separately,
    // cpu_player_johnny_dracula (id 3) and cpu_player_pony (id 8) didn't collide with anything in
    // the CPU roster but didn't match their own primary-roster ids (15, 31) either - harmless today
    // only because UnlockSnapshotBuilder lets the primary roster win whenever it already answered
    // for an id, but still wrong authored data for a character that is also primary-selectable.
    //
    // These tests read the real authored prefabs rather than synthetic profiles (matching
    // Level5AutonomousActorTests.TheLockdownDefenderOutrunsEveryAuthoredPlayerSpeed's approach) so a
    // future authoring mistake of this exact shape is caught without needing a Play Mode session.

    private const string PrimaryRosterPath = "Assets/Resources/Prefabs/menu_start/player_selected_objects";
    private const string CpuRosterPath = "Assets/Resources/Prefabs/menu_start/cpu_players_selected_objects";

    private static Dictionary<string, int> LoadRosterIdsByObjectName(string folder)
    {
        Dictionary<string, int> idsByObjectName = new Dictionary<string, int>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CharacterProfile profile = prefab != null ? prefab.GetComponent<CharacterProfile>() : null;
            if (profile == null || profile.PlayerId == 0)
            {
                // id 0 is the legacy CPU "none" record - not a real character.
                continue;
            }

            idsByObjectName[profile.PlayerObjectName] = profile.PlayerId;
        }

        return idsByObjectName;
    }

    [Test]
    public void NoTwoAuthoredCpuSelectCharactersShareAPlayerId()
    {
        Dictionary<string, int> cpuIds = LoadRosterIdsByObjectName(CpuRosterPath);

        Dictionary<int, string> seen = new Dictionary<int, string>();
        foreach (KeyValuePair<string, int> entry in cpuIds)
        {
            if (seen.TryGetValue(entry.Value, out string existingObjectName))
            {
                Assert.Fail(
                    $"cpu_players_selected_objects has two characters sharing playerId {entry.Value}: "
                        + $"'{existingObjectName}' and '{entry.Key}' - the second one's portrait/session-restore "
                        + "loses to whichever the adapter/controller saw first.");
            }

            seen[entry.Value] = entry.Key;
        }
    }

    [Test]
    public void AnAuthoredCpuSelectCharacterThatIsAlsoPrimarySelectableAgreesOnItsPlayerId()
    {
        Dictionary<string, int> primaryIds = LoadRosterIdsByObjectName(PrimaryRosterPath);
        Dictionary<string, int> cpuIds = LoadRosterIdsByObjectName(CpuRosterPath);

        foreach (KeyValuePair<string, int> cpuEntry in cpuIds)
        {
            if (primaryIds.TryGetValue(cpuEntry.Key, out int primaryId))
            {
                Assert.That(
                    cpuEntry.Value,
                    Is.EqualTo(primaryId),
                    $"'{cpuEntry.Key}' is playerId {primaryId} in the primary roster but "
                        + $"{cpuEntry.Value} in the CPU roster - the two must agree.");
            }
        }
    }
}
