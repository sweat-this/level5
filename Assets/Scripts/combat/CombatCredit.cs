using UnityEngine;

/// <summary>
/// Resolves which player's stats a kill should be credited to.
///
/// Kill counts feed the end-of-match experience award, so crediting them to
/// BasketBall.instance - whichever ball ran Start() last - misattributed them in
/// local multiplayer. Attribution now goes through the attacker's registered player slot.
/// </summary>
public static class CombatCredit
{
    /// <summary>
    /// Stats for the player who owns <paramref name="attacker"/>, or the primary player's
    /// stats when the attacker is unknown (environment kills, friendly fire, self-inflicted).
    /// Returns null only when no player stats exist at all.
    /// </summary>
    public static GameStats ResolveKillCredit(GameObject attacker)
    {
        GameLevelManager manager = GameLevelManager.instance;
        if (attacker != null && manager != null && manager.players != null)
        {
            Transform attackerRoot = attacker.transform.root;
            foreach (PlayerIdentifier player in manager.players)
            {
                if (player == null || player.player == null || player.gameStats == null)
                {
                    continue;
                }

                if (player.player.transform.root == attackerRoot)
                {
                    return player.gameStats;
                }
            }
        }

        return PrimaryPlayerStats();
    }

    public static GameStats PrimaryPlayerStats()
    {
        GameLevelManager manager = GameLevelManager.instance;
        if (manager != null
            && manager.players != null
            && manager.players.Count > 0
            && manager.players[0] != null
            && manager.players[0].gameStats != null)
        {
            return manager.players[0].gameStats;
        }

        return BasketBall.instance != null ? BasketBall.instance.GameStats : null;
    }

    /// <summary>
    /// Records one enemy kill against the crediting player. Safe to call when no
    /// attacker is known and when no player stats are available.
    /// </summary>
    public static void CreditEnemyKill(GameObject attacker, bool isBoss)
    {
        GameStats stats = ResolveKillCredit(attacker);
        if (stats == null)
        {
            return;
        }

        stats.EnemiesKilled++;
        if (isBoss)
        {
            stats.BossKilled++;
        }
        else
        {
            stats.MinionsKilled++;
        }
    }
}
