using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// The competitive definition a series was created under, frozen.
    ///
    /// A correspondence series can outlive a patch. If game six resolved against whatever the
    /// catalog happened to say this week, a balance change would silently rescore a competition two
    /// people were in the middle of - and neither of them would ever know why the result looked
    /// wrong.
    ///
    /// So the series carries its own rules. Every ruleset here is a full immutable copy, stored with
    /// the series and read back with it; the live catalog is consulted for exactly one thing, which
    /// is whether this build can still play these versions. New series pick up new rules. Existing
    /// ones keep the deal they started with.
    /// </summary>
    public sealed class SeriesSnapshot
    {
        /// <summary>
        /// The shape of the snapshot itself, not of the rules inside it. Bumped when this class
        /// gains or loses a field, so a document written by an older build is recognisable as one.
        /// </summary>
        public const int CurrentFormatVersion = 1;

        private readonly CompetitiveRuleset[] games;

        public SeriesSnapshot(
            SeriesFormat format,
            IEnumerable<CompetitiveRuleset> orderedGames,
            InformationPolicy informationPolicy,
            bool alternatesFirstAttempt = true,
            int formatVersion = CurrentFormatVersion)
        {
            List<CompetitiveRuleset> collected = new List<CompetitiveRuleset>();
            if (orderedGames != null)
            {
                foreach (CompetitiveRuleset ruleset in orderedGames)
                {
                    if (ruleset == null)
                    {
                        throw new VersusDomainException("a series playlist may not contain an empty entry");
                    }

                    collected.Add(ruleset);
                }
            }

            if (collected.Count != format.GameCount)
            {
                throw new VersusDomainException(
                    $"a {format} needs exactly {format.GameCount} playlist entries, but {collected.Count} "
                    + "were given");
            }

            games = collected.ToArray();
            Format = format;
            InformationPolicy = informationPolicy;
            AlternatesFirstAttempt = alternatesFirstAttempt;
            FormatVersion = formatVersion;
        }

        public SeriesFormat Format { get; }

        /// <summary>The rules for each game, in playing order. Frozen at creation.</summary>
        public IReadOnlyList<CompetitiveRuleset> Games => games;

        public InformationPolicy InformationPolicy { get; }

        /// <summary>
        /// Whether the right to attempt first swaps each game. Only meaningful under
        /// <see cref="InformationPolicy.OpenTarget"/>, where attempting first means setting the
        /// target blind.
        /// </summary>
        public bool AlternatesFirstAttempt { get; }

        public int FormatVersion { get; }

        public int GameCount => games.Length;

        public CompetitiveRuleset GameAt(int index)
        {
            if (index < 0 || index >= games.Length)
            {
                throw new VersusDomainException(
                    $"this series has games 1 to {games.Length}; there is no game {index + 1}");
            }

            return games[index];
        }

        /// <summary>Which participant position attempts first in a given game.</summary>
        public int FirstAttemptParticipantIndex(int gameIndex)
        {
            return AlternatesFirstAttempt ? gameIndex % 2 : 0;
        }

        /// <summary>
        /// Whether the snapshot requires a capability every one of its rulesets declares.
        ///
        /// Checked when the series is created, not when a game starts: a best-of-seven that turns
        /// out to be unplayable at game five is a worse failure than one that is refused up front.
        /// </summary>
        public bool EveryGameSupports(VersusCapability capability)
        {
            foreach (CompetitiveRuleset ruleset in games)
            {
                if (!ruleset.Supports(capability))
                {
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            return $"{Format}, {InformationPolicy}, {games.Length} game(s)";
        }
    }
}
