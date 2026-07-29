using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchInputProgressionScreenController : MonoBehaviour
{
    private Vector2 startTouchPosition, endTouchPosition;

    float swipeUpTolerance;
    float swipeDownTolerance;
    float swipeDistance;

    GraphicRaycaster m_Raycaster;
    PointerEventData m_PointerEventData;
    EventSystem m_EventSystem;

    Touch touch;
    bool buttonPressed;

    GameObject joystickGameObject;

    GameObject prevSelectedGameObject;

    public static TouchInputProgressionScreenController instance;

    public GameObject PrevSelectedGameObject { get => prevSelectedGameObject; }

    void Awake()
    {
        initializeProgressionScreenTouchControls();
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        ProgressionManager.instance.disableButtonsNotUsedForTouchInput();
#endif

        // set distance required for swipe up to be regeistered by device
        swipeUpTolerance = Screen.height / 7;
        swipeDownTolerance = Screen.height / 5;
        prevSelectedGameObject = EventSystem.current.firstSelectedGameObject;
        //Debug.Log("Start : " + prevSelectedGameObject);

        //if (EventSystem.current.currentSelectedGameObject == null)
        //{
        //    Debug.Log("EventSystem.current.currentSelectedGameObject : " + EventSystem.current.currentSelectedGameObject);
        //    EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject); // + "_description";
        //}
    }

    void Update()
    {

        // save previous button until a touch is made
        if (!buttonPressed && Input.touchCount == 0)
        {
            prevSelectedGameObject = EventSystem.current.currentSelectedGameObject;
        }

        if (Input.touchCount > 0 && !buttonPressed)
        {
            Touch touch = Input.touches[0];
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Began)
            {
                startTouchPosition = touch.position;
            }
            endTouchPosition = touch.position;
            swipeDistance = endTouchPosition.y - startTouchPosition.y;

            // swipe down on changeable options
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Ended // finger stoppped moving | *tapcount = 1 keeps pause from being called twice
                && Mathf.Abs(swipeDistance) > swipeDownTolerance // swipe is long enough
                && swipeDistance < 0 // swipe down
                && (startTouchPosition.x > (Screen.width / 2))) // if swipe on right 1/2 of screen)) 
            {
                //change option
                swipeDownOnOption();
                //// reset previous button to active button
                if (EventSystem.current.currentSelectedGameObject != prevSelectedGameObject)
                {
                    EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
                }
            }
            //swipe up on changeable options
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Ended // finger stoppped moving | *tapcount = 1 keeps pause from being called twice
                && Mathf.Abs(swipeDistance) > swipeDownTolerance // swipe is long enough
                && swipeDistance > 0 // swipe down
                && (startTouchPosition.x > (Screen.width / 2))) // if swipe on right 1/2 of screen)) 
            {
                //change option
                swipeUpOnOption();
                // reset previous button to active button
                if (EventSystem.current.currentSelectedGameObject != prevSelectedGameObject)
                {
                    EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
                }
            }
            // on double tap, perform actions
            if (touch.tapCount == 2 && touch.phase == TouchPhase.Began && !buttonPressed)
            {
                activateDoubleTappedButton();
            }
        }
    }

    private void initializeProgressionScreenTouchControls()
    {
        // find onscreen stick and disable
        if (GameObject.Find("floating_joystick") != null)
        {
            joystickGameObject = GameObject.Find("floating_joystick");
            joystickGameObject.SetActive(false);
        }

        //check if startmanager is empty and find correct GraphicRaycaster and EventSystem
        if (UnityEngine.Object.FindAnyObjectByType<ProgressionManager>() != null)
        {
            //Fetch the Raycaster from the GameObject (the Canvas)
            //m_Raycaster = StartManager.instance.gameObject.GetComponentInChildren<GraphicRaycaster>();
            m_Raycaster = GameObject.Find("progressionScreen").GetComponentInChildren<GraphicRaycaster>();
            //Fetch the Event System from the Scene
            m_EventSystem = GameObject.Find("progressionScreen").GetComponentInChildren<EventSystem>();
        }
        // else, this is not the startscreen and disable object
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void activateDoubleTappedButton()
    {
        EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        //player select
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.PlayerSelectOptionButtonName)
             && !buttonPressed)
        {
            ProgressionManager.instance.changePlayerUp();
            buttonPressed = true;
        }
        // reset points
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.ResetButtonName)
             && !buttonPressed)
        {
            Debug.Log("reset changes");
            ProgressionManager.instance.resetChanges();
            //EventSystem.current.SetSelectedGameObject(GameObject.Find(progression3AccuracyName).gameObject);
            buttonPressed = true;
        }
        // save changes
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.SaveButtonName)
             && !buttonPressed)
        {
            Debug.Log("save changes");
            ProgressionManager.instance.saveChanges();
            buttonPressed = true;
        }
        // add 3 accuracy point
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression3AccuracyName)
            && ProgressionManager.instance.ProgressionState.PointsAvailable > 0
            && ProgressionManager.instance.DataLoaded
            && !buttonPressed)
        {
            Debug.Log("add to 3");
            ProgressionManager.instance.updateThreeAccuracy(ProgressionManager.UpdateType.Add);
            buttonPressed = true;
        }
        // add 4 accuracy point
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression4AccuracyName)
            && ProgressionManager.instance.ProgressionState.PointsAvailable > 0
            && ProgressionManager.instance.DataLoaded
            && !buttonPressed)
        {

            Debug.Log("add to 4");
            ProgressionManager.instance.updateFourAccuracy(ProgressionManager.UpdateType.Add);
            buttonPressed = true;
        }
        // add 7 accuracy point
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression7AccuracyName)
            && ProgressionManager.instance.ProgressionState.PointsAvailable > 0
            && ProgressionManager.instance.DataLoaded
            && !buttonPressed)
        {
            Debug.Log("add to 7");
            ProgressionManager.instance.updateSevenAccuracy(ProgressionManager.UpdateType.Add);
            buttonPressed = true;
        }
        // confirm changes
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.ConfirmButtonName)
            && !buttonPressed)
        {
            Debug.Log("confirm changes");
            ProgressionManager.instance.confirmChanges();
            buttonPressed = true;
        }
        // cancel changes
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.CancelButtonName)
             && !buttonPressed)
        {
            Debug.Log("cancel changes");
            ProgressionManager.instance.cancelChanges();
            buttonPressed = true;
        }
        // footer
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.StartButtonName)
            && !buttonPressed)
        {
            Debug.Log("load start");
            ProgressionManager.instance.loadScene(Constants.SCENE_NAME_level_00_start);
            buttonPressed = true;
        }
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.StatsMenuButtonName)
            && !buttonPressed)
        {
            Debug.Log("load stats");
            ProgressionManager.instance.loadScene(Constants.SCENE_NAME_level_00_stats);
            buttonPressed = true;
        }
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.QuitButtonName)
                && !buttonPressed)
        {
            Debug.Log("quit");
            Application.Quit();
            buttonPressed = true;
        }
        buttonPressed = false;
    }

    //private void selectPressedButton()
    //{
    //    //Set up the new Pointer Event
    //    m_PointerEventData = new PointerEventData(m_EventSystem);
    //    //Set the Pointer Event Position to that of the mouse position
    //    m_PointerEventData.position = Input.mousePosition;

    //    //Create a list of Raycast Results
    //    List<RaycastResult> results = new List<RaycastResult>();

    //    //Raycast using the Graphics Raycaster and mouse click position
    //    m_Raycaster.Raycast(m_PointerEventData, results);
    //}

    private void swipeUpOnOption()
    {
        //Debug.Log("swipe up");
        EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.PlayerSelectOptionButtonName))
        {
            ProgressionManager.instance.changePlayerUp();
            buttonPressed = true;
        }
        // check for available points + if button already pressed
        if (ProgressionManager.instance.ProgressionState.PointsAvailable > 0
            && ProgressionManager.instance.DataLoaded
            && !buttonPressed)
        {
            if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression3AccuracyName))
            {
                ProgressionManager.instance.updateThreeAccuracy(ProgressionManager.UpdateType.Add);
                buttonPressed = true;
            }
            // mode select
            if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression4AccuracyName))
            {
                ProgressionManager.instance.updateFourAccuracy(ProgressionManager.UpdateType.Add);
                buttonPressed = true;
            }
            if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression7AccuracyName))
            {
                ProgressionManager.instance.updateSevenAccuracy(ProgressionManager.UpdateType.Add);
                buttonPressed = true;
            }
        }
        buttonPressed = false;
    }
    private void swipeDownOnOption()
    {
        EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        //Debug.Log("swipe down");
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.PlayerSelectOptionButtonName)
            && !buttonPressed)
        {
            ProgressionManager.instance.changePlayerDown();
            buttonPressed = true;
        }
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression3AccuracyName)
            && ProgressionManager.instance.ProgressionState.AddTo3 > 0
            && !buttonPressed)
        {
            ProgressionManager.instance.updateThreeAccuracy(ProgressionManager.UpdateType.Subtract);
            buttonPressed = true;
        }
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression4AccuracyName)
            && ProgressionManager.instance.ProgressionState.AddTo4 > 0
            && !buttonPressed)
        {
            ProgressionManager.instance.updateFourAccuracy(ProgressionManager.UpdateType.Subtract);
            buttonPressed = true;
        }
        if (EventSystem.current.currentSelectedGameObject.name.Equals(ProgressionManager.Progression7AccuracyName)
            && ProgressionManager.instance.ProgressionState.AddTo7 > 0
            && !buttonPressed)
        {
            ProgressionManager.instance.updateSevenAccuracy(ProgressionManager.UpdateType.Subtract);
            buttonPressed = true;
        }
        buttonPressed = false;
    }
}
