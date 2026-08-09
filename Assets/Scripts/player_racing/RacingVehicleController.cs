using Assets.Scripts.Utility;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Level5.Core.Match;

public class RacingVehicleController : MonoBehaviour
{
    // components 
    private Animator anim;
    private AnimatorStateInfo currentStateInfo;
    private GameObject dropShadow;
    //private AudioSource audiosource;
    //private SpriteRenderer spriteRenderer;
    private Rigidbody rigidBody;
    private RacingVehicleProfile vehicleProfile;
    //private BasketBall basketball;
    //private ShotMeter shotmeter;
    //private PlayerSwapAttack playerSwapAttack;
    //private PlayerHealth playerHealth;

    // walk speed #review can potentially remove
    [SerializeField]
    private float movementSpeed;
    //[SerializeField]
    //private float inAirSpeed; // leave serialized
    //[SerializeField]
    //private float blockSpeed; // leave serialized
    //[SerializeField]
    //private float attackSpeed; // leave serialized

    // get/set for following at bottom of class
    [SerializeField]
    private bool _facingRight;
    private bool _facingFront;
    private bool _locked;
    [SerializeField]
    private bool _inAir;
    [SerializeField]
    private bool _grounded;
    private bool _knockedDown;
    private bool _takeDamage;
    private bool _avoidedKnockDown;
    private bool canAttack;
    private bool canBlock;

    private bool _isGrinding;

    Vector3 playerRelativePositioning;
    //Vector3 bballRimVector;

    // customizable options
    //[SerializeField]
    //private bool playerCanBlock;
    //[SerializeField]
    //private bool playerCanAttack;
    [SerializeField]
    float _knockDownTime;
    [SerializeField]
    float _takeDamageTime;

    // movement variables
    Vector3 movement;
    [SerializeField]
    float movementHorizontal;
    float movementVertical;

    float screenXRange;
    float screenYRange;
    private RacingInputReader inputReader;

    // player take damage display
    Text damageDisplayValueText;
    GameObject damageDisplayObject;
    const string damageDisplayValueName = "player_damage_display_text";

    // control movement speed based on state
    public int currentState;
    public int idleState = Animator.StringToHash("base.idle");
    public int walkState = Animator.StringToHash("base.movement.walk");
    public int run = Animator.StringToHash("base.movement.run");
    public int bWalk = Animator.StringToHash("base.movement.basketball_dribbling");
    public int bIdle = Animator.StringToHash("base.movement.basketball_idle");
    public int knockedDownState = Animator.StringToHash("base.knockedDown");
    public int takeDamageState = Animator.StringToHash("base.takeDamage");
    public int specialState = Animator.StringToHash("base.special");
    public int attackState = Animator.StringToHash("base.attack.attack");
    public int blockState = Animator.StringToHash("base.attack.block");
    public int inAirDunkState = Animator.StringToHash("base.inair.inair_dunk");
    public int dunkState = Animator.StringToHash("base.inair.dunk");
    private bool runningToggle = true;
    private bool jumpTrigger;

    [SerializeField]
    private Text vehicleCurrentSpeedText;

    void Start()
    {
        //audiosource = GameLevelManager.instance.GetComponent<AudioSource>();
        anim = GetComponentInChildren<Animator>();
        vehicleProfile = GetComponent<RacingVehicleProfile>();
        rigidBody = GetComponent<Rigidbody>();

        dropShadow = transform.root.transform.Find("drop_shadow").gameObject;
        FacingRight = true;

        movementSpeed = vehicleProfile.Speed;
        //movementSpeed = 3;

        if (_knockDownTime == 0) { _knockDownTime = 1.5f; }
        if (_takeDamageTime == 0) { _takeDamageTime = 0.5f; }
        //if (blockSpeed == 0) { blockSpeed = 0.2f; }
        //if (attackSpeed == 0) { attackSpeed = 0f; }

        screenXRange = Screen.width / 10;
        screenYRange = Screen.height / 10;

        // AUD-053: the same guards PlayerController carries. This twin was left unguarded.
        damageDisplayObject = SceneObjects.Find(damageDisplayValueName, this);
        damageDisplayValueText = damageDisplayObject != null
            ? damageDisplayObject.GetComponent<Text>()
            : null;

        //GameOptions.sniperEnabled = true; // test flag;
        if (MatchRuntime.Rules.EnemiesEnabled || MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.SniperEnabled)
        {
            //playerSwapAttack = GetComponent<PlayerSwapAttack>();
            if (damageDisplayObject != null && damageDisplayObject.GetComponent<Canvas>() != null)
            {
                damageDisplayObject.GetComponent<Canvas>().worldCamera = Camera.main;
            }
        }
        else if (damageDisplayObject != null)
        {
            damageDisplayObject.SetActive(false);
        }

        // custom knockdown time for sniper mode
        if (MatchRuntime.Rules.SniperEnabled)
        {
            _knockDownTime = 0.75f;
        }
    }

    private RacingInputReader EnsureInputReader()
    {
        if (RacingGameManager.instance == null)
        {
            return null;
        }

        if (inputReader == null)
        {
            inputReader = new RacingInputReader(RacingGameManager.instance.Controls);
        }

        return inputReader;
    }

    // not affected by framerate
    void FixedUpdate()
    {
        //------MOVEMENT---------------------------
        if (!KnockedDown && currentState != takeDamageState)
        {
            RacingInputReader reader = EnsureInputReader();
            Vector2 moveInput = reader != null ? reader.ReadMove(screenXRange, screenYRange) : Vector2.zero;
            movementHorizontal = moveInput.x;
            movementVertical = moveInput.y;
            //movement = new Vector3(movementHorizontal, 0, movementVertical) * (movementSpeed * Time.deltaTime);
            //movement = new Vector3(movementHorizontal, 0, movementVertical) * (movementSpeed * Time.fixedDeltaTime);

            if (reader != null
                && reader.RunHeld
                && movementSpeed < vehicleProfile.MaxSpeed
                && !KnockedDown)
            {
                //Debug.Log("movementSpeed : "+ movementSpeed);
                movementSpeed = (movementSpeed * vehicleProfile.Acceleration);
            }
            if (((reader == null || !reader.RunHeld)
                //&& RacingGameManager.instance.Controls.Player.run.ReadValue<float>() > 1
                && rigidBody.linearVelocity.magnitude > 0
                && movementSpeed > vehicleProfile.Speed)
                || KnockedDown)
            {
                //Debug.Log("moving but not holding shift");
                // lose 1% of speed per frame
                movementSpeed -= (movementSpeed / 100);
            }
            //else
            //{
            //    movementSpeed = (movementSpeed * vehicleProfile.Acceleration);
            //}
            //movement = new Vector3(movementHorizontal, 0, movementVertical) * (movementSpeed * Time.deltaTime);
            //movement = new Vector3(movementHorizontal, 0, movementVertical) * (movementSpeed * Time.fixedUnscaledDeltaTime);

            // max Z speed is default speed
            if (movementSpeed > (vehicleProfile.MaxSpeed * 0.75f))
            {
                movementVertical *= 0.25f;
            }
            // less than default 50% turning
            if( movementSpeed > 0.1 && movementSpeed < vehicleProfile.Speed)
            {
                movementVertical *= 0.5f;
            }

            movement = new Vector3(movementHorizontal, 0, movementVertical) * (movementSpeed * Time.fixedDeltaTime);
            // check jump trigger and execute jump
            if (jumpTrigger)
            {
                jumpTrigger = false;
                PlayerJump();
            }
            //if (dunkTrigger
            //    && (currentState != inAirDunkState || currentState != inAirDunkState)
            //    && !InAir
            //    && Grounded
            //    && !Locked)
            //{
            //    dunkTrigger = false;
            //    PlayerDunk.instance.playerDunk();
            //}

            if (currentState != specialState)
            {
                //rigidBody.MovePosition(transform.position + movement);
                transform.Translate(movement);
                //isWalking(movement);
                IsWalking(movementHorizontal, movementVertical);
            }
        }
    }


    /// <summary>
    /// The developer speed readout.
    ///
    /// This built four concatenated strings into a live UI Text every single frame - roughly seven
    /// allocations per frame per vehicle, on a mobile target, for a diagnostic. Three of the four
    /// values come from the vehicle profile and never change during a run.
    ///
    /// Now it is development-only and rebuilds the string only when the speed actually changes.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void UpdateSpeedReadout()
    {
        if (vehicleCurrentSpeedText == null || Mathf.Approximately(movementSpeed, lastReadoutSpeed))
        {
            return;
        }

        lastReadoutSpeed = movementSpeed;
        vehicleCurrentSpeedText.text = "speed : " + movementSpeed
            + "\nmax speed : " + vehicleProfile.MaxSpeed
            + "\nacceleration : " + vehicleProfile.Acceleration
            + "\njump : " + vehicleProfile.JumpForce;
    }

    /// <summary>Last speed the readout was built for, so an unchanged speed costs nothing.</summary>
    private float lastReadoutSpeed = float.NaN;

    // Update :: once once per frame
    void Update()
    {

        // current used to determine movement speed based on animator state. walk, knockedown, moonwalk, idle, attacking, etc
        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;


        UpdateSpeedReadout();

        // knocked down
        if (KnockedDown && !Locked)
        {
            Locked = true;
            StartCoroutine(PlayerKnockedDown());
        }
        if (!KnockedDown && TakeDamage && !Locked)
        {
            Locked = true;
            StartCoroutine(PlayerTakeDamage());
        }

        // keep drop shadow on ground at all times
        if (Grounded)
        {
            dropShadow.transform.position = new Vector3(transform.root.position.x, transform.position.y + 0.01f,
                transform.root.position.z);
        }
        if (!Grounded) // player in air
        {
            //float terrainYHeight = Terrain.activeTerrain.SampleHeight(transform.position) + 0.02f;
            //Debug.Log("terrainYHeight : " + terrainYHeight);

            dropShadow.transform.position = new Vector3(transform.root.position.x, RacingGameManager.instance.TerrainHeight + 0.01f,
            transform.root.position.z);
        }

        //bballRelativePositioning = bballRimVector.x - rigidBody.position.x;
        //playerRelativePositioning = rigidBody.position - bballRimVector;

        //playerDistanceFromRim = Vector3.Distance(transform.position, new Vector3(bballRimVector.x, transform.position.y, bballRimVector.z));
        //playerDistanceFromRimFeet = playerDistanceFromRim * 6;

        // if run input or run toggle on
        RacingInputReader reader = EnsureInputReader();
        if (reader != null
            && reader.RunHeld //if button is held
            && !InAir
            && !KnockedDown
            && rigidBody.linearVelocity.magnitude > 0.1f
            && !Locked)
        {
            //running = true;
            anim.SetBool("moonwalking", true);
        }
        else
        {
            //running = false;
            //anim.SetBool("moonwalking", false);
        }

        // determine if player animation is shooting from or facing basket
        //if (Math.Abs(playerRelativePositioning.x) > 2 &&
        //    Math.Abs(playerRelativePositioning.z) < 2)
        if (movementHorizontal > 0)
        {
            FacingFront = true;
        }
        if (movementHorizontal < 0)
        {
            FacingFront = false;
        }

        //// set player shoot anim based on position
        //if (FacingFront) // facing straight toward bball goal
        //{
        //    SetPlayerAnim("basketballFacingFront", true);
        //}
        //else // side of goal, relative postion
        //{
        //    SetPlayerAnim("basketballFacingFront", false);
        //}

        // ----- control speed based on commands----------
        // idle, walk, walk with ball state
        if (/*currentState == idleState || currentState == walkState || currentState == bIdle*/
            rigidBody.linearVelocity.magnitude == 0
            && !InAir
            && !KnockedDown)
        {
            //Debug.Log("reset speed to 0 -----------------------------------------------");
            //Debug.Log("== rigidBody.velocity.magnitude : "+ rigidBody.velocity.magnitude);
            movementSpeed = vehicleProfile.Speed;
        }
        //// if run state
        //if (currentState == run ) //|| (runningToggle || running) )
        //{
        //    movementSpeed = characterProfile.RunSpeed; ;
        //}
        ////// if run state
        ////if (currentState == bWalk && hasBasketball) //|| (runningToggle || running) )
        ////{
        ////    movementSpeed = characterProfile.RunSpeedHasBall; ;
        ////}
        //if (currentState == attackState || currentState == blockState)
        //{
        //    movementSpeed = blockSpeed;
        //}
        //// inair state
        //if (InAir)//&& currentState != inAirDunkState)
        //{
        //    //CheckIsPlayerFacingGoal();
        //    if (currentState != inAirDunkState)
        //    {
        //        movementSpeed = inAirSpeed;
        //    }
        //}
        //if (Grounded
        //    && !KnockedDown
        //    && !hasBasketball
        //    && !InAir
        //    && currentState != dunkState)
        //{
        //    canAttack = true;
        //    canBlock = true;
        //}
        //else
        //{
        //    canBlock = false;
        //    canAttack = false;
        //}

        //------------------ jump -----------------------------------
        if (reader != null
            && reader.JumpPressed
            //&& !GameLevelManager.instance.Controls.Player.shoot.triggered
            //&& hasBasketball
            && (Grounded || IsGrinding)
            && !KnockedDown
            && !MatchRuntime.Rules.EnemiesOnly
            && !InAir)
        {
            //if (PlayerDunk.instance != null
            //    && PlayerDunk.instance.PlayerCanDunk
            //    && playerDistanceFromRimFeet < PlayerDunk.instance.DunkRangeFeet)
            //{
            //    dunkTrigger = true;
            //}
            //else
            //{
            jumpTrigger = true;
            //}
        }
        //------------------ shoot -----------------------------------
        //// if has ball, is in air, and pressed shoot button.
        //if (InAir
        //    && hasBasketball
        //    && GameLevelManager.instance.Controls.Player.shoot.triggered
        //    && !MatchRuntime.Rules.EnemiesOnly
        //    && currentState != inAirDunkState)
        //{
        //    //Debug.Log("shoot");
        //    CallBallToPlayer.instance.Locked = true;
        //    basketball.BasketBallState.Locked = true;
        //    CheckIsPlayerFacingGoal(); // turns player facing rim
        //    Shotmeter.MeterEnded = true;
        //    PlayerShoot();
        //}
        //------------------ attack -----------------------------------

        //if (GameLevelManager.instance.Controls.Player.shoot.triggered
        //    && GameLevelManager.instance.Controls.Player.jump.ReadValue<float>() == 1
        //    && !hasBasketball
        //    && canAttack
        //    && MatchRuntime.Rules.EnemiesEnabled)
        //{
        //    PlayerAttack();
        //}
        //else
        //{
        //    anim.SetBool("attack", false);
        //}

        //if (GameLevelManager.instance.Controls.Player.jump.ReadValue<float>() == 1
        //    //&& GameLevelManager.instance.Controls.Player.run.ReadValue<float>() == 1
        //    && !hasBasketball
        //    && canBlock
        //    && MatchRuntime.Rules.EnemiesEnabled
        //    && PlayerHealth.Block > 0)
        //{
        //    if (playerCanBlock)
        //    {
        //        PlayerBlock();
        //    }
        //    if (!playerCanBlock)
        //    {
        //        jumpTrigger = true;
        //    }
        //}
        //else
        //{
        //    // double check touch input not being used
        //    if (!TouchInputController.instance.HoldDetected)
        //    {
        //        anim.SetBool("block", false);
        //    }
        //}

        ////------------------ special -----------------------------------
        //if (GameLevelManager.instance.Controls.Player.special.triggered
        //    && !InAir
        //    && Grounded
        //    && !KnockedDown
        //    && MatchRuntime.Rules.EnemiesEnabled)
        //{
        //    PlayerSpecial();
        //}

        // if player is falling, nto sure what this is useful for. comment out
        //if (rigidBody.velocity.y > 0)
        //{
        //    //updates "highest point" as long at player still moving upwards ( velcoity > 0)
        //    finalHeight = transform.position.y;
        //    //Debug.Log("intialHeight : " + initialHeight);  
        //    //Debug.Log("finalHeight : " + finalHeight);
        //}
    }

    //public void TouchControlJumpOrShoot(Vector2 touchPosition)
    //{
    //    if (Grounded
    //        && !KnockedDown
    //        && hasBasketball
    //        && playerDistanceFromRimFeet > PlayerDunk.instance.DunkRangeFeet
    //        && touchPosition.x > (Screen.width / 2)
    //        && !Locked)
    //    {
    //        jumpTrigger = true;
    //    }
    //    if (PlayerDunk.instance != null
    //        && PlayerDunk.instance.PlayerCanDunk
    //        && playerDistanceFromRimFeet < PlayerDunk.instance.DunkRangeFeet
    //        && (currentState != inAirDunkState || currentState != inAirDunkState)
    //        && !InAir
    //        && Grounded
    //        && hasBasketball
    //        && touchPosition.x > (Screen.width / 2)
    //        && !Locked)
    //    {
    //        dunkTrigger = true;
    //    }
    //    // if has ball, is in air, and pressed shoot button.
    //    // shoot ball
    //    if (InAir
    //        && hasBasketball
    //        && touchPosition.x > (Screen.width / 2)
    //        && (currentState != inAirDunkState || currentState != inAirDunkState))
    //    {
    //        CallBallToPlayer.instance.Locked = true;
    //        basketball.BasketBallState.Locked = true;
    //        CheckIsPlayerFacingGoal(); // turns player facing rim
    //        Shotmeter.MeterEnded = true;
    //        PlayerShoot();
    //    }
    //    // call ball
    //    if (!hasBasketball
    //        && !InAir
    //        && basketball.BasketBallState.CanPullBall
    //        && !basketball.BasketBallState.Locked
    //        && Grounded
    //        && !CallBallToPlayer.instance.Locked
    //        && touchPosition.x > (Screen.width / 2))
    //    {
    //        CallBallToPlayer.instance.Locked = true;
    //        CallBallToPlayer.instance.pullBallToPlayer();
    //        CallBallToPlayer.instance.Locked = false;
    //    }
    //}
    //public void PlayerAttack()
    //{
    //    if (playerCanAttack)
    //    {
    //        // get random close attack if more than one
    //        playerSwapAttack.setCloseAttack();
    //        anim.Play("attack");
    //    }
    //}

    //public void PlayerBlock()
    //{
    //    anim.SetBool("block", true);
    //}

    //public void PlayerShoot()
    //{
    //    basketball.shootBasketBall();
    //}

    //public void PlayerSpecial()
    //{
    //    PlayAnim("special");
    //}
    //public void CheckIsPlayerFacingGoal()
    //{
    //    if (bballRelativePositioning > 0 && !FacingRight
    //        && currentState != specialState
    //        && currentState != attackState)
    //    {
    //        Flip();
    //    }

    //    if (bballRelativePositioning < 0f && FacingRight
    //        && currentState != specialState
    //        && currentState != attackState)
    //    {
    //        Flip();
    //    }
    //}

    public void PlayerJump()
    {
        rigidBody.linearVelocity = Vector3.up * vehicleProfile.JumpForce; //+ (Vector3.forward * rigidBody.velocity.x)) 
        //jumpStartTime = Time.time;

        //Shotmeter.MeterStarted = true;
        //Shotmeter.MeterStartTime = Time.time;
        //// if not dunking, start shot meter
        //if (currentState != inAirDunkState)
        //{
        //    Shotmeter.MeterStarted = true;
        //    Shotmeter.MeterStartTime = Time.time;
        //}
    }

    //-----------------------------------Walk function -----------------------------------------------------------------------
    //void isWalking(Vector3 movement)
    void IsWalking(float horizontal, float vertical)
    {
        // if moving
        //if (horizontal > 0f || horizontal < 0f || vertical > 0f || vertical < 0f)
        if (horizontal != 0 || vertical != 0f)
        {
            // not in air
            if (!InAir) // dont want walking animation playing while inAir
            {
                anim.SetBool("walking", true);
                // walking but running toggle is ON
                if (runningToggle)
                {
                    anim.SetBool("moonwalking", true);
                }
            }
        }
        // not moving
        else
        {
            anim.SetBool("walking", false);
            anim.SetBool("moonwalking", false);
            //moonwalkAudio.enabled = false;
            //running = false;
        }

        // player moving right, not facing right
        if (horizontal > 0 && !FacingRight)//&& canMove)
        {
            Flip();
        }
        // player moving left, and facing right
        if (horizontal < 0f && FacingRight)//&& canMove)
        {
            Flip();
        }
    }

    void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;

        if (MatchRuntime.Rules.EnemiesEnabled || MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.SniperEnabled)
        {
            Vector3 damageScale = damageDisplayObject.transform.localScale;
            damageScale.x *= -1;
            damageDisplayObject.transform.localScale = damageScale;
        }
    }

    // ------------------------------- take damage -------------------------------------------------------
    public IEnumerator PlayerTakeDamage()
    {
        //Debug.Log("PlayerTakeDamage");
        rigidBody.constraints =
        RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        anim.SetBool("takeDamage", true);
        anim.Play("takeDamage");

        float startTime = Time.time;
        float endTime = startTime + _takeDamageTime;
        yield return new WaitUntil(() => Time.time > endTime);
        anim.SetBool("takeDamage", false);
        yield return new WaitUntil(() => currentState != takeDamageState);

        TakeDamage = false;
        KnockedDown = false;
        Locked = false;

        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public IEnumerator PlayerFreezeForXSeconds(float time)
    {
        Debug.Log("freeze player");
        rigidBody.constraints =
        RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        anim.SetBool("takeDamage", true);
        anim.Play("takeDamage");

        float startTime = Time.time;
        float endTime = startTime + time;
        yield return new WaitUntil(() => Time.time > endTime);
        anim.SetBool("takeDamage", false);
        yield return new WaitUntil(() => currentState != takeDamageState);

        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public IEnumerator PlayerKnockedDown()
    {
        //Debug.Log("PlayerKnockedDown");
        rigidBody.constraints =
        RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        anim.SetBool("knockedDown", true);
        anim.Play("knockedDown");
        //yield return new WaitUntil(() => currentState == knockedDownState); // anim started

        float startTime = Time.time;
        float endTime = startTime + _knockDownTime;
        yield return new WaitUntil(() => Time.time > endTime);
        anim.SetBool("knockedDown", false);
        yield return new WaitUntil(() => currentState != knockedDownState);

        KnockedDown = false;
        TakeDamage = false;
        Locked = false;

        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    public void PlayerAvoidKnockedDown()
    {
        anim.Play("knockedDown");
        AvoidedKnockDown = false;
        Locked = false;
    }

    //------------------------- set animator parameters -----------------------
    public void SetPlayerAnim(string animationName, bool isTrue)
    {
        anim.SetBool(animationName, isTrue);
    }

    //------------------------- set animator parameters -----------------------
    public void SetPlayerAnimTrigger(string animationName)
    {
        anim.SetTrigger(animationName);
    }

    //-------------------play animation function ------------------------------
    // provide access to what should be private animator
    public void PlayAnim(string animationName)
    {
        anim.Play(animationName);
    }
    // ----------------------- freeze player postion ------------------------
    public void FreezePlayerPosition()
    {
        //Debug.Log("FreezePlayerPosition");
        //rigidBody.velocity = Vector3.zero;
        rigidBody.constraints = RigidbodyConstraints.FreezeRotationX
        | RigidbodyConstraints.FreezeRotationY
        | RigidbodyConstraints.FreezeRotationZ
        | RigidbodyConstraints.FreezePositionX
        | RigidbodyConstraints.FreezePositionY
        | RigidbodyConstraints.FreezePositionZ;
    }

    public void UnFreezePlayerPosition()
    {
        //Debug.Log("UnFreezePlayerPosition");
        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    // #todo find all these messageDisplay coroutines and move to seprate generic class MessageLog od something
    public void ToggleRun()
    {
        runningToggle = !runningToggle;
        Text messageText = SceneObjects.Find<Text>("messageDisplay", this);
        if (messageText == null)
        {
            return;
        }

        messageText.text = "running toggle = " + runningToggle;

        // turn off text display after 5 seconds
        StartCoroutine(BasketBall.instance.turnOffMessageLogDisplayAfterSeconds(3));
    }

    public bool IsSpecialState()
    {
        return currentState == specialState;
    }

    public bool Grounded
    {
        get { return _grounded; }
        set { _grounded = value; }
    }

    public bool InAir
    {
        get { return _inAir; }
        set { _inAir = value; }
    }

    public bool Locked
    {
        get { return _locked; }
        set { _locked = value; }
    }

    //public float RigidBodyYVelocity
    //{
    //    get { return rigidBody.velocity.y; }
    //}
    public bool FacingFront
    {
        get => _facingFront;
        set => _facingFront = value;
    }
    //public ShotMeter Shotmeter
    //{
    //    get => shotmeter;
    //    set => shotmeter = value;
    //}

    public bool KnockedDown
    {
        get => _knockedDown;
        set => _knockedDown = value;
    }
    public bool AvoidedKnockDown
    {
        get => _avoidedKnockDown;
        set => _avoidedKnockDown = value;
    }

    public Rigidbody RigidBody { get => rigidBody; set => rigidBody = value; }
    //public float MovementSpeed { get => movementSpeed; set => movementSpeed = value; }
    public bool TakeDamage { get => _takeDamage; set => _takeDamage = value; }
    public int CurrentState { get => currentState; set => currentState = value; }
    public int AttackState { get => attackState; set => attackState = value; }
    public int BlockState { get => blockState; set => blockState = value; }
    public int SpecialState { get => specialState; set => specialState = value; }
    public bool FacingRight { get => _facingRight; set => _facingRight = value; }
    public bool CanAttack { get => canAttack; set => canAttack = value; }
    //public bool PlayerCanBlock { get => playerCanBlock; set => playerCanBlock = value; }
    public bool CanBlock { get => canBlock; set => canBlock = value; }
    public Animator Anim { get => anim; set => anim = value; }
    //public AudioSource Audiosource { get => audiosource; set => audiosource = value; }
    public Text DamageDisplayValueText { get => damageDisplayValueText; set => damageDisplayValueText = value; }
    public bool IsGrinding { get => _isGrinding; set => _isGrinding = value; }
    //public float PlayerDistanceFromRim { get => playerDistanceFromRim; set => playerDistanceFromRim = value; }
    //public PlayerHealth PlayerHealth { get => playerHealth; set => playerHealth = value; }
}
