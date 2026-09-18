using System;
using System.Collections;

namespace Level5.BackendV2
{
    /// <summary>
    /// Sends one request and reports back what came over the wire.
    ///
    /// The seam between typed clients and the actual network. <see cref="UnityWebRequestTransport"/>
    /// is the real implementation; tests use an in-memory fake so client logic (auth headers,
    /// correlation ids, retry-on-expiry) can be exercised without a live server or a running
    /// PlayMode scene.
    /// </summary>
    public interface IApiTransport
    {
        IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed);
    }
}
