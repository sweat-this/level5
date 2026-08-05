public static class MatchSession
{
    public static string BeginNewMatch()
    {
        GameOptions.matchResultId = ProgressionService.CreateResultId("match");
        return GameOptions.matchResultId;
    }

    public static string EnsureCurrentMatch()
    {
        return string.IsNullOrEmpty(GameOptions.matchResultId)
            ? BeginNewMatch()
            : GameOptions.matchResultId;
    }
}
