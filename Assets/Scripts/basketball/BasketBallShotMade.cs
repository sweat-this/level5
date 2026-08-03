
using UnityEngine;
using Random = System.Random;


public class BasketBallShotMade : MonoBehaviour
{
    BasketBallState basketBallState;
    AudioSource audioSource;
    [SerializeField]
    public GameObject rimSprite;
    Animator anim;
    bool isColliding;

    const string moneyPrefabPath = "Prefabs/objects/money";
    private GameObject moneyClone;
    //int _consecutiveShotsMade = 0;
    //int _currentShotMade = 0;
    //int _currentShotAttempts = 0;
    //int _expectedShotMade = 1;
    //int _expectedShotAttempts = 1;

    public static BasketBallShotMade instance;
    private bool shotMade1;
    private bool shotMade2;

    private void Awake()
    {
        instance = this;
        //_currentShotMade = 0;
        //_currentShotAttempts = 0;
        //_expectedShotMade = 1;
        //_expectedShotAttempts = 1;
    }

    // Use this for initialization
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        anim = rimSprite.GetComponent<Animator>();
        // path to money prfab
        moneyClone = Resources.Load(moneyPrefabPath) as GameObject;

    }

    void Update()
    {
        isColliding = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if ((other.gameObject.CompareTag("basketball") || other.gameObject.CompareTag("basketballAuto") ) /*&& (!playerState.hasBasketball || !autoPlayerState.hasBasketball) */
            && !ShotMade1
            && !ShotMade2
            && gameObject.name.Equals("basketBallMadeShot1"))
        {
            shotMade1 = true;
        }
    }

    public void shotMade(GameStats gameStats, PlayerIdentifier playerIdentifier)
    {
        BasketBall basketBall = playerIdentifier.basketBallController;
        BasketBallAuto basketBallAuto = playerIdentifier.basketBallAutoController;
        basketBallState = playerIdentifier.basketBallState;
        if (isColliding)
        {
            return;
        }
        else
        {
            isColliding = true;
        }
        shotMade1 = false;
        shotMade2 = false;
        audioSource.PlayOneShot(SFXBB.instance.basketballNetSwish);
        // play rim animation
        anim.Play("madeshot");
        if (playerIdentifier.isCpu)
        {
            // add to total shot distance made total
            gameStats.TotalDistance += (basketBallAuto.LastShotDistance * 6);
            // is this the longest shot made?
            if ((basketBallAuto.LastShotDistance * 6) > gameStats.LongestShotMade)
            {
                gameStats.LongestShotMade = basketBallAuto.LastShotDistance * 6;
            }
        }
        else
        {
            // add to total shot distance made total
            gameStats.TotalDistance += (basketBall.LastShotDistance * 6);
            // is this the longest shot made?
            if ((basketBall.LastShotDistance * 6) > gameStats.LongestShotMade)
            {
                gameStats.LongestShotMade = basketBall.LastShotDistance * 6;
            }
        }
        // updates shots made/shot attempted
        updateShotMadeBasketBallStats(gameStats, basketBallState);

        // instantiate money if game requires it
        if (GameRules.instance.GameModeRequiresMoneyBall
            && basketBallState.PlayerOnMarkerOnShoot)
        //&& basketBallState.MoneyBallEnabledOnShoot)
        //&& PlayerStats.instance.Money >= 5
        //&& GameRules.instance.MoneyBallEnabled)
        {
            //Debug.Log(" instantiate moeny : player on marker at shoot");
            instantiateMoney(1);
        }
        // reset states
        basketBallState.ResetShotAttemptSnapshot();

        //GameRules.instance.updatePlayerScore();
        //// update onscreen ui stats
        //if (!playerIdentifier.isCpu )
        //{
        //    //basketBall.updateScoreText();
        //    GameRules.instance.updatePlayerScore();
        //}
        //if (playerIdentifier.isCpu)
        //{
        //    //basketBallAuto.updateScoreText();
        //    GameRules.instance.updatePlayerScore();
        //}
        // update game rules ui
    }
    void instantiateMoney(float value)
    {
        // set value of shot
        moneyClone.GetComponent<PickupObject>().updateMoneyValue(value);
        Random random = new Random();
        float distance = (float)(random.NextDouble());
        Vector3 tempPos = new Vector3(transform.position.x + distance, 0, transform.position.z - 2);
        Instantiate(moneyClone, tempPos, Quaternion.identity);
    }

    private void updateShotMadeBasketBallStats(GameStats gameStats, BasketBallState basketBallState)
    {
        // first thing, update shot made total
        // ==================== consecutive shots logic ==============================

        //// get current state of shots made/attempted
        //_currentShotMade = (int)gameStats.ShotMade;
        //_currentShotAttempts = (int)gameStats.ShotAttempt;

        //// if current is == expected made/attempt, increment consecutive and not a 2 point shot
        //// 
        //if (_currentShotMade == _expectedShotMade
        //    && _currentShotAttempts == _expectedShotAttempts
        //    && !basketBallState.TwoAttempt)
        //{
        //    _consecutiveShotsMade++;
        //    // increment expected values for next shot
        //    _expectedShotMade = _currentShotMade + 1;
        //    _expectedShotAttempts = _currentShotAttempts + 1;
        //}
        //// else, not consecutive shot. get current, increment for next expected consecutive shot
        //else
        //{
        //    _consecutiveShotsMade = 1;
        //    // increment expected values for next shot
        //    _expectedShotMade = _currentShotMade + 1;
        //    _expectedShotAttempts = _currentShotAttempts + 1;
        //}
        //// if current consecutive greater than previous high consecutive
        //if (ConsecutiveShotsMade > gameStats.MostConsecutiveShots)
        //{
        //    gameStats.MostConsecutiveShots = ConsecutiveShotsMade;
        //}

        // ==================== point total logic ==============================
        // if not 3/4/all point contest
        if (!GameRules.instance.GameModeThreePointContest
            && !GameRules.instance.GameModeFourPointContest
            && !GameRules.instance.GameModeSevenPointContest
            && !GameRules.instance.GameModeAllPointContest
            && GameOptions.gameModeSelectedId != 19)
        // game mode 19 is 1 pt per 10 feet of last shot made
        {
            if (basketBallState.TwoAttempt)
            {
                gameStats.TwoPointerMade++;
                gameStats.TotalPoints += 2;
            }

            if (basketBallState.ThreeAttempt)
            {
                gameStats.ThreePointerMade++;
                // if consecutive > 5 and game mode for 'Total Points+'
                if (gameStats.ConsecutiveShotsMade >= GameRules.instance.InThePocketActivateValue && GameOptions.gameModeSelectedId == 15)
                {
                    gameStats.TotalPoints += 4;
                }
                else
                {
                    gameStats.TotalPoints += 3;
                }
            }

            if (basketBallState.FourAttempt)
            {
                gameStats.FourPointerMade++;
                // if consecutive > 5 and game mode for 'Total Points+'
                if (gameStats.ConsecutiveShotsMade >= GameRules.instance.InThePocketActivateValue && GameOptions.gameModeSelectedId == 15)
                {
                    gameStats.TotalPoints += 6;
                }
                else
                {
                    gameStats.TotalPoints += 4;
                }

            }
            if (basketBallState.SevenAttempt)
            {
                gameStats.SevenPointerMade++;
                // if consecutive > 5 and game mode for 'Total Points+'
                if (gameStats.ConsecutiveShotsMade >= GameRules.instance.InThePocketActivateValue && GameOptions.gameModeSelectedId == 15)
                {
                    gameStats.TotalPoints += 10;
                }
                else
                {
                    gameStats.TotalPoints += 7;
                }
            }
        }
        else
        {
            int pointsScored = 0;
            // if player is on marker and marker enabled && not game mode 19
            if (basketBallState.PlayerOnMarkerOnShoot
                && GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].MarkerEnabled)
            {
                // if moneyball
                if (basketBallState.TwoAttempt)
                {
                    gameStats.TwoPointerMade++;
                    pointsScored = 2;
                }

                if (basketBallState.ThreeAttempt)
                {
                    gameStats.ThreePointerMade++;
                    pointsScored = 3;
                }

                if (basketBallState.FourAttempt)
                {
                    gameStats.FourPointerMade++;
                    pointsScored = 4;
                }

                if (basketBallState.SevenAttempt)
                {
                    gameStats.SevenPointerMade++;
                    pointsScored = 7;
                }
                // if moneyball / last shot on marker (5/5)
                if (GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].ShotAttempt 
                    == GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].MaxShotAttempt
                    && (GameOptions.gameModeThreePointContest || GameOptions.gameModeFourPointContest || GameOptions.gameModeSevenPointContest))
                {
                    gameStats.TotalPoints += (pointsScored * 2);
                    gameStats.MoneyBallMade++;
                }
                // not last shot on marker (1-4/5)
                else
                {
                    gameStats.TotalPoints += pointsScored;
                }
            }
            // is game mode 19 [Points By Distance]
            if (GameOptions.gameModeSelectedId == 19)
            {
                if (basketBallState.TwoAttempt)
                {
                    gameStats.TwoPointerMade++;
                }

                if (basketBallState.ThreeAttempt)
                {
                    gameStats.ThreePointerMade++;
                }

                if (basketBallState.FourAttempt)
                {
                    gameStats.FourPointerMade++;
                }

                if (basketBallState.SevenAttempt)
                {
                    gameStats.SevenPointerMade++;
                }
                // reset point scored if Points By Distance mode
                pointsScored = 0;
                pointsScored = Mathf.FloorToInt((BasketBall.instance.LastShotDistance * 6) / 10);
                gameStats.TotalPoints += pointsScored;
            }
        }
        // moneyball stats
        if (basketBallState.MoneyBallEnabledOnShoot)
        {
            gameStats.MoneyBallMade++;
        }
        gameStats.ShotMade = gameStats.TwoPointerMade + gameStats.ThreePointerMade 
            + gameStats.FourPointerMade + gameStats.SevenPointerMade;

        // ==================== requires position markers logic ==============================
        if (basketBallState.PlayerOnMarkerOnShoot 
            && (GameOptions.gameModeRequiresShotMarkers3s || GameOptions.gameModeRequiresShotMarkers4s || GameOptions.gameModeRequiresShotMarkers7s))
        {
            // if money ball enabled
            if (basketBallState.MoneyBallEnabledOnShoot)
            {
                int max = GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].MaxShotMade;
                GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].ShotMade = max;
            }
            // no money ball, update current shot marker stats
            else
            {
                GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].ShotMade++;
            }
        }
    }

    //public int ConsecutiveShotsMade { get => _consecutiveShotsMade; }
    public bool ShotMade1 { get => shotMade1; set => shotMade1 = value; }
    public bool ShotMade2 { get => shotMade2; set => shotMade2 = value; }
}

