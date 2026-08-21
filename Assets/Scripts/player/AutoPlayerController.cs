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
using Level5.Core;
using Level5.Core.Match;

public class AutoPlayerController : MonoBehaviour, IShooterActor
{
    // components
    // AUD-002: internal rather than private so AutoPlayerDamageReactions - a same-assembly helper
    // the coroutines below were extracted into - can reach them without a wider public surface.
    [SerializeField]
    internal Animator anim;
    private AnimatorStateInfo currentStateInfo;
    private GameObject dropShadow;
    internal Rigidbody rigidBody;
    private CharacterProfile characterProfile;
    private ShotMeter shotmeter;

    // AUD-002: the damage/knockdown reaction coroutines live here now - see
    // AutoPlayerDamageReactions. A plain object, not a component: it runs under this
    // MonoBehaviour's own StartCoroutine exactly as before, so no prefab, lifecycle, or
    // GetComponent wiring changes.
    private readonly AutoPlayerDamageReactions damageReactions;

    public AutoPlayerController()
    {
        damageReactions = new AutoPlayerDamageReactions(this);
    }
    private PlayerSwapAttack playerSwapAttack;
    private PlayerHealth playerHealth;
    private BasketBallAuto basketball;
    private GameStats gameStats;
    private PlayerIdentifier playerIdentifier;
    CallBallToPlayer callBallToPlayer;

    // CPU-6: `isCPU` and `isDefensivePlayer` used to be duplicated here alongside the sibling
    // PlayerIdentifier that owns them (AUD-013 flagged the drift risk). Removed: `isDefensivePlayer`
    // had no readers at all, and `isCPU` had exactly one writer and no readers, so both were
    // duplicate state that could disagree with PlayerIdentifier and nothing would notice.
    // PlayerIdentifier.isCpu / isDefensivePlayer are the source of truth.
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
    private bool _disintegrated;

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

    // CPU-1/CPU-2: the jump-and-shoot sequence has its own flag now. It used to borrow `Locked`,
    // which is also how the damage/knockdown/disintegrate reactions gate themselves - so a
    // knockdown arriving mid-shot could not pass its own `KnockedDown && !Locked` guard. The
    // knockdown was silently dropped, `KnockedDown` latched true, and because the jump gate
    // requires `!KnockedDown` the CPU stopped shooting until the shot completed. If the shot never
    // completed, that was the rest of the match.
    //
    // `Locked` now means exactly one thing: a damage reaction is running. This flag means: this
    // CPU has committed to a shot and has not released the ball yet.
    [SerializeField]
    private bool shootCycleActive;
    private float shootCycleStartTime;

    // Upper bound on one jump-and-shoot sequence, measured from the moment the CPU commits. The
    // release delay is at most CpuBaseStats.MAX_SHOOT_DELAY, followed by the jump arc and the shot
    // meter, so this is far longer than a healthy cycle. It exists so a CPU that loses the ball
    // between committing and launching recovers by itself rather than standing still - which is
    // what the ambient-NPC collision hack in BehaviorNpcAutonomous used to paper over.
    private const float ShootCycleTimeout = 6f;

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

        // AUD-081: same unguarded-lookup shape AUD-079 fixed in RacingVehicleController and this
        // class's human-controller twin, PlayerController - this used to dereference Find's
        // result directly, so a CPU player root without a "drop_shadow" child threw here and
        // aborted the rest of Start().
        Transform dropShadowTransform = transform.root.transform.Find("drop_shadow");
        if (dropShadowTransform == null)
        {
            Debug.LogError("AutoPlayerController on " + name + " found no 'drop_shadow' child on its root.", this);
        }
        else
        {
            dropShadow = dropShadowTransform.gameObject;
        }

        FacingRight = true;

        // CPU-5: `movementSpeed = characterProfile.Speed;` sat here and was overwritten by
        // `movementSpeed = runMovementSpeed;` further down this same method, so it never survived
        // Start. It was also the only read of characterProfile in Start, and it raced
        // CharacterProfile.Start - which is where a CPU's stats are actually derived - because both
        // are Start on this GameObject and nothing orders them. Update recomputes movementSpeed
        // from the profile every frame regardless, so removing it loses nothing.

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
        // CPU-1: a reaction taking over abandons any shot this CPU had committed to, and a cycle
        // that outlives its bound recovers on its own. Both run before the reaction gates below so
        // the reaction sees a clean shoot state in the same frame.
        if (ShootCycleActive
            && (KnockedDown || TakeDamage || Disintegrated || Time.time - shootCycleStartTime > ShootCycleTimeout))
        {
            EndShootCycle();
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
        if (!KnockedDown && !TakeDamage && !Locked && Disintegrated)
        {
            Locked = true;
            StartCoroutine(PlayerDisintegrated());
        }

        // keep drop shadow on ground at all times
        if (dropShadow != null && Grounded)
        {
            dropShadow.transform.position = new Vector3(transform.root.position.x, transform.root.position.y+0.01f,
            transform.root.position.z);
        }
        if (dropShadow != null && !Grounded) // player in air
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
            && !Locked
            && !ShootCycleActive)
        {
            BeginShootCycle();
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
        // CPU-4: distance7 is gone - the seven point branch below no longer measures from where
        // the CPU currently stands, so it does not need the remaining-distance form the other two
        // use to convert a rim-relative vector into a target.
        Vector3 finalDirection = new();
        Vector3 targetPosition = new();
        Vector3 directionOfTravelSeven = new();

        Vector3 directionOfTravel = transform.position - GameLevelManager.instance.BasketballRimVector;

        // set direction of travel to 7pt line based on which side of goal player is on
        if(playerRelativePositioning.x > 0) { directionOfTravelSeven = Vector3.right; }
        else { directionOfTravelSeven = Vector3.left; }
        // conditions for type of shot
        if (characterProfile.Accuracy3Pt > characterProfile.Accuracy4Pt
             && (GameLevelManager.instance.currentHighScoreTotalPoints - gameStats.Stats.TotalPoints) <= 12)
        {
            finalDirection = directionOfTravel + directionOfTravel.normalized * distance3;
            targetPosition = GameLevelManager.instance.BasketballRimVector + finalDirection;
        }
        if (characterProfile.Accuracy3Pt <= characterProfile.Accuracy4Pt
            || (GameLevelManager.instance.currentHighScoreTotalPoints - gameStats.Stats.TotalPoints) >= 16)
        {
            finalDirection = directionOfTravel + directionOfTravel.normalized * distance4;
            targetPosition = GameLevelManager.instance.BasketballRimVector + finalDirection;
        }
        if (((characterProfile.Accuracy7Pt >= characterProfile.Accuracy4Pt
            && characterProfile.Accuracy7Pt >= characterProfile.Accuracy3Pt)
            || (GameLevelManager.instance.currentHighScoreTotalPoints - gameStats.Stats.TotalPoints) >= 21)
            && cpuShootSevenpointers())
        {
            // CPU-4: this was `transform.position + finalDirection` - the only branch here that
            // anchored its target to the CPU rather than to the rim. Update recomputes the marker
            // every frame while the CPU has not arrived, so the target kept sliding sideways ahead
            // of it and only converged once the CPU was roughly 1.5 units past the seven point
            // line, rather than the 0.5 the other branches aim for.
            //
            // Expressed the same way the three and four point branches are: a point on the CPU's
            // own side of the rim, exactly the shot distance plus the same 0.5 margin away, fixed
            // in world space so the CPU can actually arrive at it.
            finalDirection = directionOfTravelSeven * (Constants.DISTANCE_7point + 0.5f);
            targetPosition = GameLevelManager.instance.BasketballRimVector + finalDirection;
        }
        // Reachable: neither the three nor the four point branch fires when Accuracy3Pt is the
        // higher of the two and the score gap sits between 13 and 15, so this is the genuine
        // default rather than dead code.
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

    /// <summary>
    /// CPU-1: this CPU has committed to a shot - it will jump, then release.
    /// </summary>
    private void BeginShootCycle()
    {
        shootCycleActive = true;
        shootCycleStartTime = Time.time;
    }

    /// <summary>
    /// CPU-2: the shot is over, whether it launched, was interrupted by damage, or timed out.
    ///
    /// <see cref="BasketBallAuto"/> calls this on launch. It used to reach in and write
    /// <c>Locked = false</c> directly, which made the ball the owner of the CPU's state machine:
    /// the CPU could not finish a cycle unless the ball's launch path ran to completion. The ball
    /// still reports the event - it is the only thing that knows the shot is away - but the state
    /// transition happens here, and is safe to call more than once.
    /// </summary>
    public void EndShootCycle()
    {
        shootCycleActive = false;
        shootTrigger = false;
        jumpTrigger = false;
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
        return damageReactions.PlayerTakeDamage(_takeDamageTime);
    }

    public IEnumerator PlayerFreezeForXSeconds(float time)
    {
        return damageReactions.PlayerFreezeForXSeconds(time);
    }

    public IEnumerator PlayerKnockedDown()
    {
        return damageReactions.PlayerKnockedDown(_knockDownTime);
    }

    public IEnumerator PlayerDisintegrated()
    {
        return damageReactions.PlayerDisintegrated();
    }

    public void PlayerAvoidKnockedDown()
    {
        damageReactions.PlayerAvoidKnockedDown();
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
        RigidbodyFreezeHelper.FreezePosition(rigidBody);
    }

    public void UnFreezePlayerPosition()
    {
        RigidbodyFreezeHelper.UnfreezeRotationOnly(rigidBody);
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

    /// <summary>
    /// A damage reaction (take damage / knockdown / disintegrate) is running.
    ///
    /// CPU-1: this used to also mean "a shot is in progress", which is why a knockdown landing
    /// mid-shot could never start its own coroutine. Shot state lives in
    /// <see cref="ShootCycleActive"/> now.
    /// </summary>
    public bool Locked
    {
        get { return _locked; }
        set { _locked = value; }
    }

    /// <summary>This CPU has committed to a shot and has not released the ball yet.</summary>
    public bool ShootCycleActive => shootCycleActive;

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
    public bool Disintegrated { get => _disintegrated; set => _disintegrated = value; }
    public bool FacingRight { get => _facingRight; set => _facingRight = value; }
    public float PlayerDistanceFromRim { get => playerDistanceFromRim; set => playerDistanceFromRim = value; }
    public PlayerHealth PlayerHealth { get => playerHealth; set => playerHealth = value; }
    public CallBallToPlayer CallBallToPlayer { get => callBallToPlayer; set => callBallToPlayer = value; }
    public Rigidbody RigidBody { get => rigidBody; set => rigidBody = value; }
    public CharacterProfile CharacterProfile { get => characterProfile; set => characterProfile = value; }

    // ==================== IShooterActor (player<->basketball cycle-cut slice) ====================
    // Explicit implementation: these exist only for basketball-side code reaching this controller
    // through PlayerIdentifier.Actor, so they stay off the ordinary public surface. FacingFront and
    // Grounded already satisfy the interface implicitly via the properties above, and EndShootCycle()
    // already matches the interface signature exactly - no code needed for any of the three.

    bool IShooterActor.HasBasketball { get => hasBasketball; set => hasBasketball = value; }

    bool IShooterActor.InDunkState => currentState == inAirDunkState;

    float IShooterActor.DistanceFromRim => playerDistanceFromRim;

    private ShooterAttributes? _shooterAttributes;

    // Lazily cached on first access rather than in Start(), so a basketball-side reader (e.g.
    // ShotMeter.Start()) can never race this controller's own Start() - see the cycle-cut plan's
    // execution-order note. Preserves ShooterAttributesMapper's "warn once on a missing profile"
    // behavior, since the underlying CharacterProfile reference itself does not change after Awake.
    ShooterAttributes IShooterActor.ShooterAttributes =>
        _shooterAttributes ??= ShooterAttributesMapper.From(GetComponent<CharacterProfile>());

    // Deliberately not memoized, unlike ShooterAttributes above: this is the CPU clutch-bonus roll
    // stat, read live at roll time (BasketBallAuto.rollForAutoPlayerSliderValue), matching the
    // pre-refactor behavior of reading CharacterProfile.Clutch directly at that same late point -
    // see IShooterActor.Clutch for why folding it into the memoized ShooterAttributes struct would
    // have been a regression. Reads via a fresh GetComponent rather than the characterProfile field
    // so this carries no dependency on this controller's own Start() having already run either.
    int IShooterActor.Clutch => GetComponent<CharacterProfile>()?.Clutch ?? 0;

    float IShooterActor.ShotMeterSliderValue => shotmeter.SliderValueOnButtonPress;

    bool IShooterActor.ShotMeterEnded => shotmeter.MeterEnded;

    void IShooterActor.SetAnimBool(string name, bool value) => SetPlayerAnim(name, value);

    void IShooterActor.SetAnimTrigger(string name) => SetPlayerAnimTrigger(name);

    void IShooterActor.LockCallBallToPlayer(bool locked) => callBallToPlayer.Locked = locked;

    void IShooterActor.DisplayShotMeterMessage(string message) => shotmeter.displaySliderMessageText(message);
}
