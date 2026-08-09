using System.Collections.Generic;
using Level5.Core.Versus;
using UnityEngine;

/// <summary>
/// Where the game gets its competitive rulesets.
///
/// Modelled on <c>MatchCatalogs</c>, including the fallback: authored
/// <see cref="CompetitiveRulesetDefinition"/> assets under <c>Resources/Versus</c> win where they
/// exist, and <see cref="DefaultCompetitiveRulesets"/> supplies the rest. That way the new path is
/// live before any asset has been authored, and authoring one asset replaces exactly one entry
/// instead of requiring all of them at once.
///
/// Cached, because a menu asks what is playable on every highlighted button and rebuilding a
/// catalog per frame is how a list starts stuttering.
/// </summary>
public static class VersusCatalogs
{
    public const string RulesetResourcesPath = "Versus/Rulesets";

    private static CompetitiveRulesetCatalog cached;

    public static CompetitiveRulesetCatalog Rulesets => cached ??= Build();

    public static bool IsReady => Rulesets.Count > 0;

    /// <summary>Replaces the catalog outright. For the editor and for tests.</summary>
    public static void Override(CompetitiveRulesetCatalog catalog)
    {
        cached = catalog;
    }

    public static void Reset()
    {
        cached = null;
    }

    private static CompetitiveRulesetCatalog Build()
    {
        Dictionary<string, CompetitiveRuleset> byId = new Dictionary<string, CompetitiveRuleset>();
        List<CompetitiveRuleset> ordered = new List<CompetitiveRuleset>();

        // Authored assets first, so an asset shadows the code entry with the same id rather than
        // colliding with it - a duplicate id would otherwise be reported as a catalog problem.
        foreach (CompetitiveRuleset ruleset in LoadAuthored())
        {
            if (ruleset != null && !byId.ContainsKey(ruleset.Id.Value))
            {
                byId.Add(ruleset.Id.Value, ruleset);
                ordered.Add(ruleset);
            }
        }

        foreach (CompetitiveRuleset ruleset in DefaultCompetitiveRulesets.CreateAll())
        {
            if (ruleset != null && !byId.ContainsKey(ruleset.Id.Value))
            {
                byId.Add(ruleset.Id.Value, ruleset);
                ordered.Add(ruleset);
            }
        }

        CompetitiveRulesetCatalog catalog = new CompetitiveRulesetCatalog(ordered);
        foreach (string problem in catalog.Problems)
        {
            Debug.LogError("Level 5 competitive ruleset catalog: " + problem);
        }

        return catalog;
    }

    private static List<CompetitiveRuleset> LoadAuthored()
    {
        List<CompetitiveRuleset> rulesets = new List<CompetitiveRuleset>();
        CompetitiveRulesetDefinition[] assets =
            Resources.LoadAll<CompetitiveRulesetDefinition>(RulesetResourcesPath);

        if (assets == null)
        {
            return rulesets;
        }

        foreach (CompetitiveRulesetDefinition asset in assets)
        {
            if (asset == null)
            {
                continue;
            }

            try
            {
                rulesets.Add(asset.ToRuleset());
            }
            catch (VersusDomainException exception)
            {
                // One malformed asset must not take the whole catalog down with it - the rest of
                // the game's competitive modes are still perfectly playable.
                Debug.LogError(
                    $"Level 5 competitive ruleset asset '{asset.name}' is not valid and was skipped: "
                    + exception.Message,
                    asset);
            }
        }

        return rulesets;
    }
}
