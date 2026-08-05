
using UnityEngine;
using UnityEngine.AI;

public class VehicleController : MonoBehaviour
{
    [SerializeField]
    int vehicleId;
    [SerializeField]
    float vehicleSpeed;
    [SerializeField]
    float timeToRespawn;

    // these 2 vars need to be serialzed because they set before a clone of them is 
    // configured and instantiated

    public Vector3 currentTarget;
    public string direction;

    //int currentTargetIndex;
    //int defaultTargetIndex = 0;
    //const string vehiclePosMarkersTag = "vehicle_position_marker";

    NavMeshAgent navMeshAgent;
    //GameObject spawnPoint;
    Animator animator;
    //private Rigidbody rigidbody;


    public bool facingRight;
    Vector3 bballRimVector;
    public float relativePositioning;


    private void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.autoBraking = true;
        navMeshAgent.updateRotation = false;
        navMeshAgent.speed = vehicleSpeed;
        animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        ConfigureRoute();
    }

    private void Start()
    {
        ConfigureRoute();
    }

    private void ConfigureRoute()
    {
        if (GameLevelManager.instance == null || navMeshAgent == null)
        {
            return;
        }

        bballRimVector = GameLevelManager.instance.BasketballRimVector;

        // where is vehicle spawned in relation to rim
        relativePositioning = bballRimVector.x - gameObject.transform.position.x;

        // determine which way Gameobject is facing
        if (transform.localScale.x > 0)
        {
            facingRight = true;
        }
        else
        {
            facingRight = false;
        }

        //if vehicle is on right side of rim, flip
        if (relativePositioning < 0 && facingRight)
        {
            Flip();
        }
        //if vehicle is on right side of rim, flip
        if (relativePositioning > 0 && !facingRight)
        {
            Flip();
        }

        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.destination = CurrentTarget;
        }
    }

    // Update is called once per frame
    void Update()
    {

        //set animator speed to transition to move animation
        animator.SetFloat("speed", navMeshAgent.speed);

        // Schedule the replacement before returning this instance to its pool.
        if (!navMeshAgent.pathPending && navMeshAgent.remainingDistance < 0.1f)
        {
            // call traffic manager coroutine to respawn a new instance
            TrafficManager.instance.spawnVehicle(VehicleId, Direction, timeToRespawn);
            RuntimeObjectPool.Release(gameObject);
        }
    }

    public void Flip()
    {
        FacingRight = !FacingRight;
        Vector3 thisScale = transform.localScale;
        thisScale.x *= -1;
        transform.localScale = thisScale;
    }

    public void Configure(string travelDirection, Vector3 target)
    {
        Direction = travelDirection;
        CurrentTarget = target;
    }

    public int VehicleId { get => vehicleId; set => vehicleId = value; }
    public string Direction { get => direction; set => direction = value; }
    public Vector3 CurrentTarget { get => currentTarget; set => currentTarget = value; }
    public bool FacingRight { get => facingRight; set => facingRight = value; }
}
