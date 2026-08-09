using System.Collections.Generic;
using Level5.Core.Match;
using Level5.Core.Versus;

/// <summary>
/// The competitive rules for the modes that have them, as code.
///
/// This is authored data, not logic. It exists for the same reason
/// <c>GameModeDefinitionFactory</c> does: the authored <see cref="CompetitiveRulesetDefinition"/>
/// assets do not exist yet, and shipping a code registry means the new path is live now and cannot
/// drift from the shipping data while it waits for them. <c>VersusCatalogs</c> prefers assets
/// wherever they exist, so authoring one entry replaces one entry here.
///
/// It is a registry, not a dispatcher. Nothing branches on which mode it is looking at, and adding
/// a mode never means editing the coordinator, the series, or anything that decides an outcome -
/// only adding a row here, or an asset.
///
/// The two decisions worth stating, because both are gameplay judgements rather than mechanics:
///
/// - **A contest is scored on points, with time as the tie-break.** Not on time alone. These modes
///   end when the markers are cleared *or* when the clock runs out, so a run that never finished
///   also has a completion time - and a time-first comparison would hand the win to whoever failed
///   fastest. Points cannot be gamed that way.
/// - **Which modes reject correspondence, and why.** Anything needing two people in one match at
///   once has no ruleset here at all: a battle royal, a cage match, versus-CPU and lockdown are not
///   two separate runs that can be compared, and pretending otherwise would invent gameplay.
///   Bash Up Some Nerds has a ruleset but declares only local play - one person at a time is fine,
///   but enemy spawning is not reproducible enough for two runs a week apart to be a fair contest.
/// </summary>
public static class DefaultCompetitiveRulesets
{
    /// <summary>Modes that can be played locally and by correspondence.</summary>
    private const VersusCapability Anytime =
        VersusCapability.LocalAlternating | VersusCapability.Asynchronous;

    /// <summary>Modes that work turn by turn on one device but are not fair across a delay.</summary>
    private const VersusCapability LocalOnly = VersusCapability.LocalAlternating;

    /// <summary>Every ruleset this build ships, in menu order.</summary>
    public static List<CompetitiveRuleset> CreateAll()
    {
        return new List<CompetitiveRuleset>
        {
            // ---- score attacks: most of something before the clock runs out --------------------
            Score("most-points", GameModeId.TotalPoints, "Most Points"),
            Score("points-by-distance", GameModeId.PointsByDistance, "Points by Distance"),
            Score("in-the-pocket", GameModeId.InThePocket, "In the Pocket"),

            // ---- make-count: the number of a particular shot made ------------------------------
            MakeCount("most-3-pointers", GameModeId.Total3Pointers, "Most 3 Pointers"),
            MakeCount("most-4-pointers", GameModeId.Total4Pointers, "Most 4 Pointers"),
            MakeCount("most-7-pointers", GameModeId.Total7Pointers, "Most 7 Pointers"),

            // ---- distance ----------------------------------------------------------------------
            Distance("most-distance", GameModeId.TotalDistance, "Most Distance"),

            // ---- streak ------------------------------------------------------------------------
            Streak("longest-streak", GameModeId.ConsecutiveShots, "Longest Streak"),

            // ---- contests: clear the markers, points first and time as the tie-break ------------
            Contest("three-point-contest", GameModeId.ThreePointContest, "3 Point Contest"),
            Contest("four-point-contest", GameModeId.FourPointContest, "4 Point Contest"),
            Contest("seven-point-contest", GameModeId.SevenPointContest, "7 Point Contest"),
            Contest("all-point-contest", GameModeId.AllPointContest, "All Point Contest"),
            Contest("spot-up-3s", GameModeId.SpotUp3s, "Spot Up 3s"),
            Contest("spot-up-4s", GameModeId.SpotUp4s, "Spot Up 4s"),
            Contest("spot-up-7s", GameModeId.SpotUp7s, "Spot Up 7s"),
            Contest("spot-up-all", GameModeId.SpotUpAll, "Spot Up All"),

            // ---- local only: playable turn by turn, not fair across a delay ---------------------
            new CompetitiveRuleset(
                new RulesetId("bash-up-some-nerds"),
                1,
                GameModeId.BashUpSomeNerds,
                LocalOnly,
                new[]
                {
                    ComparisonKey.Highest(AttemptMetric.Score),
                    ComparisonKey.Highest(AttemptMetric.LongestStreak)
                },
                1,
                "Bash Up Some Nerds")

            // Deliberately absent, and each for a reason that is not an oversight:
            //
            //   Battle Royal, Cage Match, Versus (CPU), Lockdown
            //       both sides have to be in the same match at the same time. There is no such
            //       thing as one participant's separate run at these, so there is nothing for this
            //       domain to compare.
            //   Beat tha Computahs
            //       a campaign against the game, not a competition between two people.
            //   Arcade, Free Play
            //       no scoring contract worth competing over.
            //
            // A mode with no entry here is not versus-capable, and a series naming one is refused
            // with UnknownRuleset rather than quietly allowed.
        };
    }

    /// <summary>
    /// Most points wins; better accuracy breaks a tie, then fewer attempts.
    ///
    /// Accuracy before attempts because two players level on points where one needed fewer shots is
    /// the more interesting distinction, and attempts alone would reward not shooting.
    /// </summary>
    private static CompetitiveRuleset Score(string id, GameModeId modeId, string displayName)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            1,
            modeId,
            Anytime,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.Score),
                ComparisonKey.Highest(AttemptMetric.Accuracy),
                ComparisonKey.Lowest(AttemptMetric.ShotsAttempted)
            },
            1,
            displayName);
    }

    /// <summary>
    /// Most of the mode's shot made wins; total points, then accuracy break a tie.
    ///
    /// <see cref="AttemptMetric.ShotsMade"/> holds the count of the shot the mode is about - threes
    /// for a threes mode - which is filled in by the result factory, the one place that knows which
    /// mode produced the run.
    /// </summary>
    private static CompetitiveRuleset MakeCount(string id, GameModeId modeId, string displayName)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            1,
            modeId,
            Anytime,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.ShotsMade),
                ComparisonKey.Highest(AttemptMetric.Score),
                ComparisonKey.Highest(AttemptMetric.Accuracy)
            },
            1,
            displayName);
    }

    private static CompetitiveRuleset Distance(string id, GameModeId modeId, string displayName)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            1,
            modeId,
            Anytime,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.TotalDistance),
                ComparisonKey.Highest(AttemptMetric.ShotsMade),
                ComparisonKey.Highest(AttemptMetric.Accuracy)
            },
            1,
            displayName);
    }

    private static CompetitiveRuleset Streak(string id, GameModeId modeId, string displayName)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            1,
            modeId,
            Anytime,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.LongestStreak),
                ComparisonKey.Highest(AttemptMetric.Score),
                ComparisonKey.Highest(AttemptMetric.Accuracy)
            },
            1,
            displayName);
    }

    /// <summary>
    /// Points first, then the faster run.
    ///
    /// Time is the tie-break rather than the objective on purpose - see the note on this class.
    /// </summary>
    private static CompetitiveRuleset Contest(string id, GameModeId modeId, string displayName)
    {
        return new CompetitiveRuleset(
            new RulesetId(id),
            1,
            modeId,
            Anytime,
            new[]
            {
                ComparisonKey.Highest(AttemptMetric.Score),
                ComparisonKey.Lowest(AttemptMetric.CompletionTimeSeconds),
                ComparisonKey.Highest(AttemptMetric.Accuracy)
            },
            1,
            displayName);
    }
}
