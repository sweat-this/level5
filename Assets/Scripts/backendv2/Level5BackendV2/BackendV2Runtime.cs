namespace Level5.BackendV2
{
    /// <summary>
    /// Where the game gets its Backend V2 clients.
    ///
    /// A composition root, not a service locator with opinions - same convention as
    /// <c>VersusRuntime</c>/<c>MatchCatalogs</c>. <see cref="Override"/> lets tests point every
    /// client at a fake transport with no live network or session state involved.
    /// </summary>
    public static class BackendV2Runtime
    {
        private static IApiTransport transport;
        private static IAuthApiClient auth;
        private static BackendV2SessionManager session;
        private static IPlayersApiClient players;
        private static IFriendsApiClient friends;
        private static ICorrespondenceApiClient correspondence;
        private static IMatchResultsApiClient matchResults;

        public static IApiTransport Transport => transport ??= new UnityWebRequestTransport(
            BackendV2ApiConfigProvider.Current, () => BackendV2SessionStore.Current?.AccessToken);

        public static IAuthApiClient Auth => auth ??= new AuthApiClient(Transport);

        public static BackendV2SessionManager Session => session ??= new BackendV2SessionManager(Auth);

        public static IPlayersApiClient Players => players ??= new PlayersApiClient(Transport, Session);

        public static IFriendsApiClient Friends => friends ??= new FriendsApiClient(Transport, Session);

        public static ICorrespondenceApiClient Correspondence =>
            correspondence ??= new CorrespondenceApiClient(Transport, Session);

        public static IMatchResultsApiClient MatchResults =>
            matchResults ??= new MatchResultsApiClient(Transport, Session);

        /// <summary>Points every client at a different transport (a fake, in tests) and rebuilds
        /// everything downstream of it so nothing keeps talking to the old one.</summary>
        public static void Override(IApiTransport apiTransport)
        {
            transport = apiTransport;
            auth = null;
            session = null;
            players = null;
            friends = null;
            correspondence = null;
            matchResults = null;
        }

        public static void Reset()
        {
            transport = null;
            auth = null;
            session = null;
            players = null;
            friends = null;
            correspondence = null;
            matchResults = null;
        }
    }
}
