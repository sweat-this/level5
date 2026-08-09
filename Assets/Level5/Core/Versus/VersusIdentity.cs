using System;

namespace Level5.Core.Versus
{
    /// <summary>
    /// The stable identity of a set of competitive rules, e.g. <c>three-point-contest</c>.
    ///
    /// This is deliberately a string and deliberately not the game mode's numeric id. A mode says
    /// how the game is played; a ruleset says how two runs at it are compared, and the two can
    /// version independently. A correspondence series persists this value and reads it back weeks
    /// later, so it must not be a display name, a scene name, an array index, or an enum number
    /// that a later build could reorder.
    /// </summary>
    public readonly struct RulesetId : IEquatable<RulesetId>
    {
        private readonly string value;

        public RulesetId(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static readonly RulesetId None = default;

        public string Value => value ?? string.Empty;

        public bool HasValue => !string.IsNullOrEmpty(value);

        /// <summary>
        /// Whether the id follows the project convention: lowercase, digits and single hyphens.
        /// Enforced when a ruleset is built rather than here, so reading an old document with an
        /// off-convention id still works.
        /// </summary>
        public bool IsWellFormed()
        {
            if (!HasValue)
            {
                return false;
            }

            if (value[0] == '-' || value[value.Length - 1] == '-')
            {
                return false;
            }

            bool previousWasHyphen = false;
            foreach (char character in value)
            {
                bool isLower = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                bool isHyphen = character == '-';

                if (!isLower && !isDigit && !isHyphen)
                {
                    return false;
                }

                if (isHyphen && previousWasHyphen)
                {
                    return false;
                }

                previousWasHyphen = isHyphen;
            }

            return true;
        }

        public bool Equals(RulesetId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is RulesetId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(RulesetId left, RulesetId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RulesetId left, RulesetId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return HasValue ? Value : "(no ruleset)";
        }
    }

    /// <summary>
    /// Who a participant is, from the competition's point of view.
    ///
    /// Opaque on purpose. It is not the account id, not the save-file key and not a device index:
    /// tying it to any of those would make one of them a dependency of every series, and would be
    /// wrong the first time a guest plays a challenge. The account layer maps its own identity onto
    /// one of these at the launch boundary.
    /// </summary>
    public readonly struct ParticipantId : IEquatable<ParticipantId>
    {
        private readonly string value;

        public ParticipantId(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static readonly ParticipantId None = default;

        public string Value => value ?? string.Empty;

        public bool HasValue => !string.IsNullOrEmpty(value);

        public bool Equals(ParticipantId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ParticipantId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(ParticipantId left, ParticipantId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ParticipantId left, ParticipantId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return HasValue ? Value : "(no participant)";
        }
    }

    /// <summary>Identity of one competitive run. Unique across every series on the device.</summary>
    public readonly struct AttemptId : IEquatable<AttemptId>
    {
        private readonly string value;

        public AttemptId(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static readonly AttemptId None = default;

        public string Value => value ?? string.Empty;

        public bool HasValue => !string.IsNullOrEmpty(value);

        public bool Equals(AttemptId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AttemptId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(AttemptId left, AttemptId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttemptId left, AttemptId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return HasValue ? Value : "(no attempt)";
        }
    }

    /// <summary>Identity of one series. The key a repository stores it under.</summary>
    public readonly struct SeriesId : IEquatable<SeriesId>
    {
        private readonly string value;

        public SeriesId(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public static readonly SeriesId None = default;

        public string Value => value ?? string.Empty;

        public bool HasValue => !string.IsNullOrEmpty(value);

        public bool Equals(SeriesId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SeriesId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public static bool operator ==(SeriesId left, SeriesId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SeriesId left, SeriesId right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return HasValue ? Value : "(no series)";
        }
    }
}
