using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// A lazily-created, scene-persistent <c>MonoBehaviour</c> that runs Backend V2 coroutines which
    /// have no natural owning component - submitting a remote attempt's result from the match-end
    /// path (must not block <c>GameRules</c>' own synchronous retry loop), and launching a remote
    /// attempt (<c>RemoteAttemptLauncher</c>, which lives in the default assembly alongside
    /// <c>VersusLauncher</c> and so needs this to be visible across the assembly boundary).
    /// </summary>
    public sealed class BackendV2CoroutineHost : MonoBehaviour
    {
        private static BackendV2CoroutineHost instance;

        public static BackendV2CoroutineHost Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                GameObject host = new GameObject("BackendV2CoroutineHost");
                Object.DontDestroyOnLoad(host);
                instance = host.AddComponent<BackendV2CoroutineHost>();
                return instance;
            }
        }
    }
}
