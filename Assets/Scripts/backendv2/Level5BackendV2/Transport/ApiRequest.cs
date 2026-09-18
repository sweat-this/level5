using System.Collections.Generic;

namespace Level5.BackendV2
{
    /// <summary>
    /// One Backend V2 call, before it becomes an actual <c>UnityWebRequest</c>.
    ///
    /// A plain data holder so request-building can be tested (relative path resolution, JSON body
    /// serialization, auth-header decisions) without ever constructing a real
    /// <c>UnityWebRequest</c>.
    /// </summary>
    public sealed class ApiRequest
    {
        public ApiRequest(
            ApiHttpMethod method,
            string relativePath,
            object body = null,
            IReadOnlyDictionary<string, string> query = null,
            bool requiresAuth = false)
        {
            Method = method;
            RelativePath = relativePath;
            Body = body;
            Query = query ?? EmptyQuery;
            RequiresAuth = requiresAuth;
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyQuery =
            new Dictionary<string, string>();

        public ApiHttpMethod Method { get; }

        /// <summary>Path relative to the configured base URI, e.g. "api/v2/auth/login". No leading slash.</summary>
        public string RelativePath { get; }

        /// <summary>Serialized as the JSON request body. Null for a request with no body.</summary>
        public object Body { get; }

        public IReadOnlyDictionary<string, string> Query { get; }

        /// <summary>Whether the current access token should be sent as a bearer header.</summary>
        public bool RequiresAuth { get; }
    }
}
