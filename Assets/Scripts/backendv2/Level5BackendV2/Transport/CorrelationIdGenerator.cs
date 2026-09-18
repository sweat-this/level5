using System;

namespace Level5.BackendV2
{
    /// <summary>Generates the per-request client-side correlation id sent as <c>X-Correlation-Id</c>.
    /// A diagnostic aid for matching a client-side log line to a server-side trace, independent of
    /// the server's own <c>traceId</c> (which Backend V2 assigns itself from
    /// <c>HttpContext.TraceIdentifier</c> regardless of what the client sends).</summary>
    public static class CorrelationIdGenerator
    {
        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
