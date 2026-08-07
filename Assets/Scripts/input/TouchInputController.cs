
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TouchPhase = UnityEngine.TouchPhase;


public class TouchInputController : MonoBehaviour
{

    private Vector2 startTouchPosition1, endTouchPosition1;
    private Vector2 startTouchPosition2, endTouchPosition2;

    float swipeUpTolerance;
    float swipeDownTolerance;
    float swipeDistance;

    [SerializeField]
    GraphicRaycaster m_Raycaster;
    PointerEventData m_PointerEventData;
    [SerializeField]
    EventSystem m_EventSystem;

    Touch touch1;
    Touch touch2;

    public static TouchInputController instance;

    bool buttonPressed;

    //bool pauseExists;
    [SerializeField]
    private bool tap1Detected;
    [SerializeField]
    private bool tap2Detected;
    //[SerializeField]
    //private bool doubleTap1Detected;
    //[SerializeField]
    //private bool doubleTap2Detected;
    [SerializeField]
    private bool hold1Detected;
    //[SerializeField]
    //private bool hold2Detected;

    [SerializeField]
    float tapInterval;
    float touchfirstTap;
    float touchLastTap;

    [SerializeField]
    PlayerController playerController;

    GameObject joystickGameObject;
    public bool HoldDetected { get => hold1Detected; set => hold1Detected = value; }

    #if UNITY_ANDROID || UNITY_IOS 
    private void Awake()
    {
        instance = this;
        initializeTouchInputController();
    }

    private void Start()
    {
        
        playerController = GameLevelManager.instance.players[0].playerController;
    }

    private void OnDisable()
    {
        hold1Detected = false;
        // clears the queued jump/attack/special as well as BlockHeld. clearing only BlockHeld
        // let a tap on the frame a scene tore down survive into the next scene.
        PlayerTouchInputState.Clear();

        if (instance == this)
        {
            instance = null;
        }
    }


    void Update()
    {
        //Debug.Log("update");
        // not paused + touch
        if (!Pause.instance.Paused && Input.touchCount > 0)
        {
            //Debug.Log("paused");
            // ====================== get touches =====================================
            // detect multi touches
            bool hasSecondTouch = Input.touchCount > 1;
            if (Input.touchCount > 1)
            {
                touch1 = Input.touches[0];
                touch2 = Input.touches[1];
            }
            else
            {
                touch1 = Input.touches[0];
            }

            // ====================== touch 1 : tap  =====================================
            if (touch1.tapCount == 1
                && touch1.phase == TouchPhase.Began
                && !buttonPressed)
            {
                tap1Detected = true;
                startTouchPosition1 = touch1.position;
            }
            else
            { tap1Detected = false; }

            // ====================== touch 1 : hold  =====================================
            //Touch 1 is hold + bottom left screen
            if (touch1.tapCount == 1
                && touch1.phase == TouchPhase.Stationary
                //&& (touch1.phase == TouchPhase.Stationary || touch1.phase == TouchPhase.Moved)
                && !buttonPressed
                && touch1.position.x < Screen.safeArea.center.x
                && touch1.position.y > Screen.safeArea.center.y
                && playerController.PlayerCanBlock
                //&& playerController.PlayerHealth.Block > 0
                && GameOptions.enemiesEnabled) // if swipe on right 1/2 of screen)) )
            {
                hold1Detected = true;
                PlayerTouchInputState.BlockHeld = true;
                startTouchPosition1 = touch1.position;
            }
            if (touch1.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Canceled)
            {
                hold1Detected = false;
                PlayerTouchInputState.BlockHeld = false;
            }

            // ====================== touch 2 : tap  =====================================
            if (hasSecondTouch
                && touch2.tapCount == 1
                && touch2.phase == TouchPhase.Began
                && !buttonPressed)
            {
                tap2Detected = true;
                startTouchPosition2 = touch2.position;
                touchfirstTap = Time.time;
            }
            else
            {
                tap2Detected = false;
            }
            if (hasSecondTouch && tap2Detected && !tap1Detected)
            {
                PlayerTouchInputState.QueueJumpOrShoot(touch2.position);
                //tap2Detected = false;
            }

            //Touch 2 is tap + hold detected + bottom right screen
            if (hasSecondTouch
                && hold1Detected
                && !buttonPressed
                && startTouchPosition2.x > Screen.safeArea.center.x
                && GameOptions.enemiesEnabled)
            // if swipe on right 1/2 of screen)) 
            //&& startTouchPosition2.x < (Screen.height / 2)) // if swipe on right 1/2 of screen)) )
            {
                // ====================== touch 2 + bottom right =====================================
                if (tap2Detected && startTouchPosition2.y < Screen.safeArea.center.y)
                {
                    //Debug.Log("tap2Detected && !doubleTap2Detected");
                    buttonPressed = true;
                    if (!playerController.hasBasketball && playerController.CanAttack)
                    {
                        //Debug.Log("player attack ");
                        tap2Detected = false;
                        PlayerTouchInputState.QueueAttack();
                    }
                    buttonPressed = false;
                }
                // ====================== touch 2 + top right =====================================
                if (tap2Detected && startTouchPosition2.y > Screen.safeArea.center.y)
                {
                    //Debug.Log("!tap2Detected && doubleTap2Detected");
                    buttonPressed = true;
                    if (!playerController.InAir
                        && playerController.Grounded
                        && !playerController.KnockedDown
                        && playerController.PlayerHealth.Special == playerController.PlayerHealth.MaxSpecial)
                    {
                        //Debug.Log("player special attack ");
                        //doubleTap2Detected = false;
                        hold1Detected = false;
                        PlayerTouchInputState.BlockHeld = false;
                        PlayerTouchInputState.QueueSpecial();
                    }
                    buttonPressed = false;
                }
            }
            // ====================== touch 1 swipe distance  =====================================

            endTouchPosition1 = touch1.position;
            swipeDistance = endTouchPosition1.y - startTouchPosition1.y;

            // ====================== touch 1 swipe down : Pause  =====================================
            // not paused + swipe down + top right of screen
            if (touch1.tapCount == 1 && touch1.phase == TouchPhase.Ended // finger stoppped moving | *tapcount = 1 keeps pause from being called twice
                && Mathf.Abs(swipeDistance) > swipeDownTolerance // swipe is long enough
                && swipeDistance < 0 // swipe down
                && startTouchPosition1.x > Screen.safeArea.center.x
                && startTouchPosition1.y > Screen.safeArea.center.y)
            {
                Pause.instance.TogglePause();
            }
        }

        if (tap1Detected && !GameOptions.EnemiesOnlyEnabled)
        {
            PlayerTouchInputState.QueueJumpOrShoot(touch1.position);
            //tap1Detected = false;
        }

        // ====================== touch 1 : Paused  =====================================

        // if paused + touch touch
        if (Pause.instance.Paused && Input.touchCount > 0)
        {
            Debug.Log("paused");
            Touch touch = Input.touches[0];
            //tap
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Began)
            {

                if (Pause.instance.StartOnPause)
                {
                    Pause.instance.StartGame();
                }
                else
                {
                    selectPressedButton(touch.position);
                }
            }
            // on double tap, perform actions
            if (touch.tapCount == 2 && touch.phase == TouchPhase.Began && !buttonPressed)
            {
                activateDoubleTappedButton();
            }
        }
    }

    private void activateDoubleTappedButton()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selectedGameObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selectedGameObject == null)
        {
            return;
        }

        string selectedButtonName = selectedGameObject.name;

        // reload
        if (selectedButtonName.Equals(Pause.instance.LoadSceneButton.name)
            && !buttonPressed)
        {
            //Debug.Log("reload");
            Pause.instance.reloadScene();
            buttonPressed = true;
        }
        // start screen
        if (selectedButtonName.Equals(Pause.instance.LoadStartScreenButton.name)
            && !buttonPressed)
        {
            //Debug.Log("start screen");
            StartCoroutine(Pause.instance.loadstartScreen());
            buttonPressed = true;
        }
        // cancel/unpause
        if (selectedButtonName.Equals(Pause.instance.CancelMenuButton.name)
            && !buttonPressed)
        {
            //Debug.Log("cancel/un pause");
            Pause.instance.TogglePause();
            buttonPressed = true;
        }
        //quit
        if (selectedButtonName.Equals(Pause.instance.QuitGameButton.name)
            && !buttonPressed)
        {
            //Debug.Log("quit");
            StartCoroutine(Pause.instance.Quit());
            buttonPressed = true;
        }
        //// new stuff
        //if (EventSystem.current.currentSelectedGameObject.name.Equals(Pause.ToggleCameraName)
        //    && !buttonPressed)
        //{
        //    //Debug.Log("toggle camera");
        //    CameraManager.instance.switchCamera();
        //    buttonPressed = true;
        //}
        // toggle fps
        // AUD-012: DevFunctions lives in Assets/Scripts/Dev and must not be reachable from a
        // release build. Guarding the call - rather than the button - keeps the pause menu layout
        // identical in every configuration; the button simply does nothing when shipped.
        if (selectedButtonName.Equals(Pause.ToggleFpsName)
            && !buttonPressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevFunctions.instance != null)
            {
                DevFunctions.instance.ToggleFpsCounter();
            }
#endif
            buttonPressed = true;
        }
        //// set max stats
        //if (EventSystem.current.currentSelectedGameObject.name.Equals(Pause.ToggleMaxStatsName)
        //    && !buttonPressed)
        //{
        //    //Debug.Log("set max stats");
        //    DevFunctions.instance.setMaxPlayerStats();
        //    buttonPressed = true;
        //}
        // toggle ui stats
        if (selectedButtonName.Equals(Pause.ToggleUiStatsName)
            && !buttonPressed)
        {
            //Debug.Log("toggle stats");
            BasketBall.instance.toggleUiStats();
            buttonPressed = true;
        }
        buttonPressed = false;
    }

    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
        messageText.text = "";
    }

    private void selectPressedButton(Vector2 screenPosition)
    {
        if (m_Raycaster == null || m_EventSystem == null)
        {
            return;
        }

        //Set up the new Pointer Event
        m_PointerEventData = new PointerEventData(m_EventSystem);
        m_PointerEventData.position = screenPosition;

        //Create a list of Raycast Results
        List<RaycastResult> results = new List<RaycastResult>();

        //Raycast using the Graphics Raycaster and mouse click position
        m_Raycaster.Raycast(m_PointerEventData, results);

        foreach (RaycastResult result in results)
        {
            Selectable selectable = result.gameObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.interactable)
            {
                m_EventSystem.SetSelectedGameObject(selectable.gameObject);
                return;
            }
        }
    }

    private void initializeTouchInputController()
    {
        // find onscreen stick and disable
        if (GameObject.Find("floating_joystick") != null)
        {
            Debug.Log("floating joystick");
            joystickGameObject = GameObject.Find("floating_joystick");
            joystickGameObject.SetActive(false);
        }

        //bool enableTestTouch = true;
        if (Input.touchSupported || SystemInfo.deviceType == DeviceType.Handheld) // || enableTestTouch)
        {
            //Fetch the Raycaster from the GameObject (the Canvas)
            m_Raycaster = GameLevelManager.instance.gameObject.GetComponentInChildren<GraphicRaycaster>();
            //Fetch the Event System from the Scene
            m_EventSystem = GameLevelManager.instance.gameObject.GetComponentInChildren<EventSystem>();
        }
        else
        {
            this.gameObject.SetActive(false);
        }
        //if(GameObject.FindObjectOfType<EventSystem>() != null)
        //{
        //    EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        //}
        swipeUpTolerance = Screen.height / 7;
        swipeDownTolerance = Screen.height / 5;
    }
    #endif
}

