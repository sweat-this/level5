using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// What leaving a match mid-turn means for a series: the player who quits loses that game.
///
/// Only a deliberate exit from the pause menu goes through here. A load error or crash never does,
/// so the interrupted attempt stays outstanding and the turn can simply be retried. A no-op when
/// no series attempt is active, so ordinary matches are unaffected.
/// </summary>
public static class VersusQuitPolicy
{
    /// <summary>True while leaving or restarting would abandon a series turn in progress.</summary>
    public static bool TurnInProgress => ActiveVersusAttempt.IsActive;

    /// <summary>
    /// Gives the active turn's game to the opponent. Returns false if nothing was recorded (no
    /// active turn, or the save failed - in which case the attempt stays retryable).
    /// </summary>
    public static bool ForfeitActiveTurn()
    {
        if (!ActiveVersusAttempt.IsActive)
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
