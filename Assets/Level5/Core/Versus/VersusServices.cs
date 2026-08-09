using System;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Where the domain gets "now".
    ///
    /// Injected rather than read from <c>DateTime.UtcNow</c> because a correspondence series is made
    /// of timestamps hours apart, and a test that cannot move time cannot check any of the
    /// behaviour that depends on them.
    /// </summary>
    public interface IVersusClock
    {
        DateTime UtcNow { get; }
    }

    /// <summary>
    /// Where the domain gets new ids.
    ///
    /// Injected for the same reason as the clock, plus one more: when a backend becomes
    /// authoritative it issues the ids, and this is the seam it slots into.
    /// </summary>
    public interface IVersusIdSource
    {
        SeriesId NewSeriesId();

        AttemptId NewAttemptId();
    }

    /// <summary>The real clock.</summary>
    public sealed class SystemVersusClock : IVersusClock
    {
        public static readonly SystemVersusClock Instance = new SystemVersusClock();

        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// Guid-backed ids, prefixed so a raw id in a log or a file name says what it identifies.
    ///
    /// Locally unique is enough today. When a server starts issuing ids it will use its own scheme
    /// and this becomes the offline fallback, which is why nothing parses meaning out of the string.
    /// </summary>
    public sealed class GuidVersusIdSource : IVersusIdSource
    {
        public static readonly GuidVersusIdSource Instance = new GuidVersusIdSource();

        public SeriesId NewSeriesId()
        {
            return new SeriesId("series-" + Guid.NewGuid().ToString("N"));
        }

        public AttemptId NewAttemptId()
        {
            return new AttemptId("attempt-" + Guid.NewGuid().ToString("N"));
        }
    }
}
