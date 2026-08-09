using Level5.Core.Match;
using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// Which competitive attempt, if any, the match now loading is playing.
///
/// The counterpart to <c>ActiveMatch</c>, and deliberately just as small. A gameplay scene must be
/// able to answer "am I part of a series?" without knowing what a series is, and a match that is not
/// part of one must behave exactly as it always has - which is why every field here is empty by
/// default and everything downstream treats empty as "an ordinary match".
///
/// The identity lives in the series document on disk, not here. This holds only the ids needed to
/// find it again, so losing this to a crash costs nothing: the attempt is still outstanding in the
/// document and is reissued on the next request.
///
/// It is tied to the exact match it was begun for. A player who abandons a competitive match to the
/// menu leaves the attempt outstanding, and without that tie the next ordinary match they played
/// would be submitted as their turn.
/// </summary>
public static class ActiveVersusAttempt
{
    private static MatchConfiguration launchedFor;

    /// <summary>The series this attempt belongs to, or none.</summary>
    public static SeriesId SeriesId { get; private set; }

    public static AttemptId AttemptId { get; private set; }

    public static ParticipantId ParticipantId { get; private set; }

    /// <summary>The rules this run is played under - the series' frozen version, not the catalog's.</summary>
    public static RulesetId RulesetId { get; private set; }

    public static int RulesetVersion { get; private set; }

    /// <summary>
    /// True when the match being played now is part of a series.
    ///
    /// "Now" is the important word. If another match has begun since this attempt was set up, the
    /// attempt belongs to a match the player walked away from, and this match is an ordinary one.
    /// </summary>
    public static bool IsActive => SeriesId.HasValue
        && AttemptId.HasValue
        && IsStillTheLaunchedMatch;

    /// <summary>
    /// Records that the next match is a competitive attempt. Called by the launch path immediately
    /// before the scene load, in the same breath as <c>ActiveMatch.Begin</c>.
    /// </summary>
    /// <param name="launchedMatch">
    /// The configuration this attempt was launched for. Passing null keeps the attempt current for
    /// whatever match is running, which is only appropriate when there is no launch to tie it to.
    /// </param>
    public static void Begin(SeriesId seriesId, Attempt attempt, MatchConfiguration launchedMatch = null)
    {
        if (!seriesId.HasValue || attempt == null)
        {
            Debug.LogError("ActiveVersusAttempt.Begin needs a series and an attempt; nothing was set.");
            return;
        }

        SeriesId = seriesId;
        AttemptId = attempt.Id;
        ParticipantId = attempt.ParticipantId;
        RulesetId = attempt.RulesetId;
        RulesetVersion = attempt.RulesetVersion;
        launchedFor = launchedMatch;
    }

    /// <summary>
    /// Whether the match currently loaded is still the one this attempt was launched for.
    ///
    /// Compared by reference, because <c>ActiveMatch.Begin</c> replaces the configuration object on
    /// every launch - so a different object is exactly what "a different match" means.
    /// </summary>
    private static bool IsStillTheLaunchedMatch =>
        launchedFor == null || ReferenceEquals(ActiveMatch.Configuration, launchedFor);

    /// <summary>
    /// Forgets the current attempt.
    ///
    /// Called once the result has been accepted and made durable, and when leaving to a menu. Never
    /// called on a submission failure - the retry needs these ids.
    /// </summary>
    public static void Clear()
    {
        // `default` rather than the types' None fields: these properties share their types' names,
        // and resolving that is a language corner nobody reading this should have to think about.
        SeriesId = default;
        AttemptId = default;
        ParticipantId = default;
        RulesetId = default;
        RulesetVersion = 0;
        launchedFor = null;
    }
}
