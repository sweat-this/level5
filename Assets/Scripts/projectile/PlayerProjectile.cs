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

    private bool spawnedFromPool;
    private bool launched;
    private bool defaultThrownProjectile;
    private bool defaultImpactProjectile;
    private bool defaultFacingRight;

    private void Awake()
    {
        defaultThrownProjectile = thrownProjectile;
        defaultImpactProjectile = impactProjectile;
        defaultFacingRight = facingRight;
        CacheReferences();
    }

    void Start()
    {
        if (!spawnedFromPool)
        {
            Launch();
        }
    }

    public void PrepareForPooledSpawn()
    {
        spawnedFromPool = true;
        launched = false;
        thrownProjectile = defaultThrownProjectile;
        impactProjectile = defaultImpactProjectile;
        facingRight = defaultFacingRight;
        CacheReferences();

        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        if (transform.parent != null && transform.parent.TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            spriteRenderer.flipX = false;
        }
    }

    public void Launch()
    {
        if (launched)
        {
            return;
        }

        launched = true;
        CacheReferences();

        if (!thrownProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForce);
            ProjectilePool.ReleaseAfter(transform.root.gameObject, lifetime);
        }

        if (thrownProjectile && !impactProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForceThrown);
            impactExplosionPrefab = LoadProjectilePrefab(impactExplosionPrefab, "Prefabs/projectile/projectile_impact_explosion");
            impactRabbitPrefab = LoadProjectilePrefab(impactRabbitPrefab, "Prefabs/projectile/projectile_impact_rabbit");
        }

        if (!thrownProjectile && impactProjectile)
        {
            ProjectilePool.ReleaseAfter(transform.root.gameObject, lifetime);
        }
    }

    private void CacheReferences()
    {
        if (rigidbody == null)
        {
            rigidbody = transform.root.GetComponent<Rigidbody>();
        }

        if (playerController == null && GameLevelManager.instance != null)
        {
            playerController = GameLevelManager.instance.PlayerController1;
        }
    }

    private GameObject LoadProjectilePrefab(GameObject currentPrefab, string resourcePath)
    {
        return currentPrefab != null ? currentPrefab : Resources.Load(resourcePath) as GameObject;
    }

    public void applyForceToDirectionFacingProjectile(float force)
    {
        if (playerController == null || rigidbody == null)
        {
            return;
        }

        if (playerController.FacingRight)
        {
            rigidbody.AddForce(force, 0, 0, ForceMode.VelocityChange);
        }
        else
        {
            Flip();
            rigidbody.AddForce(-force, 0, 0, ForceMode.VelocityChange);
        }
    }

    public void applyForceToDirectionFacingProjectile(Vector3 force)
    {
        if (playerController == null || rigidbody == null)
        {
            return;
        }

        if (playerController.FacingRight)
        {
            rigidbody.AddForce(force.x, force.y, force.z, ForceMode.VelocityChange);
        }
        else
        {
            Flip();
            rigidbody.AddForce(-force.x, force.y, force.z, ForceMode.VelocityChange);
        }
    }

    void DestroyProjectile()
    {
        ProjectilePool.Release(transform.root.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (thrownProjectile
            && !impactProjectile
            && (other.CompareTag("ground") || other.CompareTag("enemyHitbox")))
        {
            Vector3 transformAtImpact = gameObject.transform.position;
            Vector3 spawnPoint = new Vector3(transformAtImpact.x, other.transform.position.y, transformAtImpact.z);

            if (other.name.Contains("rabbit"))
            {
                Instantiate(impactRabbitPrefab, spawnPoint, Quaternion.identity);
            }
            else
            {
                Instantiate(impactExplosionPrefab, spawnPoint, Quaternion.identity);
            }

            DestroyProjectile();
        }
    }

    void Flip()
    {
        if (transform.parent != null && transform.parent.TryGetComponent(out SpriteRenderer spriteRenderer))
        {
            spriteRenderer.flipX = true;
        }
    }
}
