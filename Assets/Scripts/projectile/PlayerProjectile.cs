using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    public float lifetime;
    public float projectileForce;
    public Vector3 projectileForceThrown;
    Rigidbody rigidbody;
    PlayerController playerController;
    public bool thrownProjectile;
    public bool impactProjectile;

    GameObject impactExplosionPrefab;
    GameObject impactRabbitPrefab;

    public bool facingRight;

    void Start()
    {
        rigidbody = transform.root.GetComponent<Rigidbody>();
        playerController = GameLevelManager.instance.PlayerController1;
        if (!thrownProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForce);
            Destroy(transform.root.gameObject, lifetime);
        }
        if (thrownProjectile && !impactProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForceThrown);
            impactExplosionPrefab = Resources.Load("Prefabs/projectile/projectile_impact_explosion") as GameObject;
            impactRabbitPrefab = Resources.Load("Prefabs/projectile/projectile_impact_rabbit") as GameObject;
        }
        if (!thrownProjectile && impactProjectile)
        {
            Destroy(transform.root.gameObject, lifetime);
        }
    }
    public void applyForceToDirectionFacingProjectile(float force)
    {
        // get direction facing
        if (playerController.FacingRight)
        {
            //Debug.Log(" shoot right");
            rigidbody.AddForce(force, 0, 0, ForceMode.VelocityChange);
        }
        if (!playerController.FacingRight)
        {
            Flip();
            //Debug.Log(" shoot left");
            rigidbody.AddForce(-force, 0, 0, ForceMode.VelocityChange);
        }
    }
    public void applyForceToDirectionFacingProjectile(Vector3 force)
    {
        // get direction facing
        if (playerController.FacingRight)
        {
            rigidbody.AddForce(force.x, force.y, force.z, ForceMode.VelocityChange);
        }
        if (!playerController.FacingRight)
        {
            Flip();
            rigidbody.AddForce(-force.x, force.y, force.z, ForceMode.VelocityChange);
        }
    }
    void DestroyProjectile()
    {
        Destroy(transform.root.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        // destroy thrown projectile on impact
        if (thrownProjectile
            && !impactProjectile
            && (other.CompareTag("ground") || other.CompareTag("enemyHitbox")))
        {
            Vector3 transformAtImpact = gameObject.transform.position;
            //Vector3 transformAtImpact = other.transform.position;
            Vector3 spawnPoint;

            // if rabbit
            if (other.name.Contains("rabbit"))
            {
                spawnPoint = new Vector3(transformAtImpact.x, other.transform.position.y, transformAtImpact.z);
                Instantiate(impactRabbitPrefab, spawnPoint, Quaternion.identity);
            }
            //else
            else
            {
                //spawnPoint = new Vector3(transformAtImpact.x, 0, transformAtImpact.z);
                spawnPoint = new Vector3(transformAtImpact.x, other.transform.position.y, transformAtImpact.z);
                // explode object
                Instantiate(impactExplosionPrefab, spawnPoint, Quaternion.identity);
            }
            //Vector3 spawnPoint = new Vector3(transformAtImpact.x, other.transform.position.y, transformAtImpact.z);
            // explode object
            //Instantiate(impactExplosionPrefab, spawnPoint, Quaternion.identity);
            DestroyProjectile();
        }
    }
    void Flip()
    {
        //Debug.Log("flip : " + transform.parent.gameObject.name);
        //Vector3 thisScale = transform.root.localScale;
        //Debug.Log("thisScale : " + thisScale);
        //thisScale.x *= -1;
        //Debug.Log("thisScale.x : " + thisScale.x);
        //transform.root.localScale = thisScale;
        //Debug.Log("transform.parent.gameObject.GetComponent<SpriteRenderer>() == null : " 
            //+ (transform.parent.gameObject.GetComponent<SpriteRenderer>() == null));

        transform.parent.gameObject.GetComponent<SpriteRenderer>().flipX = true; 
    }
}
