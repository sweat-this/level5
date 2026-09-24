/// <summary>
/// The Stats screen's existing high-score row presentation fields, filled directly from a Backend V2
/// leaderboard entry by <see cref="LeaderboardEntryPresentationMapper"/> - never a
/// <c>StatsTableHighScoreRow</c> instance used as a wire DTO or throwaway data carrier.
/// </summary>
public sealed class LeaderboardRowPresentation
{
    public string UserName;
    public string Score;
    public string Character;
    public string Level;
    public string Date;
    public string HardcoreEnabled;
}
