using System;
using System.Collections.Generic;
using System.Linq;
using Level5.Core.Match;
using Level5.Core.Versus;
using Level5.Core.Versus.Persistence;
using NUnit.Framework;

/// <summary>
/// The local-simultaneous additions to the versus domain: attempts issued as a pair, results
/// submitted as a pair, and the guarantee that a pair is durable together or not at all.
///
/// Everything goes through the existing <see cref="VersusSeries"/>, <see cref="VersusGame"/> and
/// <see cref="VersusMatchCoordinator"/> over <see cref="InMemoryVersusSeriesRepository"/>, which stores
/// serialized documents, so "reload the series" really does mean "read what was saved".
/// </summary>
public class Level5VersusSimultaneousDomainTests
{
    private const VersusCapability SimultaneousCapabilities =
        VersusCapability.LocalSimultaneous | VersusCapability.LocalAlternating | VersusCapability.Asynchronous;

    private InMemoryVersusSeriesRepository repository;
    private VersusMatchCoordinator coordinator;
    private CompetitiveRuleset ruleset;

    [SetUp]
    public void SetUp()
    {
        ruleset = SimultaneousRuleset();
        repository = VersusTestFixtures.Repository();
        coordinator = VersusTestFixtures.Coordinator(repository, VersusTestFixtures.Catalog(ruleset));
    }

    /// <summary>Score, then accuracy, then fewer attempts: the same shape as the shipped most-points.</summary>
    private static CompetitiveRuleset SimultaneousRuleset(
        string id = "most-points",
        VersusCapability capabilities = SimultaneousCapabilities,
        int version = 1)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            version,
            GameModeId.TotalPoints,
            capabilities,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.Score),
                ComparisonKey.Highest(AttemptMetric.Accuracy),
                ComparisonKey.Lowest(AttemptMetric.ShotsAttempted)
            },
            1,
            "Most Points");
    }

    private SeriesId CreateSeries(SeriesFormat format, VersusMode mode = VersusMode.LocalSimultaneous)
    {
        SeriesOperation created = coordinator.CreateSeries(VersusTestFixtures.Request(
            format,
            VersusTestFixtures.Playlist(format, ruleset.Id),
            mode));
        Assert.That(created.Succeeded, Is.True, created.Validation?.ToString());
        return created.Series.Id;
    }

    private AttemptResult Result(float score, int made = 0, int attempted = 0)
    {
        return new AttemptResult.Builder(ruleset.Id, ruleset.Version)
            .Set(AttemptMetric.Score, score)
            .SetShooting(made, attempted)
            .Build();
    }

    /// <summary>Issues both attempts and submits the pair, Patrick being the series' first participant.</summary>
    private SubmissionOperation PlayGame(SeriesId id, float firstScore, float secondScore)
    {
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        Assert.That(issued.Succeeded, Is.True, issued.Validation?.ToString());
        return coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(firstScore)),
            new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.Second.ParticipantId, Result(secondScore)));
    }

    private static int StoredAttemptCount(InMemoryVersusSeriesRepository repository, SeriesId id, int gameIndex)
    {
        return VersusSeriesDocumentReader.AttemptCount(repository.RawDocument(id), gameIndex);
    }

    // ---------------------------------------------------------------- capability and validation

    [Test]
    public void ASimultaneousSeriesIsAcceptedOnlyWhenEveryRulesetHasTheCapability()
    {
        VersusMatchCoordinator unsupported = VersusTestFixtures.Coordinator(
            VersusTestFixtures.Repository(),
            VersusTestFixtures.Catalog(SimultaneousRuleset(capabilities: VersusCapability.LocalAlternating | VersusCapability.Asynchronous)));

        SeriesOperation refused = unsupported.CreateSeries(VersusTestFixtures.Request(
            SeriesFormat.BestOf3,
            VersusTestFixtures.Playlist(SeriesFormat.BestOf3, ruleset.Id),
            VersusMode.LocalSimultaneous));
        SeriesOperation accepted = coordinator.CreateSeries(VersusTestFixtures.Request(
            SeriesFormat.BestOf3,
            VersusTestFixtures.Playlist(SeriesFormat.BestOf3, ruleset.Id),
            VersusMode.LocalSimultaneous));

        Assert.That(refused.Succeeded, Is.False);
        Assert.That(refused.Validation.HasError(VersusValidationCode.CapabilityNotSupported), Is.True);
        Assert.That(accepted.Succeeded, Is.True, accepted.Validation?.ToString());
        Assert.That(accepted.Series.Mode, Is.EqualTo(VersusMode.LocalSimultaneous));
    }

    [Test]
    public void OnlyMostPointsDeclaresLocalSimultaneousInTheShippedRulesets()
    {
        List<CompetitiveRuleset> all = DefaultCompetitiveRulesets.CreateAll();

        List<string> simultaneous = all
            .Where(candidate => candidate.Supports(VersusCapability.LocalSimultaneous))
            .Select(candidate => candidate.Id.Value)
            .ToList();

        Assert.That(simultaneous, Is.EqualTo(new[] { "most-points" }));
    }

    [Test]
    public void MostPointsKeepsItsVersionKeysAndOtherCapabilities()
    {
        CompetitiveRuleset mostPoints = DefaultCompetitiveRulesets.CreateAll().Single(candidate => candidate.Id.Value == "most-points");

        Assert.That(mostPoints.Version, Is.EqualTo(1), "a new topology is not a scoring change, so not a new version");
        Assert.That(mostPoints.MinimumCompatibleVersion, Is.EqualTo(1));
        Assert.That(
            mostPoints.ComparisonKeys.Select(key => key.ToString()),
            Is.EqualTo(new[]
            {
                ComparisonKey.Highest(AttemptMetric.Score).ToString(),
                ComparisonKey.Highest(AttemptMetric.Accuracy).ToString(),
                ComparisonKey.Lowest(AttemptMetric.ShotsAttempted).ToString()
            }));
        Assert.That(mostPoints.Supports(VersusCapability.LocalAlternating), Is.True);
        Assert.That(mostPoints.Supports(VersusCapability.Asynchronous), Is.True);
    }

    [Test]
    public void EveryOtherShippedRulesetStillRefusesASimultaneousSeries()
    {
        CompetitiveRulesetCatalog catalog = new CompetitiveRulesetCatalog(DefaultCompetitiveRulesets.CreateAll());
        VersusMatchCoordinator shipped = VersusTestFixtures.Coordinator(VersusTestFixtures.Repository(), catalog);

        foreach (CompetitiveRuleset candidate in DefaultCompetitiveRulesets.CreateAll().Where(candidate => candidate.Id.Value != "most-points"))
        {
            SeriesOperation created = shipped.CreateSeries(VersusTestFixtures.Request(
                SeriesFormat.BestOf1,
                VersusTestFixtures.Playlist(SeriesFormat.BestOf1, candidate.Id),
                VersusMode.LocalSimultaneous));

            Assert.That(created.Succeeded, Is.False, candidate.Id.Value + " must not be playable as a same-time game");
            Assert.That(created.Validation.HasError(VersusValidationCode.CapabilityNotSupported), Is.True, candidate.Id.Value);
        }
    }

    [Test]
    public void ASimultaneousSeriesCannotBeOpenTarget()
    {
        SeriesOperation created = coordinator.CreateSeries(VersusTestFixtures.Request(
            SeriesFormat.BestOf1,
            VersusTestFixtures.Playlist(SeriesFormat.BestOf1, ruleset.Id),
            VersusMode.LocalSimultaneous,
            InformationPolicy.OpenTarget));

        Assert.That(created.Succeeded, Is.False);
        Assert.That(created.Validation.HasError(VersusValidationCode.CapabilityNotSupported), Is.True);
    }

    // ---------------------------------------------------------------- paired issue

    [Test]
    public void PairedIssueGivesEachParticipantExactlyOneAttemptAtTheSameCurrentGame()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);

        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);

        Assert.That(issued.Succeeded, Is.True, issued.Validation?.ToString());
        VersusSeries series = coordinator.Load(id);
        Assert.That(issued.Attempts.First.ParticipantId, Is.EqualTo(series.Participants.First.Id));
        Assert.That(issued.Attempts.Second.ParticipantId, Is.EqualTo(series.Participants.Second.Id));
        Assert.That(issued.Attempts.First.Id, Is.Not.EqualTo(issued.Attempts.Second.Id));
        Assert.That(issued.Attempts.First.GameIndex, Is.EqualTo(series.CurrentGame.Index));
        Assert.That(issued.Attempts.Second.GameIndex, Is.EqualTo(series.CurrentGame.Index));
        Assert.That(StoredAttemptCount(repository, id, 0), Is.EqualTo(2), "one attempt each, both durable");
        Assert.That(series.ViewFor(series.Participants.First.Id).CurrentGame.OwnAttemptId, Is.EqualTo(issued.Attempts.First.Id));
        Assert.That(series.ViewFor(series.Participants.Second.Id).CurrentGame.OwnAttemptId, Is.EqualTo(issued.Attempts.Second.Id));
    }

    [Test]
    public void RetryingPairedIssueCreatesNoDuplicates()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);

        SimultaneousAttemptOperation first = coordinator.IssueSimultaneousAttempts(id);
        SimultaneousAttemptOperation again = coordinator.IssueSimultaneousAttempts(id);

        Assert.That(again.Succeeded, Is.True, again.Validation?.ToString());
        Assert.That(again.Attempts.First.Id, Is.EqualTo(first.Attempts.First.Id));
        Assert.That(again.Attempts.Second.Id, Is.EqualTo(first.Attempts.Second.Id));
        Assert.That(StoredAttemptCount(repository, id, 0), Is.EqualTo(2));
    }

    [Test]
    public void ARetryAfterAnInterruptedRunHandsBackTheSamePairEvenOnceStarted()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        Assert.That(
            coordinator.StartSimultaneousAttempts(id, issued.Attempts.First.Id, issued.Attempts.Second.Id).Succeeded,
            Is.True);

        // the process dies mid-game; the next session asks again
        SimultaneousAttemptOperation resumed = coordinator.IssueSimultaneousAttempts(id);

        Assert.That(resumed.Succeeded, Is.True, resumed.Validation?.ToString());
        Assert.That(resumed.Attempts.First.Id, Is.EqualTo(issued.Attempts.First.Id));
        Assert.That(resumed.Attempts.Second.Id, Is.EqualTo(issued.Attempts.Second.Id));
        Assert.That(resumed.Attempts.First.State, Is.EqualTo(AttemptState.Started));
        Assert.That(StoredAttemptCount(repository, id, 0), Is.EqualTo(2));
    }

    [Test]
    public void StartingTheSimultaneousPairIsOneSave()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf1);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);

        repository.FailNextSave = true;
        SimultaneousAttemptOperation failed = coordinator.StartSimultaneousAttempts(id, issued.Attempts.First.Id, issued.Attempts.Second.Id);

        Assert.That(failed.Succeeded, Is.False);
        VersusSeries reloaded = coordinator.Load(id);
        Assert.That(reloaded.ViewFor(reloaded.Participants.First.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Started));
        Assert.That(reloaded.ViewFor(reloaded.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Started),
            "neither side is marked started when the save fails");
    }

    [Test]
    public void PairedIssueIsRefusedForAnAlternatingSeriesAFinishedSeriesAndAMissingSeries()
    {
        SeriesId alternating = CreateSeries(SeriesFormat.BestOf1, VersusMode.LocalAlternating);
        Assert.That(coordinator.IssueSimultaneousAttempts(alternating).Succeeded, Is.False);

        SeriesId finished = CreateSeries(SeriesFormat.BestOf1);
        Assert.That(PlayGame(finished, 10, 5).Succeeded, Is.True);
        SimultaneousAttemptOperation afterEnd = coordinator.IssueSimultaneousAttempts(finished);
        Assert.That(afterEnd.Succeeded, Is.False);

        SimultaneousAttemptOperation missing = coordinator.IssueSimultaneousAttempts(new SeriesId("nope"));
        Assert.That(missing.Succeeded, Is.False);
        Assert.That(missing.Validation.HasError(VersusValidationCode.SeriesNotFound), Is.True);
    }

    [Test]
    public void ASimultaneousSeriesRefusesToIssueOrSubmitForOneParticipantAlone()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        VersusSeries series = coordinator.Load(id);

        AttemptOperation single = coordinator.IssueAttempt(id, series.Participants.First.Id);
        Assert.That(single.Succeeded, Is.False, "there is no turn to take in a same-time game");
        Assert.That(series.CanIssueAttempt(series.Participants.First.Id, out string reason), Is.False);
        Assert.That(reason, Does.Contain("together"));

        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        SubmissionOperation alone = coordinator.SubmitResult(
            id, issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(10));
        Assert.That(alone.Succeeded, Is.False, "one side's result cannot be recorded on its own");
        VersusSeries reloaded = coordinator.Load(id);
        Assert.That(reloaded.ViewFor(reloaded.Participants.First.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));
    }

    [Test]
    public void AnAlternatingSeriesStillIssuesAndSubmitsOneParticipantAtATime()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf1, VersusMode.LocalAlternating);
        VersusSeries series = coordinator.Load(id);

        AttemptOperation first = coordinator.IssueAttempt(id, series.Participants.First.Id);
        Assert.That(first.Succeeded, Is.True, first.Validation?.ToString());
        Assert.That(coordinator.SubmitResult(id, first.Attempt.Id, series.Participants.First.Id, Result(3)).Succeeded, Is.True);

        AttemptOperation second = coordinator.IssueAttempt(id, series.Participants.Second.Id);
        Assert.That(second.Succeeded, Is.True);
        SubmissionOperation resolved = coordinator.SubmitResult(id, second.Attempt.Id, series.Participants.Second.Id, Result(9));
        Assert.That(resolved.Succeeded, Is.True);
        Assert.That(resolved.CompletedSeries, Is.True);
    }

    // ---------------------------------------------------------------- paired results resolve through the ruleset

    [Test]
    public void TheHigherScoreWinsWhicheverParticipantHoldsIt()
    {
        SeriesId firstWins = CreateSeries(SeriesFormat.BestOf1);
        SubmissionOperation one = PlayGame(firstWins, 30, 12);
        Assert.That(one.ResolvedGame, Is.True);
        Assert.That(one.Submission.GameResult.WinnerId, Is.EqualTo(one.Series.Participants.First.Id));

        SeriesId secondWins = CreateSeries(SeriesFormat.BestOf1);
        SubmissionOperation two = PlayGame(secondWins, 12, 30);
        Assert.That(two.Submission.GameResult.WinnerId, Is.EqualTo(two.Series.Participants.Second.Id));
        Assert.That(two.CompletedSeries, Is.True);
    }

    [Test]
    public void TheComparisonKeysBreakATieExactlyAsTheyDoForAnyOtherGame()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf1);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);

        // level on score; the second participant's 5/5 beats the first's 5/8 on accuracy
        SubmissionOperation submitted = coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(20, made: 5, attempted: 8)),
            new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.Second.ParticipantId, Result(20, made: 5, attempted: 5)));

        Assert.That(submitted.Succeeded, Is.True, submitted.Validation?.ToString());
        Assert.That(submitted.Submission.GameResult.Kind, Is.EqualTo(GameOutcomeKind.Decided));
        Assert.That(submitted.Submission.GameResult.WinnerId, Is.EqualTo(submitted.Series.Participants.Second.Id));
    }

    [Test]
    public void EqualOnEveryKeyIsADrawAndTheSeriesCarriesOn()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);

        SubmissionOperation drawn = PlayGame(id, 15, 15);

        Assert.That(drawn.ResolvedGame, Is.True);
        Assert.That(drawn.Submission.GameResult.Kind, Is.EqualTo(GameOutcomeKind.Draw));
        Assert.That(drawn.Submission.GameResult.HasWinner, Is.False);
        Assert.That(drawn.CompletedSeries, Is.False);
        Assert.That(drawn.Series.CurrentGame.Index, Is.EqualTo(1), "a drawn game advances to the next game");
    }

    [Test]
    public void ABestOf3AdvancesThroughTheNormalSeriesRules()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);

        SubmissionOperation game1 = PlayGame(id, 20, 10);
        Assert.That(game1.CompletedSeries, Is.False);
        Assert.That(game1.Series.CurrentGame.Index, Is.EqualTo(1));
        Assert.That(game1.Series.Score.FirstWins, Is.EqualTo(1));

        SubmissionOperation game2 = PlayGame(id, 8, 14);
        Assert.That(game2.CompletedSeries, Is.False, "one game each");
        Assert.That(game2.Series.CurrentGame.Index, Is.EqualTo(2));

        SubmissionOperation game3 = PlayGame(id, 30, 10);
        Assert.That(game3.CompletedSeries, Is.True);
        Assert.That(game3.Series.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(game3.Series.Result.WinnerId, Is.EqualTo(game3.Series.Participants.First.Id));
        Assert.That(game3.Series.Score.FirstWins, Is.EqualTo(2));
        Assert.That(game3.Series.Score.SecondWins, Is.EqualTo(1));
    }

    [Test]
    public void ABestOf7StopsAsSoonAsSomeoneHasFourAndNeverActivatesTheRest()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf7);

        for (int game = 0; game < 4; game++)
        {
            SubmissionOperation played = PlayGame(id, 20, 5);
            Assert.That(played.Succeeded, Is.True, played.Validation?.ToString());
        }

        VersusSeries done = coordinator.Load(id);
        Assert.That(done.IsOver, Is.True);
        Assert.That(done.Result.IsSweep, Is.True);
        Assert.That(done.Games.Count(game => game.Status == VersusGameStatus.Resolved), Is.EqualTo(4));
        Assert.That(done.Games.Skip(4).All(game => game.Status == VersusGameStatus.Pending), Is.True,
            "games five to seven were never needed and never issued an attempt");
        Assert.That(StoredAttemptCount(repository, id, 4), Is.Zero);
        Assert.That(coordinator.IssueSimultaneousAttempts(id).Succeeded, Is.False);
    }

    // ---------------------------------------------------------------- pairing validation

    [Test]
    public void AResultCannotBeSubmittedForTheOtherParticipantsAttempt()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);

        // participants swapped against their attempts
        SubmissionOperation swapped = coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.Second.ParticipantId, Result(10)),
            new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.First.ParticipantId, Result(5)));

        Assert.That(swapped.Succeeded, Is.False);
        AssertNothingCompleted(id);
    }

    [Test]
    public void BothResultsCannotBeForTheSameParticipantOrTheSameAttempt()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        AttemptSubmission first = new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(10));

        Assert.That(coordinator.SubmitSimultaneousResults(id, first, first).Succeeded, Is.False, "same attempt twice");
        Assert.That(
            coordinator.SubmitSimultaneousResults(
                id,
                first,
                new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.First.ParticipantId, Result(5))).Succeeded,
            Is.False,
            "the same participant twice");
        AssertNothingCompleted(id);
    }

    [Test]
    public void AnUnknownOrForeignParticipantOrAttemptIsRefused()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        AttemptSubmission real = new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(10));

        SubmissionOperation stranger = coordinator.SubmitSimultaneousResults(
            id, real, new AttemptSubmission(issued.Attempts.Second.Id, new ParticipantId("stranger"), Result(5)));
        SubmissionOperation unknownAttempt = coordinator.SubmitSimultaneousResults(
            id, real, new AttemptSubmission(new AttemptId("no-such-attempt"), issued.Attempts.Second.ParticipantId, Result(5)));

        Assert.That(stranger.Succeeded, Is.False);
        Assert.That(unknownAttempt.Succeeded, Is.False);
        AssertNothingCompleted(id);
    }

    [Test]
    public void AttemptsFromAnEarlierGameAreNotTheCurrentGamesAttempts()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation gameOne = coordinator.IssueSimultaneousAttempts(id);
        Assert.That(
            coordinator.SubmitSimultaneousResults(
                id,
                new AttemptSubmission(gameOne.Attempts.First.Id, gameOne.Attempts.First.ParticipantId, Result(10)),
                new AttemptSubmission(gameOne.Attempts.Second.Id, gameOne.Attempts.Second.ParticipantId, Result(5))).Succeeded,
            Is.True);
        SimultaneousAttemptOperation gameTwo = coordinator.IssueSimultaneousAttempts(id);

        // one of each game's attempts mixed together
        SubmissionOperation mixed = coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(gameOne.Attempts.First.Id, gameOne.Attempts.First.ParticipantId, Result(10)),
            new AttemptSubmission(gameTwo.Attempts.Second.Id, gameTwo.Attempts.Second.ParticipantId, Result(5)));

        Assert.That(mixed.Succeeded, Is.False);
        Assert.That(coordinator.Load(id).CurrentGame.Index, Is.EqualTo(1), "game two is still the current game");
    }

    [Test]
    public void ACompletedAttemptIsNeverSilentlyOverwritten()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf1);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        AttemptSubmission first = new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(10));
        AttemptSubmission second = new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.Second.ParticipantId, Result(5));
        Assert.That(coordinator.SubmitSimultaneousResults(id, first, second).Succeeded, Is.True);

        SubmissionOperation replay = coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(first.AttemptId, first.ParticipantId, Result(99)),
            second);

        Assert.That(replay.Succeeded, Is.False);
        VersusSeries stored = coordinator.Load(id);
        ParticipantGameView view = stored.Games[0].ViewFor(stored.Participants.First.Id, stored.Participants);
        Assert.That(view.OwnResult.Get(AttemptMetric.Score), Is.EqualTo(10f), "the recorded result is untouched by the replay");
    }

    [Test]
    public void AResultUnderADifferentRulesetVersionRejectsTheWholePairAndCompletesNothing()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        AttemptResult newerRules = new AttemptResult.Builder(ruleset.Id, ruleset.Version + 1)
            .Set(AttemptMetric.Score, 50)
            .Build();

        SubmissionOperation refused = coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(10)),
            new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.Second.ParticipantId, newerRules));
        SubmissionOperation wrongRuleset = coordinator.SubmitSimultaneousResults(
            id,
            new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(10)),
            new AttemptSubmission(
                issued.Attempts.Second.Id,
                issued.Attempts.Second.ParticipantId,
                new AttemptResult.Builder(new RulesetId("some-other-ruleset"), 1).Set(AttemptMetric.Score, 5).Build()));

        Assert.That(refused.Succeeded, Is.False);
        Assert.That(wrongRuleset.Succeeded, Is.False);
        AssertNothingCompleted(id);
    }

    [Test]
    public void TheDomainRefusesAPairWithoutTouchingTheAttemptsEvenInMemory()
    {
        // The coordinator reloads per call, so this is the domain's own guarantee: a refusal changes
        // nothing on the very object it was refused on.
        FakeVersusClock clock = new FakeVersusClock();
        SequentialVersusIdSource ids = new SequentialVersusIdSource();
        VersusSeries series = VersusTestFixtures.Series(
            SeriesFormat.BestOf3, ruleset, mode: VersusMode.LocalSimultaneous);
        SimultaneousAttempts pair = series.IssueSimultaneousAttempts(ids, clock);
        AttemptResult wrongVersion = new AttemptResult.Builder(ruleset.Id, 99).Set(AttemptMetric.Score, 1).Build();

        Assert.Throws<VersusDomainException>(() => series.SubmitSimultaneousResults(
            new AttemptSubmission(pair.First.Id, pair.First.ParticipantId, Result(10)),
            new AttemptSubmission(pair.Second.Id, pair.Second.ParticipantId, wrongVersion),
            clock));

        Assert.That(pair.First.IsCompleted, Is.False, "the valid half was not applied");
        Assert.That(pair.Second.IsCompleted, Is.False);
        Assert.That(series.CurrentGame.Status, Is.EqualTo(VersusGameStatus.Active));
    }

    // ---------------------------------------------------------------- atomic persistence

    [Test]
    public void APersistenceFailureLeavesNeitherResultDurableAndTheRetryIsSafe()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        AttemptSubmission first = new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(30));
        AttemptSubmission second = new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.Second.ParticipantId, Result(12));

        repository.FailNextSave = true;
        SubmissionOperation failed = coordinator.SubmitSimultaneousResults(id, first, second);

        Assert.That(failed.Succeeded, Is.False);
        Assert.That(failed.Validation.HasError(VersusValidationCode.PersistenceFailed), Is.True);
        VersusSeries afterFailure = coordinator.Load(id);
        Assert.That(afterFailure.ViewFor(afterFailure.Participants.First.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));
        Assert.That(afterFailure.ViewFor(afterFailure.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed),
            "not one half: the pair is durable together or not at all");
        Assert.That(afterFailure.Games[0].Status, Is.EqualTo(VersusGameStatus.Active));
        Assert.That(afterFailure.Score.FirstWins + afterFailure.Score.SecondWins, Is.Zero);

        SubmissionOperation retried = coordinator.SubmitSimultaneousResults(id, first, second);

        Assert.That(retried.Succeeded, Is.True, retried.Validation?.ToString());
        Assert.That(retried.Submission.GameResult.WinnerId, Is.EqualTo(retried.Series.Participants.First.Id));
        Assert.That(coordinator.Load(id).Score.FirstWins, Is.EqualTo(1), "counted exactly once");
    }

    [Test]
    public void NothingIsAnnouncedUntilTheSaveHasSucceeded()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf1);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        List<string> announced = new List<string>();
        coordinator.AttemptCompleted += (_, attempt) => announced.Add("attempt:" + attempt.ParticipantId);
        coordinator.GameResolved += (_, __) => announced.Add("game");
        coordinator.SeriesCompleted += _ => announced.Add("series");
        coordinator.SeriesAdvanced += (_, __) => announced.Add("advanced");

        AttemptSubmission first = new AttemptSubmission(issued.Attempts.First.Id, issued.Attempts.First.ParticipantId, Result(9));
        AttemptSubmission second = new AttemptSubmission(issued.Attempts.Second.Id, issued.Attempts.Second.ParticipantId, Result(4));

        repository.FailNextSave = true;
        Assert.That(coordinator.SubmitSimultaneousResults(id, first, second).Succeeded, Is.False);
        Assert.That(announced, Is.Empty, "a failed save announces nothing");

        Assert.That(coordinator.SubmitSimultaneousResults(id, first, second).Succeeded, Is.True);
        Assert.That(announced, Is.EqualTo(new[]
        {
            "attempt:" + first.ParticipantId,
            "attempt:" + second.ParticipantId,
            "game",
            "series"
        }));
    }

    [Test]
    public void AFailedPairedIssueLeavesNoHalfIssuedGameBehind()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);

        repository.FailNextSave = true;
        SimultaneousAttemptOperation failed = coordinator.IssueSimultaneousAttempts(id);

        Assert.That(failed.Succeeded, Is.False);
        Assert.That(failed.Validation.HasError(VersusValidationCode.PersistenceFailed), Is.True);
        Assert.That(StoredAttemptCount(repository, id, 0), Is.Zero, "no attempts without a durable pair");
        Assert.That(coordinator.IssueSimultaneousAttempts(id).Succeeded, Is.True);
        Assert.That(StoredAttemptCount(repository, id, 0), Is.EqualTo(2));
    }

    [Test]
    public void ASimultaneousSeriesRoundTripsThroughTheSerializerMidGameAndAfter()
    {
        SeriesId id = CreateSeries(SeriesFormat.BestOf3);
        SimultaneousAttemptOperation issued = coordinator.IssueSimultaneousAttempts(id);
        VersusSeries midGame = VersusSeriesSerializer.FromJson(VersusSeriesSerializer.ToJson(coordinator.Load(id), false));

        Assert.That(midGame.Mode, Is.EqualTo(VersusMode.LocalSimultaneous));
        Assert.That(midGame.Snapshot.GameAt(0).Supports(VersusCapability.LocalSimultaneous), Is.True,
            "the frozen ruleset keeps the capability it was played under");
        Assert.That(midGame.CanIssueSimultaneousAttempts(out _), Is.True);

        PlayGame(id, 10, 4);
        VersusSeries played = VersusSeriesSerializer.FromJson(VersusSeriesSerializer.ToJson(coordinator.Load(id), false));
        Assert.That(played.Score.FirstWins, Is.EqualTo(1));
        Assert.That(played.CurrentGame.Index, Is.EqualTo(1));
    }

    // ---------------------------------------------------------------- helpers

    private void AssertNothingCompleted(SeriesId id)
    {
        VersusSeries stored = coordinator.Load(id);
        foreach (MatchParticipant participant in new[] { stored.Participants.First, stored.Participants.Second })
        {
            Assert.That(
                stored.ViewFor(participant.Id).CurrentGame.OwnAttemptState,
                Is.Not.EqualTo(AttemptState.Completed),
                participant.DisplayName + "'s attempt must be untouched by a refused pair");
        }

        Assert.That(stored.CurrentGame.Status, Is.EqualTo(VersusGameStatus.Active));
    }
}

/// <summary>Reads counts out of a stored series document without a second copy of the schema.</summary>
internal static class VersusSeriesDocumentReader
{
    public static int AttemptCount(string json, int gameIndex)
    {
        VersusSeriesDocument document = UnityEngine.JsonUtility.FromJson<VersusSeriesDocument>(json);
        return document.games[gameIndex].attempts == null ? 0 : document.games[gameIndex].attempts.Length;
    }
}
