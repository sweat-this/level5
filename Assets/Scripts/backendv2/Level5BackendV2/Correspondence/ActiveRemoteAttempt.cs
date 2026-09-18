using Level5.Core.Match;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// Which remote (Backend V2) correspondence attempt, if any, the match now loading is playing.
    ///
    /// The Backend V2 counterpart to <c>ActiveVersusAttempt</c>, same shape and same reason: a
    /// gameplay scene must be able to finish an ordinary match without knowing a remote series
    /// exists, and a player who abandons a remote attempt to the menu must not have their next
    /// ordinary match submitted as that turn.
    /// </summary>
    public static class ActiveRemoteAttempt
    {
        private static MatchConfiguration launchedFor;

        public static RemoteAttemptContext Context { get; private set; }

        public static bool IsActive => Context != null && IsStillTheLaunchedMatch;

        public static void Begin(RemoteAttemptContext context, MatchConfiguration launchedMatch)
        {
            if (context == null)
            {
                Debug.LogError("ActiveRemoteAttempt.Begin needs a context; nothing was set.");
                return;
            }

            Context = context;
            launchedFor = launchedMatch;
        }

        private static bool IsStillTheLaunchedMatch =>
            launchedFor == null || ReferenceEquals(ActiveMatch.Configuration, launchedFor);

        public static void Clear()
        {
            Context = null;
            launchedFor = null;
        }
    }
}
