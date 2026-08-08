using System.Collections.Generic;
using Level5.Core.Match;
using NUnit.Framework;

/// <summary>
/// The match phase machine.
///
/// The property under test throughout is that the transition into Ending happens exactly once. The
/// clock, a cleared marker, a death and the pause menu can all decide a match is over in the same
/// frame; if more than one of them got through, the end-of-match work would run more than once and
/// a score would be saved twice.
/// </summary>
public class Level5MatchLifecycleTests
{
    [Test]
    public void AMatchStartsPreparingAndGoesLiveOnBeginPlay()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();

        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Preparing));
        Assert.That(lifecycle.BeginPlay(), Is.True);
        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Playing));
        Assert.That(lifecycle.IsPlaying, Is.True);
    }

    [Test]
    public void OnlyTheFirstEndRequestIsAccepted()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();
        lifecycle.BeginPlay();

        Assert.That(lifecycle.RequestEnd(MatchEndReason.TimeExpired), Is.True);
        Assert.That(lifecycle.RequestEnd(MatchEndReason.PlayerDied), Is.False);
        Assert.That(lifecycle.RequestEnd(MatchEndReason.ObjectiveComplete), Is.False);
    }

    [Test]
    public void RepeatedEndRequestsRaiseTheEndingEventOnce()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();
        List<MatchEndReason> raised = new List<MatchEndReason>();
        lifecycle.Ending += reason => raised.Add(reason);
        lifecycle.BeginPlay();

        lifecycle.RequestEnd(MatchEndReason.TimeExpired);
        lifecycle.RequestEnd(MatchEndReason.PlayerDied);

        Assert.That(raised.Count, Is.EqualTo(1));
        Assert.That(raised[0].Cause, Is.EqualTo(MatchEndCause.TimeExpired));
    }

    [Test]
    public void TheReasonKeptIsTheOneThatEndedTheMatch()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();
        lifecycle.BeginPlay();

        lifecycle.RequestEnd(new MatchEndReason(MatchEndCause.LastPlayerStanding, "everyone else down"));
        lifecycle.RequestEnd(MatchEndReason.TimeExpired);

        Assert.That(lifecycle.EndReason.Cause, Is.EqualTo(MatchEndCause.LastPlayerStanding));
        Assert.That(lifecycle.EndReason.Detail, Is.EqualTo("everyone else down"));
    }

    [Test]
    public void EndWorkThatHasNotFinishedLeavesTheMatchEnding()
    {
        // The retry path: HandleMatchEnded only reports completion once persistence, progression
        // and the campaign transition have all succeeded.
        MatchLifecycle lifecycle = new MatchLifecycle();
        lifecycle.BeginPlay();
        lifecycle.RequestEnd(MatchEndReason.TimeExpired);

        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Ending));
        Assert.That(lifecycle.IsOver, Is.True, "the match is over even while its end work retries");
    }

    [Test]
    public void CompletingEndWorkFinishesTheMatchExactlyOnce()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();
        int completions = 0;
        lifecycle.Completed += _ => completions++;
        lifecycle.BeginPlay();
        lifecycle.RequestEnd(MatchEndReason.TimeExpired);

        Assert.That(lifecycle.CompleteEnd(), Is.True);
        Assert.That(lifecycle.CompleteEnd(), Is.False);
        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Completed));
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void EndWorkCannotBeCompletedBeforeTheMatchHasEnded()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();
        lifecycle.BeginPlay();

        Assert.That(lifecycle.CompleteEnd(), Is.False);
        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Playing));
    }

    [Test]
    public void AMatchCannotGoBackToPlayingAfterItHasEnded()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();
        lifecycle.BeginPlay();
        lifecycle.RequestEnd(MatchEndReason.TimeExpired);

        Assert.That(lifecycle.BeginPlay(), Is.False);
        Assert.That(lifecycle.BeginCountdown(), Is.False);
        Assert.That(lifecycle.Phase, Is.EqualTo(MatchPhase.Ending));
    }

    // ---- when a match is over -----------------------------------------------------------------
    // These were conditions buried in Timer.Update and GameRules.IsGameOver, tangled up with null
    // guards and singleton lookups. They are the same rules; they can just be asked directly now.

    [Test]
    public void AClockAtZeroWaitsForAShotAlreadyInTheAir()
    {
        Assert.That(
            MatchEndConditions.TimeExpired(false, ballThrown: true, playerGrounded: true, consecutiveShotsMade: 0),
            Is.False,
            "a shot in flight still counts");
    }

    [Test]
    public void AClockAtZeroWaitsForAPlayerToLand()
    {
        Assert.That(
            MatchEndConditions.TimeExpired(false, ballThrown: false, playerGrounded: false, consecutiveShotsMade: 0),
            Is.False,
            "a player mid-jump gets to land");
    }

    [Test]
    public void AClockAtZeroEndsTheMatchOnceTheBallIsDownAndThePlayerIsGrounded()
    {
        Assert.That(
            MatchEndConditions.TimeExpired(false, ballThrown: false, playerGrounded: true, consecutiveShotsMade: 0),
            Is.True);
    }

    [TestCase(0, true)]
    [TestCase(2, true)]
    [TestCase(3, false)]
    [TestCase(9, false)]
    public void AConsecutiveShotsModeLetsALiveStreakPlayPastZero(int streak, bool expectedToEnd)
    {
        // Three, not two - the comment beside the original condition said two and the code said
        // three. Pinned as authored; changing it is a gameplay decision.
        Assert.That(
            MatchEndConditions.TimeExpired(true, ballThrown: false, playerGrounded: true, consecutiveShotsMade: streak),
            Is.EqualTo(expectedToEnd));
    }

    [Test]
    public void AConsecutiveShotsModeIgnoresTheBallAndTheGround()
    {
        // The streak is the only thing that matters in this mode; the polite-clock rule does not
        // apply to it.
        Assert.That(
            MatchEndConditions.TimeExpired(true, ballThrown: true, playerGrounded: false, consecutiveShotsMade: 0),
            Is.True);
    }

    [Test]
    public void AContestEndsWhenItsLastMarkerIsCleared()
    {
        Assert.That(MatchEndConditions.MarkersCleared(2), Is.False);
        Assert.That(MatchEndConditions.MarkersCleared(0), Is.True);
        Assert.That(MatchEndConditions.MarkersCleared(-1), Is.True, "never wedge on an over-decrement");
    }

    [Test]
    public void TheEndReasonSaysWhyTheClockEndedIt()
    {
        Assert.That(MatchEndConditions.TimeExpiredReason(false).Cause, Is.EqualTo(MatchEndCause.TimeExpired));
        Assert.That(MatchEndConditions.TimeExpiredReason(true).Detail, Is.Not.Empty);
    }

    [Test]
    public void PhaseChangesAreReportedInOrder()
    {
        MatchLifecycle lifecycle = new MatchLifecycle();
        List<MatchPhase> phases = new List<MatchPhase>();
        lifecycle.PhaseChanged += phase => phases.Add(phase);

        lifecycle.BeginCountdown();
        lifecycle.BeginPlay();
        lifecycle.RequestEnd(MatchEndReason.ObjectiveComplete);
        lifecycle.CompleteEnd();

        Assert.That(phases, Is.EqualTo(new[]
        {
            MatchPhase.Countdown,
            MatchPhase.Playing,
            MatchPhase.Ending,
            MatchPhase.Completed
        }));
    }
}
