using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ConfirmDialogue : MonoBehaviour
{
    [HideInInspector]
    public int NONE = 0, YES = 1, CANCEL = 2;
    [HideInInspector]
    public int result = 0;

    public Button confirmButton;
    public Button cancelButton;
    //public Button nextButton;

    public Button ConfirmButton { get => confirmButton; set => confirmButton = value; }
    public Button CancelButton { get => cancelButton; set => cancelButton = value; }
    //public Button NextButton { get => nextButton; set => nextButton = value; }

    private void Awake()
    {
        if (confirmButton == null)
        {
            confirmButton = FindButton("confirm_button");
        }

        if (confirmButton == null)
        {
            Debug.LogError("ConfirmDialogue is missing a confirm button.");
            return;
        }

        confirmButton.onClick.AddListener(ConfirmButtonOnClick);

        if (cancelButton == null)
        {
            cancelButton = FindButton("cancel_button");
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(CancelButtonOnClick);
        }
        //if (GameObject.Find("next_button") != null)
        //{
        //    cancelButton = GameObject.Find("next_button").GetComponent<Button>();
        //    cancelButton.onClick.AddListener(NextButtonOnClick);
        //}
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
        }
    }

    private Button FindButton(string buttonName)
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        GameObject buttonObject = GameObject.Find(buttonName);
        return buttonObject == null ? null : buttonObject.GetComponent<Button>();
    }

    public void ConfirmButtonOnClick()
    {
        result = YES;
    }

    public void CancelButtonOnClick()
    {
        result = CANCEL;
    }
}
