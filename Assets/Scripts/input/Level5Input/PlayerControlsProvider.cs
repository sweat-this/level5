using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class PlayerControlsProvider
{
    private static PlayerControls controls;
    private static readonly Dictionary<int, PlayerControls> gameplayControls = new Dictionary<int, PlayerControls>();
    private static int playerUsers;
    private static int uiNavigationUsers;
    private static int otherUsers;
    private static LocalGameplayDevicePlan gameplayDevicePlan;

    public static PlayerControls Controls
    {
        get
        {
            if (controls == null)
            {
                controls = new PlayerControls();
            }

            return controls;
        }
    }

    public static bool MenuSubmitTriggered
    {
        get
        {
            return Controls.UINavigation.Submit.triggered;
        }
    }

    /// <summary>
    /// Whether the "Other" action map's dev/editor change-toggle binding is currently enabled.
    /// AUD-012 Phase 2b: exposes this one <c>InputAction</c> detail as a bool, the same shape as
    /// <see cref="MenuSubmitTriggered"/>, so a consumer with no reference to the
    /// <c>Unity.InputSystem</c> package assembly (<c>Level5.Player</c>'s
    /// <c>CheerleaderSwapAnimation</c>) can read it without needing one.
    /// </summary>
    public static bool DevChangeControlEnabled
    {
        get
        {
            return Controls.Other.change.enabled;
        }
    }

    /// <summary>
    /// Whether the shared "Other" map's dev/editor change-toggle binding is currently held. AUD-012
    /// Phase 5 Slice 77: gives <c>PlayerInputReader.DebugChangeHeld</c> a shared-owner read of the same
    /// shape as <see cref="DevChangeControlEnabled"/>, so it stops reading a per-player controls
    /// instance's <c>Other</c> map - <c>AcquireGameplayControls</c> never enables <c>Other</c> on that
    /// instance, so that read was always false.
    ///
    /// This is one shared value for every local player, not a per-player read - holding the modifier
    /// from any one device affects every <c>PlayerInputReader</c> at once, the same way
    /// <c>GameLevelManager</c>'s own <c>Other</c>-gated debug toggles are global rather than scoped to
    /// whichever player triggered them.
    /// </summary>
    public static bool DevChangeHeld
    {
        get
        {
            return Controls.Other.change.ReadValue<float>() == 1;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        if (controls != null)
        {
            controls.Disable();
            controls.Dispose();
            controls = null;
        }

        DisposeGameplayControls();
        gameplayDevicePlan = null;

        playerUsers = 0;
        uiNavigationUsers = 0;
        otherUsers = 0;
    }

    /// <summary>
    /// The match-local device assignment gameplay controls are paired from, or null before a match (or
    /// the first gameplay acquisition) has established one.
    /// </summary>
    public static LocalGameplayDevicePlan GameplayDevicePlan
    {
        get { return gameplayDevicePlan; }
    }

    /// <summary>
    /// Whether the devices connected right now can seat <paramref name="localHumanCount"/> local humans.
    /// Side-effect free, so a launch source can refuse a two-human launch before it loads a scene.
    /// <see cref="TryConfigureGameplayDevicePlan(int, out string)"/> repeats the same check
    /// authoritatively when the gameplay scene composes the match.
    /// </summary>
    public static bool TryPreflightGameplayDevices(int localHumanCount, out string failureReason)
    {
        return LocalGameplayDevicePlan.TryCreate(localHumanCount, out _, out failureReason);
    }

    /// <summary>
    /// Captures the connected devices into a plan for this match and makes it the active one. Called
    /// once by the gameplay composition owner, after the roster is known and before any human
    /// <c>PlayerController</c> acquires its controls.
    /// </summary>
    public static bool TryConfigureGameplayDevicePlan(int localHumanCount, out string failureReason)
    {
        if (!LocalGameplayDevicePlan.TryCreate(localHumanCount, out LocalGameplayDevicePlan plan, out failureReason))
        {
            return false;
        }

        return TryConfigureGameplayDevicePlan(plan, out failureReason);
    }

    /// <summary>
    /// Makes <paramref name="plan"/> the active assignment for a new match. Gameplay controls still
    /// cached at this point belong to the previous match (a scene reload can compose the next match
    /// before the old owners have finished releasing), and they are paired to the previous plan, so
    /// they are disposed rather than left to be handed to the new match. Their owners' later
    /// <see cref="ReleaseGameplayControls"/> calls find nothing and do nothing.
    /// </summary>
    public static bool TryConfigureGameplayDevicePlan(LocalGameplayDevicePlan plan, out string failureReason)
    {
        failureReason = null;
        if (plan == null)
        {
            failureReason = "no gameplay device plan was provided";
            return false;
        }

        if (gameplayControls.Count > 0)
        {
            Debug.LogWarning(
                $"Configuring a new gameplay device plan while {gameplayControls.Count} gameplay controls from the "
                + "previous match are still held; disposing them so they are not reused with the new assignment.");
            DisposeGameplayControls();
        }

        gameplayDevicePlan = plan;
        return true;
    }

    /// <summary>Forgets the active plan. The gameplay composition owner calls this when its match ends.</summary>
    public static void ClearGameplayDevicePlan()
    {
        gameplayDevicePlan = null;
    }

    private static void DisposeGameplayControls()
    {
        foreach (PlayerControls playerControls in gameplayControls.Values)
        {
            playerControls.Disable();
            playerControls.Dispose();
        }

        gameplayControls.Clear();
    }

    /// <summary>
    /// Gameplay controls for a local input slot, paired to that slot's devices from the active
    /// <see cref="LocalGameplayDevicePlan"/>. <paramref name="playerId"/> is a local input slot
    /// (<c>PlayerSlot.LocalInputSlot</c>), not a roster slot.
    /// </summary>
    public static PlayerControls AcquireGameplayControls(int playerId)
    {
        if (gameplayControls.TryGetValue(playerId, out PlayerControls playerControls))
        {
            playerControls.Player.Enable();
            return playerControls;
        }

        playerControls = new PlayerControls();
        playerControls.devices = GetDevicesForPlayer(playerId);
        playerControls.Player.Enable();
        gameplayControls.Add(playerId, playerControls);
        return playerControls;
    }

    public static void ReleaseGameplayControls(int playerId)
    {
        if (!gameplayControls.TryGetValue(playerId, out PlayerControls playerControls))
        {
            return;
        }

        playerControls.Disable();
        playerControls.Dispose();
        gameplayControls.Remove(playerId);
    }

    private static InputDevice[] GetDevicesForPlayer(int playerId)
    {
        if (gameplayDevicePlan == null)
        {
            // No match composed a plan: a directly constructed player, or a test. Capture the same
            // single-human layout once and keep it, so this path also stops re-reading Gamepad.all.
            LocalGameplayDevicePlan.TryCreate(1, out gameplayDevicePlan, out _);
        }

        if (!gameplayDevicePlan.HasSlot(playerId))
        {
            Debug.LogWarning(
                $"Local input slot {playerId} has no device assignment in the active gameplay device plan "
                + $"({gameplayDevicePlan.Layout}); that player will receive no input.");
        }

        InputDevice[] devices = gameplayDevicePlan.CopyDevicesFor(playerId);
        if (playerId != 0 || gameplayDevicePlan.Layout != LocalGameplayDeviceLayout.SingleHuman)
        {
            return devices;
        }

        // A single human also owns the on-screen gamepad(s) touch UI drives (the OnScreenStick pilot in
        // touch_joystick.prefab is bound to <Gamepad>/leftStick). They are not physical devices, so the
        // plan never counts them, but the one local player is the only possible holder. Resolved here,
        // at acquisition, because the scene creates them in its own OnEnable - which may not have run
        // yet when the plan was configured from GameLevelManager.Awake.
        List<InputDevice> withOnScreen = new List<InputDevice>(devices);
        foreach (Gamepad onScreen in LocalGameplayDeviceAvailability.CaptureOnScreenGamepads())
        {
            if (!withOnScreen.Contains(onScreen))
            {
                withOnScreen.Add(onScreen);
            }
        }

        return withOnScreen.ToArray();
    }

    public static void EnableGameplayMaps()
    {
        EnablePlayer();
    }

    public static void DisableGameplayMaps()
    {
        DisablePlayer();
    }

    public static void EnableOtherMaps()
    {
        EnableOther();
    }

    public static void DisableOtherMaps()
    {
        DisableOther();
    }

    public static void EnableMenuMaps()
    {
        EnableUINavigation();
    }

    public static void DisableMenuMaps()
    {
        DisableUINavigation();
    }

    public static void EnablePlayer()
    {
        if (playerUsers++ == 0)
        {
            Controls.Player.Enable();
        }
    }

    public static void DisablePlayer()
    {
        if (playerUsers <= 0)
        {
            return;
        }

        if (--playerUsers == 0)
        {
            Controls.Player.Disable();
        }
    }

    public static void EnableUINavigation()
    {
        if (uiNavigationUsers++ == 0)
        {
            Controls.UINavigation.Enable();
        }
    }

    public static void DisableUINavigation()
    {
        if (uiNavigationUsers <= 0)
        {
            return;
        }

        if (--uiNavigationUsers == 0)
        {
            Controls.UINavigation.Disable();
        }
    }

    public static void EnableOther()
    {
        if (otherUsers++ == 0)
        {
            Controls.Other.Enable();
        }
    }

    public static void DisableOther()
    {
        if (otherUsers <= 0)
        {
            return;
        }

        if (--otherUsers == 0)
        {
            Controls.Other.Disable();
        }
    }
}
