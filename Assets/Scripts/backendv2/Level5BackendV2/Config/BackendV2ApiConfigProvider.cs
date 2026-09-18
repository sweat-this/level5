namespace Level5.BackendV2
{
    /// <summary>
    /// The Backend V2 config in effect right now.
    ///
    /// A static composition root rather than a DI-registered service, matching this codebase's
    /// existing convention (<c>VersusRuntime</c>, <c>MatchCatalogs</c>, <c>VersusCatalogs</c>):
    /// lazily-built default, an explicit <see cref="Override"/> for tests and build configuration,
    /// and a <see cref="Reset"/> so tests never leak state into one another.
    /// </summary>
    public static class BackendV2ApiConfigProvider
    {
        private static BackendV2ApiConfig current;

        public static BackendV2ApiConfig Current => current ??= BackendV2ApiConfig.Development();

        /// <summary>Points the whole game at a specific config. Build configuration and tests use this.</summary>
        public static void Override(BackendV2ApiConfig config)
        {
            current = config;
        }

        /// <summary>Back to the lazily-built default on next access.</summary>
        public static void Reset()
        {
            current = null;
        }
    }
}
