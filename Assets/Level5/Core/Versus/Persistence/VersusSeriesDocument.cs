using System;

namespace Level5.Core.Versus.Persistence
{
    /// <summary>
    /// The stored form of a series.
    ///
    /// Rules this file follows, all of them for the same reason - a correspondence document written
    /// today may be read by a build that does not exist yet:
    ///
    /// - only ids and values. No <c>GameObject</c>, no <c>MonoBehaviour</c>, no ScriptableObject
    ///   reference, nothing that means anything only inside a loaded scene;
    /// - every enum is stored as its <b>name</b>, never its number. Reordering an enum is a normal
    ///   thing to do to source code and a catastrophic thing to do to stored competitive data;
    /// - times are round-trip ISO 8601 strings, with empty standing for "not yet";
    /// - plain public fields, because Unity's <c>JsonUtility</c> is what reads and writes these and
    ///   it ignores properties.
    ///
    /// The rules for each game are stored in full rather than by reference. That is the point of a
    /// snapshot: a document that pointed at the catalog would be rescored by the next balance patch.
    /// </summary>
    [Serializable]
    public class VersusSeriesDocument
    {
        /// <summary>Shape of this document. Bumped when a field is added or removed.</summary>
        public int documentVersion = 1;

        public string seriesId = string.Empty;
        public string status = string.Empty;
        public string mode = string.Empty;
        public string informationPolicy = string.Empty;
        public int gameCount;
        public bool alternatesFirstAttempt = true;
        public int snapshotFormatVersion = SeriesSnapshot.CurrentFormatVersion;
        public string createdAtUtc = string.Empty;
        public bool archived;

        public VersusParticipantDocument first = new VersusParticipantDocument();
        public VersusParticipantDocument second = new VersusParticipantDocument();

        /// <summary>The frozen rules, one per game, in playing order.</summary>
        public VersusRulesetDocument[] rulesets = new VersusRulesetDocument[0];

        public VersusGameDocument[] games = new VersusGameDocument[0];

        /// <summary>Empty until the series is over.</summary>
        public VersusSeriesResultDocument result;
    }

    [Serializable]
    public class VersusParticipantDocument
    {
        public string participantId = string.Empty;
        public string displayName = string.Empty;
        public string kind = string.Empty;
    }

    /// <summary>
    /// A ruleset exactly as the series froze it, comparison keys included.
    ///
    /// The keys are stored rather than looked up because they are the rules. A series restored on a
    /// build whose catalog has reordered or reweighted them must still resolve the way it was set
    /// up to.
    /// </summary>
    [Serializable]
    public class VersusRulesetDocument
    {
        public string rulesetId = string.Empty;
        public int version = 1;
        public int minimumCompatibleVersion = 1;
        public int modeId;
        public string displayName = string.Empty;

        /// <summary>Capability names, e.g. <c>Asynchronous</c>. Names, not the flags number.</summary>
        public string[] capabilities = new string[0];

        public VersusComparisonKeyDocument[] comparisonKeys = new VersusComparisonKeyDocument[0];
    }

    [Serializable]
    public class VersusComparisonKeyDocument
    {
        public string metric = string.Empty;
        public string direction = string.Empty;
    }

    [Serializable]
    public class VersusGameDocument
    {
        public int index;
        public string status = string.Empty;
        public int firstAttemptParticipantIndex;
        public VersusAttemptDocument[] attempts = new VersusAttemptDocument[0];

        /// <summary>Empty until the game has a verdict.</summary>
        public VersusGameResultDocument result;
    }

    [Serializable]
    public class VersusAttemptDocument
    {
        public string attemptId = string.Empty;
        public string participantId = string.Empty;
        public int gameIndex;
        public string rulesetId = string.Empty;
        public int rulesetVersion = 1;
        public string state = string.Empty;
        public string issuedAtUtc = string.Empty;
        public string startedAtUtc = string.Empty;
        public string completedAtUtc = string.Empty;

        /// <summary>Present only for a completed attempt.</summary>
        public VersusAttemptResultDocument result;
    }

    /// <summary>
    /// A result's metrics, indexed by <see cref="AttemptMetric"/>.
    ///
    /// Stored positionally, which is why that enum's numbers are documented as never to be
    /// renumbered. A document from an older build is shorter than this build's array and a document
    /// from a newer one is longer; both are read for whatever overlaps.
    /// </summary>
    [Serializable]
    public class VersusAttemptResultDocument
    {
        public string rulesetId = string.Empty;
        public int rulesetVersion = 1;
        public float[] metrics = new float[0];
    }

    [Serializable]
    public class VersusGameResultDocument
    {
        public string kind = string.Empty;
        public string winnerId = string.Empty;
        public string resolvedAtUtc = string.Empty;
    }

    [Serializable]
    public class VersusSeriesResultDocument
    {
        public string kind = string.Empty;
        public string winnerId = string.Empty;
        public int firstWins;
        public int secondWins;
        public int draws;
        public string completedAtUtc = string.Empty;
    }
}
