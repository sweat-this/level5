using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SniperCameraController : MonoBehaviour
{
    PlayerControls controls;
    private float movementHorizontal;
    private float movementVertical;
    private Vector3 movement;
    [SerializeField]
    private float movementSpeed;
    bool isPressed;


    private void OnEnable()
    {
        controls = PlayerControlsProvider.Controls;
        PlayerControlsProvider.EnableGameplayMaps();
        //controls.PlayerTouch.Enable();
    }
    private void OnDisable()
    {
        PlayerControlsProvider.DisableGameplayMaps();
        //controls.PlayerTouch.Disable();
    }

    private void Awake()
    {
        controls = PlayerControlsProvider.Controls;
    }
    /// <summary>
    /// The controller driving this camera, or null when none is connected.
    ///
    /// Resolved on every use rather than cached in Start. <c>Gamepad.current</c> is null on any
    /// device without a controller attached - which is most of this game's audience - and caching
    /// it once meant a null dereference on every physics tick and every frame. Caching also missed
    /// a controller connected after the scene loaded and kept a stale device after one was
    /// unplugged.
    /// </summary>
    private static Gamepad ActiveGamepad => Gamepad.current;

    // Update is called once per frame
    void FixedUpdate()
    {
        //var allGamepads = Gamepad.all;
        //foreach (Gamepad g in allGamepads)
        //{
        //    Debug.Log("Gamepad" + g.name);
        //}
        //Debug.Log("Gamepad current : "+Gamepad.current);

        Gamepad gamepad = ActiveGamepad;
        if (gamepad == null)
        {
            return;
        }

        Vector2 move = gamepad.leftStick.ReadValue();
        //Debug.Log("move : " + move);
        transform.eulerAngles = (transform.eulerAngles - new Vector3(move.y * movementSpeed, -move.x * movementSpeed, 0));
        //movementHorizontal = gamepad. ReadValue<Vector2>().x;
        //movementVertical = gamepad.ReadValue<Vector2>().y;
        //movement = new Vector3(move.x, move.y, 0) * (movementSpeed * Time.fixedDeltaTime);
        //gameObject.transform.rotation = Quaternion.Euler(move.y, move.x,0);
        //movement = new Vector3(move.x, move.y, 0) * (movementSpeed * Time.fixedDeltaTime);
       //gameObject.transform.Translate(movement);
    }
    private void Update()
    {
        Gamepad gamepad = ActiveGamepad;
        if (gamepad == null)
        {
            return;
        }

        if (gamepad.buttonSouth.wasPressedThisFrame && !isPressed && SniperManager.instance != null)
        {
            isPressed = true;
            Debug.Log("button pressed");
            float random = UtilityFunctions.GetRandomFloat(0, 4);
            StartCoroutine(SniperManager.instance.StartSniperBullet(random));
            isPressed = false;
        }
    }
}
