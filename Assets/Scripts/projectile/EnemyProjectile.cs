using UnityEngine;
using Level5.Core.Match;

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

    private bool spawnedFromPool;
    private bool launched;
    private bool defaultThrownProjectile;
    private bool defaultImpactProjectile;
    private bool defaultSniperProjectile;
    private bool defaultFacingRight;
    private bool defaultIsBullet;
    private bool defaultIsLaser;
    private bool defaultIsBulletAuto;
    private Vector3 defaultProjectileForceSniper;

    private void Awake()
    {
        defaultThrownProjectile = thrownProjectile;
        defaultImpactProjectile = impactProjectile;
        defaultSniperProjectile = sniperProjectile;
        defaultFacingRight = facingRight;
        defaultIsBullet = isBullet;
        defaultIsLaser = isLaser;
        defaultIsBulletAuto = isBulletAuto;
        defaultProjectileForceSniper = projectileForceSniper;
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
        sniperProjectile = defaultSniperProjectile;
        facingRight = defaultFacingRight;
        isBullet = defaultIsBullet;
        isLaser = defaultIsLaser;
        isBulletAuto = defaultIsBulletAuto;
        projectileForceSniper = defaultProjectileForceSniper;
        CacheReferences();

        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
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

        if (!thrownProjectile && !sniperProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForce);
            ProjectilePool.ReleaseAfter(transform.root.gameObject, lifetime);
        }

        if (thrownProjectile && !impactProjectile && !sniperProjectile)
        {
            applyForceToDirectionFacingProjectile(projectileForceThrown);
            impactExplosionPrefab = LoadProjectilePrefab(impactExplosionPrefab, "Prefabs/projectile/projectile_impact_explosion");
            ProjectilePool.ReleaseAfter(transform.root.gameObject, lifetime);
        }

        if (!thrownProjectile && !impactProjectile && !sniperProjectile)
        {
            ProjectilePool.ReleaseAfter(transform.root.gameObject, lifetime);
        }

        if (sniperProjectile)
        {
            impactSniperGroundPrefab = LoadProjectilePrefab(impactSniperGroundPrefab, "Prefabs/projectile/projectile_impact_ground");
            impactSniperPlayerPrefab = LoadProjectilePrefab(impactSniperPlayerPrefab, "Prefabs/projectile/projectile_impact_player");
            if (audioSource != null)
            {
                audioSource.clip = null;
            }
            applyForceToDirectionVector(projectileForceSniper * 10);
            ProjectilePool.ReleaseAfter(transform.root.gameObject, lifetime);
        }
    }

    private void CacheReferences()
    {
        if (rigidbody == null)
        {
            rigidbody = transform.root.GetComponent<Rigidbody>();
        }

        if (audioSource == null)
        {
            audioSource = transform.root.GetComponent<AudioSource>();
        }
    }

    private GameObject LoadProjectilePrefab(GameObject currentPrefab, string resourcePath)
    {
        return currentPrefab != null ? currentPrefab : Resources.Load(resourcePath) as GameObject;
    }

    public void applyForceToDirectionFacingProjectile(float force)
    {
        if (rigidbody == null)
        {
            return;
        }

        if (facingRight)
        {
            rigidbody.AddForce(force, 0, 0, ForceMode.VelocityChange);
        }
        else
        {
            rigidbody.AddForce(-force, 0, 0, ForceMode.VelocityChange);
        }
    }

    public void applyForceToDirectionFacingProjectile(Vector3 force)
    {
        if (rigidbody == null)
        {
            return;
        }

        if (facingRight)
        {
            rigidbody.AddForce(force.x, force.y, force.z, ForceMode.VelocityChange);
        }
        else
        {
            rigidbody.AddForce(-force.x, force.y, force.z, ForceMode.VelocityChange);
        }
    }

    public void applyForceToDirectionVector(Vector3 force)
    {
        if (rigidbody == null)
        {
            return;
        }

        rigidbody.constraints = RigidbodyConstraints.None;
        rigidbody.AddForce(force.x, force.y, force.z, ForceMode.VelocityChange);
    }

    void DestroyProjectile()
    {
        ProjectilePool.Release(transform.root.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!sniperProjectile
            && !impactProjectile
            && !thrownProjectile
            && (other.CompareTag("enemyHitbox") || other.CompareTag("playerHitbox")))
        {
            DestroyProjectile();
        }

        if (thrownProjectile
            && !impactProjectile
            && !sniperProjectile
            && (other.gameObject.CompareTag("ground")
            || other.CompareTag("enemyHitbox")
            || other.CompareTag("playerHitbox")))
        {
            Vector3 transformAtImpact = other.gameObject.transform.position;
            Vector3 spawnPoint = new Vector3(transformAtImpact.x, 0, transformAtImpact.z);

            Instantiate(impactExplosionPrefab, spawnPoint, Quaternion.identity);
            DestroyProjectile();
        }

        if (sniperProjectile
            && impactProjectile
            && (other.gameObject.CompareTag("ground") || other.gameObject.layer == 11))
        {
            Vector3 transformAtImpact;
            if (isBulletAuto)
            {
                transformAtImpact = new Vector3(gameObject.transform.position.x, GameLevelManager.instance.TerrainHeight, SniperManager.instance.PlayerPosAtShoot.z);
            }
            else
            {
                transformAtImpact = SniperManager.instance.PlayerPosAtShoot;
            }

            Vector3 spawnPoint = new Vector3(transformAtImpact.x, GameLevelManager.instance.TerrainHeight, transformAtImpact.z);
            Instantiate(impactSniperGroundPrefab, spawnPoint, Quaternion.identity);
            DestroyProjectile();
        }

        if (sniperProjectile
            && impactProjectile
            && MatchRuntime.Rules.Sniper != SniperMode.Laser
            && (other.gameObject.CompareTag("enemyHitbox")
            || other.gameObject.CompareTag("playerHitbox")))
        {
            if (other.gameObject.CompareTag("playerHitbox") && sniperProjectile)
            {
                GameLevelManager.instance.Player1.gameStats.Stats.SniperHits++;
            }

            Vector3 transformAtImpact = SniperManager.instance.PlayerPosAtShoot;
            Vector3 spawnPoint = new Vector3(transformAtImpact.x, GameLevelManager.instance.TerrainHeight, transformAtImpact.z);
            Debug.Log("sniper hit : " + transformAtImpact);
            Instantiate(impactSniperPlayerPrefab, spawnPoint, Quaternion.identity);
            DestroyProjectile();
        }
    }
}
