namespace Level5.BackendV2
{
    /// <summary>
    /// Wires <see cref="BackendV2SessionPersistenceStore"/> to <see cref="BackendV2SessionStore"/>:
    /// hydrates a saved session, and keeps the on-disk copy in sync with every later change
    /// (including a silent background token refresh) for the rest of the process.
    ///
    /// Deliberately NOT a <c>[RuntimeInitializeOnLoadMethod]</c>. That was the first approach, and it
    /// broke: empirically, Unity 6000.5.7f1's batchmode EditMode test runner still fires
    /// <c>[RuntimeInitializeOnLoadMethod]</c> methods, and <c>Application.isPlaying</c> did not
    /// reliably read false while that ran, either - so the very first
    /// <c>BackendV2SessionStore.Set</c> call anywhere in the ~1600-test EditMode suite (there are
    /// several, in tests that have no idea persistence exists) started writing a real file to
    /// <c>Application.persistentDataPath</c> and reading it back for every later test that touched
    /// session state, which broke <c>Level5BackendV2SessionPersistenceTests</c>' "nothing persisted
    /// by default" assumption.
    ///
    /// <see cref="EnsureInitialized"/> is called explicitly from <c>UserAccountManager.Awake</c> -
    /// the earliest ordinary production composition seam, since the build's first scene is
    /// <c>level_00_account_loginLocal</c> - so any Backend V2 session persisted from a prior run is
    /// available application-wide, not only after a player opens Multiplayer. It is also still called
    /// from <c>CorrespondenceScreenController.Awake</c>, both because that call is what issue #159
    /// originally needed and because idempotency makes it a no-op safety net for any path that opens
    /// the correspondence screen without ever having gone through the local-account scene (e.g. a
    /// test loading the multiplayer scene directly). An EditMode test never loads either scene, so
    /// this never runs during one; idempotent, so a scene reload or a second caller does not
    /// double-subscribe.
    ///
    /// Performs zero network requests: only an in-memory event subscription and a synchronous local
    /// disk read. A restored session means the credentials are locally available for this process -
    /// not that the server has confirmed them this session. That confirmation is the existing
    /// authenticated-request pipeline's job (<see cref="AuthenticatedApiClientBase"/> ->
    /// <see cref="BackendV2SessionManager.EnsureFreshAccessToken"/>/<see cref="BackendV2SessionManager.ForceRefresh"/>),
    /// triggered by the first real request, not by restoration itself - so a temporary Backend V2
    /// outage at app launch cannot delete an otherwise locally valid persisted session.
    /// </summary>
    public static class BackendV2SessionPersistenceBootstrap
    {
        private static bool initialized;

        public static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            BackendV2SessionStore.Changed += OnSessionChanged;

            if (BackendV2SessionPersistenceStore.TryLoad(out BackendV2Session session))
            {
                BackendV2SessionStore.Set(session);
            }
        }

        private static void OnSessionChanged(BackendV2Session session)
        {
            if (session == null)
            {
                BackendV2SessionPersistenceStore.Clear();
            }
            else
            {
                BackendV2SessionPersistenceStore.Save(session);
            }
        }
    }
}
