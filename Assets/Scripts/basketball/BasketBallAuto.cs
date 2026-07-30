using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

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
    float accuracy;
    float lastShotDistance;
    float maxBasketballSpeed = 0f;

    bool playHitRimSound;
    bool locked;
    bool facingRight = true;

    float accuracyModifierX;
    private float accuracyModifierY;
    private float accuracyModifierZ;

    public static BasketBallAuto instance;

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
                updateShooterProfileText();
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

        if (GameOptions.EnemiesOnlyEnabled)
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
        if (!GameOptions.EnemiesOnlyEnabled)
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
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f); // is about 100 % transparent
                dropShadow.SetActive(true);
                basketBallState.CanPullBall = true;
                basketBallSprite.transform.rotation = Quaternion.Euler(13.6f, 0, transform.root.position.z);
            }

            //if player has ball and hasnt shot
            if (autoPlayerController.hasBasketball
                && autoPlayerController.currentState != autoPlayerController.inAirDunkState)//&& !basketBallState.Thrown)
            {
                basketBallState.CanPullBall = false;
                spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
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

    private void updateShooterProfileText()
    {
        shootProfileText.text = characterProfile.PlayerDisplayName + "\n"
                                + "2 point : " + (characterProfile.Accuracy2Pt) + "\n"
                                + "3 point : " + (characterProfile.Accuracy3Pt) + "\n"
                                + "4 point : " + (characterProfile.Accuracy4Pt) + "\n"
                                + "7 point : " + (characterProfile.Accuracy7Pt) + "\n"
                                + "release : " + characterProfile.Release + "\n"
                                + "range : " + characterProfile.Range + "\n"
                                + "speed : " + characterProfile.RunSpeed + "\n"
                                + "jump : " + characterProfile.JumpForce + "\n"
                                + "luck : " + characterProfile.Luck;
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
        //if (basketBallState.PlayerOnMarker && GameRules.instance.PositionMarkersRequired)
        //{
        //    // on shoot. 
        //    basketBallState.PlayerOnMarkerOnShoot = true;
        //    basketBallState.OnShootShotMarkerId = basketBallState.CurrentShotMarkerId;
        //    // update shot attempt stat for marker position shot from
        //    GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].ShotAttempt++;

        //    if (basketBallState.PlayerOnMarkerOnShoot
        //        && GameRules.instance.BasketBallShotMarkersList[basketBallState.OnShootShotMarkerId].ShotAttempt == 5
        //        && (GameOptions.gameModeThreePointContest || GameOptions.gameModeFourPointContest))
        //    {
        //        GameStats.MoneyBallAttempts++;
        //    }

        //    if (GameRules.instance.MoneyBallEnabled)
        //    {
        //        basketBallState.MoneyBallEnabledOnShoot = true;
        //        gameStats.MoneyBallAttempts++;
        //    }
        //}
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
        Vector3 projectileXZPos = ballPositionAtLaunch.transform.position;
        Vector3 targetXZPos = new Vector3(basketBallState.BasketBallTarget.transform.position.x,
            basketBallState.BasketBallTarget.transform.position.y,
            basketBallState.BasketBallTarget.transform.position.z);

        // rotate the object to face the target
        transform.LookAt(targetXZPos);

        // shorthands for the formula
        float R = Vector3.Distance(projectileXZPos, targetXZPos);
        float G = Physics.gravity.y;
        float tanAlpha;
        // check last shot distance. if > 500, angle = 55 degrees. almost impossible to make shot 
        // >500ft with shoot angle 45-52 that most characters have 
        if (LastShotDistance >= 500)
        {
            tanAlpha = Mathf.Tan(55 * Mathf.Deg2Rad);
        }
        else
        {
            tanAlpha = Mathf.Tan(characterProfile.ShootAngle * Mathf.Deg2Rad);
        }
        float H = targetXZPos.y - projectileXZPos.y;
        float Vz = Mathf.Sqrt(G * R * R / (2.0f * (H - R * tanAlpha)));
        float Vy = tanAlpha * Vz;

        bool critical = rollForCriticalShotChance(characterProfile.Luck);

        string shotMeterMessage = "";
        string shotMeterMessageX = "";
        string shotMeterMessageY = "";
        string shotMeterMessageZ = "";

        // if rolled critical
        if (critical)
        {
            accuracyModifierX = 0;
            accuracyModifierY = 0;
            shotMeterMessage = "critical";
        }
        /* - how tough auto player is. Profile stats. accuracy
         * - player could get hot. sort of a luck variable for entire session.
         * - 
         */


        // if >= 95 and NOT critical (release stat factored in)
        if (autoPlayerController.Shotmeter.SliderValueOnButtonPress >= 95
            && !critical)
        {
            accuracyModifierX = 0;
            accuracyModifierY = getReleaseModifier();
            accuracyModifierZ = 0;
            shotMeterMessage = ">= 95";
            shotMeterMessageY = "+ release modifier";
        }
        // NOT critical and NOT >= 95 (get X, Y modifiers)
        if (autoPlayerController.Shotmeter.SliderValueOnButtonPress < 95
            && !critical)
        {
            accuracyModifierX = getAccuracyModifier();
            accuracyModifierY = getReleaseModifier();

            shotMeterMessage = "< 95";
            shotMeterMessageX = "+ accuracy modifier";
            shotMeterMessageY = "+ release modifier";
        }

        // range modifier always factors in
        accuracyModifierZ = getRangeModifier();

        if (accuracyModifierZ != 0)
        {
            shotMeterMessageZ = "+ range modifer";
        }

        // set shot meter message
        if (shotMeterMessage != null)
        {
            shotMeterMessage = shotMeterMessage + "\n" + shotMeterMessageX + "\n" + shotMeterMessageY + "\n" + shotMeterMessageZ;
        }
        else
        {
            shotMeterMessage = shotMeterMessageX + "\n" + shotMeterMessageY + "\n" + shotMeterMessageZ;
        }

        // if no mods, cheerleader action
        if (accuracyModifierX == 0 && accuracyModifierY == 0 && accuracyModifierZ == 0)
        {
            if (BehaviorNpcCritical.instance != null)
            {
                BehaviorNpcCritical.instance.playAnimationCriticalSuccesful();
            }
            // shot meter message 
            if (critical)
            {
                shotMeterMessage = "swish + critical";
            }
            else
            {
                shotMeterMessage = "swish";
            }
        }

        autoPlayerController.Shotmeter.displaySliderMessageText(shotMeterMessage);

        float xVector = 0 + accuracyModifierX;
        float yVector = Vy + accuracyModifierY; // + (accuracyModifier * shooterProfile.shootYVariance);
        float zVector = Vz - accuracyModifierZ; //+ accuracyModifierZ; // + (accuracyModifier * shooterProfile.shootZVariance);

        // create the velocity vector in local space and get it in global space
        Vector3 localVelocity = new Vector3(xVector, yVector, zVector);
        Vector3 globalVelocity = transform.TransformDirection(localVelocity);

        // launch the object by setting its initial velocity and flipping its state
        rigidbody.linearVelocity = globalVelocity;

        autoPlayerController.hasBasketball = false;
        autoPlayerController.SetPlayerAnim("hasBasketball", false);
        autoPlayerController.shootTrigger = false;
        autoPlayerController.Locked = false;
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
    bool rollForCriticalShotChance(float maxPercent)
    {
        Random random = new Random();
        float percent = random.Next(1, 100);
        if (percent <= maxPercent)
        {
            GameStats.CriticalRolled++;
            return true;
        }

        return false;
    }
    bool rollForCriticalRangeChance(float maxPercent)
    {
        Random random = new Random();
        float percent = random.Next(1, 100);

        if (percent <= maxPercent)
        {
            return true;
        }
        return false;
    }
    bool rollForCriticalReleaseChance(float maxPercent)
    {
        Random random = new Random();
        float percent = random.Next(1, 100);

        if (percent <= maxPercent)
        {
            return true;
        }
        return false;
    }
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
        Random random = new Random();
        float percent = random.Next(1, 100);
        if (percent <= shootPercent)
        {
            //Debug.Log("percent : " + percent + " shootPercent : "+ shootPercent);
            shootPercent = 95;
        }
        else
        {
            //Debug.Log("percent : " + percent + " shootPercent : " + shootPercent);
            shootPercent =  90;
        }
        // accuracy variation. random -5, 5 range
        Random accuracyVariation1 = new Random();
        float accuracyVariationValue = accuracyVariation1.Next(-5, 5);
        shootPercent += accuracyVariationValue;

        // percent to calculate clutch bonus
        Random accuracyVariation2 = new Random();
        float accuracyVariationPercent = accuracyVariation2.Next(1, 100);

        // clutch bonus
        Random clutchBonusRandom = new Random();
        float clutchBonus = clutchBonusRandom.Next(1, 10);
        // variation % + consecutive shots (increase percent). shot streak ups percent
        // consecutive shots bonus capped at 10
        int consecShotsModifier = gameStats.ConsecutiveShotsMade;
        if (consecShotsModifier > 10)
        {
            consecShotsModifier = 10;
        }
        if (accuracyVariationPercent <= (characterProfile.Clutch/2) + consecShotsModifier)
        {
            shootPercent += clutchBonus;
        }
        if (shootPercent > 100) { return 100; }
        
        return shootPercent;
    }

    private float getAccuracyModifier()
    {
        int direction = getRandomPositiveOrNegative();
        int slider = Mathf.CeilToInt(autoPlayerController.Shotmeter.SliderValueOnButtonPress);

        float sliderModifer = (100 - slider) * 0.025f;
        float accuracyModifier = 0;

        if (basketBallState.TwoPoints)
        {
            accuracyModifier = (100 - characterProfile.Accuracy2Pt) * 0.01f;
        }

        if (basketBallState.ThreePoints)
        {
            accuracyModifier = (100 - characterProfile.Accuracy3Pt) * 0.02f;
        }

        if (basketBallState.FourPoints)
        {
            accuracyModifier = (100 - characterProfile.Accuracy4Pt) * 0.01f;
        }

        if (basketBallState.SevenPoints)
        {
            accuracyModifier = (100 - characterProfile.Accuracy7Pt) * 0.01f;
        }

        return ((sliderModifer + (accuracyModifier * sliderModifer)) * direction);
    }


    private float getRangeModifier()
    {
        int direction = 1;
        // range divided by distance to get %
        // ex. range 50 ft / shot distance 100 = 50% change of reaching rim
        float rangeAccuracy = (float)(characterProfile.Range / (lastShotDistance * 6));
        float modifier = (rangeAccuracy * direction);

        // send max percent change
        // should 1/2 of modifer
        float maxChance = modifier * 100;


        if (modifier >= 1 || rollForCriticalRangeChance(maxChance))
        {
            return 0;
        }
        else
        {
            return modifier;
        }
    }

    private float getReleaseModifier()
    {
        int direction = getRandomPositiveOrNegative();
        float accuracyModifier = 0;

        accuracyModifier = (100 - characterProfile.Release) * 0.01f;

        // get random chance for removing release modifier
        // ex if release = 85, 15% chance to remove modifiers
        if (rollForCriticalReleaseChance(characterProfile.Release))
        {
            return 0;
        }
        else
        {
            return ((accuracyModifier * 0.75f)) * direction;
        }
    }

    private int getRandomPositiveOrNegative()
    {
        var Random = new Random();
        List<int> list = new List<int> { 1, -1 };
        int finder = Random.Next(list.Count); //Then you just use this; nameDisplayString = names[finder];
        int shotDirectionModifier = list[finder];

        return shotDirectionModifier;
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
            updateShooterProfileText();
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
        scoreText.text = "shots  : " + gameStats.ShotMade + " / " + gameStats.ShotAttempt + "  " +
                         getTotalPointAccuracy().ToString("0.00") + "\n"
                         + "points : " + gameStats.TotalPoints + "\n"
                         + "2 pointers : " + gameStats.TwoPointerMade + " / " +
                         gameStats.TwoPointerAttempts + "  " + getTwoPointAccuracy().ToString("0.00") + "%\n"
                         + "3 pointers : " + gameStats.ThreePointerMade + " / " +
                         gameStats.ThreePointerAttempts + "  " + getThreePointAccuracy().ToString("0.00") + "%\n"
                         + "4 pointers : " + gameStats.FourPointerMade + " / " +
                         gameStats.FourPointerAttempts + "  : " + getFourPointAccuracy().ToString("0.00") + "%\n"
                         + "7 pointers : " + gameStats.SevenPointerMade + " / " +
                         gameStats.SevenPointerAttempts + "  " + getSevenPointAccuracy().ToString("0.00") + "%\n"
                         + "last shot distance : " + (Math.Round(lastShotDistance, 2) * 6f).ToString("0.00") + " ft." +
                         "\n"
                         + "longest shot distance : " +
                         (Math.Round(gameStats.LongestShotMade, 2)).ToString("0.00") + " ft." + "\n" +
                         "criticals rolled : " + gameStats.CriticalRolled + " / " + gameStats.ShotAttempt
                         + "  " + getCriticalPercentage().ToString("0.00") + "%\n"
                         + "consecutive shots made : " + gameStats.ConsecutiveShotsMade + "\n"
                         + "current exp : " + gameStats.getExperienceGainedFromSession();
    }

    // ============================= convert to percentages ======================================
    // * NOTE : cast to float has to be (float) num1 / num2 to work;
    //  this format will not work for some reason -- (float)(num1 / num2 to work);
    public float getCriticalPercentage()
    {
        if (gameStats.CriticalRolled > 0)
        {
            float accuracy = (float)gameStats.CriticalRolled / gameStats.ShotAttempt;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }

    public float getTotalPointAccuracy()
    {
        if (gameStats.ShotAttempt > 0)
        {
            accuracy = (float)gameStats.ShotMade / gameStats.ShotAttempt;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }

    public float getTwoPointAccuracy()
    {
        if (gameStats.TwoPointerAttempts > 0)
        {
            float accuracy = (float)gameStats.TwoPointerMade / gameStats.TwoPointerAttempts;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }

    public float getThreePointAccuracy()
    {
        if (gameStats.ThreePointerAttempts > 0)
        {
            float accuracy = (float)gameStats.ThreePointerMade / gameStats.ThreePointerAttempts;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }

    public float getFourPointAccuracy()
    {
        if (gameStats.FourPointerAttempts > 0)
        {
            float accuracy = (float)gameStats.FourPointerMade / gameStats.FourPointerAttempts;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }

    public float getSevenPointAccuracy()
    {
        if (gameStats.SevenPointerAttempts > 0)
        {
            float accuracy = (float)gameStats.SevenPointerMade / gameStats.SevenPointerAttempts;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }

    public float getAccuracy(int made, int attempt)
    {
        if (attempt > 0)
        {
            float accuracy = (float)made / attempt;
            return (accuracy * 100);
        }
        else
        {
            return 0;
        }
    }



    // ============================= getters/ setters ======================================

    public float LastShotDistance { get => lastShotDistance; set => lastShotDistance = value; }
    public GameStats GameStats => gameStats;
    public BasketBallState BasketBallState => basketBallState;
    public bool UiStatsEnabled { get; private set; }
}
