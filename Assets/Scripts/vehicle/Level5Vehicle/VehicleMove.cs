using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleMove : MonoBehaviour
{
    public int id;
    public float speed;
    public float xDistance;
    public float yDistance;

    //[SerializeField]
    //Vector3 startPosition;
    [SerializeField]
    Vector3 targetPosition;

    [SerializeField]
    SpriteRenderer spriteRenderer;

    public bool facingRight;


    // Start is called before the first frame update
    void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (xDistance == 0)
        {
            xDistance = 20;
        }
        //startPosition = transform.position;

        setTargetPosition();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        MoveVehicle();
    }

    private void MoveVehicle()
    {
        // moving right
        if (facingRight)
        {
            // Move our position a step closer to the target.
            float step = speed * Time.fixedDeltaTime; // calculate distance to move
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

            //// Check if the position of the cube and sphere are approximately equal.
            if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
            {
                facingRight = false;
                spriteRenderer.flipX = true;
                setTargetPosition();
            }
        }
        // moving left
        else
        {
            // Move our position a step closer to the target.
            float step = speed * Time.fixedDeltaTime; // calculate distance to move
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, step);

            //// Check if the position of the cube and sphere are approximately equal.
            if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
            {
                facingRight = true;
                spriteRenderer.flipX = false;
                setTargetPosition();
            }
        }
    }
    private void setTargetPosition()
    {
        if (!facingRight)
        {
            spriteRenderer.flipX = true;
            targetPosition = new Vector3(transform.position.x - xDistance, transform.position.y, transform.position.z);
        }
        else
        {
            targetPosition = new Vector3(transform.position.x + xDistance, transform.position.y, transform.position.z);
        }
    }
}
