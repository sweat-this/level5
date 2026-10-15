using System.IO;
using System.Text;
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

    /// <summary>
    /// Removes comments *and* string/char literals in one left-to-right pass, so a scanner sees only
    /// real code identifiers.
    ///
    /// Regexes cannot do this correctly in either order, which is why this is a scanner. Stripping
    /// comments first truncates a URL string like <c>"https://..."</c> at its <c>//</c> (found live
    /// against <c>Constants.cs</c>'s API-address constants). Stripping literals first fixes that but
    /// creates a worse failure in the other direction: the char-literal rule then runs over comment
    /// text, so an apostrophe in a <c>///</c> comment ("controller's") pairs with the next apostrophe
    /// anywhere in the file and silently deletes everything between - which measured at 111 lost
    /// identifiers in <c>BasketBallAuto.cs</c> alone, across 29 production files (code review,
    /// 2026-09-08). Both hazards vanish once a single pass decides what each quote means from
    /// left to right.
    ///
    /// Each removed literal or block comment leaves one space so neighbouring tokens cannot fuse.
    /// Interpolated strings are treated as plain strings - identifiers inside <c>{...}</c> holes are
    /// dropped rather than parsed, matching the previous behaviour and the same-spirit
    /// simplifications in the callers.
    /// </summary>
    internal static string StripCommentsAndLiterals(string text)
        => StripCommentsAndLiterals(text, preserveInterpolationExpressions: false);

    /// <summary>
    /// Optionally retains executable expressions in regular/verbatim interpolated strings while
    /// removing their literal and format text. Existing callers retain their literal-only policy.
    /// </summary>
    internal static string StripCommentsAndLiterals(string text, bool preserveInterpolationExpressions)
    {
        StringBuilder stripped = new StringBuilder(text.Length);
        int i = 0;
        AppendCode(text, ref i, stripped, preserveInterpolationExpressions, interpolation: false);
        return stripped.ToString();
    }

    private static void AppendCode(
        string text, ref int i, StringBuilder stripped, bool preserveInterpolationExpressions, bool interpolation)
    {
        int braces = 0;
        int parentheses = 0;
        int brackets = 0;

        while (i < text.Length)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (interpolation && c == '}' && braces == 0)
            {
                i++;
                return;
            }
            else if (interpolation && c == ':' && braces == 0 && parentheses == 0 && brackets == 0
                && next != ':' && (i == 0 || text[i - 1] != ':'))
            {
                // A top-level colon starts format text, not executable code. Conditional
                // expressions in an interpolation require parentheses; alias::name is code.
                while (i < text.Length && text[i] != '}') i++;
                if (i < text.Length) i++;
                return;
            }
            else if (preserveInterpolationExpressions && TryAppendInterpolatedString(text, ref i, stripped))
            {
                continue;
            }
            else if (c == '/' && next == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (c == '/' && next == '*')
            {
                i += 2;
                while (i < text.Length && !(text[i] == '*' && i + 1 < text.Length && text[i + 1] == '/'))
                {
                    i++;
                }

                i += 2;
                stripped.Append(' ');
            }
            else if (c == '@' && next == '"')
            {
                // Verbatim string: no escapes, and "" is a literal quote rather than a terminator.
                i += 2;
                while (i < text.Length)
                {
                    if (text[i] == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    i++;
                }

                stripped.Append(' ');
            }
            else if (c == '"' || c == '\'')
            {
                char quote = c;
                i++;
                while (i < text.Length)
                {
                    if (text[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (text[i] == quote)
                    {
                        i++;
                        break;
                    }

                    // A regular literal cannot span lines. Stopping at the newline keeps a single
                    // stray quote from swallowing the rest of the file, the failure mode this
                    // scanner exists to prevent.
                    if (text[i] == '\n')
                    {
                        break;
                    }

                    i++;
                }

                stripped.Append(' ');
            }
            else
            {
                if (interpolation)
                {
                    switch (c)
                    {
                        case '{': braces++; break;
                        case '}': braces--; break;
                        case '(': parentheses++; break;
                        case ')': parentheses--; break;
                        case '[': brackets++; break;
                        case ']': brackets--; break;
                    }
                }
                stripped.Append(c);
                i++;
            }
        }

    }

    private static bool TryAppendInterpolatedString(string text, ref int i, StringBuilder stripped)
    {
        bool regular = i + 1 < text.Length && text[i] == '$' && text[i + 1] == '"';
        bool verbatim = i + 2 < text.Length && text[i + 2] == '"'
            && ((text[i] == '$' && text[i + 1] == '@') || (text[i] == '@' && text[i + 1] == '$'));
        if (!regular && !verbatim) return false;

        stripped.Append(' ');
        i += regular ? 2 : 3;
        while (i < text.Length)
        {
            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (c == '"')
            {
                i++;
                if (verbatim && next == '"') i++;
                else break;
            }
            else if (regular && c == '\\')
            {
                i += 2;
            }
            else if (regular && c == '\n')
            {
                break; // Match the existing unterminated regular-literal recovery.
            }
            else if (c == '{' && next != '{')
            {
                i++;
                stripped.Append(' ');
                AppendCode(text, ref i, stripped, preserveInterpolationExpressions: true, interpolation: true);
                stripped.Append(' ');
            }
            else if ((c == '{' && next == '{') || (c == '}' && next == '}'))
            {
                i += 2; // Escaped braces are literal text, never an expression.
            }
            else
            {
                i++;
            }
        }
        stripped.Append(' ');
        return true;
    }

    internal static string Relative(string path)
    {
        return path.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
    }
}
