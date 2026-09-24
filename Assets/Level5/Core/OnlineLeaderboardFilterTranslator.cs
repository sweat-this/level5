/// <summary>
/// The Stats screen's four filter toggles (hardcore/traffic/enemies/sniper), translated into
/// Backend V2 leaderboard query values.
///
/// All four toggles off means "show all results for the mode" - all four query parameters are
/// omitted (null), not sent as false. Any toggle on means the exact four-value combination is sent
/// explicitly, including the ones left off (false). Deliberately does not reproduce the legacy
/// <c>APIHelper</c> behavior of forcing <c>enemies = true</c> for modes 20-22 - Backend V2 owns that
/// policy server-side now.
/// </summary>
public readonly struct OnlineLeaderboardFilterQuery
{
    public OnlineLeaderboardFilterQuery(bool? hardcore, bool? traffic, bool? enemies, bool? sniper)
    {
        Hardcore = hardcore;
        Traffic = traffic;
        Enemies = enemies;
        Sniper = sniper;
    }

    public bool? Hardcore { get; }

    public bool? Traffic { get; }

    public bool? Enemies { get; }

    public bool? Sniper { get; }
}

public static class OnlineLeaderboardFilterTranslator
{
    public static OnlineLeaderboardFilterQuery Translate(bool hardcore, bool traffic, bool enemies, bool sniper)
    {
        bool anyEnabled = hardcore || traffic || enemies || sniper;
        if (!anyEnabled)
        {
            return new OnlineLeaderboardFilterQuery(null, null, null, null);
        }

        return new OnlineLeaderboardFilterQuery(hardcore, traffic, enemies, sniper);
    }
}
