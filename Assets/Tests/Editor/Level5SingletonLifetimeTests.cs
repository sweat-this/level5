using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Guard rails for the two Unity null-semantics traps fixed in the 2026-08-09 audit.
///
/// Both are the kind of mistake that reads as correct code and stays invisible until something
/// destroys an object at the wrong moment, so they are asserted here rather than trusted to review.
/// </summary>
public class Level5SingletonLifetimeTests
{
    private static readonly string ScriptsRoot = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Scripts");

    /// <summary>
    /// Every <c>MonoBehaviour</c> holding a <c>public static ... instance</c> must release it.
    ///
    /// Unity's overloaded <c>==</c> reports a destroyed object as null, so a stale static survives
    /// almost every guard in the codebase - right up until something uses <c>?.</c> (see the test
    /// below), caches the reference across a scene load, or dereferences it directly. AUD-060.
    /// </summary>
    [Test]
    public void EverySingletonReleasesItsStaticOnDestroy()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateGameScripts())
        {
            string code = StripComments(File.ReadAllText(file));

            Match declaration = Regex.Match(code, @"public\s+static\s+(\w+)\s+instance\s*(?:=\s*null\s*)?;");
            if (!declaration.Success)
            {
                continue;
            }

            if (!Regex.IsMatch(code, @"class\s+\w+\s*:\s*MonoBehaviour"))
            {
                continue;
            }

            // A clear is any assignment of null to the static that is not the declaration itself.
            bool clears = false;
            foreach (Match assignment in Regex.Matches(code, @"\binstance\s*=\s*null\s*;"))
            {
                if (assignment.Index < declaration.Index || assignment.Index >= declaration.Index + declaration.Length)
                {
                    clears = true;
                    break;
                }
            }

            if (!clears)
            {
                offenders.Add(Relative(file));
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "these singletons never release their static, so it outlives the object it points at. "
            + "Add:\n\n    private void OnDestroy()\n    {\n        if (instance == this)\n"
            + "        {\n            instance = null;\n        }\n    }\n\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// <c>?.</c> must not be used on a <c>UnityEngine.Object</c>.
    ///
    /// The null-conditional operator compiles to a reference null check. It does not call Unity's
    /// overloaded <c>==</c>, so a destroyed object is not seen as null and the call runs on it
    /// anyway - the opposite of what the code appears to say. AUD-061.
    ///
    /// The check is deliberately narrow: it looks for <c>.instance?.</c>, which is the form that
    /// appears in this codebase and the one that always names a component.
    /// </summary>
    [Test]
    public void NoNullConditionalOnASingletonInstance()
    {
        List<string> offenders = new List<string>();

        foreach (string file in EnumerateGameScripts())
        {
            foreach (Match match in Regex.Matches(StripComments(File.ReadAllText(file)), @"\w+\.instance\?\."))
            {
                offenders.Add($"{Relative(file)}: {match.Value}");
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "?. does not go through Unity's destroyed-object check. Use `if (X.instance != null)`:\n"
            + string.Join("\n", offenders));
    }

    private static IEnumerable<string> EnumerateGameScripts()
    {
        foreach (string path in Directory.EnumerateFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Directories Unity ignores, and the quarantined copy of the old start manager.
            if (!path.Contains("~"))
            {
                yield return path;
            }
        }
    }

    private static string Relative(string path)
    {
        return path.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
    }

    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }
}
