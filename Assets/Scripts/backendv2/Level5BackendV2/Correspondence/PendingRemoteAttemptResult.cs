using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// The exact metrics payload built for a remote attempt's result submission, kept until that
    /// submission definitively succeeds or is refused as a conflict.
    ///
    /// Exists because a failed submission's metrics dictionary would otherwise be local to
    /// <see cref="RemoteAttemptResultSubmitter"/>'s coroutine and lost the moment it returns - by the
    /// time a player is looking at a "resend result" prompt in the correspondence UI, the
    /// <c>GameStats</c> the metrics were built from may no longer exist. Retrying resends this exact
    /// payload; it is never rebuilt.
    /// </summary>
    public static class PendingRemoteAttemptResult
    {
        public static RemoteAttemptContext Context { get; private set; }

        public static IReadOnlyDictionary<string, double> Metrics { get; private set; }

        public static bool HasPending => Context != null;

        public static void Stash(RemoteAttemptContext context, IReadOnlyDictionary<string, double> metrics)
        {
            Context = context;
            Metrics = metrics;
        }

        public static void Clear()
        {
            Context = null;
            Metrics = null;
        }
    }
}
