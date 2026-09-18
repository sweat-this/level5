using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;

/// <summary>
/// AUD-012 Phase 5 Slice 81: contract tests for the additive Input System <see cref="OnScreenStick"/>
/// movement pilot added to <c>touch_joystick.prefab</c> (docs/player-input-architecture.md migration
/// plan step 3, first half only - removing the legacy joystick fallback is still future work, gated on
/// device playtesting per that document and docs/player-input-smoke-validation.md's "Mobile movement"
/// section).
///
/// These are structural/asset-inspection checks (prefab composition, control path, tag, binding
/// existence, project setting), not live touch/drag behavior - real device input cannot be established
/// by a headless test runner (see docs/player-input-smoke-validation.md's "How to read this document").
/// <c>PlayerTouch</c> retirement is already covered by <see cref="Level5PlayerTouchRetirementTests"/>
/// and is not duplicated here.
/// </summary>
public class Level5OnScreenStickPilotTests
{
    private const string PrefabPath = "Assets/Resources/Prefabs/critical/touch_joystick.prefab";

    private GameObject prefabRoot;
    private PlayerControls controls;

    [SetUp]
    public void SetUp()
    {
        prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

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
    public void PrefabStillLoads()
    {
        Assert.That(prefabRoot, Is.Not.Null, "touch_joystick.prefab must still exist and load as a GameObject.");
    }

    [Test]
    public void ExactlyOneOnScreenStickPilotExists()
    {
        OnScreenStick[] sticks = prefabRoot.GetComponentsInChildren<OnScreenStick>(true);

        Assert.That(
            sticks,
            Has.Length.EqualTo(1),
            "touch_joystick.prefab must contain exactly one OnScreenStick pilot (AUD-012 Phase 5 "
            + "Slice 81) - found " + sticks.Length + ".");
    }

    [Test]
    public void OnScreenStickPilotUsesTheGamepadLeftStickControlPath()
    {
        OnScreenStick stick = prefabRoot.GetComponentsInChildren<OnScreenStick>(true).Single();

        Assert.That(
            stick.controlPath,
            Is.EqualTo("<Gamepad>/leftStick"),
            "The OnScreenStick pilot must feed <Gamepad>/leftStick - the same control path Player/"
            + "movement's gamepad composite already binds, so no PlayerControls.inputactions change was "
            + "needed for it to drive movement.");
    }

    [Test]
    public void LegacyFloatingJoystickComponentStillExists()
    {
        FloatingJoystick joystick = prefabRoot.GetComponentInChildren<FloatingJoystick>(true);

        Assert.That(
            joystick,
            Is.Not.Null,
            "The legacy FloatingJoystick fallback must remain in touch_joystick.prefab until device "
            + "playtesting allows its removal (docs/player-input-architecture.md migration plan step 3).");
    }

    [Test]
    public void TouchJoystickRootStillHasTheJoystickTag()
    {
        Assert.That(
            prefabRoot.CompareTag("joystick"),
            Is.True,
            "GameLevelManager.Start() resolves the legacy joystick via "
            + "GameObject.FindGameObjectWithTag(\"joystick\") - this tag must not move or disappear.");
    }

    [Test]
    public void PlayerMovementStillHasACompatibleGamepadLeftStickBinding()
    {
        controls = new PlayerControls();

        string[] leftStickPaths = controls.Player.movement.bindings
            .Where(binding => binding.path != null && binding.path.StartsWith("<Gamepad>/leftStick"))
            .Select(binding => binding.path)
            .ToArray();

        Assert.That(
            leftStickPaths,
            Is.Not.Empty,
            "Player/movement must keep a <Gamepad>/leftStick-compatible binding - the OnScreenStick "
            + "pilot's <Gamepad>/leftStick control path only drives movement while this holds.");
    }

    [Test]
    public void ActiveInputHandlerRemainsBoth()
    {
        string text = File.ReadAllText(Path.Combine(
            Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset"));

        Assert.That(
            text,
            Does.Contain("activeInputHandler: 2"),
            "activeInputHandler must remain 2 (Both) - the OnScreenStick pilot is additive alongside "
            + "the legacy joystick, not a switch to Input System-only.");
    }
}
