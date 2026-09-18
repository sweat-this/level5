using System;
using System.Collections;
using System.Collections.Generic;

namespace Level5.BackendV2.Tests
{
    /// <summary>
    /// An in-memory <see cref="IApiTransport"/> for EditMode tests: records every request sent
    /// through it and answers with a queued or handler-produced <see cref="RawApiResponse"/>, with
    /// no real network call and no coroutine host required.
    /// </summary>
    public sealed class FakeApiTransport : IApiTransport
    {
        private readonly Queue<RawApiResponse> queuedResponses = new Queue<RawApiResponse>();

        public List<ApiRequest> Requests { get; } = new List<ApiRequest>();

        /// <summary>When set, takes priority over any queued response.</summary>
        public Func<ApiRequest, RawApiResponse> Handler { get; set; }

        public void Enqueue(RawApiResponse response)
        {
            queuedResponses.Enqueue(response);
        }

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);

            RawApiResponse response = Handler != null
                ? Handler(request)
                : queuedResponses.Count > 0
                    ? queuedResponses.Dequeue()
                    : RawApiResponse.NetworkError();

            completed?.Invoke(response);
            yield break;
        }
    }
}
