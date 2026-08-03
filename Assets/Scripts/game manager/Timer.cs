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

        // if requires custom timer
        if (GameOptions.gameModeThreePointContest
            || GameOptions.gameModeFourPointContest
            || GameOptions.gameModeSevenPointContest
            || GameOptions.gameModeAllPointContest
            || GameOptions.customTimer > 0)
        {
            timeStart = GameOptions.customTimer;
        }
        //default 2 minute timer
        else
        {
            // timer is 2 minutes
            timeStart = 180;
        }

        modeRequiresCounter = GameOptions.gameModeRequiresCounter;
        modeRequiresCountDown = GameOptions.gameModeRequiresCountDown;

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
        // time's up, pause and reset timer text
        if (timeRemaining <= 0
            && !GameRules.instance.GameOver
            && !modeRequiresCounter
            && timerEnabled)
        {
            // ball is in the air, let the shot go before pausing 
            // or player in air and has basketball
            // not consecutive game mode
            if (GameLevelManager.instance != null
                && GameLevelManager.instance.players != null
                && GameLevelManager.instance.players.Count > 0
                && !GameLevelManager.instance.players[0].basketBallState.Thrown
                // player in air, has ball
                //&& !(GameLevelManager.instance.players[0].playerController.hasBasketball && GameLevelManager.instance.players[0].playerController.InAir)
                && GameLevelManager.instance.players[0].playerController.Grounded
                // not consecutive shots game mode
                && !GameRules.instance.GameModeRequiresConsecutiveShots)
            {
          
                //Debug.Log("game over");
                GameRules.instance.RequestGameOver();
            }
            // if consecutive shots mode and streak is less than 2
            if (GameLevelManager.instance != null
                && GameLevelManager.instance.players != null
                && GameLevelManager.instance.players.Count > 0
                && (GameRules.instance.GameModeRequiresConsecutiveShots
                && GameLevelManager.instance.players[0].gameStats.ConsecutiveShotsMade < 3))
            {
                //Debug.Log("game over");
                
                GameRules.instance.RequestGameOver();
            }
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
