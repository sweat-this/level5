using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// What came back from the wire, before it is mapped into a typed <see cref="ApiResponse{T}"/>.
    ///
    /// Kept separate from the transport itself so <see cref="ApiResponseMapper"/> can be tested with
    /// hand-built instances of this and never has to touch a real <c>UnityWebRequest</c>.
    /// </summary>
    public sealed class RawApiResponse
    {
        private RawApiResponse(
            long statusCode,
            string body,
            IReadOnlyDictionary<string, string> headers,
            bool isNetworkError,
            bool timedOut,
            string correlationId)
        {
            StatusCode = statusCode;
            Body = body;
            Headers = headers ?? EmptyHeaders;
            IsNetworkError = isNetworkError;
            TimedOut = timedOut;
            CorrelationId = correlationId;
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
            new Dictionary<string, string>();

        public long StatusCode { get; }

        public string Body { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }

        public bool IsNetworkError { get; }

        public bool TimedOut { get; }

        /// <summary>The client-generated id this request was sent with (the same value as the
        /// <c>X-Correlation-Id</c> header), so a caller can log it alongside a failure regardless
        /// of whether a server response - and therefore a server-side traceId - ever came back.</summary>
        public string CorrelationId { get; }

        public static RawApiResponse Completed(
            long statusCode,
            string body,
            IReadOnlyDictionary<string, string> headers = null,
            string correlationId = null)
        {
            return new RawApiResponse(statusCode, body, headers, isNetworkError: false, timedOut: false, correlationId);
        }

        public static RawApiResponse NetworkError(string correlationId = null)
        {
            return new RawApiResponse(0, null, null, isNetworkError: true, timedOut: false, correlationId);
        }

        public static RawApiResponse Timeout(string correlationId = null)
        {
            return new RawApiResponse(0, null, null, isNetworkError: false, timedOut: true, correlationId);
        }
    }
}
