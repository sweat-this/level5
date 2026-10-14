using System;
using System.IO;
using NUnit.Framework;
using Settings = Level5.Core.PlayerSettings;

public class PlayerSettingsPersistenceTests
{
    private string directory;
    private string path;
    private PlayerSettingsStore store;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "level5-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        path = Path.Combine(directory, PlayerSettingsStore.FileName);
        store = new PlayerSettingsStore(path);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(directory, true);

    [Test]
    public void MissingFileReturnsFullGainWithoutWriting()
    {
        AssertSettings(store.Load(), 1f, 1f, 1f);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [Test]
    public void FreshStoreRestoresSavedValues()
    {
        Assert.That(store.TrySave(new Settings(0.25f, 0.5f, 0f), out string error), Is.True, error);
        AssertSettings(new PlayerSettingsStore(path).Load(), 0.25f, 0.5f, 0f);
        Assert.That(File.Exists(path + ".bak"), Is.True);
        Assert.That(File.Exists(path + ".tmp"), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingOrCorruptPrimaryRestoresValidBackup(bool corrupt)
    {
        store.TrySave(new Settings(0.2f, 0.3f, 0.4f), out _);
        if (corrupt) File.WriteAllText(path, "broken"); else File.Delete(path);
        AssertSettings(store.Load(), 0.2f, 0.3f, 0.4f);
    }

    [Test]
    public void SaveAfterRecoveryDoesNotRotateCorruptionIntoBackup()
    {
        store.TrySave(new Settings(0.2f, 0.3f, 0.4f), out _);
        File.WriteAllText(path, "broken");
        AssertSettings(store.Load(), 0.2f, 0.3f, 0.4f);
        Assert.That(store.TrySave(new Settings(0.6f, 0.7f, 0.8f), out _), Is.True);
        File.WriteAllText(path, "broken again");
        AssertSettings(store.Load(), 0.6f, 0.7f, 0.8f);
    }

    [Test]
    public void SaveAfterBothFilesAreCorruptCreatesAValidPrimaryAndBackup()
    {
        File.WriteAllText(path, "broken");
        File.WriteAllText(path + ".bak", "broken");
        Assert.That(store.TrySave(new Settings(0.6f, 0.7f, 0.8f), out _), Is.True);
        File.Delete(path);
        AssertSettings(store.Load(), 0.6f, 0.7f, 0.8f);
    }

    [TestCase("broken")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{}")]
    [TestCase("{\"version\":2,\"master\":0,\"music\":0,\"sfx\":0}")]
    [TestCase("{\"version\":1,\"master\":0,\"music\":0}")]
    [TestCase("{\"version\":1,\"master\":\"0.5\",\"music\":0,\"sfx\":0}")]
    [TestCase("{\"version\":1,\"master\":NaN,\"music\":0,\"sfx\":0}")]
    [TestCase("{\"version\":1,\"master\":Infinity,\"music\":0,\"sfx\":0}")]
    [TestCase("{\"version\":1,\"master\":1e100,\"music\":0,\"sfx\":0}")]
    [TestCase("{\"version\":1,\"master\":-0.1,\"music\":0,\"sfx\":0}")]
    [TestCase("{\"version\":1,\"master\":0,\"master\":1,\"music\":0,\"sfx\":0}")]
    public void MalformedOrUnsupportedDocumentReturnsDeterministicDefaults(string json)
    {
        File.WriteAllText(path, json);
        File.WriteAllText(path + ".bak", json);
        AssertSettings(store.Load(), 1f, 1f, 1f);
        Assert.That(File.ReadAllText(path), Is.EqualTo(json), "Load must not overwrite invalid/future data");
    }

    [Test]
    public void RuntimeUpdatesNormalizeNonfiniteAndOutOfRangeValues()
    {
        AssertSettings(new Settings(float.NaN, -1f, 2f), 1f, 0f, 1f);
        AssertSettings(new Settings(float.PositiveInfinity, float.NegativeInfinity, 0.5f), 1f, 1f, 0.5f);
    }

    [Test]
    public void FailedWriteReportsFailureAndKeepsExistingValidFamily()
    {
        store.TrySave(new Settings(0.2f, 0.3f, 0.4f), out _);
        Directory.CreateDirectory(path + ".tmp");
        Assert.That(store.TrySave(new Settings(0f, 0f, 0f), out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
        AssertSettings(store.Load(), 0.2f, 0.3f, 0.4f);
        File.Delete(path);
        AssertSettings(store.Load(), 0.2f, 0.3f, 0.4f);
    }

    private static void AssertSettings(Settings settings, float master, float music, float sfx)
    {
        Assert.That(settings.Master, Is.EqualTo(master));
        Assert.That(settings.Music, Is.EqualTo(music));
        Assert.That(settings.Sfx, Is.EqualTo(sfx));
    }
}
