using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// One participant in a match.
    ///
    /// Replaces the <c>player1IsCpu</c>..<c>player4IsCpu</c> booleans plus the parallel
    /// <c>characterObjectNames</c> list. A slot knows what drives it, which local input device it
    /// listens to (if any), and who it plays as - so nothing downstream has to infer those from
    /// index arithmetic across three separate arrays.
    /// </summary>
    public sealed class PlayerSlot
    {
        public PlayerSlot(
            int slotId,
            PlayerControlType controlType,
            CharacterSelection character,
            int? localInputSlot = null,
            string participantId = null)
        {
            if (slotId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotId), "slot ids start at 0");
            }

            if (controlType == PlayerControlType.LocalHuman && !localInputSlot.HasValue)
            {
                throw new ArgumentException("a local human slot needs a local input slot", nameof(localInputSlot));
            }

            if (controlType != PlayerControlType.LocalHuman && localInputSlot.HasValue)
            {
                throw new ArgumentException("only a local human slot may hold a local input slot", nameof(localInputSlot));
            }

            SlotId = slotId;
            ControlType = controlType;
            Character = character ?? CharacterSelection.None;
            LocalInputSlot = localInputSlot;
            ParticipantId = participantId ?? string.Empty;
        }

        /// <summary>Position in the roster. Matches the legacy <c>pid</c> assigned during spawning.</summary>
        public int SlotId { get; }

        public PlayerControlType ControlType { get; }

        /// <summary>
        /// Which local input device index this slot reads, counting only local humans. Null for
        /// anything that is not a local human.
        /// </summary>
        public int? LocalInputSlot { get; }

        public CharacterSelection Character { get; }

        /// <summary>
        /// Opaque identity for online/asynchronous participants. Deliberately NOT the save-data
        /// account id: tying the two together here would make the account service a dependency of
        /// every match, and would be wrong the first time a guest plays a challenge.
        /// </summary>
        public string ParticipantId { get; }

        public bool IsCpu => ControlType == PlayerControlType.Cpu;

        public bool IsLocalHuman => ControlType == PlayerControlType.LocalHuman;

        public override string ToString()
        {
            return $"slot {SlotId} ({ControlType}) {Character}";
        }
    }
}
