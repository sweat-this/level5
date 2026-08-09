using System;
using System.Collections.Generic;

namespace Level5.Core.Match
{
    /// <summary>
    /// Who is playing this match, in order.
    ///
    /// The roster is built once by the launch source and never changes during a match. Slot ids are
    /// dense and start at zero so a slot id is also its index; local input slots are assigned in
    /// roster order, which is what <c>GameOptions.GetHumanPlayerInputSlot</c> computed on the fly.
    /// </summary>
    public sealed class PlayerRoster
    {
        /// <summary>Upper bound the scenes are authored for: four player spawn locations.</summary>
        public const int MaxSlots = 4;

        private readonly List<PlayerSlot> players;

        public PlayerRoster(IEnumerable<PlayerSlot> players)
        {
            this.players = players == null ? new List<PlayerSlot>() : new List<PlayerSlot>(players);

            for (int index = 0; index < this.players.Count; index++)
            {
                PlayerSlot slot = this.players[index];
                if (slot == null)
                {
                    throw new ArgumentException("a roster may not contain an empty slot", nameof(players));
                }

                if (slot.SlotId != index)
                {
                    throw new ArgumentException(
                        $"roster slot ids must be dense and ordered; slot at index {index} reports id {slot.SlotId}",
                        nameof(players));
                }
            }
        }

        public IReadOnlyList<PlayerSlot> Players => players;

        public int Count => players.Count;

        public int LocalHumanCount => CountOf(PlayerControlType.LocalHuman);

        public int CpuCount => CountOf(PlayerControlType.Cpu);

        public PlayerSlot GetBySlotId(int slotId)
        {
            return slotId >= 0 && slotId < players.Count ? players[slotId] : null;
        }

        /// <summary>The first local human, which is the slot every single-player HUD and stat path means.</summary>
        public PlayerSlot PrimaryLocalHuman
        {
            get
            {
                foreach (PlayerSlot slot in players)
                {
                    if (slot.IsLocalHuman)
                    {
                        return slot;
                    }
                }

                return players.Count > 0 ? players[0] : null;
            }
        }

        public bool Contains(PlayerControlType controlType)
        {
            return CountOf(controlType) > 0;
        }

        private int CountOf(PlayerControlType controlType)
        {
            int count = 0;
            foreach (PlayerSlot slot in players)
            {
                if (slot.ControlType == controlType)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Builds a roster from an ordered control-type/character pairing, assigning local input
        /// slots in roster order. This is the shape every current launch path produces.
        /// </summary>
        public static PlayerRoster Build(IEnumerable<PlayerRosterEntry> entries)
        {
            List<PlayerSlot> slots = new List<PlayerSlot>();
            if (entries == null)
            {
                return new PlayerRoster(slots);
            }

            int slotId = 0;
            int nextLocalInputSlot = 0;
            foreach (PlayerRosterEntry entry in entries)
            {
                int? localInputSlot = null;
                if (entry.ControlType == PlayerControlType.LocalHuman)
                {
                    localInputSlot = nextLocalInputSlot;
                    nextLocalInputSlot++;
                }

                slots.Add(new PlayerSlot(slotId, entry.ControlType, entry.Character, localInputSlot, entry.ParticipantId));
                slotId++;
            }

            return new PlayerRoster(slots);
        }

        /// <summary>A single local human. The default for every solo mode.</summary>
        public static PlayerRoster SingleLocalHuman(CharacterSelection character)
        {
            return Build(new[] { PlayerRosterEntry.LocalHuman(character) });
        }
    }

    /// <summary>Input to <see cref="PlayerRoster.Build"/>: a control type and who it plays as.</summary>
    public readonly struct PlayerRosterEntry
    {
        public PlayerRosterEntry(PlayerControlType controlType, CharacterSelection character, string participantId = null)
        {
            ControlType = controlType;
            Character = character ?? CharacterSelection.None;
            ParticipantId = participantId;
        }

        public PlayerControlType ControlType { get; }

        public CharacterSelection Character { get; }

        public string ParticipantId { get; }

        public static PlayerRosterEntry LocalHuman(CharacterSelection character)
        {
            return new PlayerRosterEntry(PlayerControlType.LocalHuman, character);
        }

        public static PlayerRosterEntry Cpu(CharacterSelection character)
        {
            return new PlayerRosterEntry(PlayerControlType.Cpu, character);
        }
    }
}
