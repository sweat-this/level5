using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public static class RuntimeObjectPool
{
    private static readonly Dictionary<GameObject, ObjectPool<GameObject>> Pools = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Pools.Clear();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    public static GameObject Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        Action<GameObject> configure = null)
    {
        if (prefab == null)
        {
            return null;
        }

        ObjectPool<GameObject> pool = GetPool(prefab);
        GameObject instance = pool.Get();
        if (instance == null)
        {
            return null;
        }

        // CreateInstance always adds this, but Release tolerates its absence, so Spawn should too
        PooledRuntimeObject pooledObject = instance.GetComponent<PooledRuntimeObject>();
        if (pooledObject != null)
        {
            pooledObject.Prepare(pool);
        }

        instance.transform.SetPositionAndRotation(position, rotation);

        // AUD-009: the order here is the contract. Reset clears the previous life while the object
        // is still inactive, then configure applies this life's values, then activation lets
        // OnEnable see both. Resetting after configure would silently discard per-spawn setup -
        // currently latent (no pooled type resets a field its caller configures) but the kind of
        // coupling that only shows up once someone adds one.
        ResetForSpawn(instance);
        configure?.Invoke(instance);
        instance.SetActive(true);
        return instance;
    }

    private static readonly List<IPooledSpawnReset> ResetBuffer = new();

    private static void ResetForSpawn(GameObject instance)
    {
        // non-allocating overload - Spawn can run several times a second in traffic-heavy scenes
        instance.GetComponentsInChildren(true, ResetBuffer);
        for (int i = 0; i < ResetBuffer.Count; i++)
        {
            ResetBuffer[i].ResetForSpawn();
        }

        ResetBuffer.Clear();
    }

    public static void Release(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        PooledRuntimeObject pooledObject = instance.GetComponent<PooledRuntimeObject>();
        if (pooledObject == null)
        {
            UnityEngine.Object.Destroy(instance);
            return;
        }

        pooledObject.Release();
    }

    private static ObjectPool<GameObject> GetPool(GameObject prefab)
    {
        if (Pools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
        {
            return pool;
        }

        pool = new ObjectPool<GameObject>(
            () => CreateInstance(prefab),
            null,
            instance => instance.SetActive(false),
            instance => UnityEngine.Object.Destroy(instance),
            false,
            8,
            64);
        Pools.Add(prefab, pool);
        return pool;
    }

    private static GameObject CreateInstance(GameObject prefab)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        instance.SetActive(false);
        if (instance.GetComponent<PooledRuntimeObject>() == null)
        {
            instance.AddComponent<PooledRuntimeObject>();
        }

        return instance;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        foreach (ObjectPool<GameObject> pool in Pools.Values)
        {
            pool.Clear();
        }

        Pools.Clear();
    }
}

public sealed class PooledRuntimeObject : MonoBehaviour
{
    private IObjectPool<GameObject> pool;
    private bool released;

    public void Prepare(IObjectPool<GameObject> owner)
    {
        pool = owner;
        released = false;
    }

    public void Release()
    {
        if (released || pool == null)
        {
            return;
        }

        released = true;
        pool.Release(gameObject);
    }
}
