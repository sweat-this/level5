using System.Collections.Generic;
using Level5.Core.Match;
using UnityEngine;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Authored competitive rules for one game mode.
    ///
    /// Kept separate from <see cref="GameModeDefinition"/> rather than added to it. A mode
    /// definition says how the game is played; this says how two runs at it are compared, and the
    /// two version independently - a scoring tweak is a new ruleset version without being a new
    /// mode. Keeping them apart also means a mode with no asset here is simply not competitive,
    /// which is the safe default: nothing is exposed to correspondence play by accident.
    ///
    /// Adding a versus-capable mode is: create one of these, give it a stable id, declare which
    /// kinds of competition it supports, list how results are compared. No central file changes.
    ///
    /// Authored data only. Never write to one of these at runtime - a ScriptableObject written in
    /// play mode keeps its value in the editor afterwards, which is how authored data silently rots.
    /// </summary>
    [CreateAssetMenu(menuName = "Level 5/Versus/Competitive Ruleset", fileName = "CompetitiveRuleset")]
    public class CompetitiveRulesetDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, lowercase with hyphens, e.g. three-point-contest. Stored in save data - never change it.")]
        [SerializeField] private string rulesetId = string.Empty;

        [Tooltip("The build of these competitive rules. Independent of the application version. Bump when scoring changes.")]
        [SerializeField] private int version = 1;

        [Tooltip("Oldest rules version this build can still score. Series older than this are refused rather than mis-scored.")]
        [SerializeField] private int minimumCompatibleVersion = 1;

        [Tooltip("The game mode that produces attempts for this ruleset.")]
        [SerializeField] private int modeId;

        [SerializeField] private string displayName = string.Empty;

        [Header("Competition")]
        [Tooltip("Which kinds of competition this mode can be played as. None means it is not competitive.")]
        [SerializeField] private VersusCapability capabilities = VersusCapability.None;

        [Header("Comparison")]
        [Tooltip("In order: the objective first, then this mode's own tie-breaks. Equal on all of them is a draw.")]
        [SerializeField] private List<AuthoredComparisonKey> comparisonKeys = new List<AuthoredComparisonKey>();

        public string RulesetId => rulesetId;

        public int Version => version;

        public GameModeId ModeId => GameModeIds.FromInt(modeId);

        public VersusCapability Capabilities => capabilities;

        /// <summary>Builds the runtime ruleset. Throws with a useful message when the asset is wrong.</summary>
        public CompetitiveRuleset ToRuleset()
        {
            List<ComparisonKey> keys = new List<ComparisonKey>();
            if (comparisonKeys != null)
            {
                foreach (AuthoredComparisonKey key in comparisonKeys)
                {
                    keys.Add(new ComparisonKey(key.metric, key.direction));
                }
            }

            return new CompetitiveRuleset(
                new RulesetId(rulesetId),
                version,
                ModeId,
                capabilities,
                keys,
                minimumCompatibleVersion,
                displayName);
        }

        /// <summary>Builds a definition in code. For the default registry, the editor and tests.</summary>
        public static CompetitiveRulesetDefinition Create(CompetitiveRuleset ruleset)
        {
            CompetitiveRulesetDefinition definition = CreateInstance<CompetitiveRulesetDefinition>();
            definition.rulesetId = ruleset.Id.Value;
            definition.version = ruleset.Version;
            definition.minimumCompatibleVersion = ruleset.MinimumCompatibleVersion;
            definition.modeId = GameModeIds.ToInt(ruleset.ModeId);
            definition.displayName = ruleset.DisplayName;
            definition.capabilities = ruleset.Capabilities;
            definition.comparisonKeys = new List<AuthoredComparisonKey>();

            foreach (ComparisonKey key in ruleset.ComparisonKeys)
            {
                definition.comparisonKeys.Add(new AuthoredComparisonKey
                {
                    metric = key.Metric,
                    direction = key.Direction
                });
            }

            definition.name = ruleset.Id.Value;
            return definition;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(rulesetId) ? "(unnamed ruleset)" : rulesetId + " v" + version;
        }
    }

    /// <summary>
    /// One comparison step, in a shape the inspector can show.
    ///
    /// A serializable class rather than the readonly struct the domain uses, because Unity will not
    /// serialize a struct with no setters and the domain type should not gain them for the editor's
    /// benefit.
    /// </summary>
    [System.Serializable]
    public class AuthoredComparisonKey
    {
        public AttemptMetric metric = AttemptMetric.Score;
        public MetricDirection direction = MetricDirection.HigherWins;
    }
}
