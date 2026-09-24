using System;
using System.Collections.Generic;
using Level5.BackendV2;
using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// <see cref="RemoteAttemptLauncher"/>: the missing "StartAttempt -&gt; mapper -&gt;
    /// ActiveMatch/ActiveRemoteAttempt/LegacyGameOptionsBridge/SceneTransition" sequence, mirroring
    /// <c>VersusLauncher.Launch</c>. The scene loader is overridden so the success path can run in
    /// EditMode without a real <c>SceneManager.LoadScene</c> call.
    /// </summary>
    public class Level5BackendV2RemoteAttemptLauncherTests
    {
        private const string RulesetIdValue = "most-points";

        [SetUp]
        public void SetUp()
        {
            GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
            LevelDefinition level = TestDefinitions.Level(4, objectName: "level_04_park", sceneDescriptor: "day");
            MatchCatalogs.Override(new GameModeCatalog(new[] { mode }), new LevelDefinitionCatalog(new[] { level }));

            CompetitiveRuleset ruleset = new CompetitiveRuleset(
                new RulesetId(RulesetIdValue),
                version: 1,
                modeId: GameModeId.TotalPoints,
                capabilities: VersusCapability.Asynchronous,
                comparisonKeys: new[] { ComparisonKey.Highest(AttemptMetric.Score) },
                minimumCompatibleVersion: 1,
                displayName: "Most Points");
            VersusCatalogs.Override(new CompetitiveRulesetCatalog(new[] { ruleset }));

            BackendV2SessionStore.Set(new BackendV2Session(
                "access-token", DateTimeOffset.UtcNow.AddHours(1), Guid.NewGuid(), "refresh-token",
                DateTimeOffset.UtcNow.AddDays(30)));
        }

        [TearDown]
        public void TearDown()
        {
            MatchCatalogs.Reset();
            VersusCatalogs.Reset();
            BackendV2SessionStore.Clear();
            BackendV2Runtime.Reset();
            RemoteAttemptLauncher.ResetSceneLoader();
            ActiveMatch.Clear();
            ActiveRemoteAttempt.Clear();
            PendingRemoteAttemptResult.Clear();
        }

        [Test]
        public void ARemoteAttemptResultStillPendingRefusesToStartAnotherAttempt()
        {
            // A previous attempt failed to submit and its result is still pending retry -
            // ActiveRemoteAttempt/PendingRemoteAttemptResult are single global slots, so starting a
            // second attempt here would silently overwrite (and lose) the first one's unretried
            // result.
            PendingRemoteAttemptResult.Stash(
                new RemoteAttemptContext(
                    Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), RulesetIdValue, 1, 1,
                    Array.Empty<ComparisonKeySummaryDto>(), new[] { "Score" }),
                new Dictionary<string, double> { ["Score"] = 1 });

            FakeApiTransport transport = new FakeApiTransport();
            BackendV2Runtime.Override(transport);

            RemoteAttemptLaunch result = default;
            CoroutineTestRunner.RunToCompletion(RemoteAttemptLauncher.Run(
                Guid.NewGuid(), 1, 4, CharacterSelection.None, null, launch => result = launch));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(transport.Requests, Is.Empty, "must fail before ever calling StartAttempt");
            Assert.That(ActiveRemoteAttempt.IsActive, Is.False);
        }

        [Test]
        public void AStartAttemptFailureNeverBeginsAMatch()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.NetworkError());
            BackendV2Runtime.Override(transport);

            RemoteAttemptLaunch result = default;
            bool completedCalled = false;
            CoroutineTestRunner.RunToCompletion(RemoteAttemptLauncher.Run(
                Guid.NewGuid(), 1, 4, CharacterSelection.None, null,
                launch => { result = launch; completedCalled = true; }));

            Assert.That(completedCalled, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(ActiveMatch.IsActive, Is.False, "a failed start must never begin a match");
            Assert.That(ActiveRemoteAttempt.IsActive, Is.False);
        }

        [Test]
        public void AMappingFailureSurfacesTheErrorAndNeverBeginsAMatch()
        {
            // The descriptor names a ruleset this test's catalog does not know about.
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.AttemptDescriptor.Replace(
                "\"most-points\"", "\"no-such-ruleset\"")));
            BackendV2Runtime.Override(transport);

            RemoteAttemptLaunch result = default;
            CoroutineTestRunner.RunToCompletion(RemoteAttemptLauncher.Run(
                Guid.NewGuid(), 1, 4, CharacterSelection.None, null, launch => result = launch));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("no-such-ruleset"));
            Assert.That(ActiveMatch.IsActive, Is.False);
            Assert.That(ActiveRemoteAttempt.IsActive, Is.False);
        }

        [Test]
        public void ASuccessfulDescriptorBuildsThroughTheMapperAndBeginsTheRemoteAttempt()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.AttemptDescriptor));
            BackendV2Runtime.Override(transport);

            List<string> loadedScenes = new List<string>();
            RemoteAttemptLauncher.OverrideSceneLoader(sceneName => loadedScenes.Add(sceneName));

            RemoteAttemptLaunch result = default;
            CoroutineTestRunner.RunToCompletion(RemoteAttemptLauncher.Run(
                Guid.NewGuid(), 1, 4, CharacterSelection.None, null, launch => result = launch));

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(ActiveMatch.IsActive, Is.True);
            Assert.That(ActiveRemoteAttempt.IsActive, Is.True);
            Assert.That(ActiveRemoteAttempt.Context.RulesetId, Is.EqualTo(RulesetIdValue));
            Assert.That(loadedScenes, Has.Count.EqualTo(1), "exactly one scene load for a successful launch");
        }

        /// <summary>Issue #179: a non-empty local character selection (as <see
        /// cref="RemoteCharacterSelectionResolver"/> now supplies, replacing the previous
        /// <c>CharacterSelection.None</c>) must reach slot zero of the resulting
        /// <see cref="MatchConfiguration"/> unchanged - the same slot <c>SpawnCoordinator</c>/
        /// <c>CharacterProfile</c> resolve the human player's profile from.</summary>
        [Test]
        public void ASuppliedCharacterReachesSlotZeroOfTheResultingMatchConfigurationUnchanged()
        {
            FakeApiTransport transport = new FakeApiTransport();
            transport.Enqueue(RawApiResponse.Completed(200, BackendV2Fixtures.AttemptDescriptor));
            BackendV2Runtime.Override(transport);
            RemoteAttemptLauncher.OverrideSceneLoader(_ => { });

            CharacterSelection character = new CharacterSelection(7, "obj7", "Character Seven", isShooter: true, isFighter: false);

            RemoteAttemptLaunch result = default;
            CoroutineTestRunner.RunToCompletion(RemoteAttemptLauncher.Run(
                Guid.NewGuid(), 1, 4, character, null, launch => result = launch));

            Assert.That(result.Succeeded, Is.True, result.Error);
            PlayerSlot slotZero = result.Configuration.Roster.GetBySlotId(0);
            Assert.That(slotZero, Is.Not.Null);
            Assert.That(slotZero.Character.CharacterId, Is.EqualTo(7));
            Assert.That(slotZero.Character.ObjectName, Is.EqualTo("obj7"));
            Assert.That(slotZero.Character.DisplayName, Is.EqualTo("Character Seven"));
            Assert.That(slotZero.Character.IsShooter, Is.True);
            Assert.That(slotZero.Character.IsFighter, Is.False);
        }
    }
}
