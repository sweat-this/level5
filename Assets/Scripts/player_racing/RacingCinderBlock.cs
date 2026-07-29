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
        target = RacingGameManager.instance.Player.transform.position;
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
                rigidbody.MovePosition(transform.position + movement);
            }
            //else
            //{
            //    movementSpeed = 0;
            //}
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
            targetReached = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && gameObject.CompareTag("obstacle") && !targetReached)
        {
            targetReached = true;
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
