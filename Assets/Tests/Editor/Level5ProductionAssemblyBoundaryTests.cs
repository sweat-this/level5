using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Phase 2d architecture guards (docs/systems-restructure-plan.md, "Phase 2 - Assembly split").
///
/// Reads source as text rather than referencing it, the same trick <see cref="Level5GameManagerEdgeTests"/>
/// uses: this file has no asmdef of its own, so it can see every folder - including the still-blocked
/// player/basketball/game-manager triangle - without becoming part of the dependency graph it checks.
///
/// The production asmdef folder list is discovered at run time (every runtime, non-test .asmdef
/// under Assets/Scripts and Assets/Level5), not hard-coded, so this guard covers every future 2b
/// slice automatically rather than needing an allowlist maintained by hand.
/// </summary>
public class Level5ProductionAssemblyBoundaryTests
{
    private static readonly string AssetsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
    private static readonly string ScriptsRoot = Path.Combine(AssetsRoot, "Scripts");
    private static readonly string Level5Root = Path.Combine(AssetsRoot, "Level5");

    /// <summary>
    /// Package/engine assemblies proven Editor-only and therefore unsafe for a runtime asmdef to
    /// reference. "Analytics" is the 2a canary's finding (systems-restructure-plan.md): it is the
    /// asmdef-based com.unity.analytics Editor-window code (`includePlatforms: ["Editor"]`), not the
    /// UnityEngine.Analytics engine module runtime code actually uses - which needs no reference.
    /// </summary>
    private static readonly string[] EditorOnlyPackageAssemblies = { "Analytics" };

    [Test]
    public void NoMigratedProductionAssemblyReachesIntoAssemblyCSharp()
    {
        List<string> migratedFolders = FindProductionAssemblyFolders();
        Assert.That(
            migratedFolders,
            Is.Not.Empty,
            "expected at least the leaf assemblies Phase 2b already migrated - if this is empty, "
            + "the discovery logic itself is broken, not the codebase");

        HashSet<string> assemblyCSharpTypes = CollectDeclaredTypeNames(
            EnumerateAssemblyCSharpScripts(migratedFolders));

        List<string> offenders = new List<string>();
        foreach (string file in EnumerateFilesUnder(migratedFolders))
        {
            string text = NormalizeSource(File.ReadAllText(file));
            HashSet<string> usedIdentifiers = new HashSet<string>(
                Regex.Matches(text, @"\b[A-Za-z_][A-Za-z0-9_]*\b").Select(m => m.Value));

            List<string> hits = usedIdentifiers.Where(assemblyCSharpTypes.Contains).ToList();
            if (hits.Count > 0)
            {
                offenders.Add($"{Relative(file)}: {string.Join(", ", hits)}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these files live in a production asmdef but reach for a type still declared in "
            + "Assembly-CSharp - either that type needs to migrate too, or this file does not belong "
            + "in this assembly yet (docs/systems-restructure-plan.md, Phase 2b0's hard rule):\n"
            + string.Join("\n", offenders));
    }

    [Test]
    public void NoProductionAssemblyReferencesAKnownEditorOnlyPackageAssembly()
    {
        List<string> offenders = new List<string>();
        foreach (string asmdefPath in FindProductionAssemblyDefinitionFiles())
        {
            string json = File.ReadAllText(asmdefPath);
            foreach (string editorOnly in EditorOnlyPackageAssemblies)
            {
                if (Regex.IsMatch(json, $"\"{Regex.Escape(editorOnly)}\""))
                {
                    offenders.Add($"{Relative(asmdefPath)} references \"{editorOnly}\"");
                }
            }
        }

        Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
    }

    private static List<string> FindProductionAssemblyDefinitionFiles()
    {
        List<string> found = new List<string>();
        if (Directory.Exists(ScriptsRoot))
        {
            found.AddRange(Directory.EnumerateFiles(ScriptsRoot, "*.asmdef", SearchOption.AllDirectories));
        }

        if (Directory.Exists(Level5Root))
        {
            found.AddRange(Directory.EnumerateFiles(Level5Root, "*.asmdef", SearchOption.AllDirectories));
        }

        return found
            .Where(path => !path.Replace('\\', '/').Contains("~"))
            .Where(IsRuntimeProductionAssembly)
            .ToList();
    }

    /// <summary>Excludes test assemblies (they opt in via optionalUnityReferences: ["TestAssemblies"]).</summary>
    private static bool IsRuntimeProductionAssembly(string asmdefPath)
    {
        return !File.ReadAllText(asmdefPath).Contains("TestAssemblies");
    }

    private static List<string> FindProductionAssemblyFolders()
    {
        return FindProductionAssemblyDefinitionFiles()
            .Select(Path.GetDirectoryName)
            .ToList();
    }

    private static IEnumerable<string> EnumerateFilesUnder(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                if (!file.Replace('\\', '/').Contains("~"))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateAssemblyCSharpScripts(IReadOnlyCollection<string> migratedFolders)
    {
        if (!Directory.Exists(ScriptsRoot))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains("~"))
            {
                continue;
            }

            bool underMigratedFolder = migratedFolders.Any(folder =>
                normalized.StartsWith(folder.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase));
            if (!underMigratedFolder)
            {
                yield return file;
            }
        }
    }

    /// <summary>
    /// Only top-level (column-zero, unindented) <c>public</c> declarations count: a nested or
    /// non-public type cannot be named by a bare identifier from another assembly at all - matching
    /// on those produced pure name collisions (e.g. a private nested <c>StatsManager.mode</c> class
    /// colliding with every unrelated local variable named <c>mode</c> across the codebase).
    /// </summary>
    private static HashSet<string> CollectDeclaredTypeNames(IEnumerable<string> files)
    {
        HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
        Regex declaration = new Regex(
            @"^public\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*(?:class|interface|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Multiline);

        foreach (string file in files)
        {
            string text = NormalizeSource(File.ReadAllText(file));
            foreach (Match match in declaration.Matches(text))
            {
                types.Add(match.Groups[1].Value);
            }
        }

        return types;
    }

    /// <summary>
    /// Strips string/char literals, then comments. Order matters: a URL string like
    /// <c>"https://..."</c> contains <c>//</c>, and stripping comments first misreads it as a
    /// comment start, truncating the string literal and desyncing every quote-pairing after it in
    /// the file (found live against <c>Constants.cs</c>'s API-address constants). Stripping strings
    /// first neutralizes the <c>//</c> before the comment stripper ever sees it.
    /// </summary>
    private static string NormalizeSource(string text)
    {
        return StripComments(StripStringLiterals(text));
    }

    /// <summary>Deliberately simple, same simplification <see cref="Level5GameManagerEdgeTests"/> makes.</summary>
    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    /// <summary>
    /// Strips string/char literal contents so a table-name constant or a <c>[Tooltip("...")]</c>
    /// string that happens to spell a foreign type's name is not mistaken for a real reference to it.
    /// Deliberately simple - does not special-case interpolated string expressions - matching the
    /// same-spirit simplifications elsewhere in this file.
    /// </summary>
    private static string StripStringLiterals(string text)
    {
        text = Regex.Replace(text, "@\"(?:[^\"]|\"\")*\"", "\"\"", RegexOptions.Singleline);
        text = Regex.Replace(text, "\"(?:\\\\.|[^\"\\\\])*\"", "\"\"");
        return Regex.Replace(text, "'(?:\\\\.|[^'\\\\])*'", "''");
    }

    private static string Relative(string path)
    {
        return path.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
    }
}
