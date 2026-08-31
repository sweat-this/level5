using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-111: production menu code (Assets/Scripts/menu_*) must use explicit, narrowly-scoped
/// failure boundaries instead of broad <c>catch (Exception)</c>. This is the same policy the
/// "Repository Validation" CI check enforces in scripts/validate-repository.ps1 - duplicated here
/// as a Unity test (per AUD-111's own guidance that a source-text/static assertion is the right
/// shape for this particular policy check) so it also runs wherever the EditMode suite runs, and
/// so a change to one guard's allowlist that forgets the other is caught immediately rather than
/// only at the next CI push.
///
/// Reads source as plain text (the same technique as <see cref="Level5ProductionAssemblyBoundaryTests"/>)
/// rather than compiling against it, so it needs no reference to Assembly-CSharp and cannot itself
/// go stale by failing to compile against a renamed type.
/// </summary>
public class MenuExceptionBoundaryPolicyTests
{
    private static readonly string RepositoryRoot = Directory.GetCurrentDirectory();
    private static readonly string ScriptsRoot = Path.Combine(RepositoryRoot, "Assets", "Scripts");

    private static readonly Regex GenericCatchPattern = new Regex(
        @"catch\s*\(\s*(System\.)?Exception", RegexOptions.Compiled);

    /// <summary>
    /// Kept identical in spirit to scripts/validate-repository.ps1's $menuGenericCatchAllowlist:
    /// PendingProgressionStore.cs, ProgressionResultStore.cs, and ProgressionService.cs each catch
    /// (System.)Exception only around a narrow persistence read/write/JSON-parse call, where file
    /// I/O and JsonUtility can throw a heterogeneous mix of exception types (IOException,
    /// UnauthorizedAccessException, ArgumentException, ...) with no single verifiable type to name.
    /// </summary>
    private static readonly string[] AllowedFiles =
    {
        "Assets/Scripts/menu_progression/PendingProgressionStore.cs",
        "Assets/Scripts/menu_progression/ProgressionResultStore.cs",
        "Assets/Scripts/menu_progression/ProgressionService.cs",
    };

    [Test]
    public void NoUnapprovedGenericCatchExceptionInProductionMenuCode()
    {
        List<string> offenders = new List<string>();
        foreach (string file in EnumerateProductionMenuFiles())
        {
            string relativePath = Level5TestSourceText.Relative(file);
            if (AllowedFiles.Contains(relativePath))
            {
                continue;
            }

            string text = Level5TestSourceText.StripComments(File.ReadAllText(file));
            foreach (Match match in GenericCatchPattern.Matches(text))
            {
                int line = text.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{relativePath}:{line}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "Unapproved generic catch(Exception) in production menu code (AUD-111) - narrow the "
                + "caught type and try scope to the real failure boundary, or add the file to "
                + "AllowedFiles here and to $menuGenericCatchAllowlist in "
                + "scripts/validate-repository.ps1 with a rationale:\n"
                + string.Join("\n", offenders));
    }

    private static IEnumerable<string> EnumerateProductionMenuFiles()
    {
        if (!Directory.Exists(ScriptsRoot))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (!Regex.IsMatch(normalized, "/menu_[^/]*/"))
            {
                continue;
            }

            // Legacy~/generated folders (Unity ignores any path segment ending in '~'), and any
            // Editor/Tests path segment (editor tooling / test code, out of AUD-111's scope).
            if (Regex.IsMatch(normalized, "/[^/]*~/") || Regex.IsMatch(normalized, "/(Tests|Editor)/"))
            {
                continue;
            }

            yield return file;
        }
    }
}
