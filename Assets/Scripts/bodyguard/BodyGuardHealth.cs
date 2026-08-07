using UnityEngine;

/// <summary>
/// Bodyguard health. Everything except how max health is chosen lives in
/// <see cref="ActorHealth"/> (AUD-004).
/// </summary>
public class BodyGuardHealth : ActorHealth
{
    [SerializeField]
    BodyGuardController bodyGuardController;
    [SerializeField]
    bool isBoss;

    const int DefaultBodyGuardHealth = 100;

    public bool IsBoss { get => isBoss; set => isBoss = value; }

    private void Start()
    {
        // bodyguards are all the same size regardless of the boss flag - unlike enemies, which
        // scale off EnemyController.IsBoss. Kept as-is; the commented-out per-role values that
        // used to sit here were never enabled.
        ResetToMaxHealth(DefaultBodyGuardHealth);

        bodyGuardController = transform.parent != null
            ? transform.parent.GetComponent<BodyGuardController>()
            : null;
    }
}
