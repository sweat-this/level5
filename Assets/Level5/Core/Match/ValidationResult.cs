using System.Collections.Generic;
using System.Text;

namespace Level5.Core.Match
{
    /// <summary>
    /// Why a match combination was rejected. Coded so tests and UI do not match on prose.
    ///
    /// Only reasons something actually produces are listed. The overhaul plan also names "CPU
    /// participants forbidden" and "modifier not permitted" as candidates; neither is here, because
    /// enforcing them would refuse launches the game currently allows, and that is a gameplay
    /// change rather than a migration.
    /// </summary>
    public enum MatchValidationCode
    {
        UnknownMode,
        UnknownLevel,
        ArenaLacksBasketball,
        ArenaLacksCombat,
        ArenaLacksCage,
        ArenaLacksBattleRoyal,
        ArenaLacksSevenPointLine,
        ArenaForbidsMode,
        ArenaLacksMultiplayer,
        RosterEmpty,
        RosterTooSmall,
        RosterTooLarge,
        ParticipantTypeNotSupported,
        CharacterCannotShoot,
        CharacterCannotFight
    }

    /// <summary>One rejection reason, with a message a launch screen can show as-is.</summary>
    public readonly struct MatchValidationError
    {
        public MatchValidationError(MatchValidationCode code, string message)
        {
            Code = code;
            Message = message;
        }

        public MatchValidationCode Code { get; }

        public string Message { get; }

        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }

    /// <summary>
    /// The verdict on a requested match. A result carries every reason it failed rather than only
    /// the first, so the menu can say what is actually wrong instead of one thing at a time.
    /// </summary>
    public sealed class ValidationResult
    {
        private static readonly MatchValidationError[] NoErrors = new MatchValidationError[0];

        private readonly List<MatchValidationError> errors;

        private ValidationResult(List<MatchValidationError> errors)
        {
            this.errors = errors;
        }

        public static ValidationResult Valid()
        {
            return new ValidationResult(null);
        }

        public static ValidationResult Invalid(MatchValidationCode code, string message)
        {
            return new ValidationResult(new List<MatchValidationError> { new MatchValidationError(code, message) });
        }

        public bool IsValid => errors == null || errors.Count == 0;

        public IReadOnlyList<MatchValidationError> Errors => (IReadOnlyList<MatchValidationError>)errors ?? NoErrors;

        public bool HasError(MatchValidationCode code)
        {
            if (errors == null)
            {
                return false;
            }

            foreach (MatchValidationError error in errors)
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

        /// <summary>Accumulates errors while a validator runs, then produces the result.</summary>
        public sealed class Builder
        {
            private List<MatchValidationError> collected;

            public void Add(MatchValidationCode code, string message)
            {
                collected ??= new List<MatchValidationError>();
                collected.Add(new MatchValidationError(code, message));
            }

            public void AddIf(bool condition, MatchValidationCode code, string message)
            {
                if (condition)
                {
                    Add(code, message);
                }
            }

            public bool HasErrors => collected != null && collected.Count > 0;

            public ValidationResult Build()
            {
                return new ValidationResult(collected);
            }
        }
    }
}
