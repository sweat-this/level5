using Assets.Scripts.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using Touch = UnityEngine.Touch;
using Level5.Core.Match;

public class AutoPlayerController : MonoBehaviour
{
    // components 
    [SerializeField]
    private Animator anim;
    private AnimatorStateInfo currentStateInfo;
    private GameObject dropShadow;
    private Rigidbody rigidBody;
    private CharacterProfile characterProfile;
    private ShotMeter shotmeter;
    private PlayerSwapAttack playerSwapAttack;
    private PlayerHealth playerHealth;
    private BasketBallAuto basketball;
    private GameStats gameStats;
    private PlayerIdentifier playerIdentifier;
    CallBallToPlayer callBallToPlayer;

    // PlayerIdentifier.isCpu/isDefensivePlayer (sibling component, same GameObject) is the source
    // of truth for identity/role (AUD-013). These fields are independently set alongside it and
    // can drift - new code should read from PlayerIdentifier rather than adding more readers here.
    public bool isCPU;
    public bool isDefensivePlayer;
    // walk speed #review can potentially remove
    [SerializeField]
    private float movementSpeed;
    [SerializeField]
    private float inAirSpeed; // leave serialized
    [SerializeField]
    private float blockSpeed; 
    // leave serialized
    [SerializeField]
    private float attackSpeed; // leave serialized

    // get/set for following at bottom of class
    [SerializeField]
    private bool _facingRight;
    [SerializeField]
    private bool _facingFront;
    private bool _locked;
    [SerializeField]
    private bool _inAir;
    [SerializeField]
    private bool _grounded;
    private bool _knockedDown;
    private bool _takeDamage;
    private bool _avoidedKnockDown;

    // player state bools
    //private bool running = false;
    public bool hasBasketball;

    // trigger player jump. bool used because activated in fixed update
    // to ensure animaion is synced with camera. camera is updated in fixed update 
    // as well
    private bool jumpTrigger = false;
    float bballRelativePositioning; // which side of the player the ball is on
    [SerializeField]
    float playerDistanceFromRim; // player distance from rim
    [SerializeField]
    float playerDistanceFromRimFeet; // player distance from rim
    Vector3 playerRelativePositioning;
    Vector3 bballRimVector;

    // customizable options
    [SerializeField]
    private bool playerCanBlock;
    [SerializeField]
    private bool playerCanAttack;
    [SerializeField]
    float _knockDownTime;
    [SerializeField]
    float _takeDamageTime;

    // movement variables
    [SerializeField]
    Vector3 movement;
    float movementHorizontal;
    float movementVertical;
    [SerializeField]
    float distanceToTarget;

    // player take damage display
    Text damageDisplayValueText;
    GameObject damageDisplayObject;
    const string damageDisplayValueName = "player_damage_display_text";

    // control movement speed based on state
    // * NOTE these can be put in a constants file probably unless custom animator
    // need to move these to function to load on start

    public int currentState;
    public int idleState;
    public int walkState;
    public int run;
    public int bWalk;
    public int bIdle;
    public int knockedDownState;
    public int takeDamageState;
    public int specialState;
    public int attackState;
    public int blockState;
    public int inAirDunkState;
    public int inAirHasBasketballFrontState;
    public int inAirHasBasketballSideState;
    public int inAirShootState;
    public int inAirShootFrontState;
    public int jumpState;
    public int inAirHasBasketball;
    public int disintegratedState;
    public bool arrivedAtTarget = false;
    public bool stateWalk = false;
    public bool stateIdle = false;
    public bool stateKnockDown = false;
    [SerializeField]
    private Vector3 targetPosition;
    GameObject basketballRim;
    //player sprite object
    GameObject spriteObject;

    //[SerializeField]
    //private float relativePositionToGoal;

    public float walkMovementSpeed;
    public float runMovementSpeed;
    public float attackMovementSpeed;

    public bool shootTrigger;
    private float terrainYHeight;

    void Start()
    {
        getAnimatorStateHashes();
        playerIdentifier = GetComponent<PlayerIdentifier>();
        basketball = playerIdentifier.isDefensivePlayer ? null : playerIdentifier.autoBasketball.GetComponent<BasketBallAuto>();
        gameStats = playerIdentifier.isDefensivePlayer ? null : playerIdentifier.autoBasketball.GetComponent<GameStats>();
        // (the two hash re-assignments that used to sit here are gone - getAnimatorStateHashes
        // above now produces the prefixed values directly. AUD-051)
        callBallToPlayer = GetComponent<CallBallToPlayer>();
        anim = playerIdentifier.autoPlayer.GetComponentInChildren<Animator>();
        characterProfile = GetComponent<CharacterProfile>();
        rigidBody = GetComponent<Rigidbody>();
        Shotmeter = GetComponentInChildren<ShotMeter>();
        PlayerHealth = GetComponentInChildren<PlayerHealth>();
        spriteObject = transform.GetComponentInChildren<SpriteRenderer>().gameObject;
        // bball rim vector, used for relative positioning
        bballRimVector = GameLevelManager.instance.BasketballRimVector;

        dropShadow = transform.root.transform.Find("drop_shadow").gameObject;
        FacingRight = true;

        movementSpeed = characterProfile.Speed;

        if (_knockDownTime == 0) { _knockDownTime = 1.5f; }
        if (_takeDamageTime == 0) { _takeDamageTime = 0.5f; }
        if (blockSpeed == 0) { blockSpeed = 0.2f; }
        //if (attackSpeed == 0) { attackSpeed = 0f; }

        // AUD-053: the same guards PlayerController carries. This twin was left unguarded.
        damageDisplayObject = SceneObjects.Find(damageDisplayValueName, this);
        damageDisplayValueText = damageDisplayObject != null
            ? damageDisplayObject.GetComponent<Text>()
            : null;

        //GameOptions.sniperEnabled = true; // test flag;
        if (MatchRuntime.Rules.EnemiesEnabled || MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.SniperEnabled)
        {
            playerSwapAttack = GetComponent<PlayerSwapAttack>();
            if (damageDisplayObject != null && damageDisplayObject.GetComponent<Canvas>() != null)
            {
                damageDisplayObject.GetComponent<Canvas>().worldCamera = Camera.main;
            }
        }
        else if (damageDisplayObject != null)
        {
            damageDisplayObject.SetActive(false);
        }
        if (MatchRuntime.CustomCamera)
        {
            spriteObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            if (damageDisplayObject != null)
            {
                damageDisplayObject.transform.rotation = Quaternion.Euler(0, 0, 0);
            }
        }
        // custom knockdown time for sniper mode
        if (MatchRuntime.Rules.SniperEnabled)
        {
            _knockDownTime = 0.75f;
        }

        _facingRight = true;
        movementSpeed = runMovementSpeed;
        //rigidBody = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();

        basketballRim = GameObject.Find("rim");
    }

    // not affected by framerate
    void FixedUpdate()
    {
        // AUD-050: this block used to re-derive movement from itself before moveToPosition had a
        // chance to set it - reading `movement.y` (the height component) as the depth component,
        // then writing it back as z and rescaling the whole vector by speed*dt a second time.
        // moveToPosition is the only thing that should author `movement`; this just clears it when
        // no step is taken, so IsWalking sees a genuine "not moving" instead of a decaying echo of
        // the last step.
        bool steppedThisFrame = false;

        if (Grounded
            && !InAir
            && !(currentState == inAirHasBasketballFrontState || currentState == inAirHasBasketballSideState)
            && currentState != knockedDownState
            && currentState != disintegratedState
            && !playerIdentifier.isDefensivePlayer
            && !arrivedAtTarget)
        {
            if (distanceToTarget > 0.05f)
            {
                moveToPosition(targetPosition);
                steppedThisFrame = true;
            }
            if (distanceToTarget <= 0.05f)
            {
                arrivedAtTarget = true;
            }
        }

        if (!steppedThisFrame)
        {
            movement = Vector3.zero;
        }

        movementHorizontal = movement.x;
        movementVertical = movement.z;

        if (currentState != specialState)
        {
            IsWalking(movementHorizontal, movementVertical);
        }
        if (jumpTrigger) 
        {
            jumpTrigger = false;
            AutoPlayerJump();
        }

        // call ball
        if (!hasBasketball
            && !playerIdentifier.isDefensivePlayer
            && !InAir
            && basketball.BasketBallState.CanPullBall
            && !basketball.BasketBallState.Locked
            && !basketball.BasketBallState.Thrown
            && Grounded
            && callBallToPlayer.CallEnabled
            && !callBallToPlayer.Locked
            && arrivedAtTarget)
        {
            callBallToPlayer.Locked = true;
            StartCoroutine(CallBall());
        }
    }


    // Update :: once once per frame
    void Update()
    {
        // current used to determine movement speed based on animator state. walk, knockedown, moonwalk, idle, attacking, etc
        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;

        // ================== auto player facing goal ==========================
        //relativePositionToGoal = GameLevelManager.instance.BasketballRimVector.x + transform.position.x;
        if (!arrivedAtTarget && !playerIdentifier.isDefensivePlayer)
        {
            targetPosition = getClosestPositionMarker();
            //targetPosition = positionMarkers[closestPositionMarkerIndex].transform.position;
        }
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
            dropShadow.transform.position = new Vector3(transform.root.position.x, transform.root.position.y+0.01f,
            transform.root.position.z);
        }
        if (!Grounded) // player in air
        {
            // AUD-052: same guard PlayerController already carries. A scene with no active Terrain
            // otherwise NREs here every frame the CPU is airborne.
            terrainYHeight = Terrain.activeTerrain != null
                ? Terrain.activeTerrain.SampleHeight(transform.position) + 0.02f
                : GameLevelManager.instance.TerrainHeight + 0.02f;
            dropShadow.transform.position = new Vector3(transform.root.position.x, terrainYHeight,
            transform.root.position.z);
        }

        bballRelativePositioning = bballRimVector.x - rigidBody.position.x;
        playerRelativePositioning = rigidBody.position - bballRimVector;
        playerDistanceFromRim = Vector3.Distance(transform.position, new Vector3(bballRimVector.x, transform.position.y, bballRimVector.z));
        playerDistanceFromRimFeet = playerDistanceFromRim * 6;
        distanceToTarget = Vector3.Distance(transform.position, targetPosition);

        // determine if player animation is shooting from or facing basket
        if (Math.Abs(playerRelativePositioning.x) > 2 &&
            Math.Abs(playerRelativePositioning.z) < 2)
        {
            FacingFront = false;
        }
        else
        {
            FacingFront = true;
        }

        // set player shoot anim based on position
        if (FacingFront) // facing straight toward bball goal
        {
            SetPlayerAnim("basketballFacingFront", true);
        }
        else // side of goal, relative postion
        {
            SetPlayerAnim("basketballFacingFront", false);
        }

        // ----- control speed based on commands----------
        // idle, walk, walk with ball state
        // AUD-049: parenthesised - `&&` binds tighter than `||`, so the guard applied only to bIdle
        if ((currentState == idleState || currentState == walkState || currentState == bIdle)
            && !InAir
            && !KnockedDown)
        {
            movementSpeed = characterProfile.Speed;
        }
        // if run state
        if (currentState == run && !hasBasketball) 
        {
            movementSpeed = characterProfile.RunSpeed; ;
        }
        // if run state has ball
        if (currentState == bWalk && hasBasketball)
        {
            movementSpeed = characterProfile.RunSpeedHasBall; ;
        }
        // if block state
        if (currentState == attackState || currentState == blockState)
        {
            movementSpeed = blockSpeed;
        }
        // inair state
        if (InAir)//&& currentState != inAirDunkState)
        {
            CheckIsPlayerFacingGoal();
            if (currentState != inAirDunkState)
            {
                movementSpeed = inAirSpeed;
            }
        }
        // -------------- states
        if (!arrivedAtTarget &&/*stateWalk && */distanceToTarget <= 0.05f /*&& !arrivedAtTarget*/ && Grounded)
        {
            //Debug.Log("arrivedAtTarget");
            arrivedAtTarget = true;
            stateWalk = false;
            stateIdle = true;
            //positionMarkerCounter++;
            rigidBody.linearVelocity = Vector3.zero;
        }
        if (!stateWalk && distanceToTarget >= 0.05f && !arrivedAtTarget && Grounded)
        {
            stateWalk = true;
            stateIdle = false;
        }
        //------------------ jump -----------------------------------
        if (hasBasketball
            //&& stateIdle
            && arrivedAtTarget
            && Grounded
            && !KnockedDown
            && !jumpTrigger
            && !shootTrigger
            && !InAir
            && !Locked)
        {
            Locked = true;
            arrivedAtTarget = false;
            StartCoroutine(SetJumptrigger());
        }

        //------------------ shoot -----------------------------------
        // if has ball, is in air, and pressed shoot button.
        // note -- At top of the jump
        if (InAir
            && hasBasketball
            && !MatchRuntime.Rules.EnemiesOnly
            && rigidBody.linearVelocity.y <= 0
            && (currentState == inAirHasBasketballFrontState || currentState == inAirHasBasketballSideState)
            && !shootTrigger)
        {
            shootTrigger = true;
            callBallToPlayer.Locked = true;
            basketball.BasketBallState.Locked = true;
            CheckIsPlayerFacingGoal(); // turns player facing rim
            Shotmeter.MeterEnded = true; // this determines ball launch. find top of the jump
            PlayerShoot();
        }
    }

    private Vector3 getClosestPositionMarker()
    {
        // * note factor in range and other variables for shot type
        // also clutch
        // and time based
        // if( < 1 minute. clutch increases accuracy)

        float distance3 =( Constants.DISTANCE_3point - playerDistanceFromRim) + 0.5f;
        float distance4 = (Constants.DISTANCE_4point - playerDistanceFromRim) + 0.5f;
        float distance7 = (Constants.DISTANCE_7point - playerDistanceFromRim) + 0.5f;
        Vector3 finalDirection = new();
        Vector3 targetPosition = new();
        Vector3 directionOfTravelSeven = new();

        Vector3 directionOfTravel = transform.position - GameLevelManager.instance.BasketballRimVector;

        // set direction of travel to 7pt line based on which side of goal player is on
        if(playerRelativePositioning.x > 0) { directionOfTravelSeven = Vector3.right; }
        else { directionOfTravelSeven = Vector3.left; }
        // conditions for type of shot
        if (characterProfile.Accuracy3Pt > characterProfile.Accuracy4Pt
             && (GameLevelManager.instance.currentHighScoreTotalPoints - gameStats.TotalPoints) <= 12)
        {
            finalDirection = directionOfTravel + directionOfTravel.normalized * distance3;
            targetPosition = GameLevelManager.instance.BasketballRimVector + finalDirection;
        }
        if (characterProfile.Accuracy3Pt <= characterProfile.Accuracy4Pt
            || (GameLevelManager.instance.currentHighScoreTotalPoints - gameStats.TotalPoints) >= 16)
        {
            finalDirection = directionOfTravel + directionOfTravel.normalized * distance4;
            targetPosition = GameLevelManager.instance.BasketballRimVector + finalDirection;
        }
        if (((characterProfile.Accuracy7Pt >= characterProfile.Accuracy4Pt
            && characterProfile.Accuracy7Pt >= characterProfile.Accuracy3Pt)
            || (GameLevelManager.instance.currentHighScoreTotalPoints - gameStats.TotalPoints) >= 21)
            && cpuShootSevenpointers())
        {
            finalDirection = directionOfTravelSeven + directionOfTravelSeven.normalized * distance7;
            targetPosition = transform.position + finalDirection;
        }
        if (targetPosition == Vector3.zero)
        {
            finalDirection = directionOfTravel + directionOfTravel.normalized * distance4;
            targetPosition = GameLevelManager.instance.BasketballRimVector + finalDirection;
        }
        if (targetPosition.z < GameLevelManager.instance.BasketballRimVector.z + 3)
        {
            targetPosition = new Vector3(targetPosition.x, targetPosition.y, GameLevelManager.instance.BasketballRimVector.z);
        }

        //Debug.Log("finalDirection : " + finalDirection);
        //Debug.Log("directionOfTravel : " + finalDirection);
        //Debug.Log("GameLevelManager.instance.BasketballRimVector : " + GameLevelManager.instance.BasketballRimVector);
        //Debug.Log("targetPosition : " + targetPosition);
        //Debug.Log("behind rim : " + ( targetPosition.z < GameLevelManager.instance.BasketballRimVector.z));

        return targetPosition;
    }
    private bool cpuShootSevenpointers(){
        bool returnValue = false;
        // AUD-055: an unset Accuracy7Pt made this Infinity, which passed the > 70 test below and
        // turned every CPU into a seven-point specialist. A character with no seven-point accuracy
        // should never take the shot.
        if (characterProfile.Accuracy7Pt <= 0)
        {
            return false;
        }

        float rangePercent = ((float)characterProfile.Range / characterProfile.Accuracy7Pt) * 100;
        //Debug.Log("name : " + characterProfile.PlayerDisplayName);
        //Debug.Log("rangePercent : " + rangePercent);
        //Debug.Log("characterProfile.Accuracy7Pt : " + characterProfile.Accuracy7Pt);

        if (MatchRuntime.LevelHasSevenPointers
            && (rangePercent > 70)) 
        { 
            returnValue = true;
        }
        else {
                //Random random = new Random();
                //float percent = random.Next(1, 100);

                //if (percent <= maxPercent)
                //{
                //    return true;
                //}
                //return false;
            returnValue = false; 
        }

        return returnValue;
    }

    private void getAnimatorStateHashes()
    {
        idleState = Animator.StringToHash("base.idle");
        walkState = Animator.StringToHash("base.movement.walk");
        run = Animator.StringToHash("base.movement.run");
        bWalk = Animator.StringToHash("base.movement.basketball_dribbling");
        bIdle = Animator.StringToHash("base.movement.basketball_idle");
        knockedDownState = Animator.StringToHash("base.knockedDown");
        takeDamageState = Animator.StringToHash("base.takeDamage");
        specialState = Animator.StringToHash("base.special");
        attackState = Animator.StringToHash("base.attack.attack");
        blockState = Animator.StringToHash("base.attack.block");
        inAirDunkState = Animator.StringToHash("base.inair.inair_dunk");
        // AUD-051: "base." prefix, matching every other hash here. Start used to re-assign these
        // two immediately afterwards with the prefixed strings, which is what kept this class
        // working while the same helper in PlayerController and AutoPlayerDefense stayed wrong.
        inAirHasBasketballFrontState = Animator.StringToHash("base.inair.inair_hasBasketball_front");
        inAirHasBasketballSideState = Animator.StringToHash("base.inair.inair_hasBasketball_side");
        inAirShootState = Animator.StringToHash("base.inair.basketball_shoot");
        inAirShootFrontState = Animator.StringToHash("base.inair.basketball_shoot_front");
        jumpState = Animator.StringToHash("base.inair.jump");
        inAirHasBasketball = Animator.StringToHash("base.inair.inair_hasBasketball");
        disintegratedState = Animator.StringToHash("base.disintegrated");
    }

    IEnumerator CallBall()
    {
        //Debug.Log("call ball auto");
        callBallToPlayer.pullBallToPlayerAuto(basketball.gameObject);
        yield return new WaitForSeconds(1.5f);
        callBallToPlayer.Locked = false;
    }
    /// <summary>
    /// Steps toward <paramref name="target"/>.
    ///
    /// AUD-050: this used to assign the normalized *direction* back into the `targetPosition`
    /// field it had just been handed, so between this call and the next Update (which recomputes
    /// the marker) the field held a unit vector rather than a world position. FixedUpdate can run
    /// more than once per Update, and the second run then steered toward a point near the origin.
    /// The direction is a local now; the field is only written by getClosestPositionMarker.
    /// </summary>
    public void moveToPosition(Vector3 target)
    {
        Vector3 directionToTarget = (target - transform.position).normalized;
        movement = directionToTarget * (movementSpeed * Time.deltaTime);
        rigidBody.MovePosition(transform.position + movement);
    }

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

    void PlayerShoot()
    {
        basketball.shootBasketBall(basketball.BasketBallState.TwoPoints,
            basketball.BasketBallState.ThreePoints,
            basketball.BasketBallState.FourPoints,
            basketball.BasketBallState.SevenPoints);
        arrivedAtTarget = false;
    }

    //public void PlayerSpecial()
    //{
    //    PlayAnim("special");
    //}
    public void CheckIsPlayerFacingGoal()
    {
        if (bballRelativePositioning > 0 && !FacingRight
            && currentState != specialState
            && currentState != attackState)
        {
            Flip();
        }

        if (bballRelativePositioning < 0f && FacingRight
            && currentState != specialState
            && currentState != attackState)
        {
            Flip();
        }
    }

    public void PlayerJump()
    {
        rigidBody.linearVelocity = Vector3.up * characterProfile.JumpForce; //+ (Vector3.forward * rigidBody.velocity.x)) 
        //jumpStartTime = Time.time;

        Shotmeter.MeterStarted = true;
        Shotmeter.MeterStartTime = Time.time;
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
                //if (runningToggle)
                //{
                //    anim.SetBool("moonwalking", true);
                //}
            }
        }
        // not moving
        else
        {
            anim.SetBool("walking", false);
            anim.SetBool("moonwalking", false);
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

        if (damageDisplayObject != null
            && (MatchRuntime.Rules.EnemiesEnabled || MatchRuntime.Rules.EnemiesOnly || MatchRuntime.Rules.SniperEnabled))
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
    IEnumerator SetJumptrigger()
    {
        float delay = (1 - (float)characterProfile.Release / 100) * CpuBaseStats.MAX_SHOOT_DELAY;
        yield return new WaitForSeconds(delay);
        jumpTrigger = true;
    }
    void AutoPlayerJump()
    {
        rigidBody.linearVelocity = Vector3.up * characterProfile.JumpForce; //+ (Vector3.forward * rigidBody.velocity.x)) 
        //jumpStartTime = Time.time;
        Shotmeter.MeterStarted = true;
        Shotmeter.MeterStartTime = Time.time;
        // if not dunking, start shot meter
        if (currentState != inAirDunkState)
        {
            Shotmeter.MeterStarted = true;
            Shotmeter.MeterStartTime = Time.time;
        }
    }
    // *NOTE most of these can be in a utility class
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
        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
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

    public bool FacingFront
    {
        get => _facingFront;
        set => _facingFront = value;
    }
    public ShotMeter Shotmeter
    {
        get => shotmeter;
        set => shotmeter = value;
    }

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
    public bool TakeDamage { get => _takeDamage; set => _takeDamage = value; }
    public bool FacingRight { get => _facingRight; set => _facingRight = value; }
    public float PlayerDistanceFromRim { get => playerDistanceFromRim; set => playerDistanceFromRim = value; }
    public PlayerHealth PlayerHealth { get => playerHealth; set => playerHealth = value; }
    public CallBallToPlayer CallBallToPlayer { get => callBallToPlayer; set => callBallToPlayer = value; }
    public Rigidbody RigidBody { get => rigidBody; set => rigidBody = value; }
    public CharacterProfile CharacterProfile { get => characterProfile; set => characterProfile = value; }
}
