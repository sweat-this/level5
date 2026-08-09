using System.Collections.Generic;
using System.Text;

namespace Level5.Core.Versus
{
    /// <summary>
    /// Why a series could not be created or continued.
    ///
    /// Coded so tests and screens match on a value rather than on prose. A separate enum from
    /// <c>MatchValidationCode</c> on purpose: growing one enum with two domains' failures makes
    /// every switch over it wrong by default, and neither domain should have to know the other's
    /// reasons.
    ///
    /// These are things a player can legitimately ask for and be refused. Things only a bug can
    /// produce throw <see cref="VersusDomainException"/> instead.
    /// </summary>
    public enum VersusValidationCode
    {
        /// <summary>The playlist names a ruleset this build does not have.</summary>
        UnknownRuleset,

        /// <summary>The playlist is a different length from the series format.</summary>
        PlaylistLengthMismatch,

        /// <summary>A ruleset in the playlist does not support the requested kind of competition.</summary>
        CapabilityNotSupported,

        /// <summary>The requested kind of competition is not implemented.</summary>
        VersusModeNotImplemented,

        /// <summary>Fewer or more than two participants, or the same participant twice.</summary>
        ParticipantsInvalid,

        /// <summary>Best-of-N was given a length the format does not allow.</summary>
        SeriesFormatInvalid,

        /// <summary>This build can no longer play the rules version the series was created under.</summary>
        RulesetVersionUnsupported,

        /// <summary>The series named does not exist in the repository.</summary>
        SeriesNotFound,

        /// <summary>The series is in a state that does not allow what was asked.</summary>
        SeriesNotPlayable,

        /// <summary>It is not this participant's turn, or they have already played this game.</summary>
        AttemptNotAvailable,

        /// <summary>The series could not be written, so the operation is not durable.</summary>
        PersistenceFailed
    }

    /// <summary>One reason, with a message a screen can show as it stands.</summary>
    public readonly struct VersusValidationError
    {
        public VersusValidationError(VersusValidationCode code, string message)
        {
            Code = code;
            Message = message;
        }

        public VersusValidationCode Code { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }

    /// <summary>
    /// The verdict on a versus request. Carries every reason it failed rather than only the first,
    /// so a screen can say what is actually wrong instead of one thing at a time.
    /// </summary>
    public sealed class VersusValidationResult
    {
        private static readonly VersusValidationError[] NoErrors = new VersusValidationError[0];

        private readonly List<VersusValidationError> errors;

        private VersusValidationResult(List<VersusValidationError> errors)
        {
            this.errors = errors;
        }

        public static VersusValidationResult Valid()
        {
            return new VersusValidationResult(null);
        }

        public static VersusValidationResult Invalid(VersusValidationCode code, string message)
        {
            return new VersusValidationResult(
                new List<VersusValidationError> { new VersusValidationError(code, message) });
        }

        public bool IsValid => errors == null || errors.Count == 0;

        public IReadOnlyList<VersusValidationError> Errors => (IReadOnlyList<VersusValidationError>)errors ?? NoErrors;

        public bool HasError(VersusValidationCode code)
        {
            if (errors == null)
            {
                return false;
            }

            foreach (VersusValidationError error in errors)
            {
                if (error.Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            if (IsValid)
            {
                return "valid";
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < errors.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append("; ");
                }

                builder.Append(errors[index].Message);
            }

            return builder.ToString();
        }

        /// <summary>Accumulates reasons while a validator runs, then produces the result.</summary>
        public sealed class Builder
        {
            private List<VersusValidationError> collected;

            public void Add(VersusValidationCode code, string message)
            {
                collected ??= new List<VersusValidationError>();
                collected.Add(new VersusValidationError(code, message));
            }

            public void AddIf(bool condition, VersusValidationCode code, string message)
            {
                if (condition)
                {
                    Add(code, message);
                }
            }

            public bool HasErrors => collected != null && collected.Count > 0;

            public VersusValidationResult Build()
            {
                return new VersusValidationResult(collected);
            }
        }
    }
}
