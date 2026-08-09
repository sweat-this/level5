using Level5.Core.Versus;

/// <summary>
/// Where the game gets its versus coordinator.
///
/// A composition root, not a service locator with opinions: it builds one coordinator over the file
/// repository and the ruleset catalog, and hands the same one out. It holds no competitive state -
/// the coordinator holds none either, and the series documents are the truth - so nothing here has
/// to be reset between matches or scenes.
///
/// <see cref="Override"/> exists so tests and the dev console can point the whole game at an
/// in-memory repository, which is the only way to exercise the flow without writing to the player's
/// real save folder.
/// </summary>
public static class VersusRuntime
{
    private static VersusMatchCoordinator coordinator;
    private static IVersusSeriesRepository repository;

    public static IVersusSeriesRepository Repository => repository ??= new FileVersusSeriesRepository();

    public static VersusMatchCoordinator Coordinator =>
        coordinator ??= new VersusMatchCoordinator(Repository, VersusCatalogs.Rulesets);

    /// <summary>Points the game at a different store. For tests, tools and the dev console.</summary>
    public static void Override(IVersusSeriesRepository seriesRepository, CompetitiveRulesetCatalog rulesets = null)
    {
        repository = seriesRepository;
        coordinator = seriesRepository == null
            ? null
            : new VersusMatchCoordinator(seriesRepository, rulesets ?? VersusCatalogs.Rulesets);
    }

    public static void Reset()
    {
        repository = null;
        coordinator = null;
    }
}
