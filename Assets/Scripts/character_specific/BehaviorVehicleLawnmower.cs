using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class BehaviorVehicleLawnmower : MonoBehaviour
{

    [SerializeField]
    public float walkMovementSpeed;
    //public float runMovementSpeed;
    //public float attackMovementSpeed;
    //public float punchCooldown;
    //public float chargeSpeed;

    public bool facingRight, walking;
    public bool canMove;

    public GameObject pos1, pos2, pos3, pos4;

    float distanceFromStartPos;
    //bool locked;
    //GameObject player;

    private float movementSpeed;
    private Rigidbody rigidBody;
    private NavMeshAgent navmeshAgent;
    public SpriteRenderer currentSprite;

    //public GameObject playerHitbox;
    Animator anim;
    AnimatorStateInfo currentStateInfo;

    // per-instance: this vehicle's own animator state, not a value shared by every instance
    int currentState;

    // the hashes are genuinely shared constants
    static readonly int idleState = Animator.StringToHash("base.idle");
    static readonly int idleState2 = Animator.StringToHash("base.idle2");
    static readonly int walkState = Animator.StringToHash("base.walk");
    static readonly int runState = Animator.StringToHash("base.run");

    Vector3 playerRelativePosition;
    bool waiting;

    public bool ignoreCollision;
    public bool idle;
    public bool moving;
    public bool outsideRange;
    public bool insideRange;
    public bool movingToTarget;

    public float maxDistance;
    private Vector3 currentTarget;
    private int currentTargetIndex;

    private GameObject[] returnPositions;
    float relativePosition;

    // Start is called before the first frame update
    void Start()
    {
        //player = GameLevelManager.Instance.Player;
        facingRight = true;
        canMove = true;
        //movementSpeed = walkMovementSpeed;

        // AUD-075: transform.Find("sprite") and the returnPositions[0] index below both threw
        // unconditionally - resolve everything Update() depends on first, and disable rather than
        // let either throw partway through Start() and leave Update() to NRE every frame.
        Transform spriteTransform = transform.Find("sprite");
        returnPositions = GameObject.FindGameObjectsWithTag("vehicle_position_marker");

        List<string> missing = new List<string>();
        if (spriteTransform == null)
        {
            missing.Add("a 'sprite' child object");
        }
        if (returnPositions.Length == 0)
        {
            missing.Add("any 'vehicle_position_marker'-tagged object");
        }

        if (missing.Count > 0)
        {
            Debug.LogError("BehaviorVehicleLawnmower on " + name + " is disabled: missing " +
                string.Join(", ", missing) + ".", this);
            enabled = false;
            return;
        }

        currentSprite = spriteTransform.GetComponent<SpriteRenderer>();
        rigidBody = GetComponent<Rigidbody>();
        navmeshAgent = GetComponent<NavMeshAgent>();
        anim = spriteTransform.GetComponent<Animator>();

        //locked = false;
        currentTargetIndex = 0;
        currentTarget = returnPositions[currentTargetIndex].transform.position;
        movingToTarget = false;
    }

    // Update is called once per frame
    void Update()
    {
        relativePosition = currentTarget.x - transform.position.x;

        if (!movingToTarget)
        {
            navmeshAgent.SetDestination(currentTarget);
        }

        if (pathComplete())
        {
            if (currentTargetIndex >= returnPositions.Length - 1)
            {
                currentTargetIndex = 0;
                currentTarget = returnPositions[currentTargetIndex].transform.position;
            }
            else
            {
                currentTargetIndex += 1;
                currentTarget = returnPositions[currentTargetIndex].transform.position;
            }
        }
        if (relativePosition < 0 && facingRight)
        {
            Flip();
        }
        if (relativePosition > 0 && !facingRight)
        {
            Flip();
        }
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

    void Flip()
    {
        //Debug.Log(" Flip()");
        facingRight = !facingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;
    }
}
