namespace Level5.BackendV2
{
    /// <summary>
    /// The RFC 7807 ProblemDetails body Backend V2 sends back for every non-2xx response, mirrored
    /// field-for-field from <c>Level5.Api.ErrorHandling.ApiExceptionHandler</c>.
    ///
    /// Never surface <see cref="Title"/> or <see cref="Code"/> to the player as-is for a 500 -
    /// the server already scrubs those to a generic message for that status, but this type carries
    /// whatever it was sent so a caller can decide, not so it can be trusted blindly.
    /// </summary>
    public sealed class ApiProblem
    {
        public ApiProblem(int status, string title, string type, string code, string traceId)
        {
            Status = status;
            Title = title;
            Type = type;
            Code = code;
            TraceId = traceId;
        }

        public int Status { get; }

        public string Title { get; }

        public string Type { get; }

        /// <summary>The stable, machine-readable error code (e.g. "not_found", "conflict").</summary>
        public string Code { get; }

        public string TraceId { get; }

        /// <summary>Used when the body could not be parsed as ProblemDetails at all (e.g. an empty body,
        /// a proxy error page, or a network failure with no server response).</summary>
        public static ApiProblem Unknown(int status)
        {
            return new ApiProblem(status, title: null, type: null, code: null, traceId: null);
        }
    }
}
