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

    // Start is called before the first frame update
    void Start()
    {
        playerIdentifier = GameLevelManager.instance.players[0];
        //cpuCharacterProfile = gameObject.GetComponent<CharacterProfile>();
        rigidBody = GetComponent<Rigidbody>();
        movementSpeed = speed;
        anim = gameObject.GetComponentInChildren<Animator>();
        FacingRight = true;
        dropShadow = transform.Find("drop_shadow").gameObject;
        getAnimatorStateHashes();
#if UNITY_ANDROID || UNITY_IOS
        inAirSpeed = 0;
        //jumpForce *= 0.8f;
        //speed *= 0.8f;
#endif
    }
    void FixedUpdate()
    {
        if (!playerCrossover)
        {
            moveToPosition(moveCpuPlayer());
        }
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
        dropShadow.transform.position = new Vector3(transform.root.position.x, shadowHeight, transform.root.position.z);
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

    public void moveToPosition(Vector3 target)
    {
        Vector3 targetPosition = new();
        targetPosition = (target - transform.position);
        
        movement = targetPosition * (movementSpeed * Time.fixedDeltaTime);
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
        if (playerCrossover)
        {
            yield return new WaitForSeconds(jumpDelay);
            yield return new WaitUntil(() => currentState != knockedDownState);
            playerCrossover = false;

            rigidBody.linearVelocity = Vector3.up * jumpForce;
            yield return new WaitUntil(() => player.playerController.Grounded);
            isLocked = false;
        }
        else
        {
            rigidBody.linearVelocity = Vector3.up * jumpForce;
            yield return new WaitUntil(() => player.playerController.Grounded);
            isLocked = false;
        }
    }
}
