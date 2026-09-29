using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>
/// Which physical layout a <see cref="LocalGameplayDevicePlan"/> settled on.
/// </summary>
public enum LocalGameplayDeviceLayout
{
    /// <summary>No local humans, so no device is reserved.</summary>
    None,

    /// <summary>One local human: keyboard, mouse, touchscreen and the first gamepad, where present.</summary>
    SingleHuman,

    /// <summary>Two local humans, one gamepad each. Keyboard and mouse belong to nobody.</summary>
    TwoGamepads,

    /// <summary>Two local humans: slot 0 on keyboard and mouse, slot 1 on the first gamepad.</summary>
    KeyboardMouseAndGamepad
}

/// <summary>
/// A snapshot of the devices the Input System currently reports, taken once so a plan is built from
/// one consistent view instead of several separate <c>Gamepad.all</c>/<c>Keyboard.current</c> reads.
/// Tests build one directly from virtual devices.
/// </summary>
public readonly struct LocalGameplayDeviceAvailability
{
    public LocalGameplayDeviceAvailability(
        Keyboard keyboard,
        Mouse mouse,
        Touchscreen touchscreen,
        IReadOnlyList<Gamepad> gamepads)
    {
        Keyboard = keyboard;
        Mouse = mouse;
        Touchscreen = touchscreen;
        Gamepads = gamepads ?? Array.Empty<Gamepad>();
    }

    public Keyboard Keyboard { get; }

    public Mouse Mouse { get; }

    public Touchscreen Touchscreen { get; }

    /// <summary>Connected physical gamepads in the Input System's own order, which is what "gamepad 0" means.</summary>
    public IReadOnlyList<Gamepad> Gamepads { get; }

    /// <summary>
    /// The physical devices the Input System reports right now. On-screen gamepads are left out of
    /// <see cref="Gamepads"/> - see <see cref="IsOnScreen"/>.
    /// </summary>
    public static LocalGameplayDeviceAvailability Capture()
    {
        List<Gamepad> gamepads = new List<Gamepad>(Gamepad.all.Count);
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (!IsOnScreen(gamepad))
            {
                gamepads.Add(gamepad);
            }
        }

        return new LocalGameplayDeviceAvailability(Keyboard.current, Mouse.current, Touchscreen.current, gamepads);
    }

    /// <summary>
    /// Whether <paramref name="device"/> is a virtual device an <c>OnScreenControl</c> (for example the
    /// <c>OnScreenStick</c> bound to <c>&lt;Gamepad&gt;/leftStick</c> in <c>touch_joystick.prefab</c>) created to
    /// carry touch UI input. It is a real <see cref="Gamepad"/> to the Input System - it shows up in
    /// <c>Gamepad.all</c> on every platform whenever such a control is active - but nobody is holding
    /// it, so it must never satisfy a "two gamepads" or "keyboard plus one gamepad" layout.
    /// </summary>
    public static bool IsOnScreen(InputDevice device)
    {
        if (device == null)
        {
            return false;
        }

        foreach (UnityEngine.InputSystem.Utilities.InternedString usage in device.usages)
        {
            if (usage == OnScreenUsage)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The on-screen gamepads that exist right now, in the Input System's order.</summary>
    public static List<Gamepad> CaptureOnScreenGamepads()
    {
        List<Gamepad> onScreen = new List<Gamepad>();
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (IsOnScreen(gamepad))
            {
                onScreen.Add(gamepad);
            }
        }

        return onScreen;
    }

    // The usage OnScreenControl tags its virtual devices with (InputSystem.AddDeviceUsage).
    private const string OnScreenUsage = "OnScreen";
}

/// <summary>
/// The match-local answer to "which physical devices does local input slot N listen to?".
///
/// <c>PlayerSlot.LocalInputSlot</c> (Level5.Core) only says which participant uses local input slot N;
/// it deliberately knows nothing about hardware. This is the Unity-side half: built once when a match
/// starts (<c>GameLevelManager</c> -&gt; <c>PlayerControlsProvider.TryConfigureGameplayDevicePlan</c>),
/// never mutated afterwards, and never persisted. Because the devices are captured here rather than
/// looked up on every acquisition, gamepad re-ordering or a hot-plug cannot silently hand one player's
/// device to the other mid-match. A paired device that disappears simply stops producing input for its
/// player; reconnecting or remapping is a later feature, not something this type attempts.
///
/// Devices are exclusive between local players by construction: no <see cref="InputDevice"/> instance
/// appears under more than one slot. Touchscreen only ever goes to a single human - it does not count as
/// a second player's device.
/// </summary>
public sealed class LocalGameplayDevicePlan
{
    /// <summary>The most local humans the current desktop layouts can seat.</summary>
    public const int MaxSupportedLocalHumans = 2;

    private readonly InputDevice[][] devicesBySlot;

    private LocalGameplayDevicePlan(LocalGameplayDeviceLayout layout, InputDevice[][] devicesBySlot)
    {
        Layout = layout;
        this.devicesBySlot = devicesBySlot;
    }

    public LocalGameplayDeviceLayout Layout { get; }

    public int LocalHumanCount => devicesBySlot.Length;

    public bool HasSlot(int localInputSlot)
    {
        return localInputSlot >= 0 && localInputSlot < devicesBySlot.Length;
    }

    /// <summary>The devices reserved for a slot; empty for a slot the plan does not seat.</summary>
    public IReadOnlyList<InputDevice> DevicesFor(int localInputSlot)
    {
        return HasSlot(localInputSlot) ? devicesBySlot[localInputSlot] : Array.Empty<InputDevice>();
    }

    /// <summary>
    /// A fresh array of a slot's devices, safe to hand to <c>PlayerControls.devices</c> without letting
    /// the consumer touch the plan's own storage.
    /// </summary>
    public InputDevice[] CopyDevicesFor(int localInputSlot)
    {
        return HasSlot(localInputSlot) ? (InputDevice[])devicesBySlot[localInputSlot].Clone() : Array.Empty<InputDevice>();
    }

    /// <summary>
    /// Builds a plan from a device snapshot, or explains why the devices cannot seat that many local
    /// humans. One human, or none, always succeeds - a solo player with no controller attached still
    /// launches, exactly as before.
    /// </summary>
    public static bool TryCreate(
        int localHumanCount,
        LocalGameplayDeviceAvailability availability,
        out LocalGameplayDevicePlan plan,
        out string failureReason)
    {
        plan = null;
        failureReason = null;

        if (localHumanCount < 0)
        {
            failureReason = $"A match cannot have {localHumanCount} local players.";
            return false;
        }

        if (localHumanCount == 0)
        {
            plan = new LocalGameplayDevicePlan(LocalGameplayDeviceLayout.None, new InputDevice[0][]);
            return true;
        }

        if (localHumanCount == 1)
        {
            List<InputDevice> devices = new List<InputDevice>();
            AddIfPresent(devices, availability.Keyboard);
            AddIfPresent(devices, availability.Mouse);
            AddIfPresent(devices, availability.Touchscreen);
            if (availability.Gamepads.Count > 0)
            {
                AddIfPresent(devices, availability.Gamepads[0]);
            }

            plan = new LocalGameplayDevicePlan(
                LocalGameplayDeviceLayout.SingleHuman,
                new[] { devices.ToArray() });
            return true;
        }

        if (localHumanCount > MaxSupportedLocalHumans)
        {
            failureReason =
                $"{localHumanCount} local players are not supported yet; at most {MaxSupportedLocalHumans} can share one device set.";
            return false;
        }

        if (availability.Gamepads.Count >= 2)
        {
            plan = new LocalGameplayDevicePlan(
                LocalGameplayDeviceLayout.TwoGamepads,
                new[]
                {
                    new InputDevice[] { availability.Gamepads[0] },
                    new InputDevice[] { availability.Gamepads[1] }
                });
            return true;
        }

        if (availability.Keyboard != null && availability.Gamepads.Count == 1)
        {
            List<InputDevice> keyboardMouse = new List<InputDevice> { availability.Keyboard };
            AddIfPresent(keyboardMouse, availability.Mouse);

            plan = new LocalGameplayDevicePlan(
                LocalGameplayDeviceLayout.KeyboardMouseAndGamepad,
                new[]
                {
                    keyboardMouse.ToArray(),
                    new InputDevice[] { availability.Gamepads[0] }
                });
            return true;
        }

        failureReason = DescribeTwoPlayerShortfall(availability);
        return false;
    }

    /// <summary>Builds a plan from the devices connected right now.</summary>
    public static bool TryCreate(int localHumanCount, out LocalGameplayDevicePlan plan, out string failureReason)
    {
        return TryCreate(localHumanCount, LocalGameplayDeviceAvailability.Capture(), out plan, out failureReason);
    }

    private static string DescribeTwoPlayerShortfall(LocalGameplayDeviceAvailability availability)
    {
        string keyboard = availability.Keyboard != null ? "keyboard" : "no keyboard";
        string touchscreen = availability.Touchscreen != null ? ", touchscreen (cannot seat a second player)" : string.Empty;
        return "Two local players need two gamepads, or a keyboard plus one gamepad. "
            + $"Detected: {keyboard}, {availability.Gamepads.Count} gamepad(s){touchscreen}.";
    }

    private static void AddIfPresent(List<InputDevice> devices, InputDevice device)
    {
        if (device != null)
        {
            devices.Add(device);
        }
    }
}
