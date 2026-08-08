using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// What an arena can host.
    ///
    /// Flags are correct here - unlike mode identity, these genuinely coexist: a level can support
    /// shooting and combat and have a seven point line. The list is derived from the flags the
    /// existing <c>LevelSelected</c> prefabs already author; do not add speculative capabilities.
    /// </summary>
    [Flags]
    public enum ArenaCapability
    {
        None = 0,

        /// <summary>Has a goal and a ball spawn; a basketball mode can be played here.</summary>
        Basketball = 1 << 0,

        /// <summary>Has the navmesh/spawns for enemies; a combat mode can be played here.</summary>
        Combat = 1 << 1,

        /// <summary>Cage-match arena.</summary>
        Cage = 1 << 2,

        /// <summary>Battle-royal arena.</summary>
        BattleRoyal = 1 << 3,

        /// <summary>Has a seven point line, so seven pointers can be scored and marked.</summary>
        SevenPointLine = 1 << 4,

        /// <summary>Has traffic to enable when the player asks for it.</summary>
        Traffic = 1 << 5,

        /// <summary>Supports a time-of-day cycle.</summary>
        TimeOfDay = 1 << 6,

        /// <summary>Supports weather.</summary>
        Weather = 1 << 7,

        /// <summary>Has enough player spawn points for a local multiplayer roster.</summary>
        Multiplayer = 1 << 8
    }
}
