using System.Collections.Generic;
using Level5.Core.Match;

/// <summary>
/// Builds a <see cref="GameModeDefinition"/> from the legacy <see cref="StartScreenModeSelected"/>
/// component authored on the start menu prefab.
///
/// This is the migration seam. While it exists the prefab stays the authored source of truth and
/// nothing has to be re-entered by hand, so the new model cannot drift from the shipping data. Once
/// the editor migration has written definition assets and the parity validator is green, the assets
/// become the source and this becomes the validator's "what the old data said" side.
///
/// Two kinds of field are filled in here rather than read:
///
/// - semantic dimensions the legacy component never carried (objective, roster bounds). The tables
///   below are the migration seed for those, and are the one place the mapping is written down.
/// - anything the legacy booleans can express that the rule dimensions cannot represent. Those are
///   reported as anomalies rather than quietly resolved, because a quiet resolution is a behaviour
///   change nobody sees. The clock is the only remaining one: a mode cannot count up and down at
///   once. Combinations that looked like contradictions - the all-point contest setting three
///   contest flags, Cage Match being marked battle royal too - turned out to be load-bearing, so
///   the dimensions carry them as flag sets instead.
/// </summary>
public static class GameModeDefinitionFactory
{
    /// <summary>What a conversion produced, including anything that could not be represented.</summary>
    public sealed class Conversion
    {
        public Conversion(GameModeDefinition definition, IReadOnlyList<string> anomalies)
        {
            Definition = definition;
            Anomalies = anomalies ?? new List<string>();
        }

        public GameModeDefinition Definition { get; }

        /// <summary>Authored states the new model cannot represent exactly. Empty means a clean conversion.</summary>
        public IReadOnlyList<string> Anomalies { get; }
    }

    public static GameModeDefinition Create(StartScreenModeSelected source)
    {
        return Convert(source).Definition;
    }

    public static Conversion Convert(StartScreenModeSelected source)
    {
        if (source == null)
        {
            return new Conversion(null, new List<string> { "a mode entry on the start menu prefab is empty" });
        }

        List<string> anomalies = new List<string>();
        string label = string.IsNullOrEmpty(source.ModeDisplayName) ? "mode " + source.ModeId : source.ModeDisplayName;

        GameModeDefinitionData data = GameModeDefinitionData.Default(source.ModeId);
        data.DisplayName = source.ModeDisplayName;
        data.ObjectName = source.ModeObjectName;
        data.Description = source.ModeDescription;
        data.HighScoreField = source.HighScoreField;

        data.ClockMode = ResolveClockMode(source, label, anomalies);
        data.ShotRule = ResolveShotRule(source, label, anomalies);
        data.CombatMode = ResolveCombatMode(source, label, anomalies);
        data.ShotMarkers = ResolveShotMarkers(source);

        data.CustomTimerSeconds = source.CustomTimer > 0f ? source.CustomTimer : 0f;
        data.RequiresBasketball = source.GameModeRequiresBasketball;
        data.RequiresMoneyBall = source.ModeRequiresMoneyBall;
        data.RequiresConsecutiveShots = source.ModeRequiresConsecutiveShots;
        data.RequiresPlayerSurvive = source.GameModeRequiresPlayerSurvive;
        data.AllowsCpuShooters = source.GameModeAllowsCpuShooters;
        data.EnemiesOnly = source.EnemiesOnlyEnabled;
        data.ArcadeMode = source.ArcadeModeActive;

        data.Objective = ResolveObjective(source, data);
        ApplyRosterRules(source.ModeId, ref data);
        ApplyArenaRequirements(ref data);

        return new Conversion(GameModeDefinition.Create(data), anomalies);
    }

    /// <summary>Converts every authored mode, collecting anomalies across the whole catalog.</summary>
    public static List<GameModeDefinition> CreateAll(
        IEnumerable<StartScreenModeSelected> sources,
        List<string> anomalies = null)
    {
        List<GameModeDefinition> definitions = new List<GameModeDefinition>();
        if (sources == null)
        {
            return definitions;
        }

        foreach (StartScreenModeSelected source in sources)
        {
            Conversion conversion = Convert(source);
            if (conversion.Definition != null)
            {
                definitions.Add(conversion.Definition);
            }

            if (anomalies != null)
            {
                anomalies.AddRange(conversion.Anomalies);
            }
        }

        return definitions;
    }

    private static MatchClockMode ResolveClockMode(StartScreenModeSelected source, string label, List<string> anomalies)
    {
        if (source.ModeRequiresCountDown && source.ModeRequiresCounter)
        {
            anomalies.Add($"{label} sets both modeRequiresCountDown and modeRequiresCounter; treating it as a countdown");
            return MatchClockMode.Countdown;
        }

        if (source.ModeRequiresCountDown)
        {
            return MatchClockMode.Countdown;
        }

        return source.ModeRequiresCounter ? MatchClockMode.CountUp : MatchClockMode.None;
    }

    /// <summary>
    /// The contest flags, carried across exactly.
    ///
    /// The authored "all point contest" sets the three point and four point flags as well as the
    /// all-ranges one, and gameplay reads each of those separately - so this is a set, not a choice.
    /// An earlier version of this took "the widest one" and the parity validator caught it dropping
    /// two flags the shipping data relies on.
    /// </summary>
    private static ShotRule ResolveShotRule(StartScreenModeSelected source, string label, List<string> anomalies)
    {
        ShotRule rule = ShotRule.Any;
        if (source.GameModeThreePointContest)
        {
            rule |= ShotRule.ThreePoint;
        }

        if (source.GameModeFourPointContest)
        {
            rule |= ShotRule.FourPoint;
        }

        if (source.GameModeSevenPointContest)
        {
            rule |= ShotRule.SevenPoint;
        }

        if (source.GameModeAllPointContest)
        {
            rule |= ShotRule.AllRanges;
        }

        return rule;
    }

    /// <summary>
    /// The combat flags, carried across exactly - Cage Match is authored as both a cage match and a
    /// battle royal, and the level filter reads both.
    /// </summary>
    private static CombatMode ResolveCombatMode(StartScreenModeSelected source, string label, List<string> anomalies)
    {
        CombatMode combat = CombatMode.None;
        if (source.IsBattleRoyal)
        {
            combat |= CombatMode.BattleRoyal;
        }

        if (source.IsCageMatch)
        {
            combat |= CombatMode.Cage;
        }

        if (combat == CombatMode.None && source.EnemiesOnlyEnabled)
        {
            combat = CombatMode.Standard;
        }

        return combat;
    }

    private static ShotMarkerRequirement ResolveShotMarkers(StartScreenModeSelected source)
    {
        ShotMarkerRequirement markers = ShotMarkerRequirement.None;
        if (source.ModeRequiresShotMarkers3S)
        {
            markers |= ShotMarkerRequirement.ThreePoint;
        }

        if (source.ModeRequiresShotMarkers4S)
        {
            markers |= ShotMarkerRequirement.FourPoint;
        }

        if (source.ModeRequiresShotMarkers7s)
        {
            markers |= ShotMarkerRequirement.SevenPoint;
        }

        return markers;
    }

    /// <summary>
    /// The objective a mode is actually played for. The legacy component never carried this, so the
    /// per-mode table is the migration seed; ids it does not list fall back to what the mode's own
    /// flags imply, which is what keeps a mode added later from silently becoming "score".
    /// </summary>
    private static MatchObjective ResolveObjective(StartScreenModeSelected source, GameModeDefinitionData data)
    {
        switch (source.ModeId)
        {
            case Modes.Total3Pointers:
            case Modes.Total4Pointers:
            case Modes.Total7Pointers:
                return MatchObjective.MakeCount;

            case Modes.TotalDistance:
                return MatchObjective.Distance;

            case Modes.ConsecutiveShots:
                return MatchObjective.ConsecutiveShots;

            case Modes.SpotUp3s:
            case Modes.SpotUp4s:
            case Modes.SpotUp7s:
            case Modes.SpotUpAll:
            case Modes.ThreePointContest:
            case Modes.FourPointContest:
            case Modes.SevenPointContest:
            case Modes.AllPointContest:
                return MatchObjective.ContestCompletion;

            case Modes.BattleRoyal:
                return MatchObjective.LastPlayerStanding;

            case Modes.CageMatch:
                return MatchObjective.Survival;

            case Modes.BeatThaComputahs:
                return MatchObjective.CampaignProgression;

            case Modes.TotalPoints:
            case Modes.InThePocket:
            case Modes.PointsByDistance:
            case Modes.BashUpSomeNerds:
            case Modes.VersusCpu:
            case Modes.Lockdown:
            case Modes.ArcadeMode:
            case Modes.FreePlay:
                return MatchObjective.Score;

            default:
                return InferObjective(data);
        }
    }

    private static MatchObjective InferObjective(GameModeDefinitionData data)
    {
        if ((data.CombatMode & CombatMode.BattleRoyal) != 0)
        {
            return MatchObjective.LastPlayerStanding;
        }

        if (data.RequiresPlayerSurvive)
        {
            return MatchObjective.Survival;
        }

        if (data.RequiresConsecutiveShots)
        {
            return MatchObjective.ConsecutiveShots;
        }

        if (data.ShotMarkers != ShotMarkerRequirement.None || data.ShotRule != ShotRule.Any)
        {
            return MatchObjective.ContestCompletion;
        }

        return MatchObjective.Score;
    }

    /// <summary>
    /// Roster bounds the legacy data never stated but the launch path enforced in code:
    /// Lockdown never adds the selected CPUs (it brings its own defender), and the two
    /// versus modes always play against at least one CPU whether or not the player picked one.
    /// </summary>
    private static void ApplyRosterRules(int modeId, ref GameModeDefinitionData data)
    {
        switch (modeId)
        {
            case Modes.Lockdown:
                data.MinPlayers = 1;
                data.MaxPlayers = 1;
                data.AddsImplicitDefender = true;
                break;

            case Modes.VersusCpu:
            case Modes.BeatThaComputahs:
                data.MinPlayers = 2;
                data.MaxPlayers = PlayerRoster.MaxSlots;
                data.RequiresCpuOpponent = true;
                break;
        }
    }

    /// <summary>
    /// A mode that needs seven point markers needs an arena that has a seven point line to put them
    /// on. The old menu never checked this, so the combination was selectable and unplayable; it is
    /// authored as a requirement here so the one place that decides compatibility can see it.
    /// </summary>
    private static void ApplyArenaRequirements(ref GameModeDefinitionData data)
    {
        if ((data.ShotMarkers & ShotMarkerRequirement.SevenPoint) != 0)
        {
            data.RequiredArenaCapabilities |= ArenaCapability.SevenPointLine;
        }
    }
}
