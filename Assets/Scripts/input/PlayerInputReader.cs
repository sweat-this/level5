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
        Vector2 actionMove = controls.Player.movement.ReadValue<Vector2>();
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (actionMove.sqrMagnitude > 0.001f)
        {
            return actionMove;
        }

        return ReadLegacyTouchMove(screenXRange, screenYRange);
#else
        return actionMove;
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
        get { return controls.Player.attack.triggered || PlayerTouchInputState.ConsumeAttack(); }
    }

    public bool BlockHeld
    {
        get
        {
            return controls.Player.block.ReadValue<float>() == 1
                || controls.Player.jump.ReadValue<float>() == 1
                || PlayerTouchInputState.BlockHeld;
        }
    }

    public bool SpecialPressed
    {
        get { return controls.Player.special.triggered || PlayerTouchInputState.ConsumeSpecial(); }
    }

    public bool DebugChangeHeld
    {
        get { return controls.Other.change.ReadValue<float>() == 1; }
    }

    public bool DebugLightningPressed
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return controls.Other.change.enabled && Input.GetKeyDown(KeyCode.Alpha8);
#else
            return false;
#endif
        }
    }

    public bool TouchBlockHeld
    {
        get
        {
            return PlayerTouchInputState.BlockHeld
                || (TouchInputController.instance != null && TouchInputController.instance.HoldDetected);
        }
    }

    public bool ConsumeTouchJumpOrShoot(out Vector2 touchPosition)
    {
        return PlayerTouchInputState.ConsumeJumpOrShoot(out touchPosition);
    }

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
    private Vector2 ReadLegacyTouchMove(float screenXRange, float screenYRange)
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
