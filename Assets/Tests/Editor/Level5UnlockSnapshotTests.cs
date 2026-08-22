using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Level5.Core.Match;
using Level5.Core.Progression;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// <see cref="UnlockSnapshot"/> is the pure, immutable answer; <see cref="UnlockSnapshotBuilder"/>
/// is the one place that resolves it from live account data. The precedence tests below are the
/// regression coverage for docs/persistence-boundaries.md: SQLite-backed data must win over the
/// JSON fallback whenever both exist, and JSON must only ever fill in what SQLite does not know
/// about - never override it.
/// </summary>
public class Level5UnlockSnapshotTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<string> writtenAccountIds = new List<string>();
    private string originalUserName;
    private int originalUserId;

    [SetUp]
    public void SetUp()
    {
        originalUserName = GameOptions.userName;
        originalUserId = GameOptions.userid;
    }

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

        foreach (string accountId in writtenAccountIds)
        {
            DeleteAccountFile(accountId);
        }

        writtenAccountIds.Clear();

        GameOptions.userName = originalUserName;
        GameOptions.userid = originalUserId;
    }

    private static void DeleteAccountFile(string accountId)
    {
        string path = CharacterProgressStore.GetAccountProgressPath(accountId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        string backup = path + ".bak";
        if (File.Exists(backup))
        {
            File.Delete(backup);
        }
    }

    private CharacterProfile MakeProfile(int playerId, bool locked)
    {
        GameObject go = new GameObject("profile_" + playerId);
        spawned.Add(go);
        CharacterProfile profile = go.AddComponent<CharacterProfile>();
        profile.PlayerId = playerId;

        PropertyInfo property = typeof(CharacterProfile).GetProperty("IsLocked");
        property.SetValue(profile, locked);

        return profile;
    }

    /// <summary>
    /// Points CharacterProgressAccountId.GetCurrent() at a fresh, disposable account id and writes
    /// a JSON progress save under it. Restored/cleaned up in TearDown.
    /// </summary>
    private void SeedJsonAccount(int legacyPlayerId, bool unlocked)
    {
        string accountId = "unlock-snapshot-test-" + legacyPlayerId;
        GameOptions.userid = 0;
        GameOptions.userName = accountId;
        writtenAccountIds.Add(accountId);

        CharacterProgressStore.Save(new CharacterProgressSave
        {
            userId = accountId,
            characters = new List<PlayerCharacterProgress>
            {
                new PlayerCharacterProgress
                {
                    characterId = "legacy-" + legacyPlayerId,
                    legacyPlayerId = legacyPlayerId,
                    unlocked = unlocked
                }
            }
        });
    }

    // ---- UnlockSnapshot: pure -----------------------------------------------------------------

    [Test]
    public void AnIdTheSnapshotWasNotBuiltWithDefaultsToLocked()
    {
        Assert.That(UnlockSnapshot.Empty.IsCharacterUnlocked(1), Is.False);
        Assert.That(UnlockSnapshot.Empty.IsLevelUnlocked(1), Is.False);
    }

    [Test]
    public void SnapshotAnswersExactlyWhatItWasBuiltWith()
    {
        UnlockSnapshot snapshot = new UnlockSnapshot(
            new Dictionary<int, bool> { { 1, true }, { 2, false } },
            new Dictionary<int, bool> { { 10, true }, { 11, false } });

        Assert.That(snapshot.IsCharacterUnlocked(1), Is.True);
        Assert.That(snapshot.IsCharacterUnlocked(2), Is.False);
        Assert.That(snapshot.IsLevelUnlocked(10), Is.True);
        Assert.That(snapshot.IsLevelUnlocked(11), Is.False);
    }

    // ---- UnlockSnapshotBuilder: levels come from the authored Locked flag ---------------------

    [Test]
    public void LevelUnlockComesFromTheAuthoredLockedFlag()
    {
        GameOptions.userid = 0;
        GameOptions.userName = "unlock-snapshot-test-levels-a";
        LevelDefinitionCatalog levels = new LevelDefinitionCatalog(new[]
        {
            TestDefinitions.Level(1, locked: false),
            TestDefinitions.Level(2, locked: true)
        });

        UnlockSnapshot snapshot = UnlockSnapshotBuilder.Build(new List<CharacterProfile>(), new List<CharacterProfile>(), levels);

        Assert.That(snapshot.IsLevelUnlocked(1), Is.True);
        Assert.That(snapshot.IsLevelUnlocked(2), Is.False);
    }

    [Test]
    public void ALevelTheCatalogDoesNotContainDefaultsToLocked()
    {
        GameOptions.userid = 0;
        GameOptions.userName = "unlock-snapshot-test-levels-b";
        UnlockSnapshot snapshot = UnlockSnapshotBuilder.Build(
            new List<CharacterProfile>(), new List<CharacterProfile>(), LevelDefinitionCatalog.Empty());

        Assert.That(snapshot.IsLevelUnlocked(1), Is.False);
    }

    // ---- UnlockSnapshotBuilder: character precedence (SQLite first, JSON fallback only) -------

    [Test]
    public void SqliteBackedProfileWinsWhenJsonDisagreesUnlocked()
    {
        SeedJsonAccount(legacyPlayerId: 501, unlocked: false);
        CharacterProfile profile = MakeProfile(501, locked: false); // SQLite: unlocked

        UnlockSnapshot snapshot = UnlockSnapshotBuilder.Build(new[] { profile }, new List<CharacterProfile>(), LevelDefinitionCatalog.Empty());

        Assert.That(snapshot.IsCharacterUnlocked(501), Is.True, "SQLite said unlocked; JSON disagreeing must not win");
    }

    [Test]
    public void SqliteBackedProfileWinsWhenJsonDisagreesLocked()
    {
        SeedJsonAccount(legacyPlayerId: 502, unlocked: true);
        CharacterProfile profile = MakeProfile(502, locked: true); // SQLite: locked

        UnlockSnapshot snapshot = UnlockSnapshotBuilder.Build(new[] { profile }, new List<CharacterProfile>(), LevelDefinitionCatalog.Empty());

        Assert.That(snapshot.IsCharacterUnlocked(502), Is.False, "SQLite said locked; JSON disagreeing must not win");
    }

    [Test]
    public void JsonIsUsedOnlyWhenSqliteHasNoAnswer()
    {
        SeedJsonAccount(legacyPlayerId: 503, unlocked: true);

        // No CharacterProfile at all for id 503 - SQLite-backed data has nothing to say.
        UnlockSnapshot snapshot = UnlockSnapshotBuilder.Build(new List<CharacterProfile>(), new List<CharacterProfile>(), LevelDefinitionCatalog.Empty());

        Assert.That(snapshot.IsCharacterUnlocked(503), Is.True, "with no SQLite answer, the JSON fallback should be used");
    }

    [Test]
    public void MissingFromBothSourcesDefaultsToLocked()
    {
        // An account id with deliberately no JSON file behind it, so this does not depend on
        // whatever "guest" happens to look like in the environment running the test.
        GameOptions.userid = 0;
        GameOptions.userName = "unlock-snapshot-test-nothing-seeded";

        UnlockSnapshot snapshot = UnlockSnapshotBuilder.Build(new List<CharacterProfile>(), new List<CharacterProfile>(), LevelDefinitionCatalog.Empty());

        Assert.That(snapshot.IsCharacterUnlocked(999999), Is.False);
    }
}
