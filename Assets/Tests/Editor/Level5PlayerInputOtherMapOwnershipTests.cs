using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// AUD-012 Phase 5 Slice 77: <c>PlayerInputReader.DebugChangeHeld</c>/<c>DebugLightningPressed</c> used
/// to read the per-player <c>PlayerControls</c> instance's <c>Other</c> map. <c>PlayerController</c>
/// builds that reader from <c>PlayerControlsProvider.AcquireGameplayControls(playerId)</c>, which only
/// ever enables that instance's <c>Player</c> map - <c>Other</c> is a shared map owned by
/// <c>PlayerControlsProvider.Controls</c> and enabled by <c>GameLevelManager.OnEnable</c> /
/// <c>EnableOther()</c>. A per-player <c>controls.Other</c> read was therefore always reading a
/// disabled action and always returning false: the debug-change modifier could never suppress
/// call-ball, and debug lightning could never fire.
///
/// This fixture covers the fix (the reader now delegates to the shared
/// <c>PlayerControlsProvider</c> owner) and adds the Phase 5 action-map ownership matrix guard: one
/// map, one documented owner, enumerated directly off <c>PlayerControls.inputactions</c> so a map
/// added without updating this fixture - or <c>PlayerTouch</c> returning - fails loudly.
/// </summary>
public class Level5PlayerInputOtherMapOwnershipTests
{
    private static readonly string ScriptsInputRoot = Path.Combine(
        Directory.GetCurrentDirectory(), "Assets", "Scripts", "input");

    private static readonly string ReaderPath = Path.Combine(ScriptsInputRoot, "Level5Input", "PlayerInputReader.cs");

    /// <summary>
    /// The live action-map ownership matrix (AUD-012 Phase 5). Keep this in lockstep with
    /// <c>docs/player-input-architecture.md</c> - a mismatch between the two is itself a defect, not
    /// something to silence here.
    /// </summary>
    private static readonly Dictionary<string, string> KnownActionMapOwners = new Dictionary<string, string>
    {
        { "Player", "per-player gameplay controls (PlayerControlsProvider.AcquireGameplayControls); shared Pause/start compatibility" },
        { "UINavigation", "menu and pause UI lifecycle (PlayerControlsProvider.EnableUINavigation/DisableUINavigation)" },
        { "Other", "shared runtime/debug lifecycle (PlayerControlsProvider.EnableOther/DisableOther; GameLevelManager.OnEnable/OnDisable)" },
    };

    /// <summary>
    /// <c>PlayerControls.Dispose()</c> destroys its <c>InputActionAsset</c> with
    /// <c>UnityEngine.Object.Destroy</c>, which the editor refuses outside play mode and reports as an
    /// error. Mirrors the identical guard in <c>Level5PlayerInputReaderCompositionTests</c>.
    /// </summary>
    private static readonly Regex DisposeOutsidePlayMode = new Regex("Destroy may not be called from edit mode");

    private const int TestPlayerId = 97;

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

    // ==================== ownership matrix guard ====================

    [Test]
    public void EveryActionMapInTheAssetHasAKnownExplicitOwner()
    {
        controls = new PlayerControls();
        string[] actualMaps = controls.asset.actionMaps.Select(map => map.name).ToArray();

        Assert.That(
            actualMaps,
            Is.EquivalentTo(KnownActionMapOwners.Keys),
            "PlayerControls.inputactions must contain exactly the action maps this ownership matrix "
            + "documents. PlayerTouch must not return, and any new action map needs an explicit owner "
            + "added to KnownActionMapOwners (and docs/player-input-architecture.md) before this test "
            + "can pass (AUD-012 Phase 5 Slice 77).");
    }

    // ==================== PlayerControlsProvider shared Other surface ====================

    [Test]
    public void AcquireGameplayControls_EnablesThePerPlayerPlayerMap()
    {
        PlayerControls playerControls = PlayerControlsProvider.AcquireGameplayControls(TestPlayerId);
        try
        {
            Assert.That(playerControls.Player.enabled, Is.True);
        }
        finally
        {
            LogAssert.Expect(LogType.Error, DisposeOutsidePlayMode);
            PlayerControlsProvider.ReleaseGameplayControls(TestPlayerId);
        }
    }

    [Test]
    public void AcquireGameplayControls_DoesNotEnableThePerPlayerOtherMap()
    {
        PlayerControls playerControls = PlayerControlsProvider.AcquireGameplayControls(TestPlayerId);
        try
        {
            Assert.That(
                playerControls.Other.enabled,
                Is.False,
                "Other has one shared owner (PlayerControlsProvider.Controls) - a per-player controls "
                + "instance must not have it enabled too.");
        }
        finally
        {
            LogAssert.Expect(LogType.Error, DisposeOutsidePlayMode);
            PlayerControlsProvider.ReleaseGameplayControls(TestPlayerId);
        }
    }

    [Test]
    public void EnableOther_ReportsTheSharedChangeControlAsEnabled_AndDisableOtherRevertsIt()
    {
        // Asserts relative to whatever this static provider's ref count already was, rather than
        // asserting an absolute "disabled" baseline this test does not own - code review, 2026-09-17.
        // No other EditMode fixture currently touches EnableOther/DisableOther, so that baseline is
        // false in practice, but this formulation stays correct even if that ever changes.
        bool enabledBeforeThisTest = PlayerControlsProvider.DevChangeControlEnabled;

        PlayerControlsProvider.EnableOther();
        try
        {
            Assert.That(PlayerControlsProvider.DevChangeControlEnabled, Is.True);
        }
        finally
        {
            PlayerControlsProvider.DisableOther();
        }

        Assert.That(
            PlayerControlsProvider.DevChangeControlEnabled,
            Is.EqualTo(enabledBeforeThisTest),
            "DisableOther must return the shared Other owner to its prior enabled state.");
    }

    [Test]
    public void DevChangeHeld_IsFalseWithNoHeldInputEvenWhileOtherIsEnabled()
    {
        // EditMode has no way to simulate a held key/button press against a real device (no
        // InputTestFixture in this project - see Level5PlayerInputReaderCompositionTests' summary for
        // why the legacy touch fallback has the same limitation). This still exercises the real code
        // path: DevChangeHeld reads a live, enabled action rather than the always-disabled per-player
        // one the bug read.
        PlayerControlsProvider.EnableOther();
        try
        {
            Assert.That(PlayerControlsProvider.DevChangeHeld, Is.False);
        }
        finally
        {
            PlayerControlsProvider.DisableOther();
        }
    }

    // ==================== PlayerInputReader source guards ====================

    [Test]
    public void PlayerInputReaderSourceNoLongerReadsThePerPlayerOtherMap()
    {
        string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(ReaderPath));

        Assert.That(
            text,
            Does.Not.Match(@"\bcontrols\.Other\b"),
            "PlayerInputReader must not read controls.Other - that per-player instance never has Other "
            + "enabled. Debug/change reads must go through the shared PlayerControlsProvider owner "
            + "instead (AUD-012 Phase 5 Slice 77).");
    }

    [TestCase("controls.Player.movement")]
    [TestCase("controls.Player.run")]
    [TestCase("controls.Player.jump")]
    [TestCase("controls.Player.shoot")]
    [TestCase("controls.Player.callball")]
    [TestCase("controls.Player.attack")]
    [TestCase("controls.Player.block")]
    [TestCase("controls.Player.special")]
    public void PlayerInputReaderSourceStillReadsGameplayFromThePerPlayerPlayerMap(string expectedRead)
    {
        string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(ReaderPath));

        Assert.That(
            text,
            Does.Contain(expectedRead),
            $"PlayerInputReader must keep reading normal gameplay input from the per-player Player map "
            + $"- expected to find '{expectedRead}' unchanged.");
    }

    [Test]
    public void PlayerInputReaderSourceUsesTheSharedProviderForDebugChangeReads()
    {
        string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(ReaderPath));

        Assert.That(text, Does.Contain("PlayerControlsProvider.DevChangeHeld"));
        Assert.That(text, Does.Contain("PlayerControlsProvider.DevChangeControlEnabled"));
    }

    // ==================== keyboard binding collision (why DebugChangeHeld is dev-gated) ====================

    /// <summary>
    /// Code review, 2026-09-17: fixing <c>DebugChangeHeld</c>'s ownership bug made a pre-existing
    /// <c>PlayerControls.inputactions</c> binding collision live for the first time. <c>&lt;Keyboard&gt;
    /// /shift</c> (<c>Player/run</c>) is Unity's synthetic control for "either shift key", the identical
    /// physical coverage as <c>&lt;Keyboard&gt;/leftShift</c> + <c>&lt;Keyboard&gt;/rightShift</c>
    /// (<c>Other/change</c>). Every keyboard player holding Shift to run would therefore also read as
    /// holding the debug/change modifier, suppressing call-ball via <c>PlayerController</c>'s
    /// <c>!reader.DebugChangeHeld</c> guard. This is why <c>PlayerInputReader.DebugChangeHeld</c> stays
    /// <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>-gated instead of being usable in release builds - see
    /// <see cref="DebugChangeHeldAndDebugLightningPressed_AreBothEditorOrDevelopmentGated"/>.
    ///
    /// If this test ever fails because the overlap was removed from the asset, that is good news, but
    /// re-check whether the gate above is still needed before removing it - do not let the two drift
    /// apart silently.
    /// </summary>
    [Test]
    public void OtherChangeAndPlayerRun_ShareTheKeyboardShiftKeys()
    {
        controls = new PlayerControls();

        string[] changeKeyboardPaths = KeyboardBindingPaths(controls.asset, "Other", "change");
        string[] runKeyboardPaths = KeyboardBindingPaths(controls.asset, "Player", "run");

        Assert.That(changeKeyboardPaths, Is.EquivalentTo(new[] { "<Keyboard>/leftShift", "<Keyboard>/rightShift" }));
        Assert.That(runKeyboardPaths, Is.EquivalentTo(new[] { "<Keyboard>/shift" }));
    }

    [Test]
    public void DebugChangeHeldAndDebugLightningPressed_AreBothEditorOrDevelopmentGated()
    {
        string text = Level5TestSourceText.StripCommentsAndLiterals(File.ReadAllText(ReaderPath));

        Assert.That(
            Regex.Matches(text, @"#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD").Count,
            Is.GreaterThanOrEqualTo(2),
            "PlayerInputReader must gate both DebugChangeHeld and DebugLightningPressed behind "
            + "UNITY_EDITOR || DEVELOPMENT_BUILD - DebugChangeHeld's gate is what keeps the Other/change "
            + "vs Player/run keyboard collision out of shipped release builds (see "
            + "OtherChangeAndPlayerRun_ShareTheKeyboardShiftKeys).");
    }

    // ==================== helpers ====================

    private static string[] KeyboardBindingPaths(InputActionAsset asset, string mapName, string actionName)
    {
        InputAction action = asset.FindActionMap(mapName, throwIfNotFound: true).FindAction(actionName, throwIfNotFound: true);
        return action.bindings
            .Where(binding => binding.path != null && binding.path.StartsWith("<Keyboard>"))
            .Select(binding => binding.path)
            .ToArray();
    }
}
