using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class DialogueManager : MonoBehaviour
{
    public ConfirmDialogue confirmationDialog;
    public ConfirmDialogue confirmationDialogTip;

    [HideInInspector]
    public int ConfirmationDialogue = 0, TipDialogue = 1;
    public int dialogueType;

    public Canvas canvas;
    private Coroutine coroutine;
    ConfirmDialogue previousDialog;

    bool buttonPressed = false;

    public static DialogueManager instance;

    private void Start()
    {
        instance = this;
        Coroutine = null;
        canvas = GameObject.FindAnyObjectByType<Canvas>();
        if(GameObject.Find("confirm_tip") != null)
        {
            dialogueType = TipDialogue;
        }
        if (GameObject.Find("confirm_update") != null)
        {
            dialogueType = ConfirmationDialogue;
        }
    }


    public IEnumerator ShowConfirmationDialog()
    {
        ConfirmDialogue dialog = null;
        if (dialogueType == ConfirmationDialogue)
        {
            dialog = Instantiate(confirmationDialog, canvas.transform); // instantiate the UI dialog box
        }
        if (dialogueType == TipDialogue)
        {
            dialog = Instantiate(confirmationDialogTip, canvas.transform); // instantiate the UI dialog box
        }

        PreviousDialog = dialog;

        while (dialog.result == dialog.NONE)
        {
            yield return null; // wait
        }

        if (dialog.result == dialog.YES)
        {
            buttonPressed = true;
            EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        }
        if (dialog.result == dialog.CANCEL)
        {
            buttonPressed = true;
            EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        }
        //if (dialog.result == dialog.NEXT)
        //{
        //    buttonPressed = true;
        //    EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        //}
        Destroy(dialog.gameObject);

        coroutine = null;
    }

    public Coroutine Coroutine { get => coroutine; set => coroutine = value; }
    public ConfirmDialogue PreviousDialog { get => previousDialog; set => previousDialog = value; }
    public bool ButtonPressed { get => buttonPressed; }
}
