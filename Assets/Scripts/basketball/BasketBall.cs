using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Utility;
using Random = UnityEngine.Random;
using Level5.Core;
using Level5.Core.Match;

public class BasketBall : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    Rigidbody rigidbody;
    AudioSource audioSource;
    CharacterProfile characterProfile;
    BasketBallState basketBallState;
    GameStats gameStats;
    Animator anim;
    PlayerController playerController;
    PlayerIdentifier playerIdentifier;
    GameObject basketBallSprite;
    GameObject basketBallPosition;
    GameObject player;
    GameObject uiStatsBackground;
    GameObject dropShadow;

    Text scoreText;
    Text shootProfileText;

    float lastShotDistance;
    float maxBasketballSpeed = 0f;

    bool playHitRimSound;
    bool facingRight = true;

    public static BasketBall instance;

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

    // =========================================================== Start() ========================================================
    // Use this for initialization
    void Start()
    {
        // player 1's ball owns the static. every consumer of BasketBall.instance means
        // "the local player's ball" - camera follow, the free-play stat save, the ui-stats
        // toggle - not "whichever ball happened to run Start() last".
        if (instance == null || IsPrimaryBasketball())
        {
            instance = this;
        }

        playerIdentifier = GetComponent<PlayerIdentifier>();
        player = playerIdentifier.player;
        playerController = player.GetComponent<PlayerController>();
        characterProfile = playerController.GetComponent<CharacterProfile>();
        basketBallPosition = player.transform.Find("basketBall_position").gameObject;
        rigidbody = GetComponent<Rigidbody>();
        gameStats =  GetComponent<GameStats>();

        basketBallState = GetComponent<BasketBallState>();

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        anim = GetComponentInChildren<Animator>();

        //basketball drop shadow
        dropShadow = transform.root.Find("drop shadow").gameObject;

        //objects
        basketBallSprite = GameObject.Find("basketball_sprite"); //used to reset drop shadow. on launch, euler position gets changed

        //bool flags
        basketBallState.Locked = false;
        basketBallState.CanPullBall = true;
        playHitRimSound = true;

        //todo: move to game manager
        //UiStatsEnabled = true;

        // cap ball speed
        maxBasketballSpeed = 25f;
        // check for ui stats ON/OFF. i know this is sloppy. its just a quick test
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
        InvokeRepeating("CheckIsBallFacingGoal", 0, 0.5f);
        InvokeRepeating("displayUiStats", 0, 0.5f);

        if (MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.IsBattleRoyal)//|| MatchRuntime.HasConfiguration)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y + 20, transform.position.z);
            rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            dropShadow.SetActive(false);
        }
    }

    // =========================================================== Update() ========================================================

    // Update is called once per frame
    private void Update()
    {
        // get speed for basketball animation
        if (!MatchRuntime.Rules.EnemiesOnly)
        {
            if (rigidbody.linearVelocity.magnitude > maxBasketballSpeed && !basketBallState.InAir)
            {
                rigidbody.linearVelocity = rigidbody.linearVelocity.normalized * maxBasketballSpeed;
            }
            // drop shadow lock to bball transform on the ground
            // AUD-052: guarded like PlayerController - no active Terrain otherwise NREs per frame
            float shadowHeight = Terrain.activeTerrain != null
                ? Terrain.activeTerrain.SampleHeight(transform.position) + 0.02f
                : GameLevelManager.instance.TerrainHeight + 0.02f;
            dropShadow.transform.position = new Vector3(transform.root.position.x, shadowHeight, transform.root.position.z);

            // change this to reduce opacity
            if (!playerController.hasBasketball)
            {
                SetBallVisible(true);
                dropShadow.SetActive(true);
                basketBallState.CanPullBall = true;
                basketBallSprite.transform.rotation = Quaternion.Euler(13.6f, 0, transform.root.position.z);
            }
            //if player has ball and hasnt shot
            if (playerController.hasBasketball
                && playerController.currentState != playerController.dunkState)//&& !basketBallState.Thrown)
            {
                basketBallState.CanPullBall = false;
                SetBallVisible(false);
                dropShadow.SetActive(false);
                playerController.SetPlayerAnim("hasBasketball", true);
                //playerState.setPlayerAnim("walking", false);
                playerController.SetPlayerAnim("moonwalking", false);

                // move basketball to launch position and disable sprite
                transform.position = new Vector3(basketBallPosition.transform.position.x,
                    basketBallPosition.transform.position.y,
                    basketBallPosition.transform.position.z);
            }
        }
    }

    /// <summary>
    /// Shows or hides the ball itself.
    ///
    /// This used to be done by tinting <c>spriteRenderer.color</c> to alpha 0. A play-mode trace
    /// confirmed the tint is applied - the ball sits exactly on the owner's hold point with
    /// alpha 0.00 - and the material is a correctly configured transparent one
    /// (<c>_SURFACE_TYPE_TRANSPARENT</c>, SrcAlpha/OneMinusSrcAlpha), yet the ball is still drawn
    /// above the player's head. The sprite is rendered with
    /// <c>Universal Render Pipeline/Particles/Unlit</c>, a particle shader rather than a sprite
    /// shader, and it does not honour the SpriteRenderer's per-renderer colour the way
    /// Sprites-Default does.
    ///
    /// Toggling the renderer says exactly what is meant and does not depend on the shader
    /// respecting vertex colour. The tint is kept so anything that does read the colour still sees
    /// the old values.
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

    public void CheckIsBallFacingGoal()
    {
        anim.SetFloat("speed", rigidbody.linearVelocity.sqrMagnitude);
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
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("basketballrim")
            && playHitRimSound
            && !playerController.hasBasketball)
        {
            playHitRimSound = false;
            audioSource.PlayOneShot(SFXBB.instance.basketballHitRim);
            basketBallState.CanPullBall = true;
            basketBallState.Thrown = false;
            basketBallState.Locked = false;
        }
        // collision : basketball + ground
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("ground")
            && !playerController.hasBasketball)
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
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("fence")
            && !playerController.hasBasketball)
        {
            audioSource.PlayOneShot(SFXBB.instance.basketballHitFence);
            basketBallState.CanPullBall = true;
            basketBallState.Thrown = false;
            basketBallState.Locked = false;
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("basketballrim") && !playHitRimSound)
        {
            playHitRimSound = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // if basketball enters player hitbox
        if (gameObject.CompareTag("basketball")
            && other.gameObject.CompareTag("playerHitbox")
            && !basketBallState.Thrown)
        {
            playerController.hasBasketball = true;
            basketBallState.Thrown = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // if basketball exits player hitbox
        if (gameObject.CompareTag("basketball") && other.gameObject.CompareTag("playerHitbox") &&
            basketBallState.Thrown)
        {
            basketBallState.Thrown = true;
        }
    }

    // =================================== shoot ball function =======================================

    public void shootBasketBall(bool two, bool three, bool four, bool seven)
    {

        // set side or front shooting animation
        if (playerController.FacingFront) // facing straight toward bball goal
        {
            playerController.SetPlayerAnimTrigger("basketballShootFront");
        }
        else // side of goal, relative postion
        {
            playerController.SetPlayerAnimTrigger("basketballShoot");
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
        Vector3 tempPos = new Vector3(
            basketBallState.BasketBallTarget.transform.position.x,
            0, 
            basketBallState.BasketBallTarget.transform.position.z);

        float tempDist = Vector3.Distance(tempPos, basketBallPosition.transform.position);
        lastShotDistance = tempDist;

        // wait for shot meter to finish calculations for accurate launch values
        StartCoroutine(LaunchBasketBall());

        //reset state flags
        basketBallState.Thrown = true;
        playerController.CallBallToPlayer.Locked = false;
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
        if (two && !three)
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
            playerController.Shotmeter.SliderValueOnButtonPress);

        if (computation.IsSwish && BehaviorNpcCritical.instance != null && !playerIdentifier.isCpu)
        {
            BehaviorNpcCritical.instance.playAnimationCriticalSuccesful();
        }

        playerController.Shotmeter.displaySliderMessageText(computation.ShotMeterMessage);

        // launch the object by setting its initial velocity and flipping its state
        rigidbody.linearVelocity = computation.GlobalVelocity;
        playerController.hasBasketball = false;
        playerController.SetPlayerAnim("hasBasketball", false);

        // analytics
        AnaylticsManager.PlayerShoot(playerController.Shotmeter.SliderValueOnButtonPress);
    }

    // ============================ Functions and Properties ==========================================

    // wair for shotmeter value calculation, launch ball
    IEnumerator LaunchBasketBall()
    {
        // get position of ball when shot
        GameObject currentBallPosition = player.transform.Find("basketBall_position").gameObject;
        // wait for shot meter to finish
        yield return new WaitUntil(() => playerController.Shotmeter.MeterEnded == false);
        //launch ball to goal      
        Launch(currentBallPosition);
    }

    // ========================== ui display ===============================

    public bool displayUiStats()
    {
        // the overlay is one shared Text object in the scene, so every ball writing to it
        // would just fight over it. only the primary ball drives it.
        if (instance != this)
        {
            return false;
        }

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
            uiStatsBackground.SetActive(false);
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

    // true when this ball belongs to players[0].
    private bool IsPrimaryBasketball()
    {
        return GameLevelManager.instance != null
            && GameLevelManager.instance.players != null
            && GameLevelManager.instance.players.Count > 0
            && GameLevelManager.instance.players[0] != null
            && GameLevelManager.instance.players[0].basketball == gameObject;
    }

    public void updateScoreText()
    {
        // reads this ball's own gameStats. it used to reassign the field to players[0]'s stats
        // here, which also redirected every shot counter that writes through the same field -
        // a second human player's attempts were recorded onto player 1. the overlay is a single
        // shared Text object, so only the primary ball writes to it (see displayUiStats).
        BasketballShotPipeline.UpdateScoreText(scoreText, gameStats, lastShotDistance);
    }

    // ============================= getters/ setters ======================================

    public float LastShotDistance { get => lastShotDistance; set => lastShotDistance = value; }
    public GameStats GameStats => gameStats;
    public BasketBallState BasketBallState => basketBallState;
    public bool UiStatsEnabled { get; private set; }
    public GameObject BasketBallPosition { get => basketBallPosition; set => basketBallPosition = value; }
    public Rigidbody Rigidbody { get => rigidbody; set => rigidbody = value; }
}
