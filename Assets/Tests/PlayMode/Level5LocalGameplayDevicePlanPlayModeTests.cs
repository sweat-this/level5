#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;

/// <summary>
/// The match-local device plan: which physical devices each local input slot listens to.
///
/// Every case builds its own <see cref="LocalGameplayDeviceAvailability"/> from virtual devices instead
/// of reading <c>Gamepad.all</c>/<c>Keyboard.current</c>, so the result never depends on what happens to
/// be plugged into the machine running the suite. It runs under <c>InputTestFixture</c> rather than in
/// EditMode so adding and removing virtual devices never touches the editor's own Input System state.
/// Provider behaviour (capture, release, reset) is in <see cref="Level5GameplayDeviceProviderPlayModeTests"/>.
/// </summary>
public class Level5LocalGameplayDevicePlanPlayModeTests : InputTestFixture
{
    private static T Device<T>() where T : InputDevice
    {
        return InputSystem.AddDevice<T>();
    }

    private LocalGameplayDeviceAvailability Availability(
        bool keyboard = false,
        bool mouse = false,
        bool touchscreen = false,
        int gamepads = 0)
    {
        List<Gamepad> pads = new List<Gamepad>();
        for (int index = 0; index < gamepads; index++)
        {
            pads.Add(Device<Gamepad>());
        }

        return new LocalGameplayDeviceAvailability(
            keyboard ? Device<Keyboard>() : null,
            mouse ? Device<Mouse>() : null,
            touchscreen ? Device<Touchscreen>() : null,
            pads);
    }

    private static LocalGameplayDevicePlan Create(int humans, LocalGameplayDeviceAvailability availability)
    {
        bool created = LocalGameplayDevicePlan.TryCreate(humans, availability, out LocalGameplayDevicePlan plan, out string reason);
        Assert.That(created, Is.True, reason);
        Assert.That(reason, Is.Null);
        return plan;
    }

    private static string Refuse(int humans, LocalGameplayDeviceAvailability availability)
    {
        bool created = LocalGameplayDevicePlan.TryCreate(humans, availability, out LocalGameplayDevicePlan plan, out string reason);
        Assert.That(created, Is.False);
        Assert.That(plan, Is.Null, "a refused layout must not hand back a usable plan");
        Assert.That(reason, Is.Not.Null.And.Not.Empty);
        return reason;
    }

    // ==================== single human ====================

    [Test]
    public void SingleHumanGetsKeyboardMouseTouchscreenAndTheFirstGamepad()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true, mouse: true, touchscreen: true, gamepads: 2);

        LocalGameplayDevicePlan plan = Create(1, devices);

        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.SingleHuman));
        Assert.That(plan.LocalHumanCount, Is.EqualTo(1));
        Assert.That(
            plan.DevicesFor(0),
            Is.EqualTo(new InputDevice[] { devices.Keyboard, devices.Mouse, devices.Touchscreen, devices.Gamepads[0] }),
            "the pre-plan behaviour for slot 0, in the same order");
        Assert.That(plan.DevicesFor(0), Has.No.Member(devices.Gamepads[1]));
    }

    [Test]
    public void SingleHumanWithKeyboardMouseAndOneGamepadMatchesTheHistoricalAssignment()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true, mouse: true, gamepads: 1);

        LocalGameplayDevicePlan plan = Create(1, devices);

        Assert.That(
            plan.DevicesFor(0),
            Is.EquivalentTo(new InputDevice[] { devices.Keyboard, devices.Mouse, devices.Gamepads[0] }));
    }

    [Test]
    public void SingleHumanWithNoDevicesStillSucceeds()
    {
        LocalGameplayDevicePlan plan = Create(1, Availability());

        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.SingleHuman));
        Assert.That(plan.DevicesFor(0), Is.Empty, "a solo launch is never refused for lack of a controller");
    }

    [Test]
    public void NoLocalHumansReservesNothing()
    {
        LocalGameplayDevicePlan plan = Create(0, Availability(keyboard: true, gamepads: 2));

        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.None));
        Assert.That(plan.LocalHumanCount, Is.EqualTo(0));
        Assert.That(plan.HasSlot(0), Is.False);
    }

    // ==================== two humans ====================

    [Test]
    public void TwoGamepadsGiveEachPlayerOneGamepadAndNobodyTheKeyboard()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true, mouse: true, touchscreen: true, gamepads: 2);

        LocalGameplayDevicePlan plan = Create(2, devices);

        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.TwoGamepads));
        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { devices.Gamepads[0] }));
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { devices.Gamepads[1] }));
    }

    [Test]
    public void TwoGamepadsFromMoreGamepadsUsesTheFirstTwoInOrder()
    {
        LocalGameplayDeviceAvailability devices = Availability(gamepads: 4);

        LocalGameplayDevicePlan plan = Create(2, devices);

        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { devices.Gamepads[0] }));
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { devices.Gamepads[1] }));
    }

    [Test]
    public void KeyboardMouseAndOneGamepadSplitsTheKeyboardFromTheGamepad()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true, mouse: true, touchscreen: true, gamepads: 1);

        LocalGameplayDevicePlan plan = Create(2, devices);

        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.KeyboardMouseAndGamepad));
        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { devices.Keyboard, devices.Mouse }));
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { devices.Gamepads[0] }));
        Assert.That(
            plan.DevicesFor(0).Concat(plan.DevicesFor(1)),
            Has.No.Member(devices.Touchscreen),
            "touch never seats a second player");
    }

    [Test]
    public void KeyboardWithoutAMouseStillSeatsSlotZeroAlongsideAGamepad()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true, gamepads: 1);

        LocalGameplayDevicePlan plan = Create(2, devices);

        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { devices.Keyboard }));
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { devices.Gamepads[0] }));
    }

    [Test]
    public void TheSameGamepadIsNeverAssignedToBothPlayers()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true, gamepads: 1);

        LocalGameplayDevicePlan plan = Create(2, devices);

        Assert.That(plan.DevicesFor(0), Has.No.Member(devices.Gamepads[0]));
        Assert.That(plan.DevicesFor(1), Has.Member(devices.Gamepads[0]));
    }

    [TestCase(1, true, true, true, 0)]
    [TestCase(1, true, true, true, 3)]
    [TestCase(2, true, true, true, 1)]
    [TestCase(2, true, true, true, 2)]
    [TestCase(2, true, true, true, 4)]
    [TestCase(2, false, false, false, 2)]
    public void NoDeviceInstanceAppearsUnderMoreThanOneSlot(
        int humans, bool keyboard, bool mouse, bool touchscreen, int gamepads)
    {
        LocalGameplayDevicePlan plan = Create(humans, Availability(keyboard, mouse, touchscreen, gamepads));

        List<InputDevice> everyAssignment = new List<InputDevice>();
        for (int slot = 0; slot < plan.LocalHumanCount; slot++)
        {
            everyAssignment.AddRange(plan.DevicesFor(slot));
        }

        Assert.That(everyAssignment, Is.Unique, "a device instance must belong to at most one local player");
    }

    // ==================== refusals ====================

    [Test]
    public void KeyboardOnlyCannotSeatTwoPlayers()
    {
        string reason = Refuse(2, Availability(keyboard: true, mouse: true));

        Assert.That(reason, Is.EqualTo(
            "Two local players need two gamepads, or a keyboard plus one gamepad. Detected: keyboard, 0 gamepad(s)."));
    }

    [Test]
    public void OneGamepadWithoutAKeyboardCannotSeatTwoPlayers()
    {
        string reason = Refuse(2, Availability(gamepads: 1));

        Assert.That(reason, Is.EqualTo(
            "Two local players need two gamepads, or a keyboard plus one gamepad. Detected: no keyboard, 1 gamepad(s)."));
    }

    [Test]
    public void AMouseAndAGamepadIsNotAKeyboardLayout()
    {
        Refuse(2, Availability(mouse: true, gamepads: 1));
    }

    [Test]
    public void ATouchOnlyDeviceCannotSeatTwoPlayers()
    {
        string reason = Refuse(2, Availability(touchscreen: true));

        Assert.That(reason, Is.EqualTo(
            "Two local players need two gamepads, or a keyboard plus one gamepad. "
            + "Detected: no keyboard, 0 gamepad(s), touchscreen (cannot seat a second player)."));
    }

    [Test]
    public void TouchPlusOneGamepadDoesNotMakeALayout()
    {
        Refuse(2, Availability(touchscreen: true, gamepads: 1));
    }

    [Test]
    public void NoDevicesAtAllCannotSeatTwoPlayers()
    {
        Refuse(2, Availability());
    }

    [Test]
    public void MoreThanTwoLocalPlayersAreRefusedEvenWithEnoughGamepads()
    {
        string reason = Refuse(3, Availability(keyboard: true, gamepads: 4));

        Assert.That(reason, Is.EqualTo("3 local players are not supported yet; at most 2 can share one device set."));
    }

    [Test]
    public void ANegativeLocalPlayerCountIsRefused()
    {
        Refuse(-1, Availability(gamepads: 2));
    }

    [Test]
    public void TheRefusalReasonIsDeterministic()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true);

        Assert.That(Refuse(2, devices), Is.EqualTo(Refuse(2, devices)));
    }

    // ==================== immutability ====================

    [Test]
    public void MutatingACopiedDeviceArrayDoesNotChangeThePlan()
    {
        LocalGameplayDeviceAvailability devices = Availability(keyboard: true, gamepads: 1);
        LocalGameplayDevicePlan plan = Create(2, devices);

        InputDevice[] copy = plan.CopyDevicesFor(1);
        copy[0] = null;

        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { devices.Gamepads[0] }));
        Assert.That(plan.CopyDevicesFor(1), Is.Not.SameAs(plan.CopyDevicesFor(1)));
    }

    [Test]
    public void ASlotOutsideThePlanHasNoDevices()
    {
        LocalGameplayDevicePlan plan = Create(2, Availability(gamepads: 2));

        Assert.That(plan.HasSlot(2), Is.False);
        Assert.That(plan.HasSlot(-1), Is.False);
        Assert.That(plan.DevicesFor(2), Is.Empty);
        Assert.That(plan.CopyDevicesFor(2), Is.Empty);
    }

    [Test]
    public void ThePlanDoesNotFollowLaterChangesToTheAvailabilitySnapshotsGamepadList()
    {
        List<Gamepad> pads = new List<Gamepad> { Device<Gamepad>(), Device<Gamepad>() };
        Gamepad first = pads[0];
        Gamepad second = pads[1];
        LocalGameplayDevicePlan plan = Create(
            2,
            new LocalGameplayDeviceAvailability(null, null, null, pads));

        pads.Reverse();

        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { first }));
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { second }));
    }
}
#endif
