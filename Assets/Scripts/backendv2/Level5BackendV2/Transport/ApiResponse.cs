namespace Level5.BackendV2
{
    /// <summary>
    /// The outcome of one typed Backend V2 call.
    ///
    /// Same family as the legacy <c>ApiResult&lt;T&gt;</c>, but carrying a structured
    /// <see cref="ApiErrorKind"/> and, where the server sent one, the raw <see cref="ApiProblem"/> -
    /// so a caller can branch on what happened instead of pattern-matching a free-text string.
    /// </summary>
    public sealed class ApiResponse<T>
    {
        private ApiResponse(bool success, T value, ApiErrorKind? errorKind, ApiProblem problem, string correlationId)
        {
            Success = success;
            Value = value;
            ErrorKind = errorKind;
            Problem = problem;
            CorrelationId = correlationId;
        }

        public bool Success { get; }

        public T Value { get; }

        public ApiErrorKind? ErrorKind { get; }

        /// <summary>Present when the server returned a ProblemDetails body; null for a network/timeout failure.</summary>
        public ApiProblem Problem { get; }

        /// <summary>The client-generated id the underlying request was sent with, for matching a
        /// failure log to a specific call. Null for a response synthesized locally (e.g. a
        /// validation failure that never reached the network).</summary>
        public string CorrelationId { get; }

        public static ApiResponse<T> Ok(T value, string correlationId = null)
        {
            return new ApiResponse<T>(true, value, null, null, correlationId);
        }

        public static ApiResponse<T> Fail(ApiErrorKind kind, ApiProblem problem = null, string correlationId = null)
        {
            return new ApiResponse<T>(false, default, kind, problem, correlationId);
        }
    }
}
