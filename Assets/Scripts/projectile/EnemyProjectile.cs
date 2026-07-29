using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    public float lifetime;
    public float projectileForce;
    public Vector3 projectileForceThrown;
    public Vector3 projectileForceSniper;
    Rigidbody rigidbody;
    public bool thrownProjectile;
    public bool impactProjectile;
    [SerializeField]
    public bool sniperProjectile;
    public bool facingRight;
    public bool isBullet;
    public bool isLaser;
    public bool isBulletAuto;
    [SerializeField]
    GameObject impactExplosionPrefab;
    [SerializeField]
    GameObject impactSniperGroundPrefab;
    [SerializeField]
    GameObject impactSniperPlayerPrefab;
    [SerializeField]
    AudioSource audioSource;

    void Start()
    {
        rigidbody = transform.root.GetComponent<Rigidbody>();
        audioSource = transform.root.GetComponent<AudioSource>();

        // regular shoot
        if (!thrownProjectile && !sniperProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForce);
            Destroy(transform.root.gameObject, lifetime);
        }
        // thrown projectile that explodes on impact
        if (thrownProjectile && !impactProjectile && !sniperProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForceThrown);
            impactExplosionPrefab = Resources.Load("Prefabs/projectile/projectile_impact_explosion") as GameObject;
            Destroy(transform.root.gameObject, lifetime);
        }
        // not sure
        if (!thrownProjectile && !impactProjectile && !sniperProjectile)
        {
            Destroy(transform.root.gameObject, lifetime);
        }
        // sniper shoot. impact on player and ground
        if (sniperProjectile)
        {
            impactSniperGroundPrefab = Resources.Load("Prefabs/projectile/projectile_impact_ground") as GameObject;
            impactSniperPlayerPrefab = Resources.Load("Prefabs/projectile/projectile_impact_player") as GameObject;
            audioSource.clip = null;
            applyForceToDirectionVector(projectileForceSniper * 10);
            Destroy(transform.root.gameObject, lifetime);
        }
    }

    // ------------------------------ move projectiles ------------------------------------------

    public void applyForceToDirectionFacingProjectile(float force)
    {
        // get direction facing
        if (facingRight)
        {
            rigidbody.AddForce(force, 0, 0, ForceMode.VelocityChange);
        }
        if (!facingRight)
        {
            rigidbody.AddForce(-force, 0, 0, ForceMode.VelocityChange);
        }
    }
    public void applyForceToDirectionFacingProjectile(Vector3 force)
    {
        // get direction facing
        if (facingRight)
        {
            rigidbody.AddForce(force.x, force.y, force.z, ForceMode.VelocityChange);
        }
        if (!facingRight)
        {
            rigidbody.AddForce(-force.x, force.y, force.z, ForceMode.VelocityChange);
        }
    }

    public void applyForceToDirectionVector(Vector3 force)
    {
        // remove rigidbody constraints
        rigidbody.constraints = RigidbodyConstraints.None;
        rigidbody.AddForce(force.x, force.y, force.z, ForceMode.VelocityChange);
    }

    void DestroyProjectile()
    {
        Destroy(transform.root.gameObject);
    }

    // ------------------------------ instantiate impact prefabs on collision ------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        // regular enemy projectile
        if (!sniperProjectile
            && !impactProjectile
            && !thrownProjectile
            && (other.CompareTag("enemyHitbox") || other.CompareTag("playerHitbox")))
        {
            DestroyProjectile();
        }

        // thrown/shot projectile 
        if (thrownProjectile // thrown projectile          
            && !impactProjectile // does NOT explode on impact. bullet, laser, etc
            && !sniperProjectile
            && (other.gameObject.CompareTag("ground")
            || other.CompareTag("enemyHitbox")
            || other.CompareTag("playerHitbox"))) // if hit ground or enemy
        {
            // get position of impact to instantiate explosion object
            Vector3 transformAtImpact = other.gameObject.transform.position;
            Vector3 spawnPoint = new Vector3(transformAtImpact.x, 0, transformAtImpact.z);

            // explode object
            Instantiate(impactExplosionPrefab, spawnPoint, Quaternion.identity);

            DestroyProjectile();
        }
        // sniper shot hits ground
        if (sniperProjectile
            && impactProjectile
            && (other.gameObject.CompareTag("ground") || other.gameObject.layer == 11))
        {
            // instantiate at position player was standing when shot occurred
            Vector3 transformAtImpact;
            if (isBulletAuto)
            {
                Vector3 target = new Vector3(gameObject.transform.position.x, GameLevelManager.instance.TerrainHeight, SniperManager.instance.PlayerPosAtShoot.z);
                //Vector3 target = new Vector3(gameObject.transform.position.x, GameLevelManager.instance.TerrainHeight, gameObject.transform.position.z);
                transformAtImpact = target;
                //transformAtImpact = gameObject.transform.position;
                //Debug.Log("target x  : " + target.x);
            }
            else
            {
                transformAtImpact = SniperManager.instance.PlayerPosAtShoot;
            }
            
            Vector3 spawnPoint = new Vector3(transformAtImpact.x, GameLevelManager.instance.TerrainHeight, transformAtImpact.z);
            //Debug.Log("sniper miss : "+ transformAtImpact);
            Instantiate(impactSniperGroundPrefab, spawnPoint, Quaternion.identity);
            DestroyProjectile();
        }
        // sniper shot hits player or enemy
        if (sniperProjectile
            && impactProjectile
            && !GameOptions.sniperEnabledLaser
            && (other.gameObject.CompareTag("enemyHitbox")
            || other.gameObject.CompareTag("playerHitbox")))
        {
            if (other.gameObject.CompareTag("playerHitbox") && sniperProjectile)
            {
                // if player hit, increase count
                GameLevelManager.instance.Player1.gameStats.SniperHits++;
            }

            // instantiate at position player was standing when shot occurred
            Vector3 transformAtImpact = SniperManager.instance.PlayerPosAtShoot;
            Vector3 spawnPoint = new Vector3(transformAtImpact.x, GameLevelManager.instance.TerrainHeight, transformAtImpact.z);
            Debug.Log("sniper hit : " + transformAtImpact);
            Instantiate(impactSniperPlayerPrefab, spawnPoint, Quaternion.identity);
            DestroyProjectile();
        }
    }
}
