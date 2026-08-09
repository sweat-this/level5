using Level5.Core.Match;
using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    float timeRemaining = 0;
    private float currentTime;
    public float CurrentTime => currentTime;
    [SerializeField]
    private float timeStart;
    int minutes = 0;
    float seconds = 0;

    [SerializeField]
    bool displayTimer = false;
    [SerializeField]
    private bool timerEnabled = false;
    private Text timerText;
    [SerializeField]
    private bool modeRequiresCountDown;
    [SerializeField]
    private bool modeRequiresCounter;

    [SerializeField]
    Text shotClockText;
    [SerializeField]
    Text scoreClockText;

    bool timerTextLocked;
    public static Timer instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            if (GetComponent<Text>() != null)
            {
                timerText = GetComponent<Text>();
                timerText.text = "";
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (GameObject.Find("shot_clock") != null)
        {
            shotClockText = GameObject.Find("shot_clock").GetComponent<Text>();
            shotClockText.text = "";
        }
        if (GameObject.Find("score_clock") != null)
        {
            scoreClockText = GameObject.Find("score_clock").GetComponent<Text>();
            scoreClockText.text = "";
        }

        // timeStart is owned by GameRules.Start (via setTimer) and deliberately NOT computed here.
        // both used to write it with different rules - this one folded "is a contest mode" into
        // the same condition as "has a custom timer", so a contest mode whose prefab left
        // CustomTimer at 0 got timeStart = 0 here and 180 from GameRules. Unity does not order
        // Start() between components, so which value survived was undefined.
        if (timeStart <= 0)
        {
            timeStart = MatchClock.DefaultMatchSeconds;
        }

        // The clock behaviour comes from the resolved rules, not from two separate globals that
        // could each be set independently. MatchClockMode makes "counts up and counts down at the
        // same time" unrepresentable.
        ResolvedMatchRules rules = MatchRuntime.Rules;
        modeRequiresCounter = rules.RequiresCounter;
        modeRequiresCountDown = rules.RequiresCountDown;

        if (modeRequiresCounter || modeRequiresCountDown)
        {
            timerEnabled = true;
            displayTimer = true;
        }
        else
        {
            timerEnabled = false;
            displayTimer = false;
        }
    }

    void Update()
    {
        if (GameRules.instance == null)
        {
            return;
        }

        // countdown timer
        currentTime += Time.deltaTime;

        if (modeRequiresCountDown)
        {
            timeRemaining = timeStart - currentTime;
            minutes = Mathf.FloorToInt(timeRemaining / 60);
            seconds = (timeRemaining - (minutes * 60));
        }

        if (modeRequiresCounter)
        {
            minutes = Mathf.FloorToInt(currentTime / 60);
            seconds = (currentTime - (minutes * 60));
        }

        // gameover, disable timer display and set text to empty
        if (GameRules.instance.GameOver || timeRemaining < 0)
        {
            displayTimer = false;
            if (timerText != null)
            {
                timerText.text = "";
            }
            if (shotClockText != null)
            {
                shotClockText.text = "";
            }
        }
        // time's up. Whether that actually ends the match is MatchEndConditions' call - the clock
        // only reports that it reached zero.
        if (timeRemaining <= 0
            && !GameRules.instance.GameOver
            && !modeRequiresCounter
            && timerEnabled)
        {
            ReportTimeExpired();
        }
        // countdown timer
        if (displayTimer
            && timerEnabled
            && modeRequiresCountDown
            && timeRemaining > 0)
        {
            if (timerText != null)
            {
                if (minutes < 1)
                {
                    timerText.text = seconds.ToString("00.000");
                }
                else
                {
                    timerText.text = minutes.ToString("00") + " : " + seconds.ToString("00.000");
                }
            }
            if (shotClockText != null)
            {
                if (minutes < 1)
                {
                    shotClockText.text = seconds.ToString("00.00");
                }
                else
                {
                    shotClockText.text = minutes.ToString("0") + ":" + seconds.ToString("00.00");
                }
            }
        }
        // counting timer
        if (displayTimer
            && timerEnabled
            && modeRequiresCounter
            && !GameRules.instance.GameOver)
        {
            if (timerText != null)
            {
                if (minutes < 1)
                {
                    timerText.text = seconds.ToString("00.000");
                }
                else
                {
                    timerText.text = minutes.ToString("00") + " : " + seconds.ToString("00.000");
                }
            }
            if (shotClockText != null)
            {
                if (minutes < 1)
                {
                    shotClockText.text = seconds.ToString("00.00");
                }
                else
                {
                    shotClockText.text = minutes.ToString("0") + ":" + seconds.ToString("00.00");
                }
            }
        }
    }

    /// <summary>
    /// Asks whether a clock at zero ends this match, and reports it if so. Returns without doing
    /// anything when the scene has no player to ask about yet.
    /// </summary>
    private void ReportTimeExpired()
    {
        PlayerIdentifier player = GameLevelManager.instance != null
            ? GameLevelManager.instance.Player1
            : null;
        if (player == null)
        {
            return;
        }

        bool requiresConsecutiveShots = GameRules.instance.GameModeRequiresConsecutiveShots;
        bool expired = MatchEndConditions.TimeExpired(
            requiresConsecutiveShots,
            player.basketBallState.Thrown,
            player.playerController.Grounded,
            player.gameStats.ConsecutiveShotsMade);

        if (expired)
        {
            GameRules.instance.RequestEnd(MatchEndConditions.TimeExpiredReason(requiresConsecutiveShots));
        }
    }

    void setCustomTimerText(string text)
    {
        if (timerText != null)
        {
            timerText.text = text;
        }
        if (shotClockText != null)
        {
            shotClockText.text = text;
        }
    }

    public float TimeStart
    {
        get => timeStart;
        set => timeStart = value;
    }

    public bool DisplayTimer
    {
        get => displayTimer;
        set => displayTimer = value;
    }

    public Text ScoreClockText { get => scoreClockText; set => scoreClockText = value; }
    public float Seconds { get => seconds; }
}
