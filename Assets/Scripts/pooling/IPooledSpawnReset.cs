/// <summary>
/// Implemented by any component on a pooled prefab that must clear per-life state before the
/// instance is handed out again.
///
/// AUD-009: pooled objects used to reset purely by convention, in <c>OnEnable</c>. That works, but
/// nothing declared it, so a new pooled prefab category could simply forget and silently reuse the
/// previous life's state - and the symptom (an enemy that spawns already dead, a vehicle still
/// driving its last route) points nowhere near the cause.
///
/// <see cref="RuntimeObjectPool.Spawn"/> calls this on every component that implements it, while
/// the instance is still inactive and <b>before</b> the caller's <c>configure</c> callback runs.
/// That ordering is the point: reset clears the previous life, then configure applies this life's
/// values, then the object is activated so <c>OnEnable</c> sees both.
///
/// Two rules for implementers:
///
/// <list type="bullet">
/// <item><description>
/// Be idempotent. Types that also reset from <c>OnEnable</c> still do, and that fires after this,
/// so <c>ResetForSpawn</c> runs at least twice per spawn.
/// </description></item>
/// <item><description>
/// Implement it on the prefab's root coordinator only, not on every component it already resets.
/// <c>EnemyController</c> implements this and cascades to <c>EnemyHealth</c>; if both implemented
/// it, health would reset three times per spawn and any diagnostic it logs would triple.
/// </description></item>
/// </list>
///
/// The instance is inactive when this runs, so no <c>StartCoroutine</c> or <c>InvokeRepeating</c>
/// here - both throw on an inactive GameObject. Field resets, rigidbody clears, and animator
/// rebinds are fine.
/// </summary>
public interface IPooledSpawnReset
{
    /// <summary>Clears state left over from this instance's previous life.</summary>
    void ResetForSpawn();
}
