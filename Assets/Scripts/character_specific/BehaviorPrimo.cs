
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class BehaviorPrimo : MonoBehaviour
{
    AudioSource runAudio;

    public float walkMovementSpeed;
    public float runMovementSpeed;
    public bool facingRight, walking;
    public bool canMove;

    public GameObject pos1, pos2, pos3;

    float distanceFromStartPos;
    bool locked;
    //GameObject player;

    private float movementSpeed;
    private Rigidbody rigidBody;
    private NavMeshAgent navmeshAgent;
    public SpriteRenderer currentSprite;

    //public GameObject playerHitbox;
    Animator anim;
    AnimatorStateInfo currentStateInfo;

    // per-instance: this NPC's own animator state, not a value shared by every instance
    int currentState;

    // the hashes are genuinely shared constants
    static readonly int idleState = Animator.StringToHash("base.idle");
    static readonly int idleState2 = Animator.StringToHash("base.idle2");
    static readonly int walkState = Animator.StringToHash("base.walk");
    static readonly int runState = Animator.StringToHash("base.run");

    Vector3 playerRelativePosition;
    //bool waiting;

    public bool ignoreCollision;
    public bool outsideRange;
    public bool insideRange;
    public bool movingToTarget;

    public float maxDistance;
    private bool reachedDestination;
    private bool isSleeping;
    private bool followPlayer;

    // Use this for initialization
    void Start()
    {
        //player = GameLevelManager.Instance.Player;
        facingRight = true;
        canMove = true;
        followPlayer = false;
        movementSpeed = walkMovementSpeed;
        currentSprite = transform.Find("sprite").GetComponent<SpriteRenderer>();
        rigidBody = GetComponent<Rigidbody>();
        navmeshAgent = GetComponent<NavMeshAgent>();
        anim = transform.Find("sprite").GetComponent<Animator>();
        locked = false;
    }

    void Update()
    {
        distanceFromStartPos = Vector3.Distance(transform.position, pos1.transform.position);
        //Debug.Log("distanceFromStartPos : " + ( distanceFromStartPos > maxDistance));

        if (distanceFromStartPos >= maxDistance)
        {
            outsideRange = true;
            insideRange = false;
        }

        if (distanceFromStartPos < maxDistance && movingToTarget)
        {
            outsideRange = false;
            insideRange = true;
            //Debug.Log("if (distanceFromStartPos <= maxDistance)");
        }

        // navmesh has no target and inside range
        if (pathComplete() && !outsideRange && !reachedDestination)
        {
            //Debug.Log("       if (pathComplete() && !outsideRange )");
            reachedDestination = true;
            movingToTarget = false;
            ignoreCollision = false;
        }
        // arrived and not sleeping
        // NPC-2: `!locked` was missing here, and it is the only thing that closes the window.
        // PrimoSleepInRandomXSeconds waits 7-20 seconds before setting isSleeping, so the guard
        // could not become false on its own - Update started another coroutine on every frame in
        // between. Each of those then set the sleep bool and cleared `locked`, so `locked` was
        // repeatedly reopened for the whole sleep, defeating the lock the retreat path also reads.
        // The sibling guard below already gates on `locked` the same way.
        if (reachedDestination && !isSleeping && !locked)
        {
            locked = true;
            StartCoroutine(PrimoSleepInRandomXSeconds());
        }

        // if outside area
        if (outsideRange && !movingToTarget && !locked && followPlayer)
        {
            locked = true;
            ignoreCollision = true;
            movingToTarget = true;

            StartCoroutine(waitOutsideRangeForXSeconds(1));
        }

        currentStateInfo = anim.GetCurrentAnimatorStateInfo(0);
        currentState = currentStateInfo.fullPathHash;

        // ----- control speed based on commands----------
        if (currentState == idleState || currentState == walkState
                                      || currentState == idleState2)
        {
            movementSpeed = walkMovementSpeed;
        }
        else
        {
            movementSpeed = runMovementSpeed;
        }

        navmeshAgent.speed = movementSpeed;

        //anim.SetFloat("speed", rigidBody.velocity.sqrMagnitude);

        //check if walking
        //  function will flip sprite if needed
        isWalking(navmeshAgent.velocity.magnitude);
    }

    IEnumerator PrimoSleepInRandomXSeconds()
    {
        int randomTimeToSleep = RandomNumber(7, 20);
        // NPC-5: scaled time, so the sleep timer stops with the rest of the world while paused.
        yield return new WaitForSeconds(randomTimeToSleep);
        isSleeping = true;
        anim.SetBool("sleep", true);
        locked = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // if collsion, wake primo

        // if primo collision with (player, basketball or flash)
        if (gameObject.name.Contains("primo")
            && (other.CompareTag("Player") || other.CompareTag("basketball") || other.name.Contains("flash"))
            && !movingToTarget
            && followPlayer)
        {
            anim.SetBool("sleep", false);
            isSleeping = false;

            //movingToTarget = true;
        }
        // if primo initial collsion with player, follow player
        if (gameObject.name.Contains("primo")
            && (other.CompareTag("Player")
            && !movingToTarget
            && !followPlayer))
        {
            followPlayer = true;
            anim.SetBool("sleep", false);
            isSleeping = false;
        }
    }

    IEnumerator waitOutsideRangeForXSeconds(float seconds)
    {
        // NPC-5: scaled time - see PrimoSleepInRandomXSeconds.
        yield return new WaitForSeconds(seconds);
        Vector3 relativePosition = pos1.transform.position - transform.position;

        if (relativePosition.x < 0 && facingRight)
        {
            Flip();
        }

        if (relativePosition.x > 0 && !facingRight)
        {
            Flip();
        }

        navmeshAgent.SetDestination(pos1.transform.position);
        navmeshAgent.updateRotation = false;
        locked = false;
    }

    protected bool pathComplete()
    {
        if (Vector3.Distance(navmeshAgent.destination, navmeshAgent.transform.position) <= navmeshAgent.stoppingDistance)
        {
            if (!navmeshAgent.hasPath || navmeshAgent.velocity.sqrMagnitude == 0f)
            {
                return true;
            }
        }
        return false;
    }

    void isWalking(float speed)
    {
        // if moving
        if (speed > 0)
        {
            anim.SetBool("walking", true);
        }
        else
        {
            anim.SetBool("walking", false);
        }
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;
    }

    public void setPlayerAnim(string animationName, bool isTrue)
    {
        anim.SetBool(animationName, isTrue);
    }

    //IEnumerator setWaitForXSeconds(float seconds)
    //{
    //    yield return new WaitForSecondsRealtime(seconds);
    //    waiting = false;
    //}

    //private Vector3 getRandomTransformFromPlayerPosition()
    //{
    //    Vector3 newTransform = new Vector3(transform.position.x + RandomNumber(-5, 5),
    //        transform.position.y,
    //        transform.position.z + RandomNumber(-3, 2));

    //    return newTransform;
    //}

    int RandomNumber(int min, int max)
    {
        int randNum = Random.Range(min, max);
        //Debug.Log("generate randNum : " + randNum);
        return randNum;
    }
}

