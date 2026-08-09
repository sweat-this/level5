using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// Which shot-position markers a mode needs active in the arena.
    ///
    /// Flags rather than a single value because the "all" spot-up and contest modes legitimately
    /// require several marker rings at once. This is deliberately separate from
    /// <see cref="ShotRule"/>: a spot-up mode requires markers without being a contest.
    /// </summary>
    [Flags]
    public enum ShotMarkerRequirement
    {
        None = 0,
        ThreePoint = 1 << 0,
        FourPoint = 1 << 1,
        SevenPoint = 1 << 2
    }
}
