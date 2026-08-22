using System.Collections.Generic;

/// <summary>
/// How far behind the leading opponent a CPU shooter is (#54).
///
/// Previously read from <c>GameLevelManager.currentHighScoreTotalPoints</c>, a field only refreshed
/// as a side effect of <c>getSortedGameStatsList()</c> - which is otherwise called for HUD sorting.
/// A CPU's comeback strategy should not depend on whether, or when, something else happened to ask
/// for the sorted list this frame. This reads the current authoritative <see cref="GameStats"/> of
/// every other valid participant directly instead.
///
/// Lives here, in <c>Assembly-CSharp</c> with no namespace, rather than alongside its sibling pure
/// types (<c>CpuShotSelectionPolicy</c>, <c>CpuSevenPointEligibility</c>) under
/// <c>Level5.Core</c>/<c>namespace Level5.Core</c>: it depends on <see cref="PlayerIdentifier"/> and
/// <see cref="GameStats"/>, both <c>Assembly-CSharp</c> types, and <c>Level5.Core</c> is deliberately
/// walled off from <c>Assembly-CSharp</c> so its arithmetic can eventually live behind a real
/// assembly boundary (see <c>ShooterAttributes</c>'s own doc comment). This is that boundary doing
/// its job, not an inconsistency to fix by moving the file.
///
/// Deliberately does not share code with <c>GameLevelManager.getSortedGameStatsList()</c>'s own
/// leader tracking, even though the two currently agree on every input (self excluded here always
/// equals the global leader excluding a leading CPU's own equal-or-greater score, in every case
/// that matters). They are independent by design: this is CPU AI's own narrow, allocation-free
/// question, not a leaderboard read, and coupling it to the HUD's sorted-list code would mean a
/// future change to *that* list's participant filtering (spectators, eliminated players, teams)
/// changes CPU shot strategy as a side effect. If the two ever need to diverge - e.g. a filtering
/// rule that should affect the scoreboard but not CPU comeback logic, or vice versa - that
/// independence is the point, not an accident to clean up.
/// </summary>
public static class CpuScoreDeficit
{
    /// <summary>
    /// max(0, leaderOpponentScore - cpuScore), where leaderOpponentScore is the highest
    /// <c>TotalPoints</c> among every other valid participant. Ignores null participants, <paramref
    /// name="self"/>, and participants with no <see cref="GameStats"/> - it does not assume roster
    /// index zero is the relevant opponent. Allocation-free.
    /// </summary>
    public static int Calculate(List<PlayerIdentifier> participants, PlayerIdentifier self, int cpuScore)
    {
        int leaderOpponentScore = 0;
        if (participants != null)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                PlayerIdentifier participant = participants[i];
                if (participant == null || participant == self || participant.gameStats == null)
                {
                    continue;
                }

                int score = participant.gameStats.Stats.TotalPoints;
                if (score > leaderOpponentScore)
                {
                    leaderOpponentScore = score;
                }
            }
        }

        int deficit = leaderOpponentScore - cpuScore;
        return deficit > 0 ? deficit : 0;
    }
}
