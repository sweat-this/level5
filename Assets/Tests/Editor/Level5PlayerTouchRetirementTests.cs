using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;

/// <summary>
/// AUD-012 Phase 5 Slice 76: <c>PlayerControls.inputactions</c>' <c>PlayerTouch</c> action map had no
/// live consumer - <c>PlayerTouchInputState</c>/<c>TouchInputController</c> (still live) never read
/// it, and its only production reads were <c>PlayerControlsProvider.EnablePlayerTouch()</c>/
/// <c>DisablePlayerTouch()</c>, themselves uncalled outside two commented-out lines in
/// <c>SniperCameraController</c>. This is the first Phase 5 invariant: every action map in the asset
/// must have a live owner.
///
/// This slice also corrects <c>PlayerControls.inputactions.meta</c>'s <c>wrapperCodePath</c>, which
/// had drifted to the stale root <c>Assets/Scripts/input/PlayerControls.cs</c> while the actual
/// generated wrapper lived at <c>Assets/Scripts/input/Level5Input/PlayerControls.cs</c> (where
/// <c>Level5.Input</c>'s asmdef is). Fixing the importer setting before reimporting keeps Unity from
/// creating a second, stray wrapper at the old path.
/// </summary>
public class Level5PlayerTouchRetirementTests
{
    private static readonly string ScriptsInputRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "input");

    private static readonly string ProviderPath = Path.Combine(
        ScriptsInputRoot, "Level5Input", "PlayerControlsProvider.cs");

    private PlayerControls controls;

    [TearDown]
    public void TearDown()
    {
        if (controls != null)
        {
            UnityEngine.Object.DestroyImmediate(controls.asset);
            controls = null;
        }
    }

    [Test]
    public void PlayerTouchActionMapNoLongerExists()
    {
        controls = new PlayerControls();

        Assert.That(controls.asset.FindActionMap("PlayerTouch", throwIfNotFound: false), Is.Null);
    }

    [Test]
    public void SurvivingActionMapsStillExist()
    {
        controls = new PlayerControls();

        Assert.That(controls.asset.FindActionMap("Player", throwIfNotFound: false), Is.Not.Null);
        Assert.That(controls.asset.FindActionMap("UINavigation", throwIfNotFound: false), Is.Not.Null);
        Assert.That(controls.asset.FindActionMap("Other", throwIfNotFound: false), Is.Not.Null);
    }

    [Test]
    public void PlayerControlsCompilesIntoLevel5Input()
    {
        Assert.That(typeof(PlayerControls).Assembly.GetName().Name, Is.EqualTo("Level5.Input"));
    }

    [Test]
    public void NoProductionWrapperExistsAtTheStaleRootPath()
    {
        string stalePath = Path.Combine(ScriptsInputRoot, "PlayerControls.cs");

        Assert.That(
            File.Exists(stalePath),
            Is.False,
            "the generated PlayerControls wrapper must not regenerate at "
            + "Assets/Scripts/input/PlayerControls.cs - it belongs under "
            + "Assets/Scripts/input/Level5Input/, where PlayerControls.inputactions.meta's "
            + "wrapperCodePath must keep pointing.");
    }

    [Test]
    public void ExactlyOneProductionPlayerControlsWrapperExists()
    {
        string[] matches = Directory.GetFiles(ScriptsInputRoot, "PlayerControls.cs", SearchOption.AllDirectories);

        Assert.That(
            matches,
            Has.Length.EqualTo(1),
            "exactly one generated PlayerControls.cs must exist under Assets/Scripts/input/ - found: "
            + string.Join(", ", matches.Select(Level5TestSourceText.Relative)));
    }

    [Test]
    public void WrapperCodePathMetaStillPointsAtLevel5Input()
    {
        string metaPath = Path.Combine(ScriptsInputRoot, "PlayerControls.inputactions.meta");
        string text = File.ReadAllText(metaPath);

        Assert.That(text, Does.Contain("wrapperCodePath: Assets/Scripts/input/Level5Input/PlayerControls.cs"));
        Assert.That(text, Does.Not.Contain("wrapperCodePath: Assets/Scripts/input/PlayerControls.cs"));
    }

    [TestCase("EnablePlayerTouch")]
    [TestCase("DisablePlayerTouch")]
    [TestCase("playerTouchUsers")]
    public void PlayerControlsProviderHasNoPlayerTouchLifecycle(string identifier)
    {
        string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(ProviderPath));

        Assert.That(
            text,
            Does.Not.Match($@"\b{identifier}\b"),
            $"PlayerControlsProvider must not reintroduce {identifier} - PlayerTouch has no live "
            + "consumer and its map was retired from PlayerControls.inputactions (AUD-012 Phase 5 "
            + "Slice 76).");
    }
}
