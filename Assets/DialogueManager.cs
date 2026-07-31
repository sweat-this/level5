using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class DialogueManager : MonoBehaviour
{
    public const int DialogNone = 0;
    public const int DialogYes = 1;
    public const int DialogCancel = 2;

    public ConfirmDialogue confirmationDialog;
    public ConfirmDialogue confirmationDialogTip;

    [HideInInspector]
    public int ConfirmationDialogue = 0, TipDialogue = 1;
    public int dialogueType;

    public Canvas canvas;
    private Coroutine coroutine;
    ConfirmDialogue previousDialog;

    bool buttonPressed = false;
    private int lastDialogResult;

    public static DialogueManager instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        Coroutine = null;
        canvas = GameObject.FindAnyObjectByType<Canvas>();
        if (GameObject.Find("confirm_tip") != null)
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
        buttonPressed = false;
        lastDialogResult = DialogNone;

        if (canvas == null)
        {
            canvas = GameObject.FindAnyObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogError("DialogueManager could not find a Canvas for the confirmation dialog.");
            coroutine = null;
            yield break;
        }

        ConfirmDialogue dialog = null;
        if (dialogueType == ConfirmationDialogue)
        {
            dialog = Instantiate(confirmationDialog, canvas.transform); // instantiate the UI dialog box
        }
        if (dialogueType == TipDialogue)
        {
            dialog = Instantiate(confirmationDialogTip, canvas.transform); // instantiate the UI dialog box
        }

        if (dialog == null)
        {
            Debug.LogError("DialogueManager could not create a confirmation dialog.");
            coroutine = null;
            yield break;
        }

        PreviousDialog = dialog;

        while (dialog.result == dialog.NONE)
        {
            yield return null; // wait
        }

        if (dialog.result == dialog.YES)
        {
            lastDialogResult = dialog.result;
            buttonPressed = true;
            SelectFirstEventSystemObject();
        }
        if (dialog.result == dialog.CANCEL)
        {
            lastDialogResult = dialog.result;
            buttonPressed = true;
            SelectFirstEventSystemObject();
        }
        //if (dialog.result == dialog.NEXT)
        //{
        //    buttonPressed = true;
        //    EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        //}
        Destroy(dialog.gameObject);

        coroutine = null;
    }

    private static void SelectFirstEventSystemObject()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(EventSystem.current.firstSelectedGameObject);
        }
    }

    public Coroutine Coroutine { get => coroutine; set => coroutine = value; }
    public ConfirmDialogue PreviousDialog { get => previousDialog; set => previousDialog = value; }
    public bool ButtonPressed { get => buttonPressed; }
    public int LastDialogResult => lastDialogResult;
}
