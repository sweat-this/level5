using System;
using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.database;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// Maps the game's existing durable score snapshot (<see cref="HighScoreModel"/>) onto Backend
    /// V2's general match-result wire contract (<see cref="SubmitMatchResultDto"/>).
    ///
    /// Lives next to <c>RemoteAttemptLauncher</c> in the default assembly rather than inside
    /// <c>Level5.BackendV2.asmdef</c>: <see cref="HighScoreModel"/> has no assembly definition of its
    /// own, and a custom assembly definition cannot reference the implicit default assembly. The
    /// <c>Level5.BackendV2</c> namespace is kept for discoverability alongside the rest of the
    /// Backend V2 client even though this file compiles elsewhere.
    ///
    /// Purely a field mapping - no network, no session state, no local persistence. Deliberately
    /// does not reproduce Backend V2's own mode-to-ranking-metric leaderboard policy: the server
    /// decides which metric a mode ranks by, and this always sends all six supported metrics.
    /// </summary>
    public static class BackendV2MatchResultAdapter
    {
        /// <summary>
        /// Adapts <paramref name="score"/> into a request ready to queue/submit, reusing
        /// <see cref="HighScoreModel.Scoreid"/> as the wire <c>ClientResultId</c> - never a second,
        /// independently-generated id. Returns false (and logs an actionable diagnostic, never an
        /// exception) when <paramref name="score"/> is null or its <c>Scoreid</c> is not a valid GUID:
        /// both are treated as a local contract violation, not something to paper over with an
        /// invented id.
        /// </summary>
        public static bool TryAdapt(HighScoreModel score, out SubmitMatchResultDto request)
        {
            request = null;
            if (score == null)
            {
                Debug.LogError("BackendV2MatchResultAdapter was given a null HighScoreModel.");
                return false;
            }

            if (!Guid.TryParse(score.Scoreid, out Guid clientResultId))
            {
                Debug.LogError(
                    "BackendV2MatchResultAdapter could not parse HighScoreModel.Scoreid ('"
                    + score.Scoreid + "') as a GUID for mode " + score.Modeid + " / level " + score.Levelid
                    + ". This match result will not be submitted to Backend V2.");
                return false;
            }

            Dictionary<string, double> metrics = new Dictionary<string, double>
            {
                [MatchResultMetric.TotalPoints.ToString()] = score.TotalPoints,
                [MatchResultMetric.ShotsMade.ToString()] = score.MaxShotMade,
                [MatchResultMetric.TotalDistance.ToString()] = score.TotalDistance,
                [MatchResultMetric.CompletionTimeSeconds.ToString()] = score.Time,
                [MatchResultMetric.LongestStreak.ToString()] = score.ConsecutiveShots,
                [MatchResultMetric.EnemiesKilled.ToString()] = score.EnemiesKilled,
            };

            MatchResultModifiersDto modifiers = new MatchResultModifiersDto
            {
                Hardcore = score.HardcoreEnabled != 0,
                TrafficEnabled = score.TrafficEnabled != 0,
                EnemiesEnabled = score.EnemiesEnabled != 0,
                SniperEnabled = score.SniperEnabled != 0,
            };

            request = new SubmitMatchResultDto(
                clientResultId,
                score.Modeid,
                score.Levelid,
                score.Characterid.ToString(CultureInfo.InvariantCulture),
                score.Version,
                score.Platform,
                metrics,
                modifiers);
            return true;
        }
    }
}
