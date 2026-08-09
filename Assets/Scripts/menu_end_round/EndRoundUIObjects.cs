using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndRoundUIObjects : MonoBehaviour
{
    [SerializeField]
    public Button nextRoundButton;
    [SerializeField]
    public Text nextRoundText;
    [SerializeField]
    public Button continueButton;
    [SerializeField]
    public Button startMenuButton;
    [SerializeField]
    Button QuitButton;
    [SerializeField]
    public Text nextRoundOpponent;
    [SerializeField]
    public Text nextRoundLevel;
    [SerializeField]
    public Text currentRoundWinnerIsCpu;
    [SerializeField]
    public Text currentRoundLoserIsCpu;
    [SerializeField]
    public Text currentRoundWinnerScore;
    [SerializeField]
    public Text currentRoundLoserScore;
    [SerializeField]
    public Text continueTimer;
    [SerializeField]
    public Text continueNumber;
    [SerializeField]
    public Text winnerText;
    [SerializeField]
    public Text loserText;
    [SerializeField]
    public Image currentRoundWinnerImage;
    [SerializeField]
    public Image currentRoundLoserImage;
    //[SerializeField]
    //public GameObject continueOptionObject;
    [SerializeField]
    public GameObject nextInfoObject;
    [SerializeField]
    public GameObject endMessageObject;
    [SerializeField]
    public Text endMessageText;

    public static EndRoundUIObjects instance;

    /// <summary>
    /// Releases the static so it cannot outlive the object it points at.
    ///
    /// Unity's overloaded == reports a destroyed object as null, so a stale static survives most
    /// guards - until something uses ?., caches the reference, or dereferences it directly. Clearing
    /// it here removes the whole class of problem rather than relying on every caller to guard.
    /// </summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // Start is called before the first frame update
    void Awake()
    {
        instance = this;
    }

    public Button QuitMenuButton
    {
        get { return QuitButton; }
    }

}
