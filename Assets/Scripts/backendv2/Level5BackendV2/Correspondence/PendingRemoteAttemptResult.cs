using System;
using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// The current Backend V2 player's pending remote-attempt result, if any - a thin, owner-scoped
    /// facade over <see cref="PendingRemoteAttemptResultStore"/>'s durable, disk-backed persistence.
    ///
    /// Exists so callers (<see cref="RemoteAttemptResultSubmitter"/>, <c>RemoteAttemptLauncher</c>,
    /// <c>CorrespondenceScreenController</c>) never need to know about disk persistence or owner
    /// bookkeeping directly - they see one pending-or-not slot for "the player currently signed in",
    /// exactly as before this became durable. Every read goes straight to
    /// <see cref="PendingRemoteAttemptResultStore"/> (no in-memory cache): cheap, always current, and
    /// automatically account-safe - switching <see cref="BackendV2SessionStore.Current"/> changes what
    /// this reports with no separate invalidation step and no risk of a stale cache leaking one
    /// player's pending result to another.
    ///
    /// A pending result belongs to whichever Backend V2 player owns it
    /// (<see cref="RemoteAttemptContext.PlayerId"/>, set when it was stashed) - it is visible here only
    /// while that same player is the one currently signed in. No session, or a different player's
    /// session, means <see cref="HasPending"/> is false: player B can never see, retry, or clear player
    /// A's pending result just by being the one who happens to be signed in.
    /// </summary>
    public static class PendingRemoteAttemptResult
    {
        public static bool HasPending => TryGetForCurrentOwner(out _, out _);

        public static RemoteAttemptContext Context =>
            TryGetForCurrentOwner(out RemoteAttemptContext context, out _) ? context : null;

        public static IReadOnlyDictionary<string, double> Metrics =>
            TryGetForCurrentOwner(out _, out IReadOnlyDictionary<string, double> metrics) ? metrics : null;

        /// <summary>Durably persists <paramref name="context"/>/<paramref name="metrics"/> as the
        /// pending result for <see cref="RemoteAttemptContext.PlayerId"/>, before any network
        /// submission is attempted. See <see cref="PendingRemoteAttemptResultEnqueueResult"/> for how
        /// a caller must react to each outcome - in particular, <c>ConflictingPayload</c>/<c>Failed</c>
        /// must never be followed by a <c>CompleteAttempt</c> call.</summary>
        public static PendingRemoteAttemptResultEnqueueResult Stash(
            RemoteAttemptContext context, IReadOnlyDictionary<string, double> metrics)
        {
            if (context == null)
            {
                return PendingRemoteAttemptResultEnqueueResult.Failed;
            }

            return PendingRemoteAttemptResultStore.Enqueue(context.PlayerId, context, metrics);
        }

        /// <summary>Removes exactly the pending entry owned by <paramref name="ownerPlayerId"/> for
        /// <paramref name="attemptId"/> - explicit rather than "whatever is currently pending", since
        /// the caller (<see cref="RemoteAttemptResultSubmitter"/>) is resolving one specific attempt's
        /// network outcome and the currently-signed-in player may have changed since that submission
        /// was sent.</summary>
        public static void Clear(Guid ownerPlayerId, Guid attemptId)
        {
            PendingRemoteAttemptResultStore.Remove(ownerPlayerId, attemptId);
        }

        private static bool TryGetForCurrentOwner(
            out RemoteAttemptContext context, out IReadOnlyDictionary<string, double> metrics)
        {
            context = null;
            metrics = null;

            Guid? owner = BackendV2SessionStore.Current?.PlayerId;
            if (owner == null)
            {
                return false;
            }

            if (!PendingRemoteAttemptResultStore.TryGetForOwner(owner.Value, out PendingRemoteAttemptResultEntry entry)
                || entry.Context == null)
            {
                return false;
            }

            context = entry.Context.ToContext();
            metrics = entry.Metrics;
            return true;
        }
    }
}
