namespace Level5.BackendV2
{
    /// <summary>
    /// The raw shape Backend V2's <c>ApiExceptionHandler</c> writes for every non-2xx response:
    /// standard RFC 7807 fields plus its own "code" and "traceId" extensions. Deserialization target
    /// only - callers use <see cref="ApiProblem"/>.
    /// </summary>
    internal sealed class ProblemDetailsWireDto
    {
        public int Status { get; set; }

        public string Title { get; set; }

        public string Type { get; set; }

        public string Code { get; set; }

        public string TraceId { get; set; }
    }
}
