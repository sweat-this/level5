using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-012 Phase 5 Slice 80: <c>GameLevelManager.Update</c>'s <c>toggle_run_keyboard</c> and
/// <c>toggle_stats_keyboard</c> debug-toggle reads (<c>Other</c> map) were the one call site missing the
/// <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> gate every sibling debug read
/// (<c>PlayerInputReader.DebugChangeHeld</c>/<c>DebugLightningPressed</c>, <c>DevFunctions.cs</c>,
/// <c>CheerleaderSwapAnimation.cs</c>) already uses - see
/// <c>docs/player-input-smoke-validation.md</c>'s "Editor/Development debug input" section, which
/// surfaced this gap in Slice 79. Both toggles were reachable in shipped release builds.
///
/// This guard reads <c>GameLevelManager.cs</c> as plain text and fails if either live read moves outside
/// an enclosing <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> ... <c>#endif</c> region, so a future edit
/// cannot silently drop the gate the way the original code lacked it.
/// </summary>
public class Level5GameLevelManagerDebugToggleGatingTests
{
    private static readonly string ManagerPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "game manager", "GameLevelManager.cs");

    private static readonly Regex EditorOrDevelopmentBuildCondition = new Regex(
        @"^UNITY_EDITOR\s*\|\|\s*DEVELOPMENT_BUILD$");

    private static readonly string[] GuardedDebugToggleReads =
    {
        "Controls.Other.toggle_run_keyboard.triggered",
        "Controls.Other.toggle_stats_keyboard.triggered",
    };

    [TestCaseSource(nameof(GuardedDebugToggleReads))]
    public void DebugToggleRead_IsEditorOrDevelopmentGated(string expectedRead)
    {
        string[] lines = File.ReadAllLines(ManagerPath);

        int lineIndex = System.Array.FindIndex(lines, line => line.Contains(expectedRead));
        Assert.That(
            lineIndex,
            Is.GreaterThanOrEqualTo(0),
            $"GameLevelManager.cs must still read '{expectedRead}' in Update - this guard exists to keep "
            + "the existing debug-toggle behavior gated, not to remove it.");

        Assert.That(
            IsGuardedByEditorOrDevelopmentBuild(lines, lineIndex),
            Is.True,
            $"'{expectedRead}' in GameLevelManager.Update must be wrapped in "
            + "#if UNITY_EDITOR || DEVELOPMENT_BUILD ... #endif so this debug toggle compiles out of "
            + "shipped release builds (AUD-012 Phase 5 Slice 80).");
    }

    /// <summary>
    /// Walks preprocessor directives up to <paramref name="lineIndex"/>, tracking the stack of open
    /// <c>#if</c> conditions, and reports whether that line sits under an
    /// <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c> condition. Deliberately does not resolve <c>#elif</c>/
    /// <c>#else</c> branch selection - this file has no such branches around the debug toggles, and the
    /// simple stack is enough to catch the one failure mode this guard targets (source-text inspection,
    /// matching the project's existing architecture-guard tests).
    /// </summary>
    private static bool IsGuardedByEditorOrDevelopmentBuild(string[] lines, int lineIndex)
    {
        var conditionStack = new Stack<string>();
        for (int i = 0; i <= lineIndex; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("#if "))
            {
                conditionStack.Push(trimmed.Substring(4).Trim());
            }
            else if (trimmed.StartsWith("#endif") && conditionStack.Count > 0)
            {
                conditionStack.Pop();
            }
        }

        return conditionStack.Any(condition => EditorOrDevelopmentBuildCondition.IsMatch(condition));
    }
}
