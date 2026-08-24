using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
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

    // ---- CPU selection/runtime parity (issue #69) ---------------------------------------------
    //
    // #69 started as "cpu_player_ak47 authors isCpu = false" but the audit that preceded this fix
    // found the menu/selection prefab (Assets/Resources/Prefabs/menu_start/cpu_players_selected_objects)
    // and the gameplay/runtime prefab SpawnCoordinator actually instantiates
    // (Assets/Resources/Prefabs/characters/cpu_players) had drifted apart on IsShooter, IsFighter,
    // CpuType and Level for more than a dozen characters - not just AK-47.
    //
    // The same audit also found the runtime catalog itself authors duplicate PlayerIds
    // (drblood/drblood_white both 1, kamille/thom both 4, flash/zilla_baby both 5, pony/woody both
    // 6) and rad_tony authoring the reserved "none" id 0. Deciding which side of those collisions
    // is correct is a separate identity-migration decision (AGENTS.md treats stable ids as a
    // contract), not something #69 resolves by guessing - so RequiredParityFieldsAgreeWithRuntimeWhereIdentityIsUnambiguous
    // below only enforces parity where PlayerId already agrees and is unique in the runtime
    // catalog. It starts covering the deferred characters automatically once that separate defect
    // is fixed.

    private const string RuntimeCpuRosterPath = "Assets/Resources/Prefabs/characters/cpu_players";

    private static Dictionary<string, CharacterProfile> LoadCpuRuntimeProfilesByObjectName()
    {
        Dictionary<string, CharacterProfile> byObjectName = new Dictionary<string, CharacterProfile>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RuntimeCpuRosterPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CharacterProfile profile = prefab != null ? prefab.GetComponentInChildren<CharacterProfile>(true) : null;
            if (profile != null)
            {
                byObjectName[profile.PlayerObjectName] = profile;
            }
        }

        return byObjectName;
    }

    private static List<CharacterProfile> LoadRealCpuSelectionProfiles()
    {
        List<CharacterProfile> profiles = new List<CharacterProfile>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CpuRosterPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CharacterProfile profile = prefab != null ? prefab.GetComponent<CharacterProfile>() : null;
            // id 0 is the legacy CPU "none" record - not a real character, excluded from parity.
            if (profile != null && profile.PlayerId != 0)
            {
                profiles.Add(profile);
            }
        }

        return profiles;
    }

    [Test]
    public void EveryRealCpuSelectionCharacterHasARuntimeGameplayCounterpart()
    {
        Dictionary<string, CharacterProfile> runtimeByObjectName = LoadCpuRuntimeProfilesByObjectName();

        foreach (CharacterProfile selection in LoadRealCpuSelectionProfiles())
        {
            Assert.That(
                runtimeByObjectName.ContainsKey(selection.PlayerObjectName),
                Is.True,
                $"'{selection.PlayerObjectName}' has a CPU selection prefab but no runtime prefab under "
                    + $"{RuntimeCpuRosterPath} - SpawnCoordinator resolves the gameplay actor by this ObjectName.");
        }
    }

    [Test]
    public void EveryRealCpuSelectionCharacterAuthorsIsCpuTrue()
    {
        foreach (CharacterProfile selection in LoadRealCpuSelectionProfiles())
        {
            Assert.That(
                selection.isCpu,
                Is.True,
                $"'{selection.PlayerObjectName}' is a real CPU selection entry but authors isCpu = false (issue #69).");
        }
    }

    [Test]
    public void RequiredParityFieldsAgreeWithRuntimeWhereIdentityIsUnambiguous()
    {
        Dictionary<string, CharacterProfile> runtimeByObjectName = LoadCpuRuntimeProfilesByObjectName();
        Dictionary<int, int> runtimeIdCounts = new Dictionary<int, int>();
        foreach (CharacterProfile runtime in runtimeByObjectName.Values)
        {
            runtimeIdCounts.TryGetValue(runtime.PlayerId, out int count);
            runtimeIdCounts[runtime.PlayerId] = count + 1;
        }

        foreach (CharacterProfile selection in LoadRealCpuSelectionProfiles())
        {
            if (!runtimeByObjectName.TryGetValue(selection.PlayerObjectName, out CharacterProfile runtime))
            {
                continue; // covered by EveryRealCpuSelectionCharacterHasARuntimeGameplayCounterpart
            }

            bool identityUnambiguous = selection.PlayerId == runtime.PlayerId && runtimeIdCounts[runtime.PlayerId] == 1;
            if (!identityUnambiguous)
            {
                continue;
            }

            string who = $"'{selection.PlayerObjectName}' (playerId {selection.PlayerId})";
            Assert.That(selection.IsShooter, Is.EqualTo(runtime.IsShooter), $"{who} IsShooter disagrees with its runtime prefab.");
            Assert.That(selection.IsFighter, Is.EqualTo(runtime.IsFighter), $"{who} IsFighter disagrees with its runtime prefab.");
            Assert.That(selection.CpuType, Is.EqualTo(runtime.CpuType), $"{who} CpuType disagrees with its runtime prefab.");
            Assert.That(selection.Level, Is.EqualTo(runtime.Level), $"{who} Level disagrees with its runtime prefab.");
        }
    }

    // ---- primary vs CPU projection (issue #69) -------------------------------------------------
    //
    // PlayerSelectCatalogAdapter used to run every profile - primary and CPU - through the same
    // projection, which recalculated Level from Experience and wrote back an effective Clutch.
    // That is correct for a primary human profile (Level tracks XP), but wrong for a CPU profile:
    // a CPU's Level is authored AI tuning that feeds CharacterProfile.calculateAccuracyAttributeRatings,
    // and its Clutch is already resolved by CharacterProfile.InitializeCpuBaselineStats before
    // selection ever sees it. A CPU authored at Level 40 / Experience 0 (e.g. cpu_player_ak47) was
    // rendering as Level 0 in the CPU slot.

    [Test]
    public void CpuProjectionDoesNotDeriveLevelFromExperience()
    {
        CharacterProfile cpu = MakeProfile(7, "AK-47", "ak47", experience: 0, isShooter: true);
        cpu.Level = 40;

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new List<CharacterProfile>(), new[] { cpu }, Unlock(7));

        Assert.That(
            catalog.FindCpu(7).Stats.Level,
            Is.EqualTo(40),
            "a CPU's authored Level is AI tuning, not XP progress - it must not be recalculated from Experience");
    }

    [Test]
    public void CpuProjectionDoesNotOverwriteClutchAlreadyResolvedByCpuInitialization()
    {
        CharacterProfile cpu = MakeProfile(7, "AK-47", "ak47", experience: 0, isShooter: true);
        cpu.Level = 40;
        cpu.Clutch = 40; // as CharacterProfile.InitializeCpuBaselineStats would already have set it

        PlayerSelectCatalogAdapter.Project(new List<CharacterProfile>(), new[] { cpu }, Unlock(7));

        Assert.That(
            cpu.Clutch,
            Is.EqualTo(40),
            "CPU projection must not rewrite Clutch that CPU initialization already resolved");
    }

    [Test]
    public void PrimaryProjectionStillDerivesLevelFromExperienceForAFortyLevelProfile()
    {
        CharacterProfile primary = MakeProfile(1, "Hero", "hero_obj", experience: CharacterLevel.ExperiencePerLevel * 40);

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(new[] { primary }, new List<CharacterProfile>(), Unlock(1));

        Assert.That(catalog.FindPrimary(1).Stats.Level, Is.EqualTo(40));
    }

    // ---- AK-47 roster validation (issue #69) ---------------------------------------------------

    [Test]
    public void AK47ProjectsIntoAShootingCpuSlotThatPassesRosterValidation()
    {
        string path = CpuRosterPath + "/cpu_player_ak47.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, $"cpu_player_ak47 selection prefab not found at {path}");
        CharacterProfile ak47 = prefab.GetComponent<CharacterProfile>();

        PlayerSelectCatalog catalog = PlayerSelectCatalogAdapter.Project(
            new List<CharacterProfile>(), new[] { ak47 }, Unlock(ak47.PlayerId));
        CharacterSelectOption cpuOption = catalog.FindCpu(ak47.PlayerId);
        Assert.That(cpuOption, Is.Not.Null);
        Assert.That(cpuOption.IsShooter, Is.True, "AK-47's CPU selection entry must be a shooter (issue #69)");

        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        GameModeCompatibility compatibility = new GameModeCompatibility(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { TestDefinitions.Level(1) }));

        PlayerRoster roster = PlayerRoster.Build(new[]
        {
            PlayerRosterEntry.LocalHuman(TestDefinitions.Character("hero", isShooter: true, isFighter: false)),
            PlayerRosterEntry.Cpu(cpuOption.ToSelection()),
        });

        ValidationResult verdict = compatibility.Validate(new MatchRequest(mode.Id, 1, roster));

        Assert.That(
            verdict.HasError(MatchValidationCode.CharacterCannotShoot),
            Is.False,
            "a real AK-47 CPU roster must pass shooting-mode validation now that its capability metadata matches its runtime archetype");
    }
}
