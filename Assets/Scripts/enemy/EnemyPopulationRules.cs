using Level5.Core.Match;

/// <summary>
/// How many enemies a match may have alive, and how many of them may hold an attack reservation
/// at once.
///
/// ENM-2: these two numbers were derived independently - once in <see cref="EnemySpawner"/> and
/// once in <c>PlayerAttackQueue.GetMaxEnemiesQueued</c> - from the same rule inputs, by two
/// separate branch chains that do not agree. A non-hardcore battle-royal cage match spawns at most
/// four enemies while the queue admits twenty. That divergence is invisible today only because the
/// queue is never the binding constraint: its capacity is greater than or equal to the spawn cap in
/// every configuration, so it never turns an enemy away that the spawner allowed to exist.
///
/// Both numbers are reproduced here exactly as they were, rather than collapsed into one. They are
/// genuinely answering different questions, and picking one of the two existing answers would
/// change enemy counts in modes this could not be play-tested against. What changes is that they
/// now live together, so a difficulty or mode change edits one file and can see both consequences.
/// </summary>
public static class EnemyPopulationRules
{
    /// <summary>Battle royal's own cap, used by both answers below.</summary>
    public const int BattleRoyalCap = 20;

    private const int HardcoreEnemiesOnlyCap = 8;
    private const int HardcoreCap = 6;
    private const int StandardCap = 4;
    private const int BattleRoyalStagedCap = 2;

    /// <summary>
    /// The most enemies <see cref="EnemySpawner"/> will keep alive.
    ///
    /// <paramref name="hasConfiguration"/> is <c>MatchRuntime.HasConfiguration</c>. The branch
    /// order is load-bearing and documented in EnemySpawner (AUD-056): the hardcore cases win
    /// first, and a battle royal that is not a cage match is staged in gradually rather than
    /// spawned at full strength, which is why it resolves low here and is then raised to
    /// <see cref="BattleRoyalCap"/> by <see cref="MaxAliveForBattleRoyal"/>.
    /// </summary>
    public static int MaxAlive(ResolvedMatchRules rules, bool hasConfiguration, bool halveForMobile)
    {
        if (rules == null)
        {
            return StandardCap;
        }

        int cap;
        if (rules.Hardcore && rules.EnemiesOnly)
        {
            cap = HardcoreEnemiesOnlyCap;
        }
        else if (rules.Hardcore && !rules.EnemiesOnly)
        {
            cap = HardcoreCap;
        }
        else if (!rules.IsBattleRoyal || !hasConfiguration)
        {
            cap = StandardCap;
        }
        else if (rules.IsCageMatch)
        {
            cap = StandardCap;
        }
        else
        {
            cap = BattleRoyalStagedCap;
        }

        // Mobile halves the standing population. Deliberately not applied to the battle royal cap
        // below - that override happens after the halving in the original and is preserved as-is.
        return halveForMobile ? cap / 2 : cap;
    }

    /// <summary>
    /// The cap a battle royal that is not a cage match raises itself to once staging begins.
    /// </summary>
    public static int MaxAliveForBattleRoyal() => BattleRoyalCap;

    /// <summary>
    /// The most enemies that may simultaneously hold an attack reservation against one actor.
    ///
    /// Must stay greater than or equal to <see cref="MaxAlive"/> for the same rules, or enemies
    /// that the spawner allowed to exist will stand idle unable to reserve a slot - see the note
    /// on ENM-1 about what a denied reservation does to enemy behaviour.
    /// </summary>
    public static int MaxQueued(ResolvedMatchRules rules)
    {
        if (rules == null)
        {
            return StandardCap;
        }

        if (rules.IsBattleRoyal)
        {
            return BattleRoyalCap;
        }

        if (rules.EnemiesOnly && rules.Hardcore)
        {
            return HardcoreEnemiesOnlyCap;
        }

        if (!rules.EnemiesOnly && rules.Hardcore)
        {
            return HardcoreCap;
        }

        return StandardCap;
    }
}
