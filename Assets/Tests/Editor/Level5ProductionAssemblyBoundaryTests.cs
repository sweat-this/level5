using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Phase 2d architecture guards (docs/systems-restructure-plan.md, "Phase 2 - Assembly split").
///
/// Reads source as text rather than referencing it, the same trick <see cref="Level5GameManagerEdgeTests"/>
/// uses: this file has no asmdef of its own, so it can see every folder - including the still-blocked
/// player/basketball/game-manager triangle - without becoming part of the dependency graph it checks.
///
/// The production asmdef list is discovered at run time (every runtime, non-test .asmdef under
/// Assets/Scripts and Assets/Level5), not hard-coded, so this guard covers every future 2b slice
/// automatically rather than needing an allowlist maintained by hand.
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

    /// <summary>
    /// Discovered once per test run and reused by every test in this fixture - each test that reads
    /// it would otherwise re-walk both script roots and re-read every .asmdef from disk.
    /// </summary>
    private static readonly Lazy<List<AsmdefInfo>> ProductionAssemblies =
        new Lazy<List<AsmdefInfo>>(DiscoverProductionAssemblies);

    [Test]
    public void NoMigratedProductionAssemblyReachesIntoAssemblyCSharp()
    {
        List<AsmdefInfo> assemblies = ProductionAssemblies.Value;
        List<string> migratedFolders = assemblies.Select(a => a.Folder).ToList();
        Assert.That(
            migratedFolders,
            Is.Not.Empty,
            "expected at least the leaf assemblies Phase 2b already migrated - if this is empty, "
            + "the discovery logic itself is broken, not the codebase");

        HashSet<string> assemblyCSharpTypes = CollectDeclaredTypeNames(EnumerateAssemblyCSharpScripts());

        List<string> offenders = new List<string>();
        foreach (string file in EnumerateFilesUnder(migratedFolders))
        {
            HashSet<string> usedIdentifiers = UsedIdentifiers(file);
            List<string> hits = usedIdentifiers.Where(assemblyCSharpTypes.Contains).ToList();
            if (hits.Count > 0)
            {
                offenders.Add($"{Level5TestSourceText.Relative(file)}: {string.Join(", ", hits)}");
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

    // A leaf-to-leaf variant of NoMigratedProductionAssemblyReachesIntoAssemblyCSharp (checking that
    // a production assembly doesn't reach into a *different* production assembly it never declared)
    // was tried and dropped: bare-identifier matching can't tell a type reference from a same-named
    // property (Assets/Level5/Core/Match/GameModeCompatibility.cs's `public GameModeCatalog Modes =>
    // modes;` false-positived against the unrelated `Modes` type in Level5.Constants). Nothing today
    // needs this guard - no production assembly references another yet - so it wasn't worth chasing
    // false positives to keep. Revisit if/when that stops being true.

    /// <summary>
    /// AUD-012 Phase 2b: proves production basketball types actually compile into the
    /// <c>Level5.Basketball</c> asmdef created for this slice, rather than falling back into
    /// <c>Assembly-CSharp</c> because of a misconfigured asmdef scope. Complements
    /// <see cref="NoMigratedProductionAssemblyReachesIntoAssemblyCSharp"/>, which checks the outbound
    /// edge (basketball source doesn't reach into Assembly-CSharp); this checks the assembly the
    /// compiler actually produced. This file has no asmdef of its own (see the class summary), so it
    /// compiles into Assembly-CSharp-Editor, which auto-references every autoReferenced runtime
    /// assembly - including Level5.Basketball - letting these types be referenced directly.
    /// </summary>
    [Test]
    public void BasketballProductionTypesCompileIntoLevel5Basketball()
    {
        const string expected = "Level5.Basketball";
        Assert.That(typeof(BasketBall).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(GameStats).Assembly.GetName().Name, Is.EqualTo(expected));
        Assert.That(typeof(BasketBallShotMarker).Assembly.GetName().Name, Is.EqualTo(expected));
    }

    [Test]
    public void NoProductionAssemblyReferencesAKnownEditorOnlyPackageAssembly()
    {
        List<string> offenders = new List<string>();
        foreach (AsmdefInfo assembly in ProductionAssemblies.Value)
        {
            foreach (string editorOnly in EditorOnlyPackageAssemblies)
            {
                if (assembly.References.Contains(editorOnly))
                {
                    offenders.Add($"{Level5TestSourceText.Relative(assembly.Path)} references \"{editorOnly}\"");
                }
            }
        }

        Assert.That(offenders, Is.Empty, string.Join("\n", offenders));
    }

    /// <summary>One discovered production (non-test) asmdef: its name, declared references, and folder.</summary>
    private sealed class AsmdefInfo
    {
        public string Path;
        public string Folder;
        public string Name;
        public HashSet<string> References;
    }

    /// <summary>Mirrors the subset of an .asmdef's JSON this file reads, for <see cref="JsonUtility"/>.</summary>
    [Serializable]
    private class AsmdefJson
    {
        public string name;
        public string[] references;
        public string[] optionalUnityReferences;
    }

    private static List<AsmdefInfo> DiscoverProductionAssemblies()
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

        List<AsmdefInfo> assemblies = new List<AsmdefInfo>();
        foreach (string path in found)
        {
            if (path.Replace('\\', '/').Contains("~"))
            {
                continue;
            }

            AsmdefJson json = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(path));

            // Test assemblies opt in via optionalUnityReferences: ["TestAssemblies"].
            if (json?.optionalUnityReferences != null && json.optionalUnityReferences.Contains("TestAssemblies"))
            {
                continue;
            }

            assemblies.Add(new AsmdefInfo
            {
                Path = path,
                Folder = Path.GetDirectoryName(path),
                Name = json?.name ?? Path.GetFileNameWithoutExtension(path),
                References = new HashSet<string>(json?.references ?? Array.Empty<string>()),
            });
        }

        return assemblies;
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
    /// Scans every <c>.cs</c> file anywhere under <c>Assets/</c> that isn't owned by some asmdef
    /// (production, test, or third-party - Unity draws no distinction) and isn't under an
    /// <c>Editor</c> or <c>Tests</c> path segment (those compile into a different predefined
    /// assembly than runtime Assembly-CSharp). Earlier versions of this scan only walked
    /// <c>Assets/Scripts</c> plus loose files directly under <c>Assets/</c>, missing vendored
    /// third-party folders with no asmdef of their own (<c>Assets/Standard Assets</c>,
    /// <c>Assets/Joystick Pack</c>, <c>Assets/OmniSARTechnologies</c> - 50 files, all genuinely part
    /// of Assembly-CSharp) - a false-negative gap found by code review, 2026-08-21.
    /// </summary>
    private static IEnumerable<string> EnumerateAssemblyCSharpScripts()
    {
        if (!Directory.Exists(AssetsRoot))
        {
            yield break;
        }

        HashSet<string> asmdefOwnedFolders = new HashSet<string>(
            Directory.EnumerateFiles(AssetsRoot, "*.asmdef", SearchOption.AllDirectories)
                .Where(path => !path.Replace('\\', '/').Contains("~"))
                .Select(path => Path.GetDirectoryName(path).Replace('\\', '/')),
            StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.EnumerateFiles(AssetsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains("~"))
            {
                continue;
            }

            if (HasPathSegment(normalized, "Editor") || HasPathSegment(normalized, "Tests"))
            {
                continue;
            }

            bool ownedByAnAsmdef = asmdefOwnedFolders.Any(folder =>
                normalized.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
            if (!ownedByAnAsmdef)
            {
                yield return file;
            }
        }
    }

    private static bool HasPathSegment(string normalizedPath, string segment)
    {
        return normalizedPath.Split('/').Any(part => string.Equals(part, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> UsedIdentifiers(string file)
    {
        string text = NormalizeSource(File.ReadAllText(file));
        return new HashSet<string>(Regex.Matches(text, @"\b[A-Za-z_][A-Za-z0-9_]*\b").Select(m => m.Value));
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
    /// recorded only when no enclosing brace belongs to a type - checked in O(1) via
    /// <c>typeFrameCount</c> rather than re-scanning the stack on every brace.
    /// </summary>
    private static void CollectTypesNotNestedInAnotherType(string text, HashSet<string> types)
    {
        Regex token = new Regex(
            @"(?<decl>\bpublic\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*"
            + @"(?:class|interface|struct|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))"
            + @"|(?<open>\{)|(?<close>\})",
            RegexOptions.Singleline);

        Stack<bool> enclosingIsType = new Stack<bool>();
        int typeFrameCount = 0;
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
                if (opensType && typeFrameCount == 0)
                {
                    types.Add(pendingTypeName);
                }

                enclosingIsType.Push(opensType);
                if (opensType)
                {
                    typeFrameCount++;
                }

                pendingTypeName = null;
            }
            else if (match.Groups["close"].Success && enclosingIsType.Count > 0)
            {
                if (enclosingIsType.Pop())
                {
                    typeFrameCount--;
                }
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
        return Level5TestSourceText.StripComments(StripStringLiterals(text));
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
}
