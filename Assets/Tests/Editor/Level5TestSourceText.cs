using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// Shared text-scanning helpers for the asmdef-free architecture-guard tests under
/// Assets/Tests/Editor (Level5GameManagerEdgeTests, Level5MatchArchitectureTests,
/// Level5SingletonLifetimeTests, Level5PlayerSelectArchitectureTests,
/// Level5VersusArchitectureTests, Level5ProductionAssemblyBoundaryTests). These tests read
/// production source as plain text rather than referencing it, so they can see every folder -
/// including ones with no asmdef of their own - without joining the dependency graph they check.
///
/// <c>StripComments</c> and <c>Relative</c> used to be re-typed, byte-for-byte identical, in each
/// of those files (code review, 2026-08-21) - a bug fix to one had to be manually re-applied to
/// the rest to stay consistent. Extracted here instead.
/// </summary>
internal static class Level5TestSourceText
{
    internal static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    internal static string Relative(string path)
    {
        return path.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
    }
}
