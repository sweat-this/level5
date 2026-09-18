using System.Collections;
using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// Wires <see cref="BackendV2SessionPersistenceStore"/> to <see cref="BackendV2SessionStore"/>:
    /// hydrates a saved session, and keeps the on-disk copy in sync with every later change
    /// (including a silent background token refresh) for the rest of the process.
    ///
    /// Deliberately NOT a <c>[RuntimeInitializeOnLoadMethod]</c>. That was the first approach, and it
    /// broke: empirically, Unity 6000.5.7f1's batchmode EditMode test runner still fires
    /// <c>[RuntimeInitializeOnLoadMethod]</c> methods, and <see cref="Application.isPlaying"/> did not
    /// reliably read false while that ran, either - so the very first
    /// <c>BackendV2SessionStore.Set</c> call anywhere in the ~1600-test EditMode suite (there are
    /// several, in tests that have no idea persistence exists) started writing a real file to
    /// <c>Application.persistentDataPath</c> and reading it back for every later test that touched
    /// session state, which broke <c>Level5BackendV2SessionPersistenceTests</c>' "nothing persisted
    /// by default" assumption.
    ///
    /// <see cref="EnsureInitialized"/> is instead called explicitly, once, from
    /// <c>CorrespondenceScreenController.Awake</c> - the one real place issue #159 needs
    /// session-restore-on-open (per its own "reconnect/resume" requirement: restore happens at
    /// screen open, not at arbitrary app boot). An EditMode test never loads that scene, so this
    /// never runs during one; idempotent, so a scene reload or a second controller instance does not
    /// double-subscribe.
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
                BackendV2CoroutineHost.Instance.StartCoroutine(RefreshRestoredSession());
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

        private static IEnumerator RefreshRestoredSession()
        {
            // A stored access token's local expiry estimate cannot be trusted after a restart of
            // unknown length - force a real refresh before anything tries to use it. A failure here
            // (revoked, expired past what TryLoad already checked, server unreachable) clears the
            // session through the normal ForceRefresh -> SessionStore.Clear path, which also deletes
            // the now-invalid persisted file via the Changed handler above.
            yield return BackendV2Runtime.Session.ForceRefresh(_ => { });
        }
    }
}
