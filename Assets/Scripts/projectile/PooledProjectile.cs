using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class PooledProjectile : MonoBehaviour
{
    private ObjectPool<GameObject> pool;
    private Coroutine releaseCoroutine;
    private bool released = true;

    public void PrepareForGet(ObjectPool<GameObject> owningPool)
    {
        pool = owningPool;
        released = false;

        if (releaseCoroutine != null)
        {
            StopCoroutine(releaseCoroutine);
            releaseCoroutine = null;
        }
    }

    public void ReleaseAfter(float seconds)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (releaseCoroutine != null)
        {
            StopCoroutine(releaseCoroutine);
        }

        releaseCoroutine = StartCoroutine(ReleaseAfterSeconds(seconds));
    }

    public void Release()
    {
        if (released)
        {
            return;
        }

        released = true;

        if (releaseCoroutine != null)
        {
            StopCoroutine(releaseCoroutine);
            releaseCoroutine = null;
        }

        if (pool == null)
        {
            Destroy(gameObject);
            return;
        }

        pool.Release(gameObject);
    }

    private IEnumerator ReleaseAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Release();
    }
}
