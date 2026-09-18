using Level5.Core.Match;

namespace Level5.BackendV2
{
    /// <summary>The outcome of <see cref="RemoteAttemptLauncher"/> trying to start and launch a
    /// remote attempt. The Backend V2 counterpart to <c>VersusLaunch</c>.</summary>
    public readonly struct RemoteAttemptLaunch
    {
        private RemoteAttemptLaunch(bool succeeded, MatchConfiguration configuration, string error)
        {
            Succeeded = succeeded;
            Configuration = configuration;
            Error = error;
        }

        public bool Succeeded { get; }

        public MatchConfiguration Configuration { get; }

        public string Error { get; }

        public static RemoteAttemptLaunch Success(MatchConfiguration configuration)
        {
            return new RemoteAttemptLaunch(true, configuration, null);
        }

        public static RemoteAttemptLaunch Failure(string error)
        {
            return new RemoteAttemptLaunch(false, null, string.IsNullOrEmpty(error)
                ? "the remote attempt could not be launched"
                : error);
        }
    }
}
