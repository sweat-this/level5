using System.Collections.Generic;
using System.Text.RegularExpressions;
using Level5.Core.Match;
using Level5.Core.Progression;
using Level5.Core.Versus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Issue #203: <see cref="VersusLauncher"/> must enforce local level eligibility
/// (<see cref="LevelEligibility.ValidateForLaunch"/>) before <c>VersusMatchCoordinator.IssueAttempt</c>,
/// the same two-stage protection <see cref="Level5.BackendV2.RemoteAttemptLauncher"/> already has for
/// remote correspondence (issue #198). <see cref="Level5VersusIntegrationTests"/> keeps the original
/// "happy path builds an ordinary match" coverage; this file is specifically about what happens
/// before and around <c>IssueAttempt</c> for each kind of local-eligibility failure, and about the
/// deliberately unchanged post-issuance mode/arena-incompatibility behavior.
/// </summary>
public class Level5VersusLauncherTests
{
    private const int EligibleLevelId = 1;
    private const int IncompatibleModeLevelId = 2;
    private const int LockedLevelId = 3;
    private const int NonSelectableLevelId = 4;
    private const int UnknownLevelId = 999;

    [SetUp]
    public void SetUp()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition eligibleLevel = TestDefinitions.Level(EligibleLevelId);
        LevelDefinition incompatibleLevel = TestDefinitions.Level(IncompatibleModeLevelId, capabilities: ArenaCapability.None);
        LevelDefinition lockedLevel = TestDefinitions.Level(LockedLevelId, locked: true);
        LevelDefinition nonSelectableLevel = TestDefinitions.Level(NonSelectableLevelId, selectable: false);

        MatchCatalogs.Override(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { eligibleLevel, incompatibleLevel, lockedLevel, nonSelectableLevel }));

        VersusRuntime.Override(VersusTestFixtures.Repository(), VersusTestFixtures.Catalog(VersusTestFixtures.ScoreRuleset()));
    }

    [TearDown]
    public void TearDown()
    {
        VersusLauncher.ResetSceneLoader();
        ActiveMatch.Clear();
        ActiveVersusAttempt.Clear();
        VersusRuntime.Reset();
        MatchCatalogs.Reset();
    }

    // ---------------------------------------------------------------- BuildMatch

    [Test]
    public void BuildMatchProducesAnOrdinaryConfigurationForAnEligibleLevel()
    {
        MatchConfiguration configuration = VersusLauncher.BuildMatch(
            VersusTestFixtures.ScoreRuleset(),
            EligibleLevelId,
            VersusTestFixtures.PatrickId,
            TestCharacter(),
            Unlocked(EligibleLevelId));

        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration.ModeId, Is.EqualTo(GameModeId.TotalPoints));
        Assert.That(configuration.Roster.Players[0].ParticipantId, Is.EqualTo(VersusTestFixtures.PatrickId.Value));
    }

    [Test]
    public void BuildMatchRefusesALockedLevel()
    {
        LogAssert.Expect(LogType.Warning, new Regex("could not be launched on level " + LockedLevelId));

        MatchConfiguration configuration = VersusLauncher.BuildMatch(
            VersusTestFixtures.ScoreRuleset(),
            LockedLevelId,
            VersusTestFixtures.PatrickId,
            TestCharacter(),
            new UnlockSnapshot(null, new Dictionary<int, bool> { [LockedLevelId] = false }));

        Assert.That(configuration, Is.Null);
    }

    [Test]
    public void BuildMatchRefusesANonSelectableLevel()
    {
        LogAssert.Expect(LogType.Warning, new Regex("could not be launched on level " + NonSelectableLevelId));

        MatchConfiguration configuration = VersusLauncher.BuildMatch(
            VersusTestFixtures.ScoreRuleset(),
            NonSelectableLevelId,
            VersusTestFixtures.PatrickId,
            TestCharacter(),
            Unlocked(NonSelectableLevelId));

        Assert.That(configuration, Is.Null);
    }

    [Test]
    public void BuildMatchFailsClosedWhenTheSnapshotIsNull()
    {
        LogAssert.Expect(LogType.Warning, new Regex("no local unlock snapshot was provided"));

        MatchConfiguration configuration = VersusLauncher.BuildMatch(
            VersusTestFixtures.ScoreRuleset(),
            EligibleLevelId,
            VersusTestFixtures.PatrickId,
            TestCharacter(),
            null);

        Assert.That(configuration, Is.Null, "a missing snapshot must fail closed rather than fall back to the permissive builder overload");
    }

    // ---------------------------------------------------------------- Launch ordering: no attempt issued

    [Test]
    public void ALockedLevelFailsBeforeIssueAttempt()
    {
        SeriesId seriesId = CreateSeries();

        VersusLaunch launch = VersusLauncher.Launch(
            seriesId,
            VersusTestFixtures.PatrickId,
            LockedLevelId,
            TestCharacter(),
            new UnlockSnapshot(null, new Dictionary<int, bool> { [LockedLevelId] = false }));

        AssertNoAttemptWasIssued(seriesId, launch);
    }

    [Test]
    public void ANonSelectableLevelFailsBeforeIssueAttempt()
    {
        SeriesId seriesId = CreateSeries();

        VersusLaunch launch = VersusLauncher.Launch(
            seriesId,
            VersusTestFixtures.PatrickId,
            NonSelectableLevelId,
            TestCharacter(),
            Unlocked(NonSelectableLevelId));

        AssertNoAttemptWasIssued(seriesId, launch);
    }

    [Test]
    public void AnUnknownLevelFailsBeforeIssueAttempt()
    {
        SeriesId seriesId = CreateSeries();

        VersusLaunch launch = VersusLauncher.Launch(
            seriesId,
            VersusTestFixtures.PatrickId,
            UnknownLevelId,
            TestCharacter(),
            Unlocked(UnknownLevelId));

        AssertNoAttemptWasIssued(seriesId, launch);
    }

    [Test]
    public void AMissingUnlockSnapshotFailsBeforeIssueAttemptRatherThanFallingBackToPermissiveBehavior()
    {
        SeriesId seriesId = CreateSeries();

        VersusLaunch launch = VersusLauncher.Launch(
            seriesId,
            VersusTestFixtures.PatrickId,
            EligibleLevelId,
            TestCharacter(),
            null);

        AssertNoAttemptWasIssued(seriesId, launch);
    }

    // ---------------------------------------------------------------- Preserve post-issuance semantics

    [Test]
    public void AKnownSelectableUnlockedButModeIncompatibleArenaStillIssuesAndLeavesTheAttemptOutstanding()
    {
        // Distinct from the local-eligibility failures above: this level exists, is selectable and is
        // unlocked - LevelEligibility.ValidateForLaunch passes it - but it has none of the capabilities
        // TotalPoints requires, so only the builder's mode/arena compatibility check catches it, which
        // deliberately happens after IssueAttempt, not before.
        LogAssert.Expect(LogType.Warning, new Regex("could not be launched on level " + IncompatibleModeLevelId));
        SeriesId seriesId = CreateSeries();

        VersusLaunch launch = VersusLauncher.Launch(
            seriesId,
            VersusTestFixtures.PatrickId,
            IncompatibleModeLevelId,
            TestCharacter(),
            Unlocked(IncompatibleModeLevelId));

        Assert.That(launch.Succeeded, Is.False);
        Assert.That(ActiveMatch.IsActive, Is.False);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);

        VersusSeries reloaded = VersusRuntime.Coordinator.Load(seriesId);
        Assert.That(
            reloaded.ViewFor(VersusTestFixtures.PatrickId).CurrentGame.OwnAttemptId,
            Is.Not.EqualTo(AttemptId.None),
            "the attempt from the failed BuildMatch call must remain outstanding, persisted, and retryable");
    }

    // ---------------------------------------------------------------- Successful path

    [Test]
    public void AnEligibleLevelIssuesTheAttemptBuildsTheMatchAndLoadsTheScene()
    {
        List<string> loadedScenes = new List<string>();
        VersusLauncher.OverrideSceneLoader(sceneName => loadedScenes.Add(sceneName));
        SeriesId seriesId = CreateSeries();

        VersusLaunch launch = VersusLauncher.Launch(
            seriesId,
            VersusTestFixtures.PatrickId,
            EligibleLevelId,
            TestCharacter(),
            Unlocked(EligibleLevelId));

        Assert.That(launch.Succeeded, Is.True, launch.Validation?.ToString());
        Assert.That(launch.Configuration, Is.Not.Null);
        Assert.That(ActiveMatch.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(loadedScenes, Has.Count.EqualTo(1), "exactly one scene load for a successful launch");

        VersusSeries reloaded = VersusRuntime.Coordinator.Load(seriesId);
        Assert.That(
            reloaded.ViewFor(VersusTestFixtures.PatrickId).CurrentGame.OwnAttemptId,
            Is.Not.EqualTo(AttemptId.None),
            "the issued attempt is persisted");
    }

    // ---------------------------------------------------------------- helpers

    private static UnlockSnapshot Unlocked(int levelId)
    {
        return new UnlockSnapshot(null, new Dictionary<int, bool> { [levelId] = true });
    }

    private static CharacterSelection TestCharacter()
    {
        return new CharacterSelection(1, "drblood", "Dr Blood", true, true);
    }

    private SeriesId CreateSeries()
    {
        SeriesOperation created = VersusRuntime.Coordinator.CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf1,
                VersusTestFixtures.Playlist(SeriesFormat.BestOf1, new RulesetId("most-points")),
                VersusMode.LocalAlternating));

        Assert.That(created.Succeeded, Is.True, created.Validation.ToString());
        return created.Series.Id;
    }

    /// <summary>
    /// Proves a launch failure happened before <c>VersusMatchCoordinator.IssueAttempt</c>: reloading
    /// the persisted series shows the current game still has no attempt at all for the participant
    /// (<see cref="AttemptId.None"/>, not merely "not completed" - unlike <c>CanIssueAttempt</c>,
    /// which stays true even once a live attempt exists, since reissuing to the same participant is
    /// idempotent), and no match/attempt globals were touched.
    /// </summary>
    private void AssertNoAttemptWasIssued(SeriesId seriesId, VersusLaunch launch)
    {
        Assert.That(launch.Succeeded, Is.False);
        Assert.That(ActiveMatch.IsActive, Is.False);
        Assert.That(ActiveVersusAttempt.IsActive, Is.False);

        VersusSeries reloaded = VersusRuntime.Coordinator.Load(seriesId);
        Assert.That(
            reloaded.ViewFor(VersusTestFixtures.PatrickId).CurrentGame.OwnAttemptId,
            Is.EqualTo(AttemptId.None),
            "no attempt should have been issued or persisted");
    }
}
