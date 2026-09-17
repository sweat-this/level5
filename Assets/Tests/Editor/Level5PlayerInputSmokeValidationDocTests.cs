using System.IO;
using NUnit.Framework;

/// <summary>
/// AUD-012 Phase 5 Slice 79: guards the existence and required section coverage of
/// <c>docs/player-input-smoke-validation.md</c>, the input smoke-validation checklist called out by
/// <c>player-input-architecture.md</c>'s migration plan step 2. This is deliberately a narrow
/// existence/heading check, not a content parser - it fails if the document is deleted or if one of
/// its required top-level categories is removed, which is the failure mode this guard exists to catch.
/// </summary>
public class Level5PlayerInputSmokeValidationDocTests
{
    private static readonly string DocPath = Path.Combine(
        Directory.GetCurrentDirectory(), "docs", "player-input-smoke-validation.md");

    private static readonly string[] RequiredSectionHeadings =
    {
        "## Keyboard gameplay",
        "## Gamepad gameplay",
        "## Editor/Development debug input",
        "## Mobile movement",
        "## Mobile gestures/actions",
        "## Menu and pause",
        "## Input-backend gate",
    };

    [Test]
    public void SmokeValidationDocExists()
    {
        Assert.That(
            File.Exists(DocPath),
            Is.True,
            $"docs/player-input-smoke-validation.md must exist - it is the input smoke-validation "
            + "checklist referenced by player-input-architecture.md's migration plan step 2.");
    }

    [TestCaseSource(nameof(RequiredSectionHeadings))]
    public void SmokeValidationDocContainsRequiredSection(string heading)
    {
        string text = File.ReadAllText(DocPath);

        Assert.That(
            text,
            Does.Contain(heading),
            $"docs/player-input-smoke-validation.md is missing the required section '{heading}' - "
            + "do not remove a checklist category without replacing its coverage.");
    }
}
