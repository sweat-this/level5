using System;
using System.Globalization;
using Level5.BackendV2;

/// <summary>
/// Converts one Backend V2 <see cref="LeaderboardEntryDto"/> into the Stats screen's existing row
/// presentation fields (<see cref="LeaderboardRowPresentation"/>).
///
/// Lives in the default assembly, next to <see cref="StatsManager"/> - the same reason
/// <c>BackendV2MatchResultAdapter</c> does: it needs to sit between a <c>Level5.BackendV2</c> wire DTO
/// and default-assembly game content (<c>LoadedData</c>, character/level lookups), which
/// <c>Level5.BackendV2.asmdef</c> cannot see.
///
/// Character/level name resolution is passed in as delegates rather than calling
/// <c>LoadedData.instance</c> directly, so the actual mapping/formatting/fallback logic here is
/// testable without a loaded Unity scene.
/// </summary>
public static class LeaderboardEntryPresentationMapper
{
    public static LeaderboardRowPresentation Map(
        LeaderboardEntryDto entry,
        string metric,
        Func<int, string> resolveCharacterDisplayName,
        Func<int, string> resolveLevelDisplayName)
    {
        if (entry == null)
        {
            return new LeaderboardRowPresentation
            {
                UserName = "",
                Score = "",
                Character = "",
                Level = "",
                Date = "",
                HardcoreEnabled = ""
            };
        }

        return new LeaderboardRowPresentation
        {
            UserName = entry.Player != null ? entry.Player.DisplayName : "",
            Score = FormatValue(entry.Value, metric),
            Character = ResolveCharacter(entry.CharacterId, resolveCharacterDisplayName),
            Level = ResolveLevel(entry.LevelId, resolveLevelDisplayName),
            Date = entry.CreatedAt.ToLocalTime().ToString(),
            HardcoreEnabled = entry.Modifiers != null && entry.Modifiers.Hardcore ? "ON" : "OFF"
        };
    }

    /// <summary>
    /// Count-like metrics (TotalPoints/ShotsMade/LongestStreak/EnemiesKilled) render as plain
    /// integers; continuous metrics (TotalDistance/CompletionTimeSeconds) keep concise decimal
    /// precision. An unrecognized/future metric name falls back to the same concise decimal
    /// formatting rather than throwing or affecting row order.
    /// </summary>
    private static string FormatValue(double value, string metric)
    {
        switch (metric)
        {
            case "TotalPoints":
            case "ShotsMade":
            case "LongestStreak":
            case "EnemiesKilled":
                return Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
            case "TotalDistance":
            case "CompletionTimeSeconds":
            default:
                return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    private static string ResolveCharacter(string characterId, Func<int, string> resolveCharacterDisplayName)
    {
        if (resolveCharacterDisplayName != null
            && int.TryParse(characterId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            string name = resolveCharacterDisplayName(id);
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }
        }

        return characterId ?? "";
    }

    private static string ResolveLevel(int levelId, Func<int, string> resolveLevelDisplayName)
    {
        string name = resolveLevelDisplayName != null ? resolveLevelDisplayName(levelId) : null;
        return !string.IsNullOrEmpty(name) ? name : "Level " + levelId.ToString(CultureInfo.InvariantCulture);
    }
}
