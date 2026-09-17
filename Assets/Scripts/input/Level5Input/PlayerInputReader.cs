using System;
using UnityEngine;

public class PlayerInputReader
{
    private readonly PlayerControls controls;

    /// <summary>
    /// AUD-012 Phase 2b Slice 26: the legacy mobile joystick's *current* axes, supplied by whoever
    /// composed this reader (production: <c>GameLevelManager</c> -&gt; <c>SpawnCoordinator</c> -&gt;
    /// <c>PlayerController.BindLegacyTouchMovementReader</c>), replacing this class's former direct
    /// <c>GameLevelManager.instance.Joystick</c> read. It is a <see cref="Func{TResult}"/> rather than
    /// a cached <see cref="Vector2"/> precisely so the value stays synchronous: <see cref="ReadMove"/>
    /// asks for the axes at the moment it runs, exactly as the old singleton read did, instead of
    /// consuming a frame-delayed snapshot. Null when nothing composed one - every non-mobile path, and
    /// any test constructing a reader directly - which reads as no legacy movement at all, the same
    /// result the old null-joystick guard produced.
    /// </summary>
    private readonly Func<Vector2> legacyTouchMovementReader;

    private Vector2 startTouchPosition;

    public PlayerInputReader(PlayerControls controls, Func<Vector2> legacyTouchMovementReader = null)
    {
        this.controls = controls;
        this.legacyTouchMovementReader = legacyTouchMovementReader;
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

    /// <summary>
    /// AUD-012 Phase 5 Slice 77: reads the shared <c>PlayerControlsProvider</c> "Other" owner rather
    /// than this reader's own per-player <see cref="controls"/>. <c>PlayerController</c> builds this
    /// reader from <c>PlayerControlsProvider.AcquireGameplayControls(playerId)</c>, which enables only
    /// that instance's <c>Player</c> map - its <c>Other</c> map is never enabled, so a
    /// <c>controls.Other</c> read here was always reading a disabled action and always returning false.
    /// The shared owner is the same one <c>GameLevelManager.OnEnable/OnDisable</c> already ref-counts
    /// via <c>PlayerControlsProvider.EnableOther/DisableOther</c>.
    ///
    /// Gated the same as <see cref="DebugLightningPressed"/>: fixing this property's ownership bug
    /// (AUD-012 Phase 5 Slice 77) made a pre-existing binding collision live for the first time - the
    /// "Other" map's <c>change</c> action bound <c>&lt;Keyboard&gt;/leftShift</c> and
    /// <c>&lt;Keyboard&gt;/rightShift</c>, the same physical keys as the "Player" map's <c>run</c>
    /// action's <c>&lt;Keyboard&gt;/shift</c> (Unity's synthetic control for "either shift key"). Every
    /// keyboard player holding Shift to run would also read as holding the debug/change modifier,
    /// silently suppressing call-ball via <c>PlayerController</c>'s <c>!reader.DebugChangeHeld</c> guard.
    /// AUD-012 Phase 5 Slice 78 resolved the collision itself by moving <c>Other/change</c>'s keyboard
    /// binding off Shift onto <c>&lt;Keyboard&gt;/backquote</c> in <c>PlayerControls.inputactions</c> -
    /// <c>Player/run</c> is unchanged. This property stays Editor/Development-gated regardless; resolving
    /// the collision is not by itself a reason to ship this debug read in release builds (see
    /// <c>Level5PlayerInputOtherMapOwnershipTests</c>' keyboard-binding-collision tests).
    /// </summary>
    public bool DebugChangeHeld
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return PlayerControlsProvider.DevChangeHeld;
#else
            return false;
#endif
        }
    }

    public bool DebugLightningPressed
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return PlayerControlsProvider.DevChangeControlEnabled && Input.GetKeyDown(KeyCode.Alpha8);
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// AUD-012 Phase 2b Slice 26: reads <see cref="PlayerTouchInputState.BlockHeld"/> alone. This used
    /// to also consult <c>TouchInputController.instance.HoldDetected</c>, but the two were never
    /// independent states: every write to <c>hold1Detected</c> in <c>TouchInputController</c> is paired
    /// with the identical write to <c>PlayerTouchInputState.BlockHeld</c> - hold begin, hold end,
    /// special release, and the disable-time <c>PlayerTouchInputState.Clear()</c> - and nothing else in
    /// production writes or reads <c>HoldDetected</c>. Dropping the second read removes this assembly's
    /// dependency on <c>TouchInputController</c> without changing what this property returns.
    /// <c>HoldDetected</c> stays where it is: <c>TouchInputController</c> still uses it as its own
    /// gesture-state flag, and migrating that controller is not part of this slice.
    /// </summary>
    public bool TouchBlockHeld
    {
        get { return PlayerTouchInputState.BlockHeld; }
    }

    public bool ConsumeTouchJumpOrShoot(out Vector2 touchPosition)
    {
        return PlayerTouchInputState.ConsumeJumpOrShoot(out touchPosition);
    }

    /// <summary>
    /// The legacy mobile joystick fallback, reached only from <see cref="ReadMove"/>'s mobile branch -
    /// the call is still gated on <c>(UNITY_ANDROID || UNITY_IOS) &amp;&amp; !UNITY_EDITOR</c>, so this
    /// runs on exactly the platforms it always did and nowhere else.
    ///
    /// The method body itself is deliberately *not* gated. Gating it as well made this code invisible
    /// to every compiler and test the project can actually run - a desktop Editor compile skipped it,
    /// no test could reach it, and a CI machine without an Android/iOS module could not check it at
    /// all - which meant a mistake as ordinary as transposing the two axes below would ship
    /// undetected. Compiling it everywhere costs one unreachable private method in a non-mobile
    /// player and buys back full static checking plus
    /// <c>Level5PlayerInputReaderCompositionTests</c>'s direct coverage of the scaling. Legacy
    /// <c>UnityEngine.Input</c> is available on every target this project builds
    /// (<c>activeInputHandler: 2</c>), which is what makes that safe - the same reason
    /// <see cref="DebugLightningPressed"/> can call <c>Input.GetKeyDown</c> above.
    /// </summary>
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

        Vector2 legacyMovement = legacyTouchMovementReader != null
            ? legacyTouchMovementReader.Invoke()
            : Vector2.zero;

        return ScaleByTouchDistance(
            legacyMovement, touch.position, startTouchPosition, screenXRange, screenYRange);
    }

    /// <summary>
    /// The unchanged touch-distance scaling: each axis is attenuated by how far the touch has been
    /// dragged from where it started, as a fraction of that axis's screen range, and left alone once
    /// the drag reaches or passes the range. A range of zero or less disables scaling for that axis.
    ///
    /// Split out of <see cref="ReadLegacyTouchMove"/> only so it can be tested: the method above reads
    /// <c>Input.touches[0]</c> and therefore returns early on any machine with no live touch, which is
    /// every CI runner and every Editor test. This is the part of the fallback with arithmetic worth
    /// protecting - in particular that the horizontal axis is scaled by horizontal displacement and the
    /// vertical by vertical, which nothing else in the project would catch if they were transposed.
    /// Pure and static: no state, no <c>Input</c> access, no platform conditions.
    /// </summary>
    private static Vector2 ScaleByTouchDistance(
        Vector2 legacyMovement,
        Vector2 touchPosition,
        Vector2 startTouchPosition,
        float screenXRange,
        float screenYRange)
    {
        float movementHorizontal = legacyMovement.x;
        float movementVertical = legacyMovement.y;

        if (screenXRange > 0)
        {
            float xRangePercent = Mathf.Abs((touchPosition.x - startTouchPosition.x) / screenXRange);
            if (xRangePercent < 1)
            {
                movementHorizontal *= xRangePercent;
            }
        }

        if (screenYRange > 0)
        {
            float yRangePercent = Mathf.Abs((touchPosition.y - startTouchPosition.y) / screenYRange);
            if (yRangePercent < 1)
            {
                movementVertical *= yRangePercent;
            }
        }

        return new Vector2(movementHorizontal, movementVertical);
    }
}
