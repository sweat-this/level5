using System.Collections.Generic;
using Level5.Core.Match;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Every competitive ruleset this build knows, looked up by id.
    ///
    /// Modelled on <see cref="GameModeCatalog"/> deliberately, including rejecting duplicates rather
    /// than letting the last one win: two definitions claiming one id is the failure that makes half
    /// the game score a contest one way and half the other.
    ///
    /// The catalog answers what this build can play. It is never consulted to resolve a game - an
    /// active series carries its own frozen rules - only to check that a series can still be played
    /// at all.
    /// </summary>
    public sealed class CompetitiveRulesetCatalog
    {
        private readonly List<CompetitiveRuleset> rulesets = new List<CompetitiveRuleset>();
        private readonly Dictionary<string, CompetitiveRuleset> byId =
            new Dictionary<string, CompetitiveRuleset>();
        private readonly List<string> problems = new List<string>();

        public CompetitiveRulesetCatalog(IEnumerable<CompetitiveRuleset> definitions)
        {
            if (definitions == null)
            {
                return;
            }

            foreach (CompetitiveRuleset ruleset in definitions)
            {
                if (ruleset == null)
                {
                    problems.Add("the competitive ruleset catalog contains an empty entry");
                    continue;
                }

                if (byId.ContainsKey(ruleset.Id.Value))
                {
                    problems.Add($"duplicate competitive ruleset id '{ruleset.Id.Value}'");
                    continue;
                }

                byId.Add(ruleset.Id.Value, ruleset);
                rulesets.Add(ruleset);
            }
        }

        public IReadOnlyList<CompetitiveRuleset> Rulesets => rulesets;

        /// <summary>Problems found while building. Empty means the catalog is sound.</summary>
        public IReadOnlyList<string> Problems => problems;

        public int Count => rulesets.Count;

        public CompetitiveRuleset Find(RulesetId id)
        {
            return id.HasValue && byId.TryGetValue(id.Value, out CompetitiveRuleset ruleset) ? ruleset : null;
        }

        /// <summary>The ruleset for a gameplay mode, or null when that mode is not competitive.</summary>
        public CompetitiveRuleset FindByMode(GameModeId modeId)
        {
            foreach (CompetitiveRuleset ruleset in rulesets)
            {
                if (ruleset.ModeId == modeId)
                {
                    return ruleset;
                }
            }

            return null;
        }

        /// <summary>Every ruleset that supports a kind of competition. The list a playlist picker offers.</summary>
        public List<CompetitiveRuleset> Supporting(VersusCapability capability)
        {
            List<CompetitiveRuleset> matches = new List<CompetitiveRuleset>();
            foreach (CompetitiveRuleset ruleset in rulesets)
            {
                if (ruleset.Supports(capability))
                {
                    matches.Add(ruleset);
                }
            }

            return matches;
        }

        public static CompetitiveRulesetCatalog Empty()
        {
            return new CompetitiveRulesetCatalog(null);
        }
    }
}
