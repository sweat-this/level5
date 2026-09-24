using System;
using Assets.Scripts.database;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// The ownership-safe entry point for queuing a general Backend V2 match-result submission from
    /// an already locally-durable <see cref="HighScoreModel"/>.
    ///
    /// A pending result is only ever created when a Backend V2 session already exists at the moment
    /// the durable score is produced, and is owned by that exact player from then on - never created
    /// retroactively once some player later signs in, and never derived from V1/local identity
    /// (<c>GameOptions.userid</c>/<c>userName</c>). This is what stops a player from later claiming a
    /// match that was actually played without a Backend V2 identity.
    ///
    /// Lives next to <see cref="BackendV2MatchResultAdapter"/> in the default assembly for the same
    /// reason - see that type's doc comment.
    /// </summary>
    public static class BackendV2MatchResultSubmission
    {
        /// <summary>Call once, immediately after <paramref name="score"/> has become locally durable
        /// (SQLite or <c>PendingMatchPersistenceStore</c>) - never before. No-ops with no Backend V2
        /// session, a null score, or an already-logged adapter failure.
        ///
        /// Never lets an exception escape: this is a best-effort side effect of match-end/campaign
        /// persistence, called from <c>GameRules.SaveMatchResults</c> and
        /// <c>EndRoundMenuManager.saveGame</c> - the latter has no enclosing try/catch of its own, so
        /// an unhandled exception here would silently skip that method's remaining statements (the V1
        /// upload trigger and the <c>CampaignGameStats</c> component reset).</summary>
        public static void TryQueue(HighScoreModel score)
        {
            try
            {
                BackendV2Session session = BackendV2SessionStore.Current;
                if (session == null)
                {
                    return;
                }

                if (!BackendV2MatchResultAdapter.TryAdapt(score, out SubmitMatchResultDto request))
                {
                    return;
                }

                MatchResultSubmissionCoordinator.Enqueue(session.PlayerId, request);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "BackendV2MatchResultSubmission.TryQueue failed; this must never block or skip the "
                    + "caller's own remaining work. " + exception);
            }
        }
    }
}
