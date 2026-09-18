namespace Level5.BackendV2
{
    /// <summary>
    /// How a failed Backend V2 call is classified, so a caller can react without inspecting a raw
    /// status code or parsing a message string.
    /// </summary>
    public enum ApiErrorKind
    {
        /// <summary>400 - the request itself was invalid.</summary>
        Validation = 0,

        /// <summary>401 with no evidence the token was simply stale - missing/malformed credentials.</summary>
        Unauthenticated = 1,

        /// <summary>401 where refreshing and retrying once is the right response.</summary>
        Expired = 2,

        /// <summary>403 - authenticated, but not allowed to do this.</summary>
        Forbidden = 3,

        /// <summary>404 - not found, or hidden from this participant.</summary>
        NotFound = 4,

        /// <summary>409 - conflicts with server state (e.g. a non-idempotent retry, an illegal transition).</summary>
        Conflict = 5,

        /// <summary>429 - rate limited.</summary>
        RateLimited = 6,

        /// <summary>5xx - the server failed or is unavailable.</summary>
        ServerError = 7,

        /// <summary>No response reached the server at all (offline, DNS, connection refused, ...).</summary>
        Network = 8,

        /// <summary>The request timed out before a response arrived.</summary>
        Timeout = 9,

        /// <summary>A 2xx response whose body could not be parsed as the expected shape.</summary>
        MalformedResponse = 10
    }
}
