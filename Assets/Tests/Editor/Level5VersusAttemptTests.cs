using System;
using Level5.Core.Versus;
using NUnit.Framework;

/// <summary>
/// The attempt lifecycle.
///
/// This is where the retry exploit is closed. Everything else in the domain trusts that an attempt
/// completes exactly once, and this is the only place that promise is kept.
/// </summary>
public class Level5VersusAttemptTests
{
    private static readonly DateTime Noon = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void ANewAttemptIsCreatedAndCarriesEverythingATicketNeeds()
    {
        Attempt attempt = Issue();

        Assert.That(attempt.State, Is.EqualTo(AttemptState.Created));
        Assert.That(attempt.Id.Value, Is.EqualTo("attempt-1"));
        Assert.That(attempt.ParticipantId, Is.EqualTo(VersusTestFixtures.PatrickId));
        Assert.That(attempt.GameIndex, Is.EqualTo(0));
        Assert.That(attempt.RulesetId.Value, Is.EqualTo("most-points"));
        Assert.That(attempt.RulesetVersion, Is.EqualTo(1));
        Assert.That(attempt.IssuedAtUtc, Is.EqualTo(Noon));
        Assert.That(attempt.Result, Is.Null);
        Assert.That(attempt.IsLive, Is.True);
    }

    [Test]
    public void TheLegalRouteIsCreatedReadyStartedCompleted()
    {
        Attempt attempt = Issue();

        Assert.That(attempt.MarkReady(), Is.True);
        Assert.That(attempt.State, Is.EqualTo(AttemptState.Ready));

        Assert.That(attempt.Start(Noon.AddMinutes(1)), Is.True);
        Assert.That(attempt.State, Is.EqualTo(AttemptState.Started));

        attempt.Complete(Result(47), Noon.AddMinutes(3));

        Assert.That(attempt.State, Is.EqualTo(AttemptState.Completed));
        Assert.That(attempt.IsCompleted, Is.True);
        Assert.That(attempt.IsLive, Is.False);
        Assert.That(attempt.Result.Get(AttemptMetric.Score), Is.EqualTo(47));
        Assert.That(attempt.CompletedAtUtc, Is.EqualTo(Noon.AddMinutes(3)));
    }

    [Test]
    public void RepeatingATransitionIsANoOpRatherThanAnError()
    {
        // A double-tapped button and a scene reload during a run both land here, and neither is a
        // mistake worth failing on.
        Attempt attempt = Issue();

        attempt.MarkReady();
        Assert.That(attempt.MarkReady(), Is.False);

        attempt.Start(Noon);
        Assert.That(attempt.Start(Noon.AddMinutes(1)), Is.False);
        Assert.That(attempt.StartedAtUtc, Is.EqualTo(Noon), "the first start time is kept");
    }

    [Test]
    public void CompletingTwiceIsRefused()
    {
        // The retry exploit: play, see a bad score, play again, submit the better one. The second
        // submission has to fail loudly rather than overwrite.
        Attempt attempt = Issue();
        attempt.Complete(Result(12), Noon);

        Assert.Throws<VersusDomainException>(() => attempt.Complete(Result(99), Noon.AddMinutes(5)));
        Assert.That(attempt.Result.Get(AttemptMetric.Score), Is.EqualTo(12), "the first result stands");
    }

    [Test]
    public void AnAbandonedAttemptCannotBeCompleted()
    {
        Attempt attempt = Issue();
        Assert.That(attempt.Abandon(), Is.True);
        Assert.That(attempt.Abandon(), Is.False, "abandoning twice is a no-op");

        Assert.Throws<VersusDomainException>(() => attempt.Complete(Result(10), Noon));
    }

    [Test]
    public void ACompletedAttemptCannotBeAbandoned()
    {
        Attempt attempt = Issue();
        attempt.Complete(Result(10), Noon);

        Assert.Throws<VersusDomainException>(() => attempt.Abandon());
    }

    [Test]
    public void AnAbandonedAttemptCannotBeStarted()
    {
        Attempt attempt = Issue();
        attempt.Abandon();

        Assert.Throws<VersusDomainException>(() => attempt.Start(Noon));
    }

    [Test]
    public void AResultFromAnotherRulesetIsRefused()
    {
        Attempt attempt = Issue();
        CompetitiveRuleset other = VersusTestFixtures.ContestRuleset();

        Assert.Throws<VersusDomainException>(
            () => attempt.Complete(VersusTestFixtures.Result(other, 47), Noon));
        Assert.That(attempt.State, Is.EqualTo(AttemptState.Created), "a refused result changes nothing");
    }

    [Test]
    public void AResultFromAnotherVersionOfTheSameRulesetIsRefused()
    {
        // The series is being played under version 1. A build that has moved on to version 2 must
        // not be able to submit a version 2 score into it.
        Attempt attempt = Issue();
        CompetitiveRuleset version2 = VersusTestFixtures.ScoreRuleset(version: 2);

        Assert.Throws<VersusDomainException>(
            () => attempt.Complete(VersusTestFixtures.Result(version2, 47), Noon));
    }

    [Test]
    public void CompletingWithoutHavingStartedStillRecordsAStartTime()
    {
        // A run that finished obviously started. Leaving the start time empty would make an attempt
        // that was never started and one whose start was never recorded look the same.
        Attempt attempt = Issue();
        attempt.MarkReady();
        attempt.Complete(Result(30), Noon.AddMinutes(2));

        Assert.That(attempt.StartedAtUtc, Is.EqualTo(Noon.AddMinutes(2)));
    }

    [Test]
    public void AnAttemptWithoutTheThingsThatIdentifyItIsRefused()
    {
        Assert.Throws<VersusDomainException>(
            () => Attempt.Issue(default, VersusTestFixtures.PatrickId, 0, new RulesetId("x"), 1, Noon));

        Assert.Throws<VersusDomainException>(
            () => Attempt.Issue(new AttemptId("a"), default, 0, new RulesetId("x"), 1, Noon));

        Assert.Throws<VersusDomainException>(
            () => Attempt.Issue(new AttemptId("a"), VersusTestFixtures.PatrickId, -1, new RulesetId("x"), 1, Noon));

        Assert.Throws<VersusDomainException>(
            () => Attempt.Issue(new AttemptId("a"), VersusTestFixtures.PatrickId, 0, default, 1, Noon));

        Assert.Throws<VersusDomainException>(
            () => Attempt.Issue(new AttemptId("a"), VersusTestFixtures.PatrickId, 0, new RulesetId("x"), 0, Noon));
    }

    [Test]
    public void AStoredAttemptThatSaysItIsCompleteWithNoResultIsTreatedAsCorruption()
    {
        Assert.Throws<VersusDomainException>(
            () => Attempt.Restore(
                new AttemptId("attempt-1"),
                VersusTestFixtures.PatrickId,
                0,
                new RulesetId("most-points"),
                1,
                AttemptState.Completed,
                Noon,
                Noon,
                Noon,
                null));
    }

    [Test]
    public void AResultReadsBackMissingMetricsAsZeroRatherThanFailing()
    {
        // A document from an older build has a shorter metric array. Refusing it would lose a
        // competition over a metric that did not exist when it started.
        AttemptResult result = AttemptResult.FromValues(
            new RulesetId("most-points"),
            1,
            new[] { 47f });

        Assert.That(result.Get(AttemptMetric.Score), Is.EqualTo(47f));
        Assert.That(result.Get(AttemptMetric.TotalDistance), Is.EqualTo(0f));
    }

    [Test]
    public void TheBuilderWorksOutAccuracyRatherThanLettingEveryCallerDoIt()
    {
        AttemptResult result = new AttemptResult.Builder(new RulesetId("most-points"), 1)
            .SetShooting(made: 7, attempted: 10)
            .Build();

        Assert.That(result.Get(AttemptMetric.ShotsMade), Is.EqualTo(7f));
        Assert.That(result.Get(AttemptMetric.ShotsAttempted), Is.EqualTo(10f));
        Assert.That(result.Get(AttemptMetric.Accuracy), Is.EqualTo(70f));

        AttemptResult noShots = new AttemptResult.Builder(new RulesetId("most-points"), 1)
            .SetShooting(0, 0)
            .Build();

        Assert.That(noShots.Get(AttemptMetric.Accuracy), Is.EqualTo(0f), "no divide by zero");
    }

    private static Attempt Issue()
    {
        return Attempt.Issue(
            new AttemptId("attempt-1"),
            VersusTestFixtures.PatrickId,
            0,
            new RulesetId("most-points"),
            1,
            Noon);
    }

    private static AttemptResult Result(float score)
    {
        return new AttemptResult.Builder(new RulesetId("most-points"), 1)
            .Set(AttemptMetric.Score, score)
            .Build();
    }
}
