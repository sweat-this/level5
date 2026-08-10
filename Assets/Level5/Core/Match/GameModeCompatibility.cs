using System.Collections.Generic;

namespace Level5.Core.Match
{
    /// <summary>
    /// The single owner of "can this be played?".
    ///
    /// This used to be spread across recursive selection methods in <c>StartManager</c>, which meant
    /// the rules lived in navigation code, could not be tested without a UI scene, and would have
    /// been reimplemented differently by every future launch source. It is a pure service: no Unity
    /// objects, no globals, no side effects.
    ///
    /// The menu asks it which entries to offer; the builder asks it again at launch. UI filtering is
    /// convenience, the launch check is the authority.
    ///
    /// The arena rules below are the old menu conditions translated one for one. Where legacy had a
    /// rule only in one direction (a cage mode needs a cage arena, but a cage arena does not refuse
    /// other modes) that asymmetry is preserved deliberately - this migration changes ownership, not
    /// which combinations are playable.
    /// </summary>
    public sealed class GameModeCompatibility
    {
        private readonly GameModeCatalog modes;
        private readonly LevelDefinitionCatalog levels;

        public GameModeCompatibility(GameModeCatalog modes, LevelDefinitionCatalog levels)
        {
            this.modes = modes ?? GameModeCatalog.Empty();
            this.levels = levels ?? LevelDefinitionCatalog.Empty();
        }

        public GameModeCatalog Modes => modes;

        public LevelDefinitionCatalog Levels => levels;

        /// <summary>Full check of a request: mode, level, roster and modifiers together.</summary>
        public ValidationResult Validate(MatchRequest request)
        {
            ValidationResult.Builder builder = new ValidationResult.Builder();

            if (request == null)
            {
                builder.Add(MatchValidationCode.UnknownMode, "no match was requested");
                return builder.Build();
            }

            GameModeDefinition mode = modes.Find(request.ModeId);
            if (mode == null)
            {
                builder.Add(
                    MatchValidationCode.UnknownMode,
                    $"game mode {(int)request.ModeId} is not in the mode catalog");
            }

            LevelDefinition level = levels.Find(request.LevelId);
            if (level == null)
            {
                builder.Add(
                    MatchValidationCode.UnknownLevel,
                    $"level {request.LevelId} is not in the level catalog");
            }

            if (mode == null || level == null)
            {
                return builder.Build();
            }

            AddArenaErrors(builder, mode, level);
            AddRosterErrors(builder, mode, level, request.Roster, request.Modifiers);

            return builder.Build();
        }

        /// <summary>
        /// The single fighter/shooter capability query. Player select and full roster validation
        /// both call this rather than each re-deriving the rule, so they cannot drift apart.
        ///
        /// A fighting setup needs a fighter, a shooting setup needs a shooter. Enemies switched on
        /// as a modifier makes a shooting mode a fighting one for this purpose, exactly as the
        /// legacy menu treated it. An empty character (no selection yet) is reported playable so
        /// callers that have not resolved a character do not get a spurious rejection from this
        /// query alone.
        ///
        /// Unlock/account availability is not part of this method - it is not a property of the
        /// game mode.
        /// </summary>
        public static bool CharacterCanPlay(GameModeDefinition mode, MatchModifiers modifiers, CharacterSelection character)
        {
            if (character == null || character.IsEmpty)
            {
                return true;
            }

            bool fightersRequired = (mode != null && mode.EnemiesOnly) || (modifiers != null && modifiers.EnemiesRequested);
            return fightersRequired ? character.IsFighter : character.IsShooter;
        }

        /// <summary>Whether a mode and an arena fit, ignoring roster and modifiers.</summary>
        public bool CanPlay(GameModeDefinition mode, LevelDefinition level)
        {
            if (mode == null || level == null)
            {
                return false;
            }

            ValidationResult.Builder builder = new ValidationResult.Builder();
            AddArenaErrors(builder, mode, level);
            return !builder.HasErrors;
        }

        /// <summary>Every arena the given mode can be played in, in catalog order.</summary>
        public IReadOnlyList<LevelDefinition> LevelsFor(GameModeDefinition mode)
        {
            List<LevelDefinition> compatible = new List<LevelDefinition>();
            if (mode == null)
            {
                return compatible;
            }

            foreach (LevelDefinition level in levels.Definitions)
            {
                if (CanPlay(mode, level))
                {
                    compatible.Add(level);
                }
            }

            return compatible;
        }

        /// <summary>Every mode the given arena can host, in catalog order.</summary>
        public IReadOnlyList<GameModeDefinition> ModesFor(LevelDefinition level)
        {
            List<GameModeDefinition> compatible = new List<GameModeDefinition>();
            if (level == null)
            {
                return compatible;
            }

            foreach (GameModeDefinition mode in modes.Definitions)
            {
                if (CanPlay(mode, level))
                {
                    compatible.Add(mode);
                }
            }

            return compatible;
        }

        /// <summary>
        /// The next compatible level index for a mode, stepping by <paramref name="step"/> from
        /// <paramref name="startIndex"/> and wrapping.
        ///
        /// This replaces <c>changeSelectedLevelUp()</c> calling itself: a bounded walk over the
        /// catalog terminates whether or not any level is compatible, where the recursion did not -
        /// with no compatible level it recursed until the stack ran out. Returns
        /// <paramref name="startIndex"/> when nothing fits, so the menu holds still instead.
        /// </summary>
        public int NextCompatibleLevelIndex(GameModeDefinition mode, int startIndex, int step)
        {
            int count = levels.Count;
            if (mode == null || count == 0 || step == 0)
            {
                return startIndex;
            }

            for (int offset = 1; offset <= count; offset++)
            {
                int index = IndexMath.Wrap(startIndex + (offset * step), count);
                if (CanPlay(mode, levels.Definitions[index]))
                {
                    return index;
                }
            }

            return startIndex;
        }

        /// <summary>
        /// The compatible level index to hold after a mode change: the current one if it still
        /// fits, otherwise the nearest one in the given direction.
        /// </summary>
        public int CompatibleLevelIndexFor(GameModeDefinition mode, int currentIndex, int step)
        {
            if (mode == null || levels.Count == 0)
            {
                return currentIndex;
            }

            int wrapped = IndexMath.Wrap(currentIndex, levels.Count);
            if (CanPlay(mode, levels.Definitions[wrapped]))
            {
                return wrapped;
            }

            return NextCompatibleLevelIndex(mode, wrapped, step);
        }

        private static void AddArenaErrors(ValidationResult.Builder builder, GameModeDefinition mode, LevelDefinition level)
        {
            // The two halves of the old "is shooting level"/"is fighting level" condition. Note it
            // keys off EnemiesOnly, not off whether the mode happens to use a ball, because that is
            // what the menu did.
            builder.AddIf(
                !mode.EnemiesOnly && !level.Supports(ArenaCapability.Basketball),
                MatchValidationCode.ArenaLacksBasketball,
                $"'{level.DisplayName}' has no basketball setup, so '{mode.DisplayName}' cannot be played there");

            builder.AddIf(
                mode.EnemiesOnly && !level.Supports(ArenaCapability.Combat),
                MatchValidationCode.ArenaLacksCombat,
                $"'{level.DisplayName}' has no combat setup, so '{mode.DisplayName}' cannot be played there");

            builder.AddIf(
                mode.IsCageMatch && !level.Supports(ArenaCapability.Cage),
                MatchValidationCode.ArenaLacksCage,
                $"'{level.DisplayName}' is not a cage arena");

            builder.AddIf(
                mode.IsBattleRoyal && !level.Supports(ArenaCapability.BattleRoyal),
                MatchValidationCode.ArenaLacksBattleRoyal,
                $"'{level.DisplayName}' is not a battle royal arena");

            // The old menu also rejected the reverse - a battle royal arena under any other mode.
            builder.AddIf(
                !mode.IsBattleRoyal && level.Supports(ArenaCapability.BattleRoyal),
                MatchValidationCode.ArenaForbidsMode,
                $"'{level.DisplayName}' is a battle royal arena and only hosts battle royal");

            ArenaCapability forbidden = mode.ForbiddenArenaCapabilities & level.Capabilities;
            builder.AddIf(
                forbidden != ArenaCapability.None,
                MatchValidationCode.ArenaForbidsMode,
                $"'{mode.DisplayName}' cannot be played in an arena with {forbidden}");

            // Authored requirements. The mode factory sets SevenPointLine here for the seven-point
            // modes, which is the one combination the old menu let through and the arena cannot
            // actually serve: no seven point line means no seven pointers to make.
            ArenaCapability missing = mode.RequiredArenaCapabilities & ~level.Capabilities;
            if (missing != ArenaCapability.None)
            {
                MatchValidationCode code = missing == ArenaCapability.SevenPointLine
                    ? MatchValidationCode.ArenaLacksSevenPointLine
                    : MatchValidationCode.ArenaForbidsMode;
                builder.Add(code, $"'{level.DisplayName}' is missing {missing} required by '{mode.DisplayName}'");
            }
        }

        private static void AddRosterErrors(
            ValidationResult.Builder builder,
            GameModeDefinition mode,
            LevelDefinition level,
            PlayerRoster roster,
            MatchModifiers modifiers)
        {
            if (roster == null || roster.Count == 0)
            {
                builder.Add(MatchValidationCode.RosterEmpty, "a match needs at least one participant");
                return;
            }

            builder.AddIf(
                roster.Count < mode.MinPlayers,
                MatchValidationCode.RosterTooSmall,
                $"'{mode.DisplayName}' needs at least {mode.MinPlayers} participant(s)");

            builder.AddIf(
                roster.Count > mode.MaxPlayers,
                MatchValidationCode.RosterTooLarge,
                $"'{mode.DisplayName}' allows at most {mode.MaxPlayers} participant(s)");

            builder.AddIf(
                roster.Count > PlayerRoster.MaxSlots,
                MatchValidationCode.RosterTooLarge,
                $"a match supports at most {PlayerRoster.MaxSlots} participants");

            builder.AddIf(
                roster.LocalHumanCount > 1 && !level.Supports(ArenaCapability.Multiplayer),
                MatchValidationCode.ArenaLacksMultiplayer,
                $"'{level.DisplayName}' does not support local multiplayer");

            // Fighting modes need fighters and shooting modes need shooters - CharacterCanPlay is
            // the same query player select uses while cycling characters, so the two cannot drift.
            bool fightersRequired = mode.EnemiesOnly || (modifiers != null && modifiers.EnemiesRequested);

            foreach (PlayerSlot slot in roster.Players)
            {
                if (slot.ControlType == PlayerControlType.RemoteHuman
                    || slot.ControlType == PlayerControlType.ReplayGhost)
                {
                    builder.Add(
                        MatchValidationCode.ParticipantTypeNotSupported,
                        $"{slot.ControlType} participants are not supported by this build");
                    continue;
                }

                if (slot.Character == null || slot.Character.IsEmpty || CharacterCanPlay(mode, modifiers, slot.Character))
                {
                    continue;
                }

                builder.Add(
                    fightersRequired ? MatchValidationCode.CharacterCannotFight : MatchValidationCode.CharacterCannotShoot,
                    fightersRequired
                        ? $"{slot.Character} cannot fight, so cannot play '{mode.DisplayName}'"
                        : $"{slot.Character} cannot shoot, so cannot play '{mode.DisplayName}'");
            }
        }
    }
}
