using UnityEngine;

public class PlayerInputReader
{
    private readonly PlayerControls controls;
    private Vector2 startTouchPosition;

    public PlayerInputReader(PlayerControls controls)
    {
        this.controls = controls;
    }

    public Vector2 ReadMove(float screenXRange, float screenYRange)
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        return ReadTouchMove(screenXRange, screenYRange);
#else
        return controls.Player.movement.ReadValue<Vector2>();
#endif
    }

    public bool RunHeld
    {
        get { return controls.Player.run.ReadValue<float>() == 1; }
    }

    public bool JumpPressed
    {
        get { return controls.Player.jump.triggered; }
    }

    public bool ShootPressed
    {
        get { return controls.Player.shoot.triggered; }
    }

    public bool CallBallPressed
    {
        get { return controls.Player.callball.triggered; }
    }

    public bool AttackPressed
    {
        get { return controls.Player.attack.triggered; }
    }

    public bool BlockHeld
    {
        get
        {
            return controls.Player.block.ReadValue<float>() == 1
                || controls.Player.jump.ReadValue<float>() == 1;
        }
    }

    public bool SpecialPressed
    {
        get { return controls.Player.special.triggered; }
    }

    public bool DebugChangeHeld
    {
        get { return controls.Other.change.ReadValue<float>() == 1; }
    }

    public bool DebugLightningPressed
    {
        get
        {
            return controls.Other.change.enabled && Input.GetKeyDown(KeyCode.Alpha8);
        }
    }

    public bool TouchBlockHeld
    {
        get
        {
            return TouchInputController.instance != null && TouchInputController.instance.HoldDetected;
        }
    }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private Vector2 ReadTouchMove(float screenXRange, float screenYRange)
    {
        if (Input.touchCount == 0)
        {
            return Vector2.zero;
        }

        Touch touch = Input.touches[0];
        if (touch.phase == TouchPhase.Began)
        {
            startTouchPosition = touch.position;
        }

        if (GameLevelManager.instance == null || GameLevelManager.instance.Joystick == null)
        {
            return Vector2.zero;
        }

        float movementHorizontal = GameLevelManager.instance.Joystick.Horizontal;
        float movementVertical = GameLevelManager.instance.Joystick.Vertical;

        if (screenXRange > 0)
        {
            float xRangePercent = Mathf.Abs((touch.position.x - startTouchPosition.x) / screenXRange);
            if (xRangePercent < 1)
            {
                movementHorizontal *= xRangePercent;
            }
        }

        if (screenYRange > 0)
        {
            float yRangePercent = Mathf.Abs((touch.position.y - startTouchPosition.y) / screenYRange);
            if (yRangePercent < 1)
            {
                movementVertical *= yRangePercent;
            }
        }

        return new Vector2(movementHorizontal, movementVertical);
    }
#endif
}
