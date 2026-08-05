using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class PlayerControlsProvider
{
    private static PlayerControls controls;
    private static readonly Dictionary<int, PlayerControls> gameplayControls = new Dictionary<int, PlayerControls>();
    private static int playerUsers;
    private static int playerTouchUsers;
    private static int uiNavigationUsers;
    private static int otherUsers;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        if (controls != null)
        {
            controls.Disable();
            controls.Dispose();
            controls = null;
        }

        foreach (PlayerControls playerControls in gameplayControls.Values)
        {
            playerControls.Disable();
            playerControls.Dispose();
        }

        gameplayControls.Clear();

        playerUsers = 0;
        playerTouchUsers = 0;
        uiNavigationUsers = 0;
        otherUsers = 0;
    }

    public static PlayerControls AcquireGameplayControls(int playerId)
    {
        if (gameplayControls.TryGetValue(playerId, out PlayerControls playerControls))
        {
            playerControls.Player.Enable();
            return playerControls;
        }

        playerControls = new PlayerControls();
        InputDevice[] devices = GetDevicesForPlayer(playerId);
        playerControls.devices = devices;
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
        List<InputDevice> devices = new List<InputDevice>();
        if (playerId == 0)
        {
            AddDevice(devices, Keyboard.current);
            AddDevice(devices, Mouse.current);
            AddDevice(devices, Touchscreen.current);
        }

        if (Gamepad.all.Count > playerId)
        {
            AddDevice(devices, Gamepad.all[playerId]);
        }

        return devices.ToArray();
    }

    private static void AddDevice(List<InputDevice> devices, InputDevice device)
    {
        if (device != null)
        {
            devices.Add(device);
        }
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

    public static void EnablePlayerTouch()
    {
        if (playerTouchUsers++ == 0)
        {
            Controls.PlayerTouch.Enable();
        }
    }

    public static void DisablePlayerTouch()
    {
        if (playerTouchUsers <= 0)
        {
            return;
        }

        if (--playerTouchUsers == 0)
        {
            Controls.PlayerTouch.Disable();
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
