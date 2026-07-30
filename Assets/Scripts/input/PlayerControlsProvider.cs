using UnityEngine;
public static class PlayerControlsProvider
{
    private static PlayerControls controls;
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

        playerUsers = 0;
        playerTouchUsers = 0;
        uiNavigationUsers = 0;
        otherUsers = 0;
    }

    public static void EnableGameplayMaps()
    {
        EnablePlayer();
    }

    public static void DisableGameplayMaps()
    {
        DisablePlayer();
    }

    public static void EnableDebugMaps()
    {
        EnableOther();
    }

    public static void DisableDebugMaps()
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
