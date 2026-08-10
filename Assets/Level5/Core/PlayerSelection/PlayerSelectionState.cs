using System.Collections.Generic;
using Level5.Core.Match;

namespace Level5.Core.PlayerSelection
{
    /// <summary>
    /// What player select currently has chosen, expressed by stable character identity - and
    /// nothing else. No mode/level/friend/options state, no persistence calls.
    ///
    /// One primary local-human slot (always present once a selection has been made) and a fixed
    /// number of optional CPU draft slots, matching the current start-menu UI. An inactive CPU
    /// slot is absence, not a fake character.
    /// </summary>
    public sealed class PlayerSelectionState
    {
        /// <summary>CPU draft slots the current start-menu UI exposes.</summary>
        public const int CpuSlotCount = 3;

        private readonly List<PlayerSelectionSlot> cpuSlots;

        public PlayerSelectionState()
        {
            PrimaryCharacterId = null;
            cpuSlots = new List<PlayerSelectionSlot>(CpuSlotCount);
            for (int i = 0; i < CpuSlotCount; i++)
            {
                cpuSlots.Add(PlayerSelectionSlot.Inactive(PlayerControlType.Cpu));
            }
        }

        /// <summary>The local human's chosen character. Null until a first selection is made.</summary>
        public int? PrimaryCharacterId { get; set; }

        /// <summary>Ordered CPU draft slots. Always <see cref="CpuSlotCount"/> entries, some may be inactive.</summary>
        public IReadOnlyList<PlayerSelectionSlot> CpuSlots => cpuSlots;

        /// <summary>How many participants the current draft adds up to, including the primary.</summary>
        public int ParticipantCount
        {
            get
            {
                int count = 1;
                foreach (PlayerSelectionSlot slot in cpuSlots)
                {
                    if (slot.IsActive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        internal void SetCpuSlot(int slotIndex, PlayerSelectionSlot slot)
        {
            cpuSlots[slotIndex] = slot;
        }

        internal PlayerSelectionSlot GetCpuSlot(int slotIndex)
        {
            return cpuSlots[slotIndex];
        }
    }
}
