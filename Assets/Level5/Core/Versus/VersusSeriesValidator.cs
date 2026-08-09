using System.Collections.Generic;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Decides whether a requested series can exist, and builds its frozen snapshot when it can.
    ///
    /// Everything is checked before the series is created, never during it. A best-of-seven that
    /// turns out at game five to contain a mode nobody can play asynchronously has already wasted
    /// four games of two people's time; refusing it at creation costs one message.
    /// </summary>
    public sealed class VersusSeriesValidator
    {
        private readonly CompetitiveRulesetCatalog catalog;

        public VersusSeriesValidator(CompetitiveRulesetCatalog catalog)
        {
            this.catalog = catalog ?? CompetitiveRulesetCatalog.Empty();
        }

        /// <summary>
        /// Validates a request and, on success, produces the snapshot the series will be frozen at.
        ///
        /// Validation and snapshot building are one step because they need the same lookups, and
        /// splitting them would mean resolving every ruleset twice with a window in between where
        /// the catalog could change.
        /// </summary>
        public SeriesValidation Validate(SeriesRequest request)
        {
            VersusValidationResult.Builder errors = new VersusValidationResult.Builder();

            if (request == null)
            {
                return SeriesValidation.Failure(
                    VersusValidationResult.Invalid(
                        VersusValidationCode.ParticipantsInvalid,
                        "no series request was given"));
            }

            ValidateParticipants(request, errors);

            if (request.Mode == VersusMode.OnlineRealtime)
            {
                errors.Add(
                    VersusValidationCode.VersusModeNotImplemented,
                    "real-time online series are not implemented yet");
            }

            if (request.Playlist.Count != request.Format.GameCount)
            {
                errors.Add(
                    VersusValidationCode.PlaylistLengthMismatch,
                    $"a {request.Format} needs {request.Format.GameCount} games in its playlist, "
                    + $"but {request.Playlist.Count} were chosen");
            }

            VersusCapability required = request.Mode == VersusMode.OnlineRealtime
                ? VersusCapability.OnlineRealtime
                : VersusModes.RequiredCapability(request.Mode);

            List<CompetitiveRuleset> resolved = new List<CompetitiveRuleset>();
            for (int index = 0; index < request.Playlist.Count; index++)
            {
                RulesetId id = request.Playlist[index];
                CompetitiveRuleset ruleset = catalog.Find(id);

                if (ruleset == null)
                {
                    errors.Add(
                        VersusValidationCode.UnknownRuleset,
                        $"game {index + 1} asks for the ruleset '{id}', which this version of the "
                        + "game does not have");
                    continue;
                }

                if (!ruleset.Supports(required))
                {
                    errors.Add(
                        VersusValidationCode.CapabilityNotSupported,
                        $"{ruleset.DisplayName} cannot be played as {DescribeMode(request.Mode)}");
                    continue;
                }

                resolved.Add(ruleset);
            }

            if (errors.HasErrors)
            {
                return SeriesValidation.Failure(errors.Build());
            }

            SeriesSnapshot snapshot = new SeriesSnapshot(
                request.Format,
                resolved,
                request.InformationPolicy,
                request.AlternatesFirstAttempt);

            return SeriesValidation.Success(snapshot);
        }

        /// <summary>
        /// Whether this build can still play a series that was created under older rules.
        ///
        /// Checked when an attempt is issued rather than when the series is loaded, so a player can
        /// always read a series they can no longer play - seeing the history of a competition that
        /// has aged out is better than an error where the series used to be.
        /// </summary>
        public VersusValidationResult ValidatePlayable(SeriesSnapshot snapshot)
        {
            VersusValidationResult.Builder errors = new VersusValidationResult.Builder();

            for (int index = 0; index < snapshot.GameCount; index++)
            {
                CompetitiveRuleset frozen = snapshot.GameAt(index);
                CompetitiveRuleset current = catalog.Find(frozen.Id);

                if (current == null)
                {
                    errors.Add(
                        VersusValidationCode.UnknownRuleset,
                        $"game {index + 1} was set up as {frozen.DisplayName}, which this version of "
                        + "the game no longer has");
                    continue;
                }

                if (!current.CanPlayVersion(frozen.Version))
                {
                    errors.Add(
                        VersusValidationCode.RulesetVersionUnsupported,
                        $"this series plays {frozen.DisplayName} under rules version {frozen.Version}, "
                        + $"which this version of the game can no longer score "
                        + $"(it supports {current.MinimumCompatibleVersion} to {current.Version})");
                }
            }

            return errors.Build();
        }

        private static void ValidateParticipants(SeriesRequest request, VersusValidationResult.Builder errors)
        {
            if (request.Challenger == null || request.Opponent == null)
            {
                errors.Add(VersusValidationCode.ParticipantsInvalid, "a series needs two participants");
                return;
            }

            if (request.Challenger.Id == request.Opponent.Id)
            {
                errors.Add(
                    VersusValidationCode.ParticipantsInvalid,
                    "a series needs two different participants");
            }
        }

        private static string DescribeMode(VersusMode mode)
        {
            switch (mode)
            {
                case VersusMode.LocalSimultaneous: return "a same-time local game";
                case VersusMode.LocalAlternating: return "a take-turns local game";
                case VersusMode.Asynchronous: return "a play-any-time game";
                case VersusMode.OnlineRealtime: return "a live online game";
                default: return mode.ToString();
            }
        }
    }

    /// <summary>The outcome of validating a series request: a snapshot, or the reasons there is none.</summary>
    public readonly struct SeriesValidation
    {
        private SeriesValidation(SeriesSnapshot snapshot, VersusValidationResult validation)
        {
            Snapshot = snapshot;
            Validation = validation;
        }

        /// <summary>The frozen competitive definition, or null when the request was refused.</summary>
        public SeriesSnapshot Snapshot { get; }

        public VersusValidationResult Validation { get; }

        public bool Succeeded => Snapshot != null;

        public static SeriesValidation Success(SeriesSnapshot snapshot)
        {
            return new SeriesValidation(snapshot, VersusValidationResult.Valid());
        }

        public static SeriesValidation Failure(VersusValidationResult validation)
        {
            return new SeriesValidation(null, validation);
        }
    }
}
