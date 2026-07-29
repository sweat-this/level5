using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TouchInputEndRoundMenuController : MonoBehaviour
{
    private Vector2 startTouchPosition, endTouchPosition;

    float swipeUpTolerance;
    float swipeDownTolerance;
    float swipeDistance;

    [SerializeField]
    GameObject prevSelectedGameObject;

    GraphicRaycaster m_Raycaster;
    PointerEventData m_PointerEventData;
    EventSystem m_EventSystem;

    Touch touch;
    bool buttonPressed;

    GameObject joystickGameObject;

    [SerializeField]
    public string currentHighlightedButton;

    const string nextButton = "next_button";
    const string startMenuButton = "start_menu_button";
    const string quitButton = "quit_button";

    public static TouchInputEndRoundMenuController instance;


    void Awake()
    {
        //initializeEndRoundTouchControls();
    }

    private void Start()
    {
        //StartManager.instance.disableButtonsNotUsedForTouchInput();
        // set distance required for swipe up to be regeistered by device
        swipeUpTolerance = Screen.height / 7;
        swipeDownTolerance = Screen.height / 5;
        if (EventSystem.current == null)
        {
            enabled = false;
            return;
        }

        prevSelectedGameObject = EventSystem.current.firstSelectedGameObject;
        //Debug.Log("screen width : " + Screen.width);
    }

    void Update()
    {
        if (EventSystem.current == null)
            return;

        //// if no button selected, return to previous
        if (EventSystem.current.currentSelectedGameObject == null && prevSelectedGameObject != null)
        {
            EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);
        }

        if (EventSystem.current.currentSelectedGameObject == null)
            return;

        currentHighlightedButton = EventSystem.current.currentSelectedGameObject.name;

        // save previous button until a touch is made
        if (!buttonPressed && Input.touchCount == 0)
        {
            prevSelectedGameObject = EventSystem.current.currentSelectedGameObject;
        }
        // if touch
        if (Input.touchCount > 0 && !buttonPressed)
        {
            Touch touch = Input.touches[0];
            if (touch.tapCount == 1 && touch.phase == TouchPhase.Began)
            {
                startTouchPosition = touch.position;
            }
            endTouchPosition = touch.position;
            swipeDistance = endTouchPosition.y - startTouchPosition.y;

            // on double tap, perform actions
            if (touch.tapCount == 2 && touch.phase == TouchPhase.Began && !buttonPressed)
            {
                activateDoubleTappedButton();
            }
        }
    }

    //private void initializeEndRoundTouchControls()
    //{
    //    // find onscreen stick and disable
    //    if (GameObject.Find("floating_joystick") != null)
    //    {
    //        joystickGameObject = GameObject.Find("floating_joystick");
    //        joystickGameObject.SetActive(false);
    //    }

    //    //check if StartManager is empty and find correct GraphicRaycaster and EventSystem
    //    if (GameObject.FindObjectOfType<StartManager>() != null)
    //    {
    //        //Fetch the Raycaster from the GameObject (the Canvas)
    //        //m_Raycaster = StartManager.instance.gameObject.GetComponentInChildren<GraphicRaycaster>();
    //        m_Raycaster = GameObject.Find("touch_joystick").GetComponentInChildren<GraphicRaycaster>();
    //        //Fetch the Event System from the Scene
    //        m_EventSystem = GameObject.Find("touch_joystick").GetComponentInChildren<EventSystem>();
    //    }
    //    // else, this is not the startscreen and disable object
    //    else
    //    {
    //        gameObject.SetActive(false);
    //    }
    //}


    private void activateDoubleTappedButton()
    {
        buttonPressed = true;
        EventSystem.current.SetSelectedGameObject(prevSelectedGameObject);

        if (currentHighlightedButton.Equals(nextButton))
        {
            EndRoundMenuManager.instance.pressNext();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(startMenuButton))
        {
            EndRoundMenuManager.instance.pressStartMenu();
            buttonPressed = true;
        }
        if (currentHighlightedButton.Equals(quitButton))
        {
            EndRoundMenuManager.instance.pressQuit();
            buttonPressed = true;
        }
        buttonPressed = false;
    }
}
