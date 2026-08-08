using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// The authoritative description of one match: validated once, then read-only.
    ///
    /// Nothing on this object or the objects it holds has a setter. That is the whole point - the
    /// old model let any script change the rules after the match had started, so which value was
    /// current depended on script execution order. If a system needs something that changes while
    /// playing, that belongs in a runtime owner, not here.
    /// </summary>
    public sealed class MatchConfiguration
    {
        public MatchConfiguration(
            GameModeDefinition mode,
            LevelDefinition level,
            PlayerRoster roster,
            MatchModifiers modifiers,
            ResolvedMatchRules rules,
            CheerleaderSelection cheerleader = null,
            string source = null)
        {
            Mode = mode != null ? mode : throw new ArgumentNullException(nameof(mode));
            Level = level != null ? level : throw new ArgumentNullException(nameof(level));
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Modifiers = modifiers ?? MatchModifiers.Default;
            Cheerleader = cheerleader ?? CheerleaderSelection.None;
            Source = string.IsNullOrEmpty(source) ? "unknown" : source;

            if (Roster.Count == 0)
            {
                throw new ArgumentException("a match configuration needs at least one participant", nameof(roster));
            }
        }

        public GameModeDefinition Mode { get; }

        public LevelDefinition Level { get; }

        public PlayerRoster Roster { get; }

        public MatchModifiers Modifiers { get; }

        public ResolvedMatchRules Rules { get; }

        public CheerleaderSelection Cheerleader { get; }

        /// <summary>Which launch source produced this. Diagnostics only.</summary>
        public string Source { get; }

        public GameModeId ModeId => Mode.Id;

        public int LevelId => Level.LevelId;

        /// <summary>The scene to load for this match.</summary>
        public string SceneName => Level.SceneName;

        public override string ToString()
        {
            return $"{Mode.DisplayName} @ {Level.DisplayName} ({Roster.Count} participant(s))";
        }
    }
}
