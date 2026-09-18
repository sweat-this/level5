using System;

namespace Level5.BackendV2
{
    /// <summary>
    /// Where and how the Backend V2 client talks to the server.
    ///
    /// Deliberately its own type rather than more entries in the legacy <c>Constants</c> class: that
    /// class is one absolute URL constant per endpoint with no environment concept at all, which does
    /// not scale to a typed client and has never had a dev/staging/prod split to begin with.
    ///
    /// Immutable and validated at construction, so an invalid config never gets far enough to make a
    /// request with it.
    /// </summary>
    public sealed class BackendV2ApiConfig
    {
        /// <summary>The Backend V2 Kestrel HTTPS dev port, from this project's own launchSettings.json.</summary>
        private const string DevelopmentBaseUri = "https://localhost:7029/";

        public BackendV2ApiConfig(
            Uri baseUri,
            BackendV2Environment environment,
            int requestTimeoutSeconds = 10,
            bool allowInsecureLocalhost = false)
        {
            if (baseUri == null)
            {
                throw new ArgumentException("a Backend V2 config needs a base URI", nameof(baseUri));
            }

            if (!baseUri.IsAbsoluteUri)
            {
                throw new ArgumentException(
                    $"the Backend V2 base URI '{baseUri}' must be absolute", nameof(baseUri));
            }

            bool isLocalHost = string.Equals(baseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(baseUri.Host, "127.0.0.1", StringComparison.Ordinal);

            bool isHttps = string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            bool isAllowedInsecureLocal = allowInsecureLocalhost && isLocalHost
                && string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);

            if (!isHttps && !isAllowedInsecureLocal)
            {
                throw new ArgumentException(
                    $"the Backend V2 base URI '{baseUri}' must use https "
                    + "(unless it is an explicitly allowed insecure localhost endpoint)",
                    nameof(baseUri));
            }

            if (requestTimeoutSeconds <= 0)
            {
                throw new ArgumentException(
                    "the Backend V2 request timeout must be greater than zero seconds",
                    nameof(requestTimeoutSeconds));
            }

            BaseUri = EnsureTrailingSlash(baseUri);
            Environment = environment;
            RequestTimeoutSeconds = requestTimeoutSeconds;
            AllowInsecureLocalhost = allowInsecureLocalhost;
        }

        /// <summary>
        /// A base URI without a trailing slash silently drops its last path segment when combined
        /// with a relative path (RFC 3986 "merge" semantics - <c>new Uri(new
        /// Uri("https://host/prefix"), "api/v2/x")</c> resolves to <c>https://host/api/v2/x</c>,
        /// dropping "/prefix"). That would misroute every request under a path-prefixed gateway or
        /// reverse proxy, silently, with no error at configuration time. Normalizing here means
        /// <see cref="UnityWebRequestTransport"/> can rely on simple relative combination.
        /// </summary>
        private static Uri EnsureTrailingSlash(Uri uri)
        {
            if (uri.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
            {
                return uri;
            }

            UriBuilder builder = new UriBuilder(uri) { Path = uri.AbsolutePath + "/" };
            return builder.Uri;
        }

        public Uri BaseUri { get; }

        public BackendV2Environment Environment { get; }

        public int RequestTimeoutSeconds { get; }

        public bool AllowInsecureLocalhost { get; }

        /// <summary>The default local-development config, pointed at this repo's own Backend V2 dev port.</summary>
        public static BackendV2ApiConfig Development()
        {
            return new BackendV2ApiConfig(
                new Uri(DevelopmentBaseUri),
                BackendV2Environment.Development,
                requestTimeoutSeconds: 10,
                allowInsecureLocalhost: true);
        }

        /// <summary>
        /// A config for a real deployment. There is no hardcoded staging/production URL to default
        /// to yet, so the caller (build configuration, not this class) supplies it explicitly.
        /// </summary>
        public static BackendV2ApiConfig Custom(
            Uri baseUri,
            BackendV2Environment environment,
            int requestTimeoutSeconds = 10)
        {
            return new BackendV2ApiConfig(baseUri, environment, requestTimeoutSeconds, allowInsecureLocalhost: false);
        }
    }
}
