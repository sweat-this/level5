using System.Collections.Generic;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using NUnit.Framework;

/// <summary>
/// The correspondence flow, driven entirely through the coordinator.
///
/// Every test here throws the coordinator away between turns and builds a new one over the same
/// store. That is what "the application stopped and started again" means to this architecture: no
/// object survives, only the document, and the competition carries on regardless.
/// </summary>
public class Level5VersusCorrespondenceTests
{
    private InMemoryVersusSeriesRepository repository;
    private CompetitiveRulesetCatalog catalog;
    private FakeVersusClock clock;
    private SequentialVersusIdSource ids;

    [SetUp]
    public void SetUp()
    {
        repository = VersusTestFixtures.Repository();
        catalog = VersusTestFixtures.Catalog(
            VersusTestFixtures.ScoreRuleset(),
            VersusTestFixtures.ContestRuleset(),
            VersusTestFixtures.ScoreRuleset("local-only", capabilities: VersusCapability.LocalAlternating));
        clock = new FakeVersusClock();
        ids = new SequentialVersusIdSource();
    }

    [Test]
    public void PlayerAPlaysNowAndPlayerBAnswersDaysLater()
    {
        SeriesId seriesId = CreateSeries(SeriesFormat.BestOf1);

        // --- Monday. Patrick takes his turn, then the application closes. ---------------------
        AttemptOperation patrickTurn = NewSession().IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        Assert.That(patrickTurn.Succeeded, Is.True);

        NewSession().StartAttempt(seriesId, patrickTurn.Attempt.Id);
        SubmissionOperation patrickResult = NewSession().SubmitResult(
            seriesId,
            patrickTurn.Attempt.Id,
            VersusTestFixtures.PatrickId,
            Result(47));

        Assert.That(patrickResult.Succeeded, Is.True);
        Assert.That(patrickResult.ResolvedGame, Is.False, "still waiting for Alex");

        // --- Thursday. Nothing of Monday's session exists any more. -----------------------------
        clock.AdvanceDays(3);

        VersusSeries waiting = NewSession().Load(seriesId);
        Assert.That(waiting.Status, Is.EqualTo(SeriesStatus.Active));
        Assert.That(
            waiting.ViewFor(VersusTestFixtures.AlexId).CurrentGame.OpponentResult,
            Is.Null,
            "Alex still cannot see what Patrick scored on Monday");

        AttemptOperation alexTurn = NewSession().IssueAttempt(seriesId, VersusTestFixtures.AlexId);
        SubmissionOperation alexResult = NewSession().SubmitResult(
            seriesId,
            alexTurn.Attempt.Id,
            VersusTestFixtures.AlexId,
            Result(31));

        Assert.That(alexResult.ResolvedGame, Is.True);
        Assert.That(alexResult.CompletedSeries, Is.True);
        Assert.That(alexResult.Series.Result.WinnerId, Is.EqualTo(VersusTestFixtures.PatrickId));
    }

    [Test]
    public void AWholeBestOfSevenSurvivesASessionBreakBetweenEveryTurn()
    {
        SeriesId seriesId = CreateSeries(SeriesFormat.BestOf7);

        // Patrick takes four straight, with a new session for every single turn.
        for (int game = 0; game < 4; game++)
        {
            TakeTurnInAFreshSession(seriesId, VersusTestFixtures.PatrickId, 10);
            TakeTurnInAFreshSession(seriesId, VersusTestFixtures.AlexId, 1);
            clock.AdvanceDays(1);
        }

        VersusSeries finished = NewSession().Load(seriesId);

        Assert.That(finished.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(finished.Score.ToString(), Is.EqualTo("4-0"));
        Assert.That(finished.Games[4].Status, Is.EqualTo(VersusGameStatus.Pending));
        Assert.That(finished.Games[6].Status, Is.EqualTo(VersusGameStatus.Pending));
    }

    [Test]
    public void AnInterruptedTurnComesBackAsTheSameTurnRatherThanAFreshGo()
    {
        // Issued, then the application died before the scene loaded. The next request must hand
        // back the same attempt; minting a second one would let a player reroll a bad start.
        SeriesId seriesId = CreateSeries(SeriesFormat.BestOf3);

        AttemptOperation first = NewSession().IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        AttemptOperation second = NewSession().IssueAttempt(seriesId, VersusTestFixtures.PatrickId);

        Assert.That(second.Attempt.Id, Is.EqualTo(first.Attempt.Id));
    }

    [Test]
    public void ASubmissionThatCannotBeSavedIsReportedAsFailedSoTheCallerRetries()
    {
        SeriesId seriesId = CreateSeries(SeriesFormat.BestOf1);
        AttemptOperation turn = NewSession().IssueAttempt(seriesId, VersusTestFixtures.PatrickId);

        repository.FailNextSave = true;
        SubmissionOperation failed = NewSession().SubmitResult(
            seriesId,
            turn.Attempt.Id,
            VersusTestFixtures.PatrickId,
            Result(47));

        Assert.That(failed.Succeeded, Is.False);
        Assert.That(failed.Validation.HasError(VersusValidationCode.PersistenceFailed), Is.True);

        // The stored series never saw the result, so retrying is the correct thing to do and works.
        SubmissionOperation retried = NewSession().SubmitResult(
            seriesId,
            turn.Attempt.Id,
            VersusTestFixtures.PatrickId,
            Result(47));

        Assert.That(retried.Succeeded, Is.True);
        Assert.That(NewSession().Load(seriesId).ViewFor(VersusTestFixtures.PatrickId)
            .CurrentGame.OwnResult.Get(AttemptMetric.Score), Is.EqualTo(47));
    }

    [Test]
    public void SubmittingTheSameFinishedTurnTwiceIsRefused()
    {
        SeriesId seriesId = CreateSeries(SeriesFormat.BestOf3);
        AttemptOperation turn = NewSession().IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        NewSession().SubmitResult(seriesId, turn.Attempt.Id, VersusTestFixtures.PatrickId, Result(12));

        SubmissionOperation again = NewSession().SubmitResult(
            seriesId,
            turn.Attempt.Id,
            VersusTestFixtures.PatrickId,
            Result(99));

        Assert.That(again.Succeeded, Is.False);
        Assert.That(
            NewSession().Load(seriesId).ViewFor(VersusTestFixtures.PatrickId)
                .CurrentGame.OwnResult.Get(AttemptMetric.Score),
            Is.EqualTo(12),
            "the first score stands");
    }

    [Test]
    public void AChallengeSitsAsAnInvitationUntilItIsAcceptedOrTurnedDown()
    {
        SeriesOperation created = NewSession().CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf3,
                VersusTestFixtures.Playlist(SeriesFormat.BestOf3, new RulesetId("most-points")),
                requiresInvitation: true));

        SeriesId seriesId = created.Series.Id;
        Assert.That(created.Series.Status, Is.EqualTo(SeriesStatus.Invited));

        AttemptOperation tooEarly = NewSession().IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        Assert.That(tooEarly.Succeeded, Is.False);
        Assert.That(tooEarly.Validation.HasError(VersusValidationCode.SeriesNotPlayable), Is.True);

        clock.AdvanceDays(2);
        SeriesOperation accepted = NewSession().AcceptChallenge(seriesId);

        Assert.That(accepted.Succeeded, Is.True);
        Assert.That(NewSession().Load(seriesId).Status, Is.EqualTo(SeriesStatus.Active));
        Assert.That(NewSession().IssueAttempt(seriesId, VersusTestFixtures.PatrickId).Succeeded, Is.True);
    }

    [Test]
    public void ADeclinedChallengeIsOverAndStaysThatWay()
    {
        SeriesOperation created = NewSession().CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf1,
                VersusTestFixtures.Playlist(SeriesFormat.BestOf1, new RulesetId("most-points")),
                requiresInvitation: true));

        NewSession().DeclineChallenge(created.Series.Id);

        Assert.That(NewSession().Load(created.Series.Id).Status, Is.EqualTo(SeriesStatus.Declined));
        Assert.That(NewSession().AcceptChallenge(created.Series.Id).Succeeded, Is.False);
    }

    [Test]
    public void OutstandingTurnsCanBeListedWithoutLoadingAnySeries()
    {
        CreateSeries(SeriesFormat.BestOf1);
        CreateSeries(SeriesFormat.BestOf3);

        List<SeriesSummary> mine = NewSession().ListActiveFor(VersusTestFixtures.PatrickId);

        Assert.That(mine.Count, Is.EqualTo(2));
        Assert.That(NewSession().ListActiveFor(new ParticipantId("stranger")), Is.Empty);
    }

    [Test]
    public void ACompletedSeriesDropsOutOfTheOutstandingList()
    {
        SeriesId seriesId = CreateSeries(SeriesFormat.BestOf1);
        TakeTurnInAFreshSession(seriesId, VersusTestFixtures.PatrickId, 47);
        TakeTurnInAFreshSession(seriesId, VersusTestFixtures.AlexId, 31);

        Assert.That(NewSession().ListActiveFor(VersusTestFixtures.PatrickId), Is.Empty);
        Assert.That(NewSession().ListSeries().Count, Is.EqualTo(1), "but it is still stored");
    }

    [Test]
    public void ASeriesIsRefusedWhenAModeInItCannotBePlayedByCorrespondence()
    {
        SeriesOperation created = NewSession().CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf1,
                new[] { new RulesetId("local-only") },
                VersusMode.Asynchronous));

        Assert.That(created.Succeeded, Is.False);
        Assert.That(created.Validation.HasError(VersusValidationCode.CapabilityNotSupported), Is.True);

        // The same mode in the same format is perfectly fine sitting next to each other.
        SeriesOperation locally = NewSession().CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf1,
                new[] { new RulesetId("local-only") },
                VersusMode.LocalAlternating));

        Assert.That(locally.Succeeded, Is.True);
    }

    [Test]
    public void ASeriesIsRefusedWhenItsPlaylistIsTheWrongLengthOrNamesNothingReal()
    {
        SeriesOperation shortPlaylist = NewSession().CreateSeries(
            VersusTestFixtures.Request(SeriesFormat.BestOf5, new[] { new RulesetId("most-points") }));

        Assert.That(shortPlaylist.Succeeded, Is.False);
        Assert.That(shortPlaylist.Validation.HasError(VersusValidationCode.PlaylistLengthMismatch), Is.True);

        SeriesOperation unknown = NewSession().CreateSeries(
            VersusTestFixtures.Request(SeriesFormat.BestOf1, new[] { new RulesetId("does-not-exist") }));

        Assert.That(unknown.Validation.HasError(VersusValidationCode.UnknownRuleset), Is.True);
    }

    [Test]
    public void RealTimeOnlineIsRefusedByNameRatherThanHalfWorking()
    {
        SeriesOperation created = NewSession().CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf1,
                new[] { new RulesetId("most-points") },
                VersusMode.OnlineRealtime));

        Assert.That(created.Succeeded, Is.False);
        Assert.That(created.Validation.HasError(VersusValidationCode.VersusModeNotImplemented), Is.True);
    }

    [Test]
    public void ASeriesWithOneParticipantPlayingThemselvesIsRefused()
    {
        SeriesRequest request = new SeriesRequest(
            VersusTestFixtures.Patrick(),
            VersusTestFixtures.Patrick(),
            SeriesFormat.BestOf1,
            new[] { new RulesetId("most-points") },
            VersusMode.Asynchronous);

        SeriesOperation created = NewSession().CreateSeries(request);

        Assert.That(created.Succeeded, Is.False);
        Assert.That(created.Validation.HasError(VersusValidationCode.ParticipantsInvalid), Is.True);
    }

    [Test]
    public void APlaylistCanMixDifferentModesAcrossTheSeries()
    {
        SeriesOperation created = NewSession().CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf3,
                new[]
                {
                    new RulesetId("three-point-contest"),
                    new RulesetId("most-points"),
                    new RulesetId("three-point-contest")
                }));

        Assert.That(created.Succeeded, Is.True);
        Assert.That(created.Series.Snapshot.GameAt(0).Id.Value, Is.EqualTo("three-point-contest"));
        Assert.That(created.Series.Snapshot.GameAt(1).Id.Value, Is.EqualTo("most-points"));
    }

    [Test]
    public void EventsAreRaisedForEveryMomentAnInboxWouldCareAbout()
    {
        // The events exist so a turn inbox can be built as a projection later without the series
        // domain ever knowing that notifications are a thing.
        List<string> seen = new List<string>();
        VersusMatchCoordinator coordinator = NewSession();
        coordinator.SeriesCreated += series => seen.Add("created");
        coordinator.AttemptIssued += (series, attempt) => seen.Add("issued");
        coordinator.AttemptCompleted += (series, attempt) => seen.Add("completed");
        coordinator.GameResolved += (series, game) => seen.Add("resolved");
        coordinator.SeriesAdvanced += (series, game) => seen.Add("advanced");
        coordinator.SeriesCompleted += series => seen.Add("series-completed");

        SeriesOperation created = coordinator.CreateSeries(
            VersusTestFixtures.Request(
                SeriesFormat.BestOf3,
                VersusTestFixtures.Playlist(SeriesFormat.BestOf3, new RulesetId("most-points"))));

        SeriesId seriesId = created.Series.Id;
        AttemptOperation patrick = coordinator.IssueAttempt(seriesId, VersusTestFixtures.PatrickId);
        coordinator.SubmitResult(seriesId, patrick.Attempt.Id, VersusTestFixtures.PatrickId, Result(47));
        AttemptOperation alex = coordinator.IssueAttempt(seriesId, VersusTestFixtures.AlexId);
        coordinator.SubmitResult(seriesId, alex.Attempt.Id, VersusTestFixtures.AlexId, Result(31));

        Assert.That(seen, Is.EqualTo(new[]
        {
            "created", "issued", "completed", "issued", "completed", "resolved", "advanced"
        }));
    }

    [Test]
    public void RivalryHistoryIsRebuiltFromCompletedSeriesRatherThanCounted()
    {
        PlayWholeSeries(SeriesFormat.BestOf1, patrickScore: 47, alexScore: 31);
        PlayWholeSeries(SeriesFormat.BestOf1, patrickScore: 50, alexScore: 12);
        PlayWholeSeries(SeriesFormat.BestOf1, patrickScore: 10, alexScore: 60);

        RivalryRecord rivalry = NewSession().Rivalry(VersusTestFixtures.PatrickId, VersusTestFixtures.AlexId);

        Assert.That(rivalry.SeriesPlayed, Is.EqualTo(3));
        Assert.That(rivalry.SeriesWinsLeft, Is.EqualTo(2));
        Assert.That(rivalry.SeriesWinsRight, Is.EqualTo(1));
        Assert.That(rivalry.GameWinsLeft, Is.EqualTo(2));
        Assert.That(rivalry.CurrentStreak, Is.EqualTo(-1), "Alex took the most recent one");
        Assert.That(rivalry.LongestStreakLeft, Is.EqualTo(2));
        Assert.That(rivalry.LastPlayedUtc, Is.Not.Null);

        // Rebuilding gives the same answer, because there is nothing stored to drift.
        RivalryRecord again = NewSession().Rivalry(VersusTestFixtures.PatrickId, VersusTestFixtures.AlexId);
        Assert.That(again.SeriesWinsLeft, Is.EqualTo(rivalry.SeriesWinsLeft));
    }

    [Test]
    public void ASweepIsRecordedAsOne()
    {
        PlayWholeSeries(SeriesFormat.BestOf3, patrickScore: 10, alexScore: 1);

        RivalryRecord rivalry = NewSession().Rivalry(VersusTestFixtures.PatrickId, VersusTestFixtures.AlexId);

        Assert.That(rivalry.SweepsLeft, Is.EqualTo(1));
        Assert.That(rivalry.GameWinsLeft, Is.EqualTo(2));
        Assert.That(rivalry.GameWinsRight, Is.EqualTo(0));
    }

    // ---- helpers ------------------------------------------------------------------------------

    /// <summary>
    /// A brand new coordinator over the same store.
    ///
    /// Standing in for "the application was closed and opened again". Nothing carries over except
    /// the stored documents, which is the whole claim being tested.
    /// </summary>
    private VersusMatchCoordinator NewSession()
    {
        return VersusTestFixtures.Coordinator(repository, catalog, clock, ids);
    }

    private SeriesId CreateSeries(SeriesFormat format)
    {
        SeriesOperation created = NewSession().CreateSeries(
            VersusTestFixtures.Request(
                format,
                VersusTestFixtures.Playlist(format, new RulesetId("most-points"))));

        Assert.That(created.Succeeded, Is.True, created.Validation.ToString());
        return created.Series.Id;
    }

    private void TakeTurnInAFreshSession(SeriesId seriesId, ParticipantId participantId, float score)
    {
        AttemptOperation issued = NewSession().IssueAttempt(seriesId, participantId);
        Assert.That(issued.Succeeded, Is.True, issued.Validation.ToString());

        NewSession().StartAttempt(seriesId, issued.Attempt.Id);
        SubmissionOperation submitted = NewSession().SubmitResult(
            seriesId,
            issued.Attempt.Id,
            participantId,
            Result(score));

        Assert.That(submitted.Succeeded, Is.True, submitted.Validation.ToString());
    }

    private void PlayWholeSeries(SeriesFormat format, float patrickScore, float alexScore)
    {
        SeriesId seriesId = CreateSeries(format);

        while (NewSession().Load(seriesId).IsActive)
        {
            TakeTurnInAFreshSession(seriesId, VersusTestFixtures.PatrickId, patrickScore);
            TakeTurnInAFreshSession(seriesId, VersusTestFixtures.AlexId, alexScore);
            clock.AdvanceDays(1);
        }
    }

    private static AttemptResult Result(float score)
    {
        return new AttemptResult.Builder(new RulesetId("most-points"), 1)
            .Set(AttemptMetric.Score, score)
            .Build();
    }
}
