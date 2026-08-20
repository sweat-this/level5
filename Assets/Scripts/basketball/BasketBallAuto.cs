using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Utility;
using Random = UnityEngine.Random;
using Level5.Core;
using Level5.Core.Match;

public class BasketBallAuto : MonoBehaviour
{
    [SerializeField]
    public int pid;
    [SerializeField]
    public int bid;
    [SerializeField]
    public int bsid;
    SpriteRenderer spriteRenderer;
    [SerializeField]
    Rigidbody rigidbody;
    AudioSource audioSource;
    [SerializeField]
    BasketBallState basketBallState;
    [SerializeField]
    GameStats gameStats;
    [SerializeField]
    Animator anim;
    //ShotMeter shotMeter;
    [SerializeField]
    GameObject basketBallSprite;
    [SerializeField]
    GameObject basketBallPosition;
    GameObject basketBallTarget;
    //[SerializeField]
    //GameObject player;
    [SerializeField]
    GameObject autoPlayer;
    [SerializeField]
    AutoPlayerController autoPlayerController;
    CharacterProfile characterProfile;
    GameObject dropShadow;

    Text scoreText;
    Text shootProfileText;
    GameObject uiStatsBackground;

    float releaseVelocityY;
    float lastShotDistance;
    float maxBasketballSpeed = 0f;

    bool playHitRimSound;
    bool locked;
    bool facingRight = true;

    public static BasketBallAuto instance;

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

    private void Awake()
    {
        //autoPlayer = GameLevelManager.instance.AutoPlayer;
        //autoPlayerState = GameLevelManager.instance.AutoPlayerController;
        //basketBallState = GetComponent<BasketBallState>();
        //pid = autoPlayerState.Pid;
        //basketBallState.PlayerId = pid;
        //basketBallState.isCpu = true;
        //Debug.Log("pid : " + pid + "  bid : " + bid);
    }
    // =========================================================== Start() ========================================================
    // Use this for initialization
    void Start()
    {
        instance = this;
        autoPlayer = GetComponent<PlayerIdentifier>().autoPlayer;
        autoPlayerController = autoPlayer.GetComponent<AutoPlayerController>();
        characterProfile = autoPlayerController.GetComponent<CharacterProfile>();
        basketBallPosition = autoPlayer.transform.Find("basketBall_position").gameObject;
        basketBallState = GetComponent<BasketBallState>();
        basketBallState.isCpu = true;
        bsid = pid;
        //shotMeter = Assets.Scripts.Utility.UtilityFunctions.FindDeepChild(autoPlayer.transform, "shot_meter").GetComponent<ShotMeter>();
        rigidbody = gameObject.GetComponent<Rigidbody>();
        gameStats = GetComponent<GameStats>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        anim = GetComponentInChildren<Animator>();

        //basketball drop shadow
        dropShadow = transform.root.Find("drop shadow").gameObject;

        //objects
        //basketBallSprite = GameObject.Find("basketball_sprite"); //used to reset drop shadow. on launch, euler position gets changed
        basketBallPosition = autoPlayer.transform.Find("basketBall_position").gameObject;

        //bool flags
        basketBallState.Locked = false;
        basketBallState.CanPullBall = true;
        playHitRimSound = true;

        //todo: move to game manager
        UiStatsEnabled = false;

        // cap ball speed
        maxBasketballSpeed = 25f;
        if (GameObject.Find("ui_stats") != null)
        {
            shootProfileText = GameObject.Find("shooterProfileTextObject").GetComponent<Text>();
            scoreText = GameObject.Find("shootStatsTextObject").GetComponent<Text>();
            uiStatsBackground = GameObject.Find("textBackground");

            if (UiStatsEnabled)
            {
                updateScoreText();
                BasketballShotPipeline.UpdateShooterProfileText(shootProfileText, ShooterAttributesFactory.From(characterProfile));
                uiStatsBackground.SetActive(true);
            }
            else
            {
                scoreText.text = "";
                shootProfileText.text = "";
                uiStatsBackground.SetActive(false);
            }
        }
        //InvokeRepeating("CheckIsBallFacingGoalAuto", 0, 0.5f);
        //InvokeRepeating("displayUiStats", 0, 0.5f);

        if (MatchRuntime.Rules.EnemiesOnly)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y + 20, transform.position.z);
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    // =========================================================== Update() ========================================================

    // Update is called once per frame
    private void Update()
    {
        // get speed for basketball animation
        CheckIsBallFacingGoalAuto();
        if (!MatchRuntime.Rules.EnemiesOnly)
        {
            if (rigidbody.linearVelocity.magnitude > maxBasketballSpeed && !basketBallState.InAir)
            {
                rigidbody.linearVelocity = rigidbody.linearVelocity.normalized * maxBasketballSpeed;
            }
            // drop shadow lock to bball transform on the ground
            dropShadow.transform.position = new Vector3(transform.root.position.x, 0.01f, transform.root.position.z);

            // change this to reduce opacity
            if (!autoPlayerController.hasBasketball)
            {
                SetBallVisible(true);
                dropShadow.SetActive(true);
                basketBallState.CanPullBall = true;
                basketBallSprite.transform.rotation = Quaternion.Euler(13.6f, 0, transform.root.position.z);
            }

            //if player has ball and hasnt shot
            if (autoPlayerController.hasBasketball
                && autoPlayerController.currentState != autoPlayerController.inAirDunkState)//&& !basketBallState.Thrown)
            {
                basketBallState.CanPullBall = false;
                SetBallVisible(false);
                dropShadow.SetActive(false);
                autoPlayerController.SetPlayerAnim("hasBasketball", true);
                //autoPlayerState.setPlayerAnim("walking", false);
                autoPlayerController.SetPlayerAnim("moonwalking", false);

                // move basketball to launch position and disable sprite
                transform.position = new Vector3(basketBallPosition.transform.position.x,
                    basketBallPosition.transform.position.y,
                    basketBallPosition.transform.position.z);
            }
        }
    }

    /// <summary>
    /// Shows or hides the ball itself. Same fix as BasketBall.SetBallVisible: tinting
    /// spriteRenderer.color to alpha 0 does not hide it, because the sprite renders with
    /// Universal Render Pipeline/Particles/Unlit, which does not honour the SpriteRenderer's
    /// per-renderer colour. Toggling the renderer does.
    /// </summary>
    private void SetBallVisible(bool visible)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.enabled = visible;
        spriteRenderer.color = new Color(1f, 1f, 1f, visible ? 1f : 0f);
    }

    public void CheckIsBallFacingGoalAuto()
    {
        //Debug.Log("+++++++++++++++++++++speed :"+rigidbody.velocity.sqrMagnitude);
        anim.SetFloat("speed", rigidbody.linearVelocity.sqrMagnitude);
        //bballRelativePositioning = GameLevelManager.instance.BasketballRimVector.x - rigidbody.position.x;

        if (rigidbody.linearVelocity.x > 0 && !facingRight)
        {
            Flip();
        }

        if (rigidbody.linearVelocity.x < 0f && facingRight)
        {
            Flip();
        }
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;
    }


    // =========================================================== Collisions ========================================================

    private void OnCollisionEnter(Collision other)
    {
        // collision : basketball + rim
        if (gameObject.CompareTag("basketballAuto") && other.gameObject.CompareTag("basketballrim")
            && playHitRimSound
            && !autoPlayerController.hasBasketball)
        {
            playHitRimSound = false;
            audioSource.PlayOneShot(SFXBB.instance.basketballHitRim);
            basketBallState.CanPullBall = true;
            basketBallState.Thrown = false;
            basketBallState.Locked = false;
        }
        // collision : basketball + ground
        if (gameObject.CompareTag("basketballAuto") && other.gameObject.CompareTag("ground")
            && !autoPlayerController.hasBasketball)
        {
            basketBallState.CanPullBall = true;
            //reset rotation
            transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f));
            dropShadow.transform.rotation = Quaternion.Euler(90, 0, 0);
            basketBallState.Locked = false;
            basketBallState.Thrown = false;
            audioSource.PlayOneShot(SFXBB.instance.basketballBounce);
        }
        // collision : basketball + fence
        if (gameObject.CompareTag("basketballAuto") && other.gameObject.CompareTag("fence")
            && !autoPlayerController.hasBasketball)
        {
            audioSource.PlayOneShot(SFXBB.instance.basketballHitFence);
            basketBallState.CanPullBall = true;
            basketBallState.Thrown = false;
            basketBallState.Locked = false;
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (gameObject.CompareTag("basketballAuto") && other.gameObject.CompareTag("basketballrim") && !playHitRimSound)
        {
            playHitRimSound = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // if basketball enters player hitbox
        if (gameObject.CompareTag("basketballAuto")
            && other.gameObject.CompareTag("autoPlayerHitbox")
            && !basketBallState.Thrown)
        {
            autoPlayerController.hasBasketball = true;
            basketBallState.Thrown = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // if basketball exits player hitbox
        if (gameObject.CompareTag("basketballAuto") && other.gameObject.CompareTag("playerHitbox") &&
            basketBallState.Thrown)
        {
            basketBallState.Thrown = true;
        }
    }

    // =================================== shoot ball function =======================================

    public void shootBasketBall(bool two, bool three, bool four, bool seven)
    {
        //Debug.Log("-----shootBasketBall");
        // set side or front shooting animation
        if (autoPlayerController.FacingFront) // facing straight toward bball goal
        {
            autoPlayerController.SetPlayerAnimTrigger("basketballShootFront");
        }
        else // side of goal, relative postion
        {
            autoPlayerController.SetPlayerAnimTrigger("basketballShoot");
        }

        // reset ball rotation
        // #NOTE : hopefully this check works for issue : ball is hot but doesnt go toward goal
        transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, 0f));

        //// check for and set money ball
        //if (GameRules.instance.MoneyBallEnabled)
        //{
        //    basketBallState.MoneyBallEnabledOnShoot = true;
        //    PlayerStats.instance.Money -= 5; // moneyball spent
        //    BasketBallStats.MoneyBallAttempts++;
        //    GameRules.instance.MoneyBallEnabled = false;
        //}
        //else
        //{
        //    basketBallState.MoneyBallEnabledOnShoot = false;
        //}


        //// let basketball rim know current statistics of made/attempt for every shot
        //// this is more determining consecutive shots
        //// on make, if made = made+1 && attempt = attempt +1 ---> consecutive++
        //BasketBallShotMade.instance.setCurrentShotsMadeAttempted((int)basketBallStats.ShotMade, (int)basketBallStats.ShotAttempt);

        // let basketball state know what type of shot is attempted
        updateBasketBallStateShotTypeOnShoot(two, three, four, seven);

        // player on shot marker and game mode requires markers
        BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(basketBallState, gameStats);
        //calculate shot distance 
        Vector3 tempPos = new Vector3(basketBallState.BasketBallTarget.transform.position.x,
            0, basketBallState.BasketBallTarget.transform.position.z);
        float tempDist = Vector3.Distance(tempPos, basketBallPosition.transform.position);
        lastShotDistance = tempDist;

        // wait for shot meter to finish calculations for accurate launch values
        StartCoroutine(LaunchBasketBall());

        //reset state flags
        basketBallState.Thrown = true;
        autoPlayerController.CallBallToPlayer.Locked = false;
    }

    public void updateBasketBallStateShotTypeOnShoot(bool two, bool three, bool four, bool seven)
    {
        //Debug.Log("*********************************************** 2 : " + two);
        //Debug.Log("*********************************************** 3 : " + three);
        //Debug.Log("*********************************************** 4 : " + four);
        //Debug.Log("*********************************************** 7 : " + seven);

        // Clear stale shot snapshot data from a previous miss before setting the new attempt.
        basketBallState.ResetShotAttemptSnapshot();

        // identify is in 2 or 3 point range for stat counters
        if (two && !three )
        {
            basketBallState.TwoAttempt = true;
            gameStats.TwoPointerAttempts++;
            gameStats.ShotAttempt++;
        }
        if (three && !four)
        {
            basketBallState.ThreeAttempt = true;
            gameStats.ThreePointerAttempts++;
            gameStats.ShotAttempt++;
        }
        if (four && !three)
        {
            basketBallState.FourAttempt = true;
            gameStats.FourPointerAttempts++;
            gameStats.ShotAttempt++;
        }
        if (seven)
        {
            basketBallState.SevenAttempt = true;
            gameStats.SevenPointerAttempts++;
            gameStats.ShotAttempt++;
        }
        //GameRules.instance.updatePlayerScore();
    }

    // =================================== Launch ball function =======================================
    void Launch(GameObject ballPositionAtLaunch)
    {
        BasketballShotPipeline.LaunchComputation computation = BasketballShotPipeline.ComputeLaunch(
            transform,
            ballPositionAtLaunch.transform.position,
            basketBallState.BasketBallTarget.transform.position,
            ShooterAttributesFactory.From(characterProfile),
            basketBallState,
            gameStats,
            LastShotDistance,
            autoPlayerController.Shotmeter.SliderValueOnButtonPress);

        if (computation.IsSwish && BehaviorNpcCritical.instance != null)
        {
            BehaviorNpcCritical.instance.playAnimationCriticalSuccesful();
        }

        autoPlayerController.Shotmeter.displaySliderMessageText(computation.ShotMeterMessage);

        // launch the object by setting its initial velocity and flipping its state
        rigidbody.linearVelocity = computation.GlobalVelocity;

        autoPlayerController.hasBasketball = false;
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
        // CPU-2: the ball reports that the shot is away - it is the only thing that knows - but
        // the CPU owns the state transition. This used to write `shootTrigger` and `Locked`
        // directly, which meant the CPU could not complete a shoot cycle unless this method ran.
        autoPlayerController.EndShootCycle();
    }

    // ============================ Functions and Properties ==========================================

    // wait for shotmeter value calculation, launch ball
    IEnumerator LaunchBasketBall()
    {
        // get position of ball when shot
        GameObject currentBallPosition = autoPlayer.transform.Find("basketBall_position").gameObject;
        // wait for shot meter to finish
        yield return new WaitUntil(() => autoPlayerController.Shotmeter.MeterEnded == true);
        //yield return new WaitUntil(() => Time.time >= (autoPlayerState.Shotmeter.MeterStartTime + 0.5f));
        //launch ball to goal      
        Launch(currentBallPosition);
    }

    // ========================== shot accuracy functions ==========================================
    public float  rollForAutoPlayerSliderValue()
    {
        float shootPercent = 0;
        if (basketBallState.TwoPoints) { shootPercent = characterProfile.Accuracy2Pt / 2; }
        if (basketBallState.ThreePoints) { shootPercent = characterProfile.Accuracy3Pt / 2; }
        if (basketBallState.FourPoints) { shootPercent = characterProfile.Accuracy4Pt / 2; }
        if (basketBallState.SevenPoints) { shootPercent = characterProfile.Accuracy7Pt / 2; }
        //default if none assigned
        if(shootPercent == 0) { shootPercent = 90; }
        // get base value
        if (UtilityFunctions.RollPercent(shootPercent))
        {
            shootPercent = 95;
        }
        else
        {
            shootPercent =  90;
        }
        // accuracy variation. random -5, 5 range.
        // float overload so the range is symmetric - the int overload was max-exclusive,
        // which biased this 0.5 low and could never reach +5.
        float accuracyVariationValue = Random.Range(-5f, 5f);
        shootPercent += accuracyVariationValue;

        // clutch bonus
        float clutchBonus = Random.Range(1f, 10f);
        // consecutive shots (increase percent). shot streak ups percent
        // consecutive shots bonus capped at 10
        int consecShotsModifier = gameStats.ConsecutiveShotsMade;
        if (consecShotsModifier > 10)
        {
            consecShotsModifier = 10;
        }
        if (UtilityFunctions.RollPercent((characterProfile.Clutch / 2f) + consecShotsModifier))
        {
            shootPercent += clutchBonus;
        }
        if (shootPercent > 100) { return 100; }
        
        return shootPercent;
    }

    // ========================== ui display ===============================

    public bool displayUiStats()
    {
        //Debug.Log("displayUiStats() -- UiStatsEnabled : "+ UiStatsEnabled);
        if (scoreText == null || shootProfileText == null || uiStatsBackground == null)
        {
            return false;
        }

        if (UiStatsEnabled)
        {
            updateScoreText();
            BasketballShotPipeline.UpdateShooterProfileText(shootProfileText, ShooterAttributesFactory.From(characterProfile));
            uiStatsBackground.SetActive(true);
            return true;
        }
        else
        {
            scoreText.text = "";
            shootProfileText.text = "";
            //uiStatsBackground.SetActive(false);
            return false;
        }
    }

    public void toggleUiStats()
    {
        UiStatsEnabled = !UiStatsEnabled;
        Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
        messageText.text = "ui stats = " + UiStatsEnabled;

        // turn off text display after 5 seconds
        StartCoroutine(turnOffMessageLogDisplayAfterSeconds(3));
    }

    public IEnumerator turnOffMessageLogDisplayAfterSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Text messageText = GameObject.Find("messageDisplay").GetComponent<Text>();
        messageText.text = "";
    }

    public void updateScoreText()
    {
        BasketballShotPipeline.UpdateScoreText(scoreText, gameStats, lastShotDistance);
    }

    // ============================= getters/ setters ======================================

    public float LastShotDistance { get => lastShotDistance; set => lastShotDistance = value; }
    public GameStats GameStats => gameStats;
    public BasketBallState BasketBallState => basketBallState;
    public bool UiStatsEnabled { get; private set; }
}
