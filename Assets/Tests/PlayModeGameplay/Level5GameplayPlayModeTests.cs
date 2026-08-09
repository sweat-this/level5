#if UNITY_INCLUDE_TESTS
using System.Collections;
using Level5.Core.Match;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode coverage for runtime gameplay code.
///
/// This file exists to prove something that was not true before AUD-059: that gameplay code can be
/// tested at all. Everything in <c>Assets/Scripts</c> compiles into the predefined
/// <c>Assembly-CSharp</c>, and a Unity assembly definition cannot reference a predefined assembly -
/// so <c>Level5.PlayModeTests</c> structurally could not see <c>GameStats</c>, <c>GameRules</c>,
/// <c>VersusMatchReporter</c> or anything else that actually runs the game.
///
/// It lives in a folder with **no** asmdef, which is what lets it compile alongside the code it
/// tests, and the whole file is behind <c>UNITY_INCLUDE_TESTS</c> so none of it can reach a
/// shipping player build.
///
/// The structural fix - splitting <c>Assets/Scripts</c> into real assemblies - is still open. This
/// unblocks testing without waiting for it.
/// </summary>
public class Level5GameplayPlayModeTests
{
    private InMemoryVersusSeriesRepository repository;

    [SetUp]
    public void SetUp()
    {
        repository = new InMemoryVersusSeriesRepository();
        VersusRuntime.Override(repository);
    }

    [TearDown]
    public void TearDown()
    {
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();
        MatchCatalogs.Reset();
    }

    /// <summary>
    /// A competitive turn, played in play mode, from real match stats to a resolved series.
    ///
    /// This is the path the edit-mode suite could only test in pieces: a live <c>GameStats</c>
    /// component on a real GameObject, through <c>VersusMatchReporter</c> - the exact call
    /// <c>GameRules</c> makes at match end - into the series.
    /// </summary>
    [UnityTest]
    public IEnumerator AVersusTurnIsReportedFromLiveMatchStats()
    {
        SeriesOperation created = VersusRuntime.Coordinator.CreateSeries(new SeriesRequest(
            new MatchParticipant(new ParticipantId("patrick"), "Patrick"),
            new MatchParticipant(new ParticipantId("alex"), "Alex"),
            SeriesFormat.BestOf1,
            new[] { new RulesetId("most-points") },
            VersusMode.Asynchronous,
            InformationPolicy.SealedAttempt,
            false,
            true,
            "play mode test"));

        Assert.That(created.Succeeded, Is.True, created.Validation.ToString());
        SeriesId seriesId = created.Series.Id;

        yield return ReportTurn(seriesId, new ParticipantId("patrick"), totalPoints: 42);
        yield return ReportTurn(seriesId, new ParticipantId("alex"), totalPoints: 17);

        VersusSeries finished = VersusRuntime.Coordinator.Load(seriesId);

        Assert.That(finished.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(finished.Result.WinnerId, Is.EqualTo(new ParticipantId("patrick")));
    }

    /// <summary>
    /// The AUD-060 fix, checked the only way it can be: destroy the object and look at the static.
    ///
    /// An edit-mode test cannot do this - it needs a real destroy, which needs a frame.
    /// </summary>
    [UnityTest]
    public IEnumerator ASceneScopedSingletonReleasesItsStaticWhenDestroyed()
    {
        GameObject host = new GameObject("match-controller-under-test");
        MatchController controller = host.AddComponent<MatchController>();

        yield return null;

        Assert.That(MatchController.instance, Is.EqualTo(controller), "it claims the static while alive");

        Object.Destroy(host);
        yield return null;

        Assert.That(
            MatchController.instance,
            Is.Null,
            "a destroyed singleton must not leave its static pointing at itself");
    }

    /// <summary>
    /// A match that is not part of a series must report nothing, in a real frame.
    ///
    /// Every existing game mode takes this branch at match end; if it ever stopped being free, all
    /// of them would pay for it.
    /// </summary>
    [UnityTest]
    public IEnumerator AnOrdinaryMatchReportsNothingToTheVersusSystem()
    {
        GameObject host = new GameObject("stats-under-test");
        GameStats stats = host.AddComponent<GameStats>();
        stats.TotalPoints = 25;

        yield return null;

        Assert.That(ActiveVersusAttempt.IsActive, Is.False);
        Assert.That(
            VersusMatchReporter.TryReport(stats, GameModeId.TotalPoints, 60f),
            Is.True,
            "the match-end work reports itself finished with nothing to do");
        Assert.That(repository.Count, Is.EqualTo(0), "and nothing was written");

        Object.Destroy(host);
    }

    private IEnumerator ReportTurn(SeriesId seriesId, ParticipantId participantId, int totalPoints)
    {
        AttemptOperation issued = VersusRuntime.Coordinator.IssueAttempt(seriesId, participantId);
        Assert.That(issued.Succeeded, Is.True, issued.Validation.ToString());

        ActiveVersusAttempt.Begin(seriesId, issued.Attempt);
        VersusRuntime.Coordinator.StartAttempt(seriesId, issued.Attempt.Id);

        GameObject host = new GameObject("game-stats-" + participantId.Value);
        GameStats stats = host.AddComponent<GameStats>();
        stats.TotalPoints = totalPoints;
        stats.ShotMade = 6;
        stats.ShotAttempt = 10;

        yield return null;

        Assert.That(
            VersusMatchReporter.TryReport(stats, GameModeId.TotalPoints, 90f),
            Is.True,
            "the turn was stored");

        Object.Destroy(host);
        yield return null;
    }
}
#endif
