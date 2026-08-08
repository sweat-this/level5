using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// Which combat rules a mode plays under.
    ///
    /// Flags rather than one value, for the same reason as <see cref="ShotRule"/>: the authored
    /// Cage Match mode sets both <c>isBattleRoyal</c> and <c>isCageMatch</c>, and both of those
    /// booleans are read separately downstream - including by the level filter, which is why a cage
    /// match needs a battle royal arena today. Collapsing them to one value would have silently
    /// changed which arenas the mode can be played in.
    /// </summary>
    [Flags]
    public enum CombatMode
    {
        /// <summary>No combat rules; a basketball mode.</summary>
        None = 0,

        /// <summary>Open combat in a normal arena.</summary>
        Standard = 1 << 0,

        /// <summary>Cage match: confined arena, requires a cage-capable level.</summary>
        Cage = 1 << 1,

        /// <summary>Battle royal: requires a battle-royal-capable level.</summary>
        BattleRoyal = 1 << 2
    }
}
