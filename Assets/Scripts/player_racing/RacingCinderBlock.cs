using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RacingCinderBlock : MonoBehaviour
{
    [SerializeField]
    Vector3 target;
    [SerializeField]
    Vector3 movement;
    [SerializeField]
    float acceleration;
    [SerializeField]
    float maxSpeed;
    [SerializeField]
    float movementSpeed;
    [SerializeField]
    float defaultMovementSpeed;
    [SerializeField]
    private Rigidbody rigidbody;
    [SerializeField]
    bool targetReached;
    [SerializeField]
    bool isLocked = false;
    [SerializeField]
    GameObject dropShadow;
    [SerializeField]
    bool destroyObject = false;

    // Start is called before the first frame update
    void Start()
    {
        rigidbody = GetComponent<Rigidbody>();
        // AUD-012 Phase 4 code review finding: this used to assign the player's raw world position to
        // `target` directly, instead of the normalized direction every other write to this field
        // produces (see pursuePlayer()). Under the old MovePosition path this only ever mattered for
        // one physics step (pursuePlayer(), called every FixedUpdate, immediately renormalized it) -
        // but it is clearer, and no more expensive, to seed a well-defined direction up front than to
        // rely on a one-tick self-correction. pursuePlayer() also depends on
        // RacingGameManager.instance.Player/.PlayerController, exactly as the two lines below already
        // do, so calling it here introduces no new dependency.
        pursuePlayer();
        //Debug.Log("RacingGameManager.instance.CharacterProfile.MaxSpeed : " + RacingGameManager.instance.CharacterProfile.MaxSpeed);
        maxSpeed = RacingGameManager.instance.CharacterProfile.MaxSpeed * 1.6f;
        //acceleration = RacingGameManager.instance.CharacterProfile.Acceleration * 2f;
        movementSpeed = defaultMovementSpeed;
        //InvokeRepeating("pursuePlayer", 0, 0.4f);
    }

    private void Update()
    {
        // update drop shadow
        if (dropShadow != null)
        {
            dropShadow.transform.position = new Vector3(dropShadow.transform.position.x,
                RacingGameManager.instance.TerrainHeight + 0.01f,
                dropShadow.transform.position.z);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!targetReached)
        {
            if (movementSpeed < maxSpeed)
            {
                //movementSpeed = 1 + (acceleration / 100);
                movementSpeed = (movementSpeed + (acceleration / 100));
            }

            if (!RacingGameManager.instance.PlayerController.KnockedDown)
            {
                movement = target * (movementSpeed * Time.fixedDeltaTime);
                //movement = targetPosition * (movementSpeed * Time.deltaTime);
                // AUD-012 Phase 4: this chase intentionally owns X/Y/Z (see pursuePlayer's vertical
                // target offset), so it commands a full-vector Rigidbody velocity rather than routing
                // through RigidbodyLocomotionMotor, which is planar-only by design.
                rigidbody.linearVelocity = target * movementSpeed;
            }
            else
            {
                // AUD-012 Phase 4: MovePosition produced no further displacement the instant this
                // branch stopped being reached. A persistent Rigidbody velocity does not stop on its
                // own, so the last commanded chase velocity must be released explicitly while the
                // player is knocked down - full vector, since this chase owns X/Y/Z.
                StopChaseVelocity();
            }
            pursuePlayer();
        }
        if (targetReached)
        {
            if (!isLocked)
            {
                Vector3 force = new Vector3(10, -20, 0);
                rigidbody.AddForce(force, ForceMode.VelocityChange);
            }
        }
        if(destroyObject && gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// AUD-012 Phase 4: the single authoritative transition from chase into the impact phase.
    /// pursuePlayer()'s own arrival check and OnTriggerEnter's player-collision path can both reach
    /// this during one cinder block's life - guarding on the current state makes the chase-velocity
    /// clear below run exactly once, not every frame once already in the impact phase.
    /// </summary>
    private void TransitionToTargetReached()
    {
        if (targetReached)
        {
            return;
        }

        targetReached = true;
        StopChaseVelocity();
    }

    /// <summary>
    /// AUD-012 Phase 4: zeroes the full chase velocity - X/Y/Z, not only X/Z - because this chase
    /// intentionally drives all three axes (see pursuePlayer's vertical target offset), unlike the
    /// planar roles RigidbodyLocomotionMotor serves.
    /// </summary>
    private void StopChaseVelocity()
    {
        rigidbody.linearVelocity = Vector3.zero;
    }

    public void pursuePlayer()
    {

        Vector3 newVector = new Vector3(0, 0, 0);
        float xDirection = 0;

        if (RacingGameManager.instance.PlayerController.FacingFront)
        {
            xDirection = 1;
        }
        if (!RacingGameManager.instance.PlayerController.FacingFront)
        {
            xDirection = -1;
        }
        newVector = new Vector3(RacingGameManager.instance.Player.transform.position.x + xDirection,
            RacingGameManager.instance.Player.transform.position.y + 2,
            RacingGameManager.instance.Player.transform.position.z);
        target = (newVector - transform.position).normalized;
        if ((newVector.x - transform.position.x) < 1)
        {
            TransitionToTargetReached();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && gameObject.CompareTag("obstacle") && !targetReached)
        {
            TransitionToTargetReached();
            //isLocked = true;
            Debug.Log("target reached");
            //movementSpeed = defaultMovementSpeed;
        }
        if ((other.CompareTag("ground") || other.CompareTag("playerHitbox") || other.gameObject.layer == 11) && targetReached && !isLocked)
        {
            isLocked = true;
            //rigidbody.detectCollisions = false;
            Debug.Log("attack player");
            //targetReached = true;
            Vector3 instantiateVector = new Vector3(RacingGameManager.instance.Player.transform.position.x - 25,
                RacingGameManager.instance.Player.transform.position.y + 2,
                RacingGameManager.instance.Player.transform.position.z);
            Instantiate(RacingGameManager.instance.CinderBlockPrefab, instantiateVector, Quaternion.identity);

            Destroy(this.gameObject);
            Debug.Log("ENTER : destroy object");
            destroyObject = true;
            //movementSpeed = defaultMovementSpeed;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if ((other.CompareTag("ground") || other.CompareTag("playerHitbox") || other.gameObject.layer == 11) && targetReached)
        {        
            Destroy(this.gameObject);
            Debug.Log("EXIT : destroy object");
            destroyObject = true;
            //movementSpeed = defaultMovementSpeed;
        }
    }
}
