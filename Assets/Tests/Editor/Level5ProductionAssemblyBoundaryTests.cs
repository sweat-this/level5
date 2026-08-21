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

    /// <summary>
    /// Scans <c>Assets/Scripts</c> (recursively, minus migrated folders) plus loose <c>.cs</c> files
    /// directly under <c>Assets/</c> itself - e.g. <c>Assets/DialogueManager.cs</c>, which has no
    /// asmdef and so is part of Assembly-CSharp exactly like everything under Scripts. Does not walk
    /// other top-level asset folders (vendored third-party packages, Editor-only code, Assets/Tests'
    /// own asmdef-free test assemblies): those either compile into a different assembly than runtime
    /// Assembly-CSharp or are out of this migration's scope, and pulling them in risks reintroducing
    /// generic-name collisions like the one that motivated <see cref="CollectTypesNotNestedInAnotherType"/>.
    /// </summary>
    private static IEnumerable<string> EnumerateAssemblyCSharpScripts(IReadOnlyCollection<string> migratedFolders)
    {
        if (Directory.Exists(AssetsRoot))
        {
            foreach (string file in Directory.EnumerateFiles(AssetsRoot, "*.cs", SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }
        }

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
    /// Only <c>public</c> declarations not nested inside another type count: a type nested inside a
    /// class/struct/interface can't be named by a bare identifier from another assembly at all -
    /// matching on those produced pure name collisions (a private nested <c>StatsManager.mode</c>
    /// class was colliding with every unrelated local variable named <c>mode</c> across the
    /// codebase). Nesting inside a <c>namespace</c> block does not exclude a type the same way -
    /// this project's identifier-based matching doesn't distinguish "needs a using directive" from
    /// "is directly in scope" either, so a namespace-scoped type is still a real, reachable name and
    /// belongs in the set (most of this codebase's Assembly-CSharp model/service types are declared
    /// this way, e.g. <c>HighScoreModel</c>, <c>UserModel</c>).
    /// </summary>
    private static HashSet<string> CollectDeclaredTypeNames(IEnumerable<string> files)
    {
        HashSet<string> types = new HashSet<string>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            string text = NormalizeSource(File.ReadAllText(file));
            CollectTypesNotNestedInAnotherType(text, types);
        }

        return types;
    }

    /// <summary>
    /// Walks brace depth, tracking whether each open brace belongs to a type (class/struct/interface/
    /// enum) or something else (namespace, method body, block). A <c>public</c> type declaration is
    /// recorded only when none of its enclosing braces belongs to a type.
    /// </summary>
    private static void CollectTypesNotNestedInAnotherType(string text, HashSet<string> types)
    {
        Regex token = new Regex(
            @"(?<decl>\bpublic\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
            + @"(?:class|interface|struct|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))"
            + @"|(?<open>\{)|(?<close>\})",
            RegexOptions.Singleline);

        List<bool> enclosingIsType = new List<bool>();
        string pendingTypeName = null;

        foreach (Match match in token.Matches(text))
        {
            if (match.Groups["decl"].Success)
            {
                pendingTypeName = match.Groups["name"].Value;
            }
            else if (match.Groups["open"].Success)
            {
                bool opensType = pendingTypeName != null;
                if (opensType && !enclosingIsType.Contains(true))
                {
                    types.Add(pendingTypeName);
                }

                enclosingIsType.Add(opensType);
                pendingTypeName = null;
            }
            else if (match.Groups["close"].Success && enclosingIsType.Count > 0)
            {
                enclosingIsType.RemoveAt(enclosingIsType.Count - 1);
            }
        }
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
