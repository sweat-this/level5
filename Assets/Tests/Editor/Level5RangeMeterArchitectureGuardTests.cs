using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

/// <summary>
/// AUD-010 Phase 1c permanent guards: <c>RangeMeter</c> must resolve its shooter data through an
/// explicitly bound <see cref="Level5.Core.IShooterActor"/> (<see cref="RangeMeter.BindOwner"/>),
/// never through <c>GameLevelManager.instance.players[0]</c>, a <c>PlayerIdentifier</c>, or a
/// concrete player type - and it must schedule its presentation refresh exactly once.
///
/// Narrower than <see cref="Level5PlayerBasketballEdgeTests"/>: that guard restricts the whole
/// <c>Assets/Scripts/basketball</c> folder from concrete player types but does not forbid
/// <c>PlayerIdentifier</c>/<c>GameLevelManager</c> outright (both are still legitimate for
/// <c>ShotMeter</c>, which this issue does not migrate). This guard is scoped to
/// <c>RangeMeter.cs</c> alone and forbids all five.
/// </summary>
public class Level5RangeMeterArchitectureGuardTests
{
    private static readonly string RangeMeterPath = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "basketball", "RangeMeter.cs");

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
        return Level5TestSourceText.StripComments(File.ReadAllText(RangeMeterPath));
    }

    [Test]
    public void RangeMeterDoesNotReferenceGlobalOrConcretePlayerTypes()
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
            "AUD-010 Phase 1c: RangeMeter.cs must resolve its shooter through an explicitly bound "
            + "IShooterActor (BindOwner), not through: " + string.Join(", ", offenders));
    }

    [Test]
    public void RangeMeterExposesExplicitOwnerBinding()
    {
        string text = SourceText();

        // Matches on parameter types only, not names - a parameter rename is not an ownership
        // regression and should not fail this guard.
        Assert.That(
            text,
            Does.Match(@"public\s+void\s+BindOwner\s*\(\s*IShooterActor\s+\w+\s*,\s*bool\s+\w+\s*\)"),
            "RangeMeter must expose an explicit BindOwner(IShooterActor, bool) API rather than "
            + "rediscovering its owner through global/scene lookup.");
    }

    [Test]
    public void RangeMeterSchedulesItsRefreshExactlyOnce()
    {
        string text = SourceText();

        int calls = Regex.Matches(text, @"InvokeRepeating\s*\(").Count;

        Assert.That(
            calls,
            Is.EqualTo(1),
            "RangeMeter's production source must call InvokeRepeating exactly once - the legacy "
            + $"implementation scheduled it twice (before and after visibility gating). Found {calls} call(s).");
    }
}
