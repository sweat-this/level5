using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

/// <summary>
/// AUD-009: structurally near-identical to <see cref="RuntimeObjectPool"/> (same <see cref="ObjectPool{T}"/>
/// backing, same capacity), but projectiles need a post-activation <c>Launch()</c> step that generic
/// pooled objects don't, so this stays a separate implementation rather than a forced merge. It was
/// missing the scene-unload/domain-reload cleanup <see cref="RuntimeObjectPool"/> already has -
/// without it, <see cref="poolsByPrefab"/> is a static dictionary that outlives every scene load,
/// accumulating pools whose instances were destroyed with the scene they belonged to. Fixed by
/// mirroring that same proven pattern here.
/// </summary>
public static class ProjectilePool
{
    private static readonly Dictionary<GameObject, ObjectPool<GameObject>> poolsByPrefab = new Dictionary<GameObject, ObjectPool<GameObject>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        poolsByPrefab.Clear();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        foreach (ObjectPool<GameObject> pool in poolsByPrefab.Values)
        {
            pool.Clear();
        }

        poolsByPrefab.Clear();
    }

    public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Action<GameObject> configure = null)
    {
        if (prefab == null)
        {
            return null;
        }

        ObjectPool<GameObject> pool = GetPool(prefab);
        GameObject instance = pool.Get();
        PooledProjectile pooledProjectile = instance.GetComponent<PooledProjectile>();
        pooledProjectile.PrepareForGet(pool);

        instance.transform.SetPositionAndRotation(position, rotation);
        ResetRigidbody(instance);
        PrepareProjectileComponents(instance);
        configure?.Invoke(instance);
        instance.SetActive(true);
        LaunchProjectileComponents(instance);

        return instance;
    }

    public static void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledProjectile pooledProjectile = instance.GetComponent<PooledProjectile>();
        if (pooledProjectile == null)
        {
            UnityEngine.Object.Destroy(instance);
            return;
        }

        pooledProjectile.Release();
    }

    public static void ReleaseAfter(GameObject instance, float seconds)
    {
        if (instance == null)
        {
            return;
        }

        PooledProjectile pooledProjectile = instance.GetComponent<PooledProjectile>();
        if (pooledProjectile == null)
        {
            UnityEngine.Object.Destroy(instance, seconds);
            return;
        }

        pooledProjectile.ReleaseAfter(seconds);
    }

    private static ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (poolsByPrefab.TryGetValue(prefab, out ObjectPool<GameObject> pool))
        {
            return pool;
        }

        pool = new ObjectPool<GameObject>(
            () => CreatePooledInstance(prefab),
            null,
            pooledObject => pooledObject.SetActive(false),
            pooledObject => UnityEngine.Object.Destroy(pooledObject),
            false,
            8,
            64);

        poolsByPrefab[prefab] = pool;
        return pool;
    }

    private static GameObject CreatePooledInstance(GameObject prefab)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.SetActive(false);

        PooledProjectile pooledProjectile = instance.GetComponent<PooledProjectile>();
        if (pooledProjectile == null)
        {
            pooledProjectile = instance.AddComponent<PooledProjectile>();
        }

        return instance;
    }

    private static void ResetRigidbody(GameObject instance)
    {
        Rigidbody projectileRigidbody = instance.transform.root.GetComponent<Rigidbody>();
        if (projectileRigidbody == null)
        {
            return;
        }

        projectileRigidbody.linearVelocity = Vector3.zero;
        projectileRigidbody.angularVelocity = Vector3.zero;
    }

    private static void PrepareProjectileComponents(GameObject instance)
    {
        foreach (PlayerProjectile projectile in instance.GetComponentsInChildren<PlayerProjectile>(true))
        {
            projectile.PrepareForPooledSpawn();
        }

        foreach (EnemyProjectile projectile in instance.GetComponentsInChildren<EnemyProjectile>(true))
        {
            projectile.PrepareForPooledSpawn();
        }
    }

    private static void LaunchProjectileComponents(GameObject instance)
    {
        foreach (PlayerProjectile projectile in instance.GetComponentsInChildren<PlayerProjectile>(true))
        {
            projectile.Launch();
        }

        foreach (EnemyProjectile projectile in instance.GetComponentsInChildren<EnemyProjectile>(true))
        {
            projectile.Launch();
        }
    }
}
