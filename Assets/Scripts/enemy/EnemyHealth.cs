using UnityEngine;

/// <summary>
/// Enemy health. Everything except how max health is chosen lives in <see cref="ActorHealth"/>
/// (AUD-004).
/// </summary>
// deliberately does NOT implement IPooledSpawnReset: EnemyController is the pooled root and its
// ResetForSpawn already cascades here. Implementing it on both would reset health three times per
// spawn and triple the missing-controller error below (AUD-009).
public class EnemyHealth : ActorHealth
{
    [SerializeField]
    EnemyController enemyController;

    // the inspector-configured max, captured before any spawn reset rewrites it. ResetForSpawn
    // runs on every OnEnable (so on every pool reuse), and the hardcore bonus is applied each
    // time - it has to scale a stable base or it compounds across respawns.
    int configuredMaxEnemyHealth;

    const int DefaultSpawnHealth = 50;
    const int BossSpawnHealth = 150;

    private void Awake()
    {
        enemyController = gameObject.transform.root.GetComponent<EnemyController>();
        configuredMaxEnemyHealth = maxHealth;
    }

    private void OnEnable()
    {
        ResetForSpawn();
    }

    public void ResetForSpawn()
    {
        // this whole body used to be wrapped in `if (enemyController != null)`, so a missing
        // controller skipped the reset silently. With no controller AND no inspector-set health,
        // maxHealth stayed 0 - and the Health setter then clamps every write to zero, producing
        // an enemy that can neither take damage nor die.
        int spawnHealth;
        if (enemyController == null)
        {
            Debug.LogError(
                "EnemyHealth on " + name + " found no EnemyController on its hierarchy root, so it "
                + "cannot tell a boss from a minion. Falling back to its configured max health.",
                this);

            // respect a health value configured on the prefab; only the zero case is the bug
            spawnHealth = configuredMaxEnemyHealth > 0
                ? configuredMaxEnemyHealth
                : DefaultSpawnHealth;
        }
        else
        {
            // minion is checked before boss, matching the original precedence for an actor
            // that somehow carries both flags
            bool isBossSpawn = !enemyController.IsMinion && enemyController.IsBoss;
            spawnHealth = isBossSpawn ? BossSpawnHealth : DefaultSpawnHealth;
        }

        ResetToMaxHealth(spawnHealth);
    }
}
