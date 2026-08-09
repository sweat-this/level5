using UnityEngine;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Diagnostics for the handful of moments that decide a competition.
    ///
    /// One line per state change, never per frame: a series creates, advances and completes a few
    /// times a session, so logging all of it costs nothing and makes "why does the score say 3-1"
    /// answerable from a device log. Every line carries the series id, because the question is
    /// always about one particular series.
    ///
    /// Nothing here logs a result value. A log is a place a sealed attempt would leak from, and on a
    /// shared device the log is readable.
    /// </summary>
    public static class VersusLog
    {
        private const string Prefix = "[versus] ";

        /// <summary>Turned off in a build that does not want the noise. On by default.</summary>
        public static bool Enabled = true;

        public static void SeriesCreated(VersusSeries series, SeriesRequest request)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.Log(
                $"{Prefix}series {series.Id} created: {series.Participants}, {series.Snapshot.Format}, "
                + $"{series.Mode}, {series.Snapshot.InformationPolicy}, from {request.Source}");
        }

        public static void AttemptIssued(VersusSeries series, Attempt attempt)
        {
            if (!Enabled || attempt == null)
            {
                return;
            }

            Debug.Log(
                $"{Prefix}series {series.Id} game {attempt.GameIndex + 1}: attempt {attempt.Id} issued to "
                + $"{attempt.ParticipantId} under {attempt.RulesetId.Value} v{attempt.RulesetVersion}");
        }

        public static void AttemptStarted(VersusSeries series, Attempt attempt)
        {
            if (!Enabled || attempt == null)
            {
                return;
            }

            Debug.Log($"{Prefix}series {series.Id}: attempt {attempt.Id} started");
        }

        public static void AttemptCompleted(VersusSeries series, Attempt attempt)
        {
            if (!Enabled || attempt == null)
            {
                return;
            }

            Debug.Log(
                $"{Prefix}series {series.Id} game {attempt.GameIndex + 1}: attempt {attempt.Id} completed by "
                + $"{attempt.ParticipantId}");
        }

        public static void GameResolved(VersusSeries series, VersusGame game)
        {
            if (!Enabled || game == null)
            {
                return;
            }

            Debug.Log(
                $"{Prefix}series {series.Id} game {game.Number} resolved: {game.Result}. Series now {series.Score}");
        }

        public static void SeriesAdvanced(VersusSeries series, VersusGame game)
        {
            if (!Enabled || game == null)
            {
                return;
            }

            Debug.Log(
                $"{Prefix}series {series.Id} advanced to game {game.Number} ({game.Ruleset.Id.Value} "
                + $"v{game.Ruleset.Version})");
        }

        public static void SeriesCompleted(VersusSeries series)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.Log($"{Prefix}series {series.Id} completed: {series.Result}");
        }

        public static void SeriesRestored(VersusSeries series)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.Log(
                $"{Prefix}series {series.Id} restored: {series.Status}, {series.Score}, "
                + $"current game {(series.CurrentGame == null ? 0 : series.CurrentGame.Number)}");
        }

        public static void SubmissionRejected(SeriesId seriesId, AttemptId attemptId, string reason)
        {
            if (!Enabled)
            {
                return;
            }

            // A warning rather than an error: a rejected submission is the system working. The
            // caller decides whether its own situation is worse than that.
            Debug.LogWarning($"{Prefix}series {seriesId}: attempt {attemptId} was refused - {reason}");
        }
    }
}
