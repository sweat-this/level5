using System;
using Level5.Core.Progression;

namespace Level5.Core.Match
{
    /// <summary>The outcome of asking the builder for a match: either a configuration or reasons.</summary>
    public readonly struct MatchBuildResult
    {
        private MatchBuildResult(MatchConfiguration configuration, ValidationResult validation)
        {
            Configuration = configuration;
            Validation = validation;
        }

        public MatchConfiguration Configuration { get; }

        public ValidationResult Validation { get; }

        public bool Succeeded => Configuration != null;

        public static MatchBuildResult Success(MatchConfiguration configuration)
        {
            return new MatchBuildResult(configuration, ValidationResult.Valid());
        }

        public static MatchBuildResult Failure(ValidationResult validation)
        {
            return new MatchBuildResult(null, validation);
        }
    }

    /// <summary>
    /// Turns a request into the one authoritative <see cref="MatchConfiguration"/>.
    ///
    /// Every launch path goes through here, including ones that never touch the menu, which is why
    /// it revalidates rather than trusting that the UI already filtered. Menu filtering is a
    /// convenience for the player; this is the gate.
    /// </summary>
    public sealed class MatchConfigurationBuilder
    {
        private readonly GameModeCatalog modes;
        private readonly LevelDefinitionCatalog levels;
        private readonly GameModeCompatibility compatibility;

        public MatchConfigurationBuilder(GameModeCatalog modes, LevelDefinitionCatalog levels)
            : this(modes, levels, new GameModeCompatibility(modes, levels))
        {
        }

        public MatchConfigurationBuilder(
            GameModeCatalog modes,
            LevelDefinitionCatalog levels,
            GameModeCompatibility compatibility)
        {
            this.modes = modes ?? GameModeCatalog.Empty();
            this.levels = levels ?? LevelDefinitionCatalog.Empty();
            this.compatibility = compatibility ?? new GameModeCompatibility(this.modes, this.levels);
        }

        public GameModeCompatibility Compatibility => compatibility;

        /// <summary>
        /// Builds and validates a configuration. <paramref name="unlock"/> is the launch-time
        /// re-check of account unlock state - every launch path goes through here, so this is the
        /// one place a stale menu index, a programmatic caller, or a future UI bug is stopped from
        /// starting locked content, using the exact same <see cref="LevelEligibility"/> policy the
        /// menu's cycling uses. Passing null skips the unlock/selectable re-check entirely (previous
        /// behavior, for callers not yet migrated).
        /// </summary>
        public MatchBuildResult Build(MatchRequest request, UnlockSnapshot unlock = null)
        {
            ValidationResult validation = compatibility.Validate(request);
            if (!validation.IsValid)
            {
                return MatchBuildResult.Failure(validation);
            }

            GameModeDefinition mode = modes.Find(request.ModeId);
            LevelDefinition level = levels.Find(request.LevelId);
            MatchModifiers modifiers = request.Modifiers ?? MatchModifiers.Default;

            if (unlock != null && !LevelEligibility.IsSelectableContent(level))
            {
                return MatchBuildResult.Failure(ValidationResult.Invalid(
                    MatchValidationCode.LevelNotSelectable,
                    $"'{level.DisplayName}' is not selectable"));
            }

            if (unlock != null && !LevelEligibility.IsUnlockedForAccount(level, unlock))
            {
                return MatchBuildResult.Failure(ValidationResult.Invalid(
                    MatchValidationCode.LevelLocked,
                    $"'{level.DisplayName}' is locked"));
            }

            ResolvedMatchRules rules = Resolve(mode, level, request.Roster, modifiers);

            MatchConfiguration configuration = new MatchConfiguration(
                mode,
                level,
                request.Roster,
                modifiers,
                rules,
                request.Cheerleader,
                request.Source);

            return MatchBuildResult.Success(configuration);
        }

        /// <summary>
        /// Resolves the authored rules against the arena, the roster and the player's modifiers.
        ///
        /// Public so the parity tests can resolve without going through validation, and so a
        /// characterization export can show exactly what each mode resolves to.
        /// </summary>
        public static ResolvedMatchRules Resolve(
            GameModeDefinition mode,
            LevelDefinition level,
            PlayerRoster roster,
            MatchModifiers modifiers)
        {
            if (mode == null)
            {
                throw new ArgumentNullException(nameof(mode));
            }

            modifiers ??= MatchModifiers.Default;
            int participants = roster == null ? 1 : Math.Max(1, roster.Count);

            // A mode played without a ball has enemies whether or not the player asked for them;
            // so does a fighting mode and a battle royal. GameLevelManager used to switch this on
            // at scene start ("if basketball doesn't exist, enable enemies"), which meant the scene
            // was editing the match rules after the menu had settled them.
            bool enemiesEnabled = modifiers.EnemiesRequested
                || mode.EnemiesOnly
                || mode.IsBattleRoyal
                || !mode.RequiresBasketball;

            // Traffic can only be on where there is traffic to switch on.
            bool trafficEnabled = modifiers.TrafficRequested
                && level != null
                && level.Supports(ArenaCapability.Traffic);

            // One ball per participant, unless the mode pins the count. Lockdown is one-on-one
            // against a defender and only ever wants a single ball in play.
            int basketballCount = mode.AddsImplicitDefender ? 1 : participants;

            float customTimerSeconds = mode.CustomTimerSeconds > 0f ? mode.CustomTimerSeconds : 0f;

            return new ResolvedMatchRules(
                objective: mode.Objective,
                clockMode: mode.ClockMode,
                customTimerSeconds: customTimerSeconds,
                matchLengthSeconds: MatchClock.StartSeconds(customTimerSeconds),
                combatMode: mode.CombatMode,
                shotRule: mode.ShotRule,
                shotMarkers: mode.ShotMarkers,
                requiresBasketball: mode.RequiresBasketball,
                basketballCount: basketballCount,
                requiresMoneyBall: mode.RequiresMoneyBall,
                requiresConsecutiveShots: mode.RequiresConsecutiveShots,
                requiresPlayerSurvive: mode.RequiresPlayerSurvive,
                allowsCpuShooters: mode.AllowsCpuShooters,
                enemiesEnabled: enemiesEnabled,
                trafficEnabled: trafficEnabled,
                obstaclesEnabled: modifiers.ObstaclesRequested,
                sniper: modifiers.Sniper,
                difficulty: modifiers.Difficulty,
                hardcore: modifiers.Hardcore,
                arcadeMode: mode.ArcadeMode,
                addsImplicitDefender: mode.AddsImplicitDefender,
                enemiesOnly: mode.EnemiesOnly);
        }
    }
}
