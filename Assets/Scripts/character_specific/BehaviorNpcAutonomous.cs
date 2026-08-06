using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BehaviorNpcAutonomous : MonoBehaviour
{

    public float walkMovementSpeed;
    public float runMovementSpeed;
    public float attackMovementSpeed;
    public float punchCooldown;
    public float chargeSpeed;

    [SerializeField]
    public bool facingRight;
    public bool walking;

    public GameObject pos1, pos2, pos3, pos4, pos5;

    float distanceFromStartPos;
    bool locked;

    private float movementSpeed;
    private Rigidbody rigidBody;
    private NavMeshAgent navmeshAgent;
    Animator anim;
    AnimatorStateInfo currentStateInfo;

    // per-instance: this NPC's own animator state, not a value shared by every NPC
    int currentState;

    // the hashes are genuinely shared constants
    static readonly int idleState = Animator.StringToHash("base.idle");
    static readonly int idleState2 = Animator.StringToHash("base.idle2");
    static readonly int walkState = Animator.StringToHash("base.walk");
    static readonly int runState = Animator.StringToHash("base.run");
    static readonly int attackState = Animator.StringToHash("base.attack");
    //static int attackState = Animator.StringToHash("base.attack");

    [SerializeField]
    Vector3 playerRelativePosition;

    public bool ignoreCollision;
    public bool idle;
    public bool moving;
    public bool outsideRange;
    public bool insideRange;
    public bool movingToTarget;

    // if npc has attack
    [SerializeField]
    public bool canAttack;

    public float maxDistance;

    private GameObject[] returnPositions;
    private GameObject spriteObject;

    // Use this for initialization
    void Start()
    {
        spriteObject = transform.GetComponentInChildren<SpriteRenderer>().gameObject;
        if (GameOptions.customCamera)
        {
            spriteObject.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        facingRight = true;
        movementSpeed = walkMovementSpeed;
        rigidBody = GetComponent<Rigidbody>();
        navmeshAgent = GetComponent<NavMeshAgent>();
        anim = transform.Find("sprite").GetComponent<Animator>();

        // positions flash will retreat to
        returnPositions = GameObject.FindGameObjectsWithTag("flash_return_position");
        locked = false;

        InvokeRepeating("checkNPCState", 0, 1f);
    }

    void Update()
    {

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
        //rigidBody.velocity = movement * movementSpeed;
        navmeshAgent.speed = movementSpeed;
        if (rigidBody != null)
        {
            anim.SetFloat("speed", rigidBody.linearVelocity.sqrMagnitude);
        }
        ////check if walking
        ////  function will flip sprite if needed
        isWalking(navmeshAgent.velocity.magnitude);
    }

    private void checkNPCState()
    {
        distanceFromStartPos = Vector3.Distance(transform.position, returnPositions[0].transform.position);
        //Debug.Log("distanceFromStartPos : " + ( distanceFromStartPos > maxDistance));

        if (distanceFromStartPos >= maxDistance && movingToTarget)
        {
            outsideRange = true;
            insideRange = false;
            //Debug.Log("if(distanceFromStartPos > maxDistance && !movingToTarget)");
        }

        if (distanceFromStartPos < maxDistance && movingToTarget)
        {
            outsideRange = false;
            insideRange = true;
            //Debug.Log("if (distanceFromStartPos <= maxDistance)");
        }

        // navmesh has no target and inside range
        if (pathComplete())
        {
            movingToTarget = false;
            ignoreCollision = false;
        }

        // if outside area
        if (outsideRange 
            && !movingToTarget 
            && !locked
            && currentState != attackState)
        {
            locked = true;
            ignoreCollision = true;
            movingToTarget = true;

            StartCoroutine(waitOutsideRangeForXSeconds(5));
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        //if ((gameObject.name.Contains("flash") || gameObject.name.Contains("mouse") || gameObject.name.Contains("ghost")) 
        if (gameObject.CompareTag("auto_npc")
            && (other.CompareTag("Player") || other.CompareTag("enemy") || other.CompareTag("basketball") || other.CompareTag("knock_down_attack"))
            && !ignoreCollision && !movingToTarget)
        {
            if (!canAttack || !other.CompareTag("Player"))
            {
                movingToTarget = true;

                Vector3 newVector = getRandomTransformFromPlayerPosition();
                Vector3 oldVector = transform.position;
                Vector3 relativePosition = newVector - oldVector;

                if (relativePosition.x < 0 && facingRight)
                {
                    Flip();
                }
                if (relativePosition.x > 0 && !facingRight)
                {
                    Flip();
                }
                if (navmeshAgent != null)
                {
                    navmeshAgent.SetDestination(newVector);
                    //disable rotation
                    navmeshAgent.updateRotation = false;
                }
            }
            if (canAttack && (other.CompareTag("Player") || other.CompareTag("enemy")))
            {
                Debug.Log("play attack anim");

                anim.Play("attack");
                // play anim then move

                movingToTarget = true;

                Vector3 newVector = getRandomTransformFromPlayerPosition();
                Vector3 oldVector = transform.position;
                Vector3 relativePosition = newVector - oldVector;

                if (relativePosition.x < 0 && facingRight)
                {
                    Flip();
                }
                if (relativePosition.x > 0 && !facingRight)
                {
                    Flip();
                }
                navmeshAgent.SetDestination(newVector);
                //disable rotation
                navmeshAgent.updateRotation = false;

            }
        }
        //if ((gameObject.name.Contains("flash") || gameObject.name.Contains("mouse") || gameObject.name.Contains("ghost")) 
        if (gameObject.CompareTag("auto_npc")
            && other.CompareTag("basketball")
            && other.GetComponent<PlayerIdentifier>().isCpu
            && other.GetComponent<PlayerIdentifier>().autoPlayer.GetComponent<AutoPlayerController>().Locked)
        {
            other.GetComponent<PlayerIdentifier>().autoPlayer.GetComponent<AutoPlayerController>().Locked = false;
        }
    }

    IEnumerator waitOutsideRangeForXSeconds(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        int finder = Random.Range(0, returnPositions.Length); //Then you just use this; nameDisplayString = names[finder];
        GameObject randPos = returnPositions[finder];

        Vector3 relativePosition = randPos.transform.position - transform.position;

        if (relativePosition.x < 0 && facingRight)
        {
            Flip();
        }

        if (relativePosition.x > 0 && !facingRight)
        {
            Flip();
        }

        navmeshAgent.SetDestination(randPos.transform.position);
        navmeshAgent.updateRotation = false;
        locked = false;
    }

    protected bool pathComplete()
    {
        if (Vector3.Distance(navmeshAgent.destination, navmeshAgent.transform.position) <= navmeshAgent.stoppingDistance)
        {
            if (!navmeshAgent.hasPath || navmeshAgent.velocity.sqrMagnitude == 0f)
            {
                // if not facing goal, flip
                if (GameLevelManager.instance.BasketballRimVector.x < 0 && facingRight)
                {
                    Flip();
                }
                if (GameLevelManager.instance.BasketballRimVector.x > 0 && !facingRight)
                {
                    Flip();
                }
                return true;
            }
        }
        return false;
    }

    void isWalking(float speed)
    {
        if (currentState != attackState || !canAttack)
        {
            // if moving
            if (speed > 0)
            {
                anim.SetBool("run", true);
            }
            else
            {
                anim.SetBool("run", false);
            }
        }
    }

    void Flip()
    {
        //Debug.Log(" Flip()");
        facingRight = !facingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;
    }

    private Vector3 getRandomTransformFromPlayerPosition()
    {
        Vector3 newTransform = new Vector3(transform.position.x + RandomNumber(-5, 5),
            transform.position.y,
            transform.position.z + RandomNumber(-3, 2));

        return newTransform;
    }

    int RandomNumber(int min, int max)
    {
        int randNum = Random.Range(min, max);
        return randNum;
    }
}

