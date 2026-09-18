using UnityEngine;

namespace Level5.BackendV2
{
    /// <summary>
    /// A lazily-created, scene-persistent <c>MonoBehaviour</c> that runs Backend V2 coroutines which
    /// have no natural owning component - specifically, submitting a remote attempt's result from
    /// the match-end path, which must not block <c>GameRules</c>' own synchronous retry loop.
    /// </summary>
    internal sealed class BackendV2CoroutineHost : MonoBehaviour
    {
        private static BackendV2CoroutineHost instance;

        internal static BackendV2CoroutineHost Instance
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
