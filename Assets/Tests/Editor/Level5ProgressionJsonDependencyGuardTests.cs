using System.IO;
using NUnit.Framework;

/// <summary>
/// Retiring the JSON character-progress projection (docs/persistence-boundaries.md) only holds if
/// the production progression path never grows the dependency back. Reads source as plain text, the
/// same asmdef-free technique <see cref="Level5TestSourceText"/>'s other consumers use (see its class
/// doc), so this sees every file it checks without joining their dependency graph.
///
/// Does not ban <see cref="CharacterProgressStore"/> itself - its <c>DeleteAccountFiles</c>/
/// <c>Save</c>/<c>TryLoadExisting</c> remain legitimate (profile-deletion cleanup, and tests that seed
/// or assert legacy JSON fixtures). Only the specific projection-write/projection-repair/JSON-unlock-
/// fallback identifiers this issue removed are banned from these three production files.
/// </summary>
public class Level5ProgressionJsonDependencyGuardTests
{
    private static readonly string ProgressionServicePath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_progression", "ProgressionService.cs");
    private static readonly string LoadManagerPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_loading", "LoadManager.cs");
    private static readonly string UnlockSnapshotBuilderPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "menu_start", "UnlockSnapshotBuilder.cs");

    private static readonly string[] RetiredJsonProjectionIdentifiers =
    {
        "TryApplyProgressionSnapshot",
        "GetPendingProgressionProjections",
        "MarkProgressionProjectionApplied",
        "RepairPendingJsonProjections",
        "AddJsonFallback",
    };

    [Test]
    public void ProgressionServiceHasNoRetiredJsonProjectionDependency()
    {
        AssertNoRetiredIdentifiers(ProgressionServicePath);
    }

    [Test]
    public void LoadManagerHasNoRetiredJsonProjectionDependency()
    {
        AssertNoRetiredIdentifiers(LoadManagerPath);
    }

    [Test]
    public void UnlockSnapshotBuilderHasNoRetiredJsonProjectionDependency()
    {
        AssertNoRetiredIdentifiers(UnlockSnapshotBuilderPath);
    }

    /// <summary>
    /// LoadManager's startup repair must still reach SQLite pending-progression recovery - just
    /// through the renamed, narrower method - so this proves the call site survived the rename
    /// rather than being silently dropped.
    /// </summary>
    [Test]
    public void LoadManagerStillCallsTheRenamedPendingProgressionRepair()
    {
        string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(LoadManagerPath));

        Assert.That(
            text,
            Does.Match(@"\bRepairPendingProgression\s*\("),
            "LoadManager must still call ProgressionService.RepairPendingProgression() on the "
            + "database-ready startup path.");
    }

    private static void AssertNoRetiredIdentifiers(string path)
    {
        // StripCommentsAndLiterals (not the naive two-pass StripComments) - LoadManager.cs has a "//"
        // comment containing a literal "/*" substring ("databaseReady/*TableExists"), which the
        // naive regex misreads as a real block-comment opener and, searching for the next "*/",
        // silently eats a large swath of real code in between. StripCommentsAndLiterals's
        // character-by-character scanner does not have this failure mode.
        string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(path));

        foreach (string identifier in RetiredJsonProjectionIdentifiers)
        {
            Assert.That(
                text,
                Does.Not.Match(@"\b" + identifier + @"\b"),
                Level5TestSourceText.Relative(path) + " must not reference retired JSON-projection "
                + "identifier \"" + identifier + "\" - see docs/persistence-boundaries.md.");
        }
    }
}
