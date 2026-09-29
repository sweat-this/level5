#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// <see cref="PlayerControlsProvider"/>'s use of the match-local <see cref="LocalGameplayDevicePlan"/>:
/// each local input slot's <see cref="PlayerControls"/> is paired only to that slot's captured devices,
/// the assignment is captured rather than re-resolved, and release/reset dispose exactly what they
/// should. Runs under <c>InputTestFixture</c> so the machine's real keyboard and gamepads are out of
/// the picture and every device here is a virtual one this fixture added.
/// </summary>
public class Level5GameplayDeviceProviderPlayModeTests : InputTestFixture
{
    private static readonly Regex NoDeviceAssignment = new Regex("has no device assignment in the active gameplay device plan");

    public override void Setup()
    {
        base.Setup();
        ResetProvider();
    }

    public override void TearDown()
    {
        ResetProvider();
        base.TearDown();
    }

    private static void ResetProvider()
    {
        // The same method Unity runs at SubsystemRegistration: disposes every cached controls instance
        // and forgets the plan, so no test inherits another's static state.
        MethodInfo reset = typeof(PlayerControlsProvider).GetMethod(
            "ResetState", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(reset, Is.Not.Null, "PlayerControlsProvider.ResetState must exist");
        reset.Invoke(null, null);
    }

    private static InputDevice[] DevicesOf(PlayerControls controls)
    {
        Assert.That(controls.devices.HasValue, Is.True, "gameplay controls must be paired to an explicit device list");
        return controls.devices.Value.ToArray();
    }

    private static void Configure(int localHumans)
    {
        Assert.That(
            PlayerControlsProvider.TryConfigureGameplayDevicePlan(localHumans, out string reason),
            Is.True,
            reason);
    }

    // ==================== per-slot device pairing ====================

    [Test]
    public void EachSlotIsPairedOnlyToItsOwnDevicesFromTwoGamepads()
    {
        InputSystem.AddDevice<Keyboard>();
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();
        Configure(2);

        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);
        PlayerControls slot1 = PlayerControlsProvider.AcquireGameplayControls(1);

        Assert.That(DevicesOf(slot0), Is.EqualTo(new InputDevice[] { padA }));
        Assert.That(DevicesOf(slot1), Is.EqualTo(new InputDevice[] { padB }));
    }

    [Test]
    public void EachSlotIsPairedOnlyToItsOwnDevicesFromKeyboardMouseAndOneGamepad()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
        Configure(2);

        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);
        PlayerControls slot1 = PlayerControlsProvider.AcquireGameplayControls(1);

        Assert.That(DevicesOf(slot0), Is.EqualTo(new InputDevice[] { keyboard, mouse }));
        Assert.That(DevicesOf(slot1), Is.EqualTo(new InputDevice[] { pad }));
    }

    [Test]
    public void ASingleHumanIsStillPairedToKeyboardMouseAndTheFirstGamepad()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Gamepad first = InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();
        keyboard.MakeCurrent();
        mouse.MakeCurrent();
        Configure(1);

        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);

        Assert.That(DevicesOf(slot0), Is.EquivalentTo(new InputDevice[] { keyboard, mouse, first }));
    }

    [Test]
    public void ASlotThePlanDoesNotSeatGetsNoDevicesRatherThanAnotherPlayersGamepad()
    {
        InputSystem.AddDevice<Gamepad>();
        Configure(1);

        LogAssert.Expect(LogType.Warning, NoDeviceAssignment);
        PlayerControls slot1 = PlayerControlsProvider.AcquireGameplayControls(1);

        Assert.That(DevicesOf(slot1), Is.Empty);
    }

    // ==================== capture, not re-resolution ====================

    [Test]
    public void ReorderingTheGlobalGamepadsAfterConfigurationDoesNotSwapOwnership()
    {
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();
        Configure(2);

        // The Input System now reports [padB, padC]: padA is gone and a new pad took the last position,
        // which is exactly what re-resolving Gamepad.all[index] per acquisition would have mistaken for
        // "player 0 is padB".
        InputSystem.RemoveDevice(padA);
        Gamepad padC = InputSystem.AddDevice<Gamepad>();
        Assert.That(Gamepad.all.ToArray(), Is.EqualTo(new Gamepad[] { padB, padC }), "the scenario must actually reorder");

        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);
        PlayerControls slot1 = PlayerControlsProvider.AcquireGameplayControls(1);

        Assert.That(DevicesOf(slot0), Is.EqualTo(new InputDevice[] { padA }), "player 0 keeps the pad it started with");
        Assert.That(DevicesOf(slot1), Is.EqualTo(new InputDevice[] { padB }), "player 1 keeps the pad it started with");
    }

    [Test]
    public void ReacquiringAfterReleaseUsesTheCapturedPlanNotTheCurrentDevices()
    {
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();
        Configure(2);
        PlayerControlsProvider.AcquireGameplayControls(1);

        PlayerControlsProvider.ReleaseGameplayControls(1);
        InputSystem.RemoveDevice(padA);
        PlayerControls reacquired = PlayerControlsProvider.AcquireGameplayControls(1);

        Assert.That(DevicesOf(reacquired), Is.EqualTo(new InputDevice[] { padB }));
    }

    [Test]
    public void AnUnconfiguredAcquisitionCapturesOneSingleHumanPlanAndKeepsIt()
    {
        Gamepad first = InputSystem.AddDevice<Gamepad>();
        Assert.That(PlayerControlsProvider.GameplayDevicePlan, Is.Null);

        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);
        Assert.That(PlayerControlsProvider.GameplayDevicePlan, Is.Not.Null);
        Assert.That(PlayerControlsProvider.GameplayDevicePlan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.SingleHuman));
        Assert.That(DevicesOf(slot0), Has.Member(first));

        PlayerControlsProvider.ReleaseGameplayControls(0);
        InputSystem.RemoveDevice(first);
        Gamepad replacement = InputSystem.AddDevice<Gamepad>();

        PlayerControls again = PlayerControlsProvider.AcquireGameplayControls(0);

        Assert.That(DevicesOf(again), Has.Member(first));
        Assert.That(DevicesOf(again), Has.No.Member(replacement));
    }

    // ==================== provider semantics preserved ====================

    [Test]
    public void RepeatedAcquisitionReturnsTheCachedControlsWithThePlayerMapEnabled()
    {
        InputSystem.AddDevice<Gamepad>();
        Configure(1);

        PlayerControls first = PlayerControlsProvider.AcquireGameplayControls(0);
        first.Player.Disable();
        PlayerControls second = PlayerControlsProvider.AcquireGameplayControls(0);

        Assert.That(second, Is.SameAs(first));
        Assert.That(second.Player.enabled, Is.True, "reacquiring re-enables the Player map, as before");
        Assert.That(second.Other.enabled, Is.False, "Other stays the shared owner's, not a per-player map");
    }

    [UnityTest]
    public IEnumerator ReleaseDisposesOnlyTheRequestedSlot()
    {
        InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();
        Configure(2);
        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);
        PlayerControls slot1 = PlayerControlsProvider.AcquireGameplayControls(1);
        InputActionAsset slot0Asset = slot0.asset;
        InputActionAsset slot1Asset = slot1.asset;

        PlayerControlsProvider.ReleaseGameplayControls(0);
        yield return null;

        Assert.That(slot0Asset == null, Is.True, "slot 0's controls were disposed");
        Assert.That(slot1Asset != null, Is.True, "slot 1's controls were left alone");
        Assert.That(slot1.Player.enabled, Is.True);
        Assert.That(PlayerControlsProvider.AcquireGameplayControls(1), Is.SameAs(slot1));
        Assert.That(PlayerControlsProvider.AcquireGameplayControls(0), Is.Not.SameAs(slot0), "slot 0 gets a fresh instance");
    }

    [Test]
    public void ReleasingASlotThatWasNeverAcquiredDoesNothing()
    {
        InputSystem.AddDevice<Gamepad>();
        Configure(1);
        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);

        Assert.DoesNotThrow(() => PlayerControlsProvider.ReleaseGameplayControls(1));

        Assert.That(slot0.Player.enabled, Is.True);
    }

    // ==================== configuration ====================

    [Test]
    public void ConfiguringTheNextMatchDisposesStaleControlsInsteadOfReusingThem()
    {
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Configure(1);
        PlayerControls stale = PlayerControlsProvider.AcquireGameplayControls(0);
        InputActionAsset staleAsset = stale.asset;
        InputSystem.RemoveDevice(padA);
        Gamepad padB = InputSystem.AddDevice<Gamepad>();

        // A scene reload composes the next match before the previous match's owners have released.
        LogAssert.Expect(LogType.Warning, new Regex("previous match are still held"));
        Configure(1);
        PlayerControls next = PlayerControlsProvider.AcquireGameplayControls(0);

        Assert.That(next, Is.Not.SameAs(stale), "the new match never inherits controls paired by the old plan");
        Assert.That(DevicesOf(next), Has.Member(padB));
        Assert.That(DevicesOf(next), Has.No.Member(padA));
        Assert.DoesNotThrow(() => PlayerControlsProvider.ReleaseGameplayControls(0), "the old owner's late release is harmless");
    }

    [Test]
    public void AnUnseatableRosterFailsConfigurationAndLeavesNoPlan()
    {
        InputSystem.AddDevice<Keyboard>();

        bool configured = PlayerControlsProvider.TryConfigureGameplayDevicePlan(2, out string reason);

        Assert.That(configured, Is.False);
        Assert.That(reason, Does.StartWith("Two local players need two gamepads"));
        Assert.That(PlayerControlsProvider.GameplayDevicePlan, Is.Null);
    }

    [Test]
    public void APreflightReportsSeatabilityWithoutChangingTheActivePlan()
    {
        Assert.That(PlayerControlsProvider.TryPreflightGameplayDevices(2, out string refusal), Is.False);
        Assert.That(refusal, Is.Not.Null.And.Not.Empty);

        InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();

        Assert.That(PlayerControlsProvider.TryPreflightGameplayDevices(2, out string accepted), Is.True);
        Assert.That(accepted, Is.Null);
        Assert.That(PlayerControlsProvider.TryPreflightGameplayDevices(1, out _), Is.True);
        Assert.That(PlayerControlsProvider.GameplayDevicePlan, Is.Null, "preflight has no side effects");
    }

    [Test]
    public void ClearingThePlanLetsTheNextMatchConfigureADifferentLayout()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        keyboard.MakeCurrent();
        InputSystem.AddDevice<Gamepad>();
        Configure(2);
        Assert.That(PlayerControlsProvider.GameplayDevicePlan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.KeyboardMouseAndGamepad));

        PlayerControlsProvider.ClearGameplayDevicePlan();
        Assert.That(PlayerControlsProvider.GameplayDevicePlan, Is.Null);
        InputSystem.AddDevice<Gamepad>();
        Configure(2);

        Assert.That(PlayerControlsProvider.GameplayDevicePlan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.TwoGamepads));
    }

    // ==================== on-screen gamepads ====================

    private static Gamepad AddOnScreenGamepad()
    {
        // What an OnScreenControl bound to <Gamepad>/leftStick does: a real Gamepad-layout device
        // tagged with the "OnScreen" usage, visible in Gamepad.all like any physical pad.
        Gamepad onScreen = InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDeviceUsage(onScreen, "OnScreen");
        return onScreen;
    }

    [Test]
    public void AnOnScreenGamepadIsNotCapturedAsAPhysicalGamepad()
    {
        Gamepad physical = InputSystem.AddDevice<Gamepad>();
        Gamepad onScreen = AddOnScreenGamepad();
        Assert.That(Gamepad.all.ToArray(), Is.EquivalentTo(new[] { physical, onScreen }), "the scenario must put both in Gamepad.all");

        LocalGameplayDeviceAvailability availability = LocalGameplayDeviceAvailability.Capture();

        Assert.That(availability.Gamepads, Is.EqualTo(new[] { physical }));
        Assert.That(LocalGameplayDeviceAvailability.IsOnScreen(onScreen), Is.True);
        Assert.That(LocalGameplayDeviceAvailability.IsOnScreen(physical), Is.False);
    }

    [Test]
    public void AnOnScreenGamepadNeverMakesATwoPlayerLayout()
    {
        InputSystem.AddDevice<Keyboard>().MakeCurrent();
        AddOnScreenGamepad();

        Assert.That(PlayerControlsProvider.TryPreflightGameplayDevices(2, out string reason), Is.False);
        StringAssert.Contains("0 gamepad(s)", reason);
    }

    [Test]
    public void TwoPlayersAreNeverGivenTheOnScreenGamepadEvenWhenItIsListedFirst()
    {
        Gamepad onScreen = AddOnScreenGamepad();
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();
        Configure(2);

        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);
        PlayerControls slot1 = PlayerControlsProvider.AcquireGameplayControls(1);

        Assert.That(DevicesOf(slot0), Is.EqualTo(new InputDevice[] { padA }));
        Assert.That(DevicesOf(slot1), Is.EqualTo(new InputDevice[] { padB }));
        Assert.That(DevicesOf(slot0), Has.No.Member(onScreen));
        Assert.That(DevicesOf(slot1), Has.No.Member(onScreen));
    }

    [Test]
    public void ASingleHumanStillOwnsTheOnScreenGamepadEvenWhenItAppearsAfterConfiguration()
    {
        Gamepad physical = InputSystem.AddDevice<Gamepad>();
        Configure(1);

        // The scene's OnScreenControl enables after GameLevelManager.Awake configured the plan.
        Gamepad onScreen = AddOnScreenGamepad();
        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);

        Assert.That(DevicesOf(slot0), Has.Member(physical));
        Assert.That(DevicesOf(slot0), Has.Member(onScreen), "mobile touch movement rides the on-screen gamepad");
    }

    [Test]
    public void ASingleHumanWithOnlyAnOnScreenGamepadStillGetsIt()
    {
        Gamepad onScreen = AddOnScreenGamepad();
        Configure(1);

        PlayerControls slot0 = PlayerControlsProvider.AcquireGameplayControls(0);

        Assert.That(DevicesOf(slot0), Is.EqualTo(new InputDevice[] { onScreen }));
    }

    // ==================== subsystem reset ====================

    [UnityTest]
    public IEnumerator SubsystemResetDisposesEveryPlayersControlsAndForgetsThePlan()
    {
        InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();
        Configure(2);
        InputActionAsset slot0Asset = PlayerControlsProvider.AcquireGameplayControls(0).asset;
        InputActionAsset slot1Asset = PlayerControlsProvider.AcquireGameplayControls(1).asset;
        InputActionAsset sharedAsset = PlayerControlsProvider.Controls.asset;

        ResetProvider();
        yield return null;

        Assert.That(slot0Asset == null, Is.True);
        Assert.That(slot1Asset == null, Is.True);
        Assert.That(sharedAsset == null, Is.True, "the shared menu/debug controls are reset too");
        Assert.That(PlayerControlsProvider.GameplayDevicePlan, Is.Null);
        Assert.That(
            PlayerControlsProvider.TryConfigureGameplayDevicePlan(2, out string reason),
            Is.True,
            "reset also lifts the held-controls guard: " + reason);
    }
}
#endif
