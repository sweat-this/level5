using Assets.Scripts.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.PlayerLoop;

public class AutoPlayerDefense : MonoBehaviour
{
    public PlayerIdentifier playerIdentifier;
    //private CharacterProfile cpuCharacterProfile;

    [SerializeField] Vector3 playerPosition;
    [SerializeField] float playerRelativePositioning;
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private Vector3 movement;
    [SerializeField] private float playerGuardingDistance; //hustle
    [SerializeField] private float speed; // speed
    [SerializeField] private float inAirSpeed; //acceleration
    [SerializeField] private float jumpForce; //jump

    [SerializeField] private float jumpDelay; //awareness
    [SerializeField] private float delayPercent; //awareness
    [SerializeField] private float crossoverPercent; //agility
    [SerializeField] private float stamina; //stamina
    [SerializeField] private float knockDownTime = 1f; //hustle

    private Animator anim;
    private Rigidbody rigidBody;
    public float movementSpeed;
    GameObject dropShadow;

    public CpuBaseStats.DefensiveType defensiveType;

    public int blockedShots;

    private AnimatorStateInfo currentStateInfo;

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
    private float movementHorizontal;
    private float movementVertical;
    private bool FacingRight;
    public float distanceToTarget;
    public float playerDistanceToGoal;

    public bool arrivedAtTarget;
    public bool jumpTrigger;
    public bool grounded;
    public bool inAir;
    public bool isLocked;
    public bool playerCrossover;

    // DEF-2: upper bound on one shot contest, from leaving the ground to releasing isLocked. A
    // jump arc is well under a second; this only ever fires when the guarded player stops
    // reporting Grounded at all.
    private const float MaxContestDuration = 4f;

    // DEF-4: shortest interval between two crossover rolls, so rapid direction changes cannot
    // reroll it every frame.
    private const float CrossoverRollCooldown = 0.5f;
    private float nextCrossoverRollTime;

    // Start is called before the first frame update
    void Start()
    {
        ResolveGuardedPlayerIfMissing();
        //cpuCharacterProfile = gameObject.GetComponent<CharacterProfile>();
        rigidBody = GetComponent<Rigidbody>();
        movementSpeed = speed;
        anim = gameObject.GetComponentInChildren<Animator>();
        FacingRight = true;
        // AUD-081: same unguarded-lookup shape AUD-079 fixed in RacingVehicleController - this
        // used to dereference Find's result directly, so a root without a "drop_shadow" child
        // threw here and aborted the rest of Start().
        Transform dropShadowTransform = transform.Find("drop_shadow");
        if (dropShadowTransform == null)
        {
            Debug.LogError("AutoPlayerDefense on " + name + " found no 'drop_shadow' child.", this);
        }
        else
        {
            dropShadow = dropShadowTransform.gameObject;
        }

        getAnimatorStateHashes();
#if UNITY_ANDROID || UNITY_IOS
        inAirSpeed = 0;
        //jumpForce *= 0.8f;
        //speed *= 0.8f;
#endif
    }
    void FixedUpdate()
    {
        // DEF-5: `!playerCrossover` was the only condition here. The knockdown and disintegrate
        // checks its counterparts carry were absent - currently masked, because PlayerKnockedDown
        // freezes the X and Z constraints so the MovePosition below has no visible effect, but the
        // guard was missing rather than unnecessary, and DEF-1 changed how that step is computed.
        if (playerCrossover
            || currentState == knockedDownState
            || currentState == disintegratedState)
        {
            return;
        }

        moveToPosition(moveCpuPlayer());
    }
    // Update is called once per frame
    void Update()
    {
        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;

        // drop shadow lock to bball transform on the ground
        // AUD-052: guarded like PlayerController - no active Terrain otherwise NREs every frame
        float shadowHeight = Terrain.activeTerrain != null
            ? Terrain.activeTerrain.SampleHeight(transform.position) + 0.02f
            : GameLevelManager.instance.TerrainHeight + 0.02f;
        if (dropShadow != null)
        {
            dropShadow.transform.position = new Vector3(transform.root.position.x, shadowHeight, transform.root.position.z);
        }
        distanceToTarget = Vector3.Distance(transform.position, targetPosition);
        playerDistanceToGoal = Vector3.Distance(playerPosition, GameLevelManager.instance.BasketballRimVector);

        if(distanceToTarget < 0.05)
        {
            arrivedAtTarget = true;
        }
        else
        {
            arrivedAtTarget = false;
        }
        if (!arrivedAtTarget)
        {
            anim.SetBool("walking", true);
        }
        // not moving
        else
        {
            anim.SetBool("walking", false);
            anim.SetBool("moonwalking", false);
        }

        // player moving right, not facing right
        if (playerRelativePositioning > 0 && !FacingRight)//&& canMove)
        {
            Flip();
        }
        // player moving left, and facing right
        if (playerRelativePositioning < 0  && FacingRight)//&& canMove)
        {
            Flip();
        }
        if (playerIdentifier.playerController.currentState == playerIdentifier.playerController.inAirHasBasketball 
            && !inAir
            && !isLocked)
        {
            isLocked = true;
            jumpTrigger = true;
        }      
        if (jumpTrigger)
        {
            jumpTrigger = false;
            StartCoroutine( AutoPlayerJump(playerIdentifier));
        }
        if(inAir) { SetPlayerAnim("jump",true); }
        if(grounded) { SetPlayerAnim("jump",false); }

        playerRelativePositioning = playerIdentifier.player.transform.position.x - transform.position.x;
        playerPosition = playerIdentifier.player.transform.position;

        if (inAir)
        {
            movementSpeed = inAirSpeed;
        }
        else
        {
            movementSpeed = speed;
        }
        //if (!playerCrossover)
        //{
        //    moveToPosition(moveCpuPlayer());
        //}
    }


    public void SetPlayerAnim(string animationName, bool isTrue)
    {
        anim.SetBool(animationName, isTrue);
    }

    /// <summary>
    /// The player this defender guards. DEF-3: explicit and assignable, matching the seam
    /// <see cref="BodyGuardController.AssignProtectedActor"/> and
    /// <see cref="EnemyController.AssignTargetQueue"/> already use.
    /// </summary>
    public PlayerIdentifier GuardedPlayer => playerIdentifier;

    public void AssignGuardedPlayer(PlayerIdentifier player)
    {
        playerIdentifier = player;
    }

    /// <summary>
    /// DEF-3: this was <c>playerIdentifier = GameLevelManager.instance.players[0]</c> inline in
    /// Start - an unguarded index into a global roster, resolved once, with no assignment path.
    /// The transitional fallback still resolves the primary local human, which is the only actor
    /// the lockdown mode's defender has ever guarded; a mode that needs to guard someone else
    /// should call <see cref="AssignGuardedPlayer"/> instead of changing this.
    /// </summary>
    private void ResolveGuardedPlayerIfMissing()
    {
        if (playerIdentifier != null)
        {
            return;
        }

        playerIdentifier = GameLevelManager.instance != null
            ? GameLevelManager.instance.Player1
            : null;

        if (playerIdentifier == null)
        {
            Debug.LogError(
                $"AutoPlayerDefense on {name} could not resolve a player to guard; disabling.",
                this);
            enabled = false;
        }
    }

    Vector3 moveCpuPlayer()
    {
        Vector3 directionOfTravel = (new Vector3(playerPosition.x, 0, playerPosition.z + playerGuardingDistance) - new Vector3(GameLevelManager.instance.BasketballRimVector.x, 0, GameLevelManager.instance.BasketballRimVector.z).normalized);
        //if (playerIdentifier.playerController.InAir)
        //{
        //    //targetPosition = playerIdentifier.basketBallController.BasketBallPosition.transform.position;
        //    targetPosition = playerIdentifier.basketball.transform.position;
        //}
        //else
        //{
        //    targetPosition = LerpByDistance(new Vector3(playerPosition.x, 0, playerPosition.z), new Vector3(GameLevelManager.instance.BasketballRimVector.x, 0, GameLevelManager.instance.BasketballRimVector.z), playerGuardingDistance);
        //}
        //targetPosition = LerpByDistance(new Vector3(playerPosition.x, 0, playerPosition.z), new Vector3(GameLevelManager.instance.BasketballRimVector.x, 0, GameLevelManager.instance.BasketballRimVector.z), playerGuardingDistance);
        targetPosition = LerpByDistance(playerPosition,GameLevelManager.instance.BasketballRimVector, playerGuardingDistance);
        return targetPosition;
    }
    IEnumerator AddDelayToMove(float delay)
    {
        yield return new WaitForSeconds(delay);
        playerCrossover = false;
    }

    public Vector3 LerpByDistance(Vector3 A, Vector3 B, float x)
    {
        Vector3 P = x * Vector3.Normalize(B - A) + A;

        return P;
    }

    /// <summary>
    /// Steps toward <paramref name="target"/> at <see cref="movementSpeed"/> units per second.
    ///
    /// DEF-1: this used to multiply the raw, un-normalized <c>(target - position)</c> vector by
    /// speed and delta time, so the step was proportional to how far away the target was -
    /// <c>speed</c> behaved as a spring constant, not a speed, and a large displacement produced a
    /// single oversized MovePosition step. This is the same defect AUD-050 fixed in
    /// <see cref="AutoPlayerController.moveToPosition"/>, which this now matches: normalize the
    /// direction, and clamp the step to the remaining distance so arriving cannot overshoot.
    ///
    /// This changes how the lockdown defender feels. With the authored guard distance the two
    /// forms agree at roughly one unit of separation; beyond that the old code was faster, inside
    /// it slower. <c>speed</c> is now literally units per second and wants a Play Mode retune.
    /// </summary>
    public void moveToPosition(Vector3 target)
    {
        Vector3 toTarget = target - transform.position;
        float distanceRemaining = toTarget.magnitude;
        if (distanceRemaining <= Mathf.Epsilon)
        {
            movement = Vector3.zero;
            return;
        }

        float step = Mathf.Min(movementSpeed * Time.fixedDeltaTime, distanceRemaining);
        movement = (toTarget / distanceRemaining) * step;
        rigidBody.MovePosition(rigidBody.position + movement);
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
        // AUD-051: "base." prefix, matching every other hash here and the animator's full path
        inAirHasBasketballFrontState = Animator.StringToHash("base.inair.inair_hasBasketball_front");
        inAirHasBasketballSideState = Animator.StringToHash("base.inair.inair_hasBasketball_side");
        inAirShootState = Animator.StringToHash("base.inair.basketball_shoot");
        inAirShootFrontState = Animator.StringToHash("base.inair.basketball_shoot_front");
        jumpState = Animator.StringToHash("base.inair.jump");
        inAirHasBasketball = Animator.StringToHash("base.inair.inair_hasBasketball");
        disintegratedState = Animator.StringToHash("base.disintegrated");
    }

    void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;

        // DEF-4: the chance of being crossed over is rolled here, once per sprite flip. Flips
        // happen whenever the guarded player crosses this defender's x axis, so a player who
        // jitters left and right rerolls it as fast as they can change direction - the probability
        // was effectively per-direction-change rather than per-move. The cooldown below bounds how
        // often it can be rolled without moving the roll itself, which belongs with a dribble move
        // rather than with a facing change and is a larger change than this.
        if (Time.time < nextCrossoverRollTime)
        {
            return;
        }

        nextCrossoverRollTime = Time.time + CrossoverRollCooldown;

        float randomNum = UtilityFunctions.GetRandomFloat(0, 100);
        if (randomNum < crossoverPercent && !playerCrossover && !inAir)
        {
            playerCrossover = true;
            StartCoroutine(PlayerKnockedDown());
        }
        //if (GameOptions.enemiesEnabled || GameOptions.EnemiesOnlyEnabled || GameOptions.sniperEnabled)
        //{
        //    Vector3 damageScale = damageDisplayObject.transform.localScale;
        //    damageScale.x *= -1;
        //    damageDisplayObject.transform.localScale = damageScale;
        //}
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
        float endTime = startTime + knockDownTime;
        yield return new WaitUntil(() => Time.time > endTime);
        anim.SetBool("knockedDown", false);
        yield return new WaitUntil(() => currentState != knockedDownState);

        rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
        playerCrossover = false;
    }
    IEnumerator AutoPlayerJump(PlayerIdentifier player)
    {
        //Debug.Log("AutoPlayerJump");
        float randomNum = UtilityFunctions.GetRandomFloat(0, 100);
        if (randomNum < delayPercent && !playerCrossover)
        {
            playerCrossover = true;
        }
        // DEF-2: the two branches differed only in this pre-jump delay; the jump and the wait that
        // releases isLocked were duplicated below them. Shared now so the release path exists once.
        if (playerCrossover)
        {
            yield return new WaitForSeconds(jumpDelay);
            yield return new WaitUntil(() => currentState != knockedDownState);
            playerCrossover = false;
        }

        rigidBody.linearVelocity = Vector3.up * jumpForce;
        yield return WaitForGuardedPlayerToLand(player);
        isLocked = false;
    }

    /// <summary>
    /// Waits for the guarded player to land, or for the contest to time out.
    ///
    /// DEF-2: this was an unbounded <c>WaitUntil(() =&gt; player.playerController.Grounded)</c> and
    /// was the only path that cleared <see cref="isLocked"/>. A guarded player who never reported
    /// grounded - knocked into a frozen state, disabled, destroyed, or landed somewhere the ground
    /// check misses - left the defender locked for the remainder of the match, silently never
    /// contesting another shot. The deadline is far longer than a jump arc, so a healthy contest
    /// still ends on the landing rather than on the clock.
    /// </summary>
    private IEnumerator WaitForGuardedPlayerToLand(PlayerIdentifier player)
    {
        float deadline = Time.time + MaxContestDuration;
        yield return new WaitUntil(() =>
            player == null
            || player.playerController == null
            || player.playerController.Grounded
            || Time.time >= deadline);
    }
}
