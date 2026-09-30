using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// What leaving a match mid-turn means for a series: the player who quits loses that game.
///
/// Only a deliberate exit from the pause menu goes through here. A load error or crash never does,
/// so the interrupted attempt stays outstanding and the turn can simply be retried. A no-op when
/// no series attempt is active, so ordinary matches are unaffected.
///
/// A quit only counts once it is durable. <see cref="TryPrepareForExplicitExit"/> is the gate every
/// deliberate exit passes through: it says "leave" only when nothing is left owing to the series
/// document, so an in-memory forfeit, or a finished run whose result has not been saved yet, can
/// never be walked away from into a free retake.
///
/// A simultaneous game fails closed instead. Two participants are playing one match, and a quit
/// from the pause menu does not say which of them is conceding, so nobody is forfeited: the exit is
/// refused until the game's result has been durably recorded (after which no attempt is active and
/// leaving is ordinary). A crash or process exit is an interruption, not a quit, and leaves both
/// attempts outstanding for the same retry as any other interrupted game.
/// </summary>
public static class VersusQuitPolicy
{
    /// <summary>
    /// True while the match is still being played for a series turn, so leaving it is a quit.
    /// False once the match has ended: the run is finished, and if its result is still waiting on a
    /// save retry the attempt stays outstanding rather than being forfeited.
    /// </summary>
    public static bool TurnInProgress => ActiveVersusAttempt.IsActive && !MatchHasEnded;

    /// <summary>True while an unreported series attempt exists, ended or not. A restart would retake it.</summary>
    public static bool AttemptOutstanding => ActiveVersusAttempt.IsActive;

    /// <summary>
    /// True while a simultaneous game is being played: two participants in one match, so no exit can
    /// be attributed to one of them. The pause menu reads this to say why leaving is unavailable
    /// instead of offering the single-participant "Quit Turn (Forfeit)".
    /// </summary>
    public static bool SimultaneousGameInProgress =>
        ActiveVersusAttempt.IsActive && ActiveVersusAttempt.IsSimultaneous && !MatchHasEnded;

    private static bool MatchHasEnded => MatchController.instance != null && MatchController.instance.IsOver;

    /// <summary>
    /// Whether the player may deliberately leave the match (Start/Menu, Quit) right now.
    ///
    /// <list type="bullet">
    /// <item>No series attempt is active: true. Nothing is owed, ordinary matches are untouched.</item>
    /// <item>A turn is still being played: the quit forfeits the game, and this is true only if that
    /// forfeit was durably saved. On a failed save the attempt stays outstanding, the caller must stay
    /// in the match, and a later call retries.</item>
    /// <item>A simultaneous game is being played: false, and nothing is forfeited. There is no
    /// participant to attribute the quit to.</item>
    /// <item>The match has already ended but its attempt is still outstanding: false. That is not a
    /// quit - the result is earned and <see cref="VersusMatchReporter"/> owns making it durable through
    /// the match-end retry loop. Nothing is forfeited here; once the reporter succeeds and clears the
    /// attempt this returns true.</item>
    /// </list>
    /// </summary>
    public static bool TryPrepareForExplicitExit()
    {
        if (!ActiveVersusAttempt.IsActive)
        {
            return true;
        }

        if (MatchHasEnded)
        {
            return false;
        }

        if (ActiveVersusAttempt.IsSimultaneous)
        {
            return false;
        }

        return ForfeitActiveTurn();
    }

    /// <summary>
    /// Gives the active turn's game to the opponent. Returns false if nothing was recorded (no
    /// active turn, a simultaneous game - which has no single player to forfeit - or a save that
    /// failed, in which case the attempt stays retryable).
    /// </summary>
    public static bool ForfeitActiveTurn()
    {
        if (!TurnInProgress || ActiveVersusAttempt.IsSimultaneous)
        {
            return false;
        }

        SeriesOperation operation = VersusRuntime.Coordinator.ForfeitGame(
            ActiveVersusAttempt.SeriesId,
            ActiveVersusAttempt.ParticipantId);

        if (!operation.Succeeded)
        {
            Debug.LogWarning("Could not record the forfeited turn: " + operation.Validation);
            return false;
        }

        ActiveVersusAttempt.Clear();
        return true;
    }
}
