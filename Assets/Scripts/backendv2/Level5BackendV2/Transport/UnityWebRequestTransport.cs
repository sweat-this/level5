using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace Level5.BackendV2
{
    /// <summary>
    /// The real <see cref="IApiTransport"/>, built on <c>UnityWebRequest</c>.
    ///
    /// Unlike legacy <c>APIHelper</c>, there is no global request lock here - each call gets its own
    /// <c>UnityWebRequest</c> and requests may run concurrently. Safe retry behavior belongs in typed
    /// clients for the specific operations Backend V2 designed for retry, not in a blanket transport
    /// policy here.
    /// </summary>
    public sealed class UnityWebRequestTransport : IApiTransport
    {
        private readonly BackendV2ApiConfig config;
        private readonly Func<string> accessTokenProvider;

        /// <param name="accessTokenProvider">
        /// Returns the current access token, or null/empty if there is none. Passed in rather than
        /// depending on the session manager directly, so the transport stays a pure HTTP concern.
        /// </param>
        public UnityWebRequestTransport(BackendV2ApiConfig config, Func<string> accessTokenProvider)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.accessTokenProvider = accessTokenProvider ?? (() => null);
        }

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            string url = BuildUrl(request);

            using UnityWebRequest webRequest = new UnityWebRequest(url, MethodName(request.Method));
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            string correlationId = CorrelationIdGenerator.NewId();

            try
            {
                if (request.Body != null)
                {
                    byte[] payload = Encoding.UTF8.GetBytes(BackendV2Json.Serialize(request.Body));
                    webRequest.uploadHandler = new UploadHandlerRaw(payload);
                    webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                }

                webRequest.SetRequestHeader("Accept", "application/json");
                webRequest.SetRequestHeader("X-Correlation-Id", correlationId);
                webRequest.timeout = config.RequestTimeoutSeconds;

                if (request.RequiresAuth)
                {
                    string token = accessTokenProvider();
                    if (!string.IsNullOrEmpty(token))
                    {
                        webRequest.SetRequestHeader("Authorization", "Bearer " + token);
                    }
                }

                yield return webRequest.SendWebRequest();

                RawApiResponse response = ToRawResponse(webRequest, correlationId);
                completed?.Invoke(response);
            }
            finally
            {
                webRequest.uploadHandler?.Dispose();
                webRequest.downloadHandler?.Dispose();
            }
        }

        private string BuildUrl(ApiRequest request)
        {
            Uri combined = new Uri(config.BaseUri, request.RelativePath);
            if (request.Query.Count == 0)
            {
                return combined.ToString();
            }

            StringBuilder builder = new StringBuilder(combined.ToString());
            builder.Append('?');
            bool first = true;
            foreach (KeyValuePair<string, string> entry in request.Query)
            {
                if (entry.Value == null)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append('&');
                }

                builder.Append(UnityWebRequest.EscapeURL(entry.Key));
                builder.Append('=');
                builder.Append(UnityWebRequest.EscapeURL(entry.Value));
                first = false;
            }

            return builder.ToString();
        }

        private static string MethodName(ApiHttpMethod method)
        {
            switch (method)
            {
                case ApiHttpMethod.Get:
                    return UnityWebRequest.kHttpVerbGET;
                case ApiHttpMethod.Post:
                    return UnityWebRequest.kHttpVerbPOST;
                case ApiHttpMethod.Patch:
                    return "PATCH";
                case ApiHttpMethod.Delete:
                    return UnityWebRequest.kHttpVerbDELETE;
                default:
                    throw new ArgumentOutOfRangeException(nameof(method), method, "unhandled HTTP method");
            }
        }

        private static RawApiResponse ToRawResponse(UnityWebRequest webRequest, string correlationId)
        {
            // UnityWebRequest has no dedicated timeout result: a timeout surfaces as a
            // ConnectionError whose message says so. ProtocolError means the server answered with
            // an error status code (4xx/5xx) - that is a completed response, not a transport failure.
            if (webRequest.result == UnityWebRequest.Result.ConnectionError
                || webRequest.result == UnityWebRequest.Result.DataProcessingError)
            {
                bool timedOut = webRequest.error != null
                    && webRequest.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
                return timedOut ? RawApiResponse.Timeout(correlationId) : RawApiResponse.NetworkError(correlationId);
            }

            return RawApiResponse.Completed(
                webRequest.responseCode,
                webRequest.downloadHandler?.text,
                ExtractHeaders(webRequest),
                correlationId);
        }

        private static IReadOnlyDictionary<string, string> ExtractHeaders(UnityWebRequest webRequest)
        {
            return webRequest.GetResponseHeaders() ?? EmptyHeaders;
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>();
    }
}
