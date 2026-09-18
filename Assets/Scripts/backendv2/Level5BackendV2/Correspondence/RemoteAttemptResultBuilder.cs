using System;
using System.Collections.Generic;
using Level5.Core.Versus;

namespace Level5.BackendV2
{
    /// <summary>
    /// Converts a local, positionally-indexed <see cref="AttemptResult"/> into the named-metric
    /// dictionary Backend V2's <c>CompleteAttemptDto</c> expects.
    ///
    /// The two enums share every member name by design
    /// (<see cref="AttemptMetric"/> here, Backend V2's <c>ResultMetric</c> on the wire), so the
    /// mapping is a name lookup, not a translation table to keep in sync by hand.
    /// </summary>
    public static class RemoteAttemptResultBuilder
    {
        /// <summary>Builds exactly the metrics the descriptor required - no more, no less. Throws if
        /// a required name is not a metric this build knows; the caller
        /// (<see cref="RemoteAttemptDescriptorMapper"/>) already refuses a descriptor with an
        /// unrecognized required metric before a match is ever launched, so this should never fire
        /// in practice.</summary>
        public static IReadOnlyDictionary<string, double> BuildMetrics(
            AttemptResult result, IReadOnlyList<string> requiredResultMetrics)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            Dictionary<string, double> metrics = new Dictionary<string, double>();
            foreach (string metricName in requiredResultMetrics ?? Array.Empty<string>())
            {
                if (!Enum.TryParse(metricName, ignoreCase: false, out AttemptMetric metric))
                {
                    throw new InvalidOperationException(
                        $"required result metric '{metricName}' is not known to this build");
                }

                metrics[metricName] = result.Get(metric);
            }

            return metrics;
        }
    }
}
