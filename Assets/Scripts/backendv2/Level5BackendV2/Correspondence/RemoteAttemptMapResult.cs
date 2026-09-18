using Level5.Core.Match;

namespace Level5.BackendV2
{
    /// <summary>The outcome of mapping an <see cref="AttemptDescriptorDto"/> to a playable match.
    /// A failure here must happen before any scene load is attempted.</summary>
    public readonly struct RemoteAttemptMapResult
    {
        private RemoteAttemptMapResult(
            bool succeeded, MatchConfiguration configuration, RemoteAttemptContext context, string error)
        {
            Succeeded = succeeded;
            Configuration = configuration;
            Context = context;
            Error = error;
        }

        public bool Succeeded { get; }

        public MatchConfiguration Configuration { get; }

        public RemoteAttemptContext Context { get; }

        /// <summary>A clear, player/log-safe reason. Null on success.</summary>
        public string Error { get; }

        public static RemoteAttemptMapResult Success(MatchConfiguration configuration, RemoteAttemptContext context)
        {
            return new RemoteAttemptMapResult(true, configuration, context, null);
        }

        public static RemoteAttemptMapResult Failure(string error)
        {
            return new RemoteAttemptMapResult(false, null, null, error);
        }
    }
}
