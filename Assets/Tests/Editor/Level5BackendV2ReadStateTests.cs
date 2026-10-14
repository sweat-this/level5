using System;
using System.IO;
using Level5.BackendV2;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    public class Level5BackendV2ReadStateTests
    {
        private string directory;
        private Guid player;
        private string key;
        [SetUp] public void SetUp()
        { directory = Path.Combine(Path.GetTempPath(), "level5-read-state-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); player = Guid.NewGuid(); key = "challenge:" + Guid.NewGuid().ToString("N"); }
        [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        private string PathFor(Guid id) => Path.Combine(directory, "backendv2-correspondence-read-" + id.ToString("N") + ".json");
        [Test] public void FreshInstallIsUnreadAndRenderingDoesNotWrite()
        { var state = new CorrespondenceReadState(player, directory); Assert.That(state.IsUnread(key), Is.True); Assert.That(state.IsUnread(null), Is.False); Assert.That(Directory.GetFiles(directory), Is.Empty); }
        [Test] public void ExplicitAcknowledgementSurvivesNewInstance()
        { var state = new CorrespondenceReadState(player, directory); Assert.That(state.Acknowledge(key), Is.True); Assert.That(new CorrespondenceReadState(player, directory).IsUnread(key), Is.False); }
        [Test] public void RepeatedAcknowledgementDoesNotWriteAgain()
        { var state = new CorrespondenceReadState(player, directory); state.Acknowledge(key); string before = File.ReadAllText(PathFor(player)); state.Acknowledge(key); Assert.That(File.ReadAllText(PathFor(player)), Is.EqualTo(before)); }
        [Test] public void NextGameHasNewAttentionButRevisionsDoNot()
        { string series = Guid.NewGuid().ToString("N"); var state = new CorrespondenceReadState(player, directory); state.Acknowledge("turn:" + series + ":1"); Assert.That(state.IsUnread("turn:" + series + ":2"), Is.True); Assert.That(state.IsUnread("turn:" + series + ":1"), Is.False); }
        [Test] public void AccountsRemainIsolatedWhenSwitchingAndReturning()
        { var a = new CorrespondenceReadState(player, directory); a.Acknowledge(key); var b = new CorrespondenceReadState(Guid.NewGuid(), directory); Assert.That(b.IsUnread(key), Is.True); Assert.That(new CorrespondenceReadState(player, directory).IsUnread(key), Is.False); }
        [Test] public void CorruptPrimaryRecoversValidatedBackup()
        { var state = new CorrespondenceReadState(player, directory); state.Acknowledge(key); File.WriteAllText(PathFor(player), "broken JSON"); Assert.That(new CorrespondenceReadState(player, directory).IsUnread(key), Is.False); }
        [Test] public void CorruptBothFilesStartUnreadAndCanBeRepaired()
        { File.WriteAllText(PathFor(player), "{}"); File.WriteAllText(PathFor(player) + ".bak", "broken"); var state = new CorrespondenceReadState(player, directory); Assert.That(state.IsUnread(key), Is.True); Assert.That(state.Acknowledge(key), Is.True); Assert.That(new CorrespondenceReadState(player, directory).IsUnread(key), Is.False); }
        [Test] public void FileWithWrongOwnerIsRejected()
        { new CorrespondenceReadState(player, directory).Acknowledge(key); Guid other = Guid.NewGuid(); File.Copy(PathFor(player), PathFor(other)); Assert.That(new CorrespondenceReadState(other, directory).IsUnread(key), Is.True); }
        [Test] public void StorageContainsOnlyAcknowledgementMetadata()
        { new CorrespondenceReadState(player, directory).Acknowledge(key); string json = File.ReadAllText(PathFor(player)); Assert.That(json, Does.Contain(key)); Assert.That(json, Does.Not.Contain("Token")); Assert.That(json, Does.Not.Contain("Result")); Assert.That(json, Does.Not.Contain("Opponent")); }
        [TestCase("turn:bad:1")] [TestCase("revision:123")] [TestCase("challenge:00000000000000000000000000000000")]
        public void InvalidAttentionIdentitiesAreNotPersisted(string invalid)
        { Assert.That(new CorrespondenceReadState(player, directory).Acknowledge(invalid), Is.False); Assert.That(Directory.GetFiles(directory), Is.Empty); }
        [Test] public void FailedWriteKeepsUnread()
        { string blocked = Path.Combine(directory, "file"); File.WriteAllText(blocked, "x"); var state = new CorrespondenceReadState(player, blocked); Assert.That(state.Acknowledge(key), Is.False); Assert.That(state.IsUnread(key), Is.True); Assert.That(state.ErrorMessage, Is.Not.Null); }
    }
}
