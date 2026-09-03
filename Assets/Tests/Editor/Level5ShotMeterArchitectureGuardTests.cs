using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// Permanent guards over <c>ShotMeter</c>'s three explicit composition boundaries:
///
/// - actor ownership (AUD-010 Phase 1c) - resolved through an explicitly bound
///   <see cref="Level5.Core.IShooterActor"/> (<see cref="ShotMeter.BindOwner"/>), never through a
///   parent <c>PlayerIdentifier</c> or a concrete player type;
/// - match rules (AUD-010 Phase 2b0) - resolved through an explicitly bound
///   <see cref="Level5.Core.Match.ResolvedMatchRules"/> (<see cref="ShotMeter.BindMatchRules"/>), never
///   through <c>MatchRuntime</c>;
/// - a CPU's automatic meter resolution - reaches its own basketball only through an explicitly bound
///   and validated <see cref="IBasketballRuntime"/> (<see cref="ShotMeter.BindBasketballRuntime"/>),
///   never a global lookup.
///
/// Mirrors <see cref="Level5RangeMeterArchitectureGuardTests"/>'s shape and scope: this guard is
/// scoped to <c>ShotMeter.cs</c> alone.
/// </summary>
public class Level5ShotMeterArchitectureGuardTests
{
    private static readonly string ShotMeterPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "ShotMeter.cs");

    private static readonly string[] ForbiddenTypeNames =
    {
        "GameLevelManager",
        "PlayerIdentifier",
        "PlayerController",
        "AutoPlayerController",
        "CharacterProfile",
    };

    private static string SourceText()
    {
        return Level5TestSourceText.StripComments(File.ReadAllText(ShotMeterPath));
    }

    [Test]
    public void ShotMeterDoesNotReferenceGlobalOrConcretePlayerTypes()
    {
        string text = SourceText();
        System.Collections.Generic.List<string> offenders = new System.Collections.Generic.List<string>();

        foreach (string type in ForbiddenTypeNames)
        {
            if (Regex.IsMatch(text, $@"\b{type}\b"))
            {
                offenders.Add(type);
            }
        }

        Assert.That(
            offenders,
            Is.Empty,
            "AUD-010 Phase 1c: ShotMeter.cs must resolve its shooter through an explicitly bound "
            + "IShooterActor (BindOwner), not through: " + string.Join(", ", offenders));
    }

    [Test]
    public void ShotMeterExposesExplicitOwnerBinding()
    {
        string text = SourceText();

        // Matches on parameter types only, not names - a parameter rename is not an ownership
        // regression and should not fail this guard.
        Assert.That(
            text,
            Does.Match(@"public\s+void\s+BindOwner\s*\(\s*IShooterActor\s+\w+\s*,\s*bool\s+\w+\s*\)"),
            "ShotMeter must expose an explicit BindOwner(IShooterActor, bool) API rather than "
            + "rediscovering its owner through a parent PlayerIdentifier or scene/global lookup.");
    }

    [Test]
    public void ShotMeterExposesExplicitOptionalBasketballRuntimeBinding()
    {
        string text = SourceText();

        Assert.That(
            text,
            Does.Match(@"public\s+void\s+BindBasketballRuntime\s*\(\s*IBasketballRuntime\s+\w+\s*\)"),
            "ShotMeter must expose an explicit BindBasketballRuntime(IBasketballRuntime) API for a "
            + "CPU's automatic meter resolution, separate from actor ownership binding.");
    }

    /// <summary>
    /// AUD-010 Phase 2b0 permanent guard: <c>ShotMeter</c> must carry no <c>MatchRuntime</c>
    /// dependency. <c>Start()</c>'s visibility gate used to read <c>MatchRuntime.Rules</c> directly;
    /// match rules now arrive once through <c>BindMatchRules(ResolvedMatchRules)</c>, bound by
    /// composition (<c>SpawnCoordinator.BindShotMeters</c>). This fails the build if a future change
    /// reintroduces a direct <c>MatchRuntime</c> read on this type instead of using the bound
    /// reference.
    /// </summary>
    [Test]
    public void ShotMeterHasNoMatchRuntimeReference()
    {
        string text = SourceText();

        Assert.That(
            text,
            Does.Not.Match(@"\bMatchRuntime\b"),
            "ShotMeter must have zero MatchRuntime references - match rules must arrive through "
            + "BindMatchRules(ResolvedMatchRules), bound once at match composition, not by reading "
            + "MatchRuntime directly.");
    }

    [Test]
    public void ShotMeterExposesExplicitMatchRulesBinding()
    {
        string text = SourceText();

        Assert.That(
            text,
            Does.Match(@"public\s+void\s+BindMatchRules\s*\(\s*ResolvedMatchRules\s+\w+\s*\)"),
            "ShotMeter must expose an explicit BindMatchRules(ResolvedMatchRules) API rather than "
            + "reading MatchRuntime.Rules directly, separate from actor ownership binding.");
    }
}
