using UnityEngine;

public class RacingInputReader
{
    private readonly PlayerControls controls;
    private Vector2 startTouchPosition;

    public RacingInputReader(PlayerControls controls)
    {
        this.controls = controls;
    }

    public Vector2 ReadMove(float screenXRange, float screenYRange)
    {
        Vector2 actionMove = controls.Player.movement.ReadValue<Vector2>();
#if UNITY_ANDROID && !UNITY_EDITOR
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

#if UNITY_ANDROID && !UNITY_EDITOR
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

        if (RacingGameManager.instance == null || RacingGameManager.instance.Joystick == null)
        {
            return Vector2.zero;
        }

        float movementHorizontal = RacingGameManager.instance.Joystick.Horizontal;
        float movementVertical = RacingGameManager.instance.Joystick.Vertical;

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
