using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public static class ProjectilePool
{
    private static readonly Dictionary<GameObject, ObjectPool<GameObject>> poolsByPrefab = new Dictionary<GameObject, ObjectPool<GameObject>>();

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
