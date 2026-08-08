using System;

namespace Level5.Core.Match
{
    /// <summary>
    /// Which contest scoring a mode applies.
    ///
    /// Flags, not a single value, because the authored data combines them: "all point contest" is
    /// marked as a three point contest and a four point contest as well as an all-range one, and
    /// gameplay reads all three of those booleans separately. A single value would have quietly
    /// dropped two of them - the parity validator over the shipping prefab is what caught it.
    ///
    /// <see cref="Any"/> (no flags) means the mode is not a contest at all, which is not the same as
    /// <see cref="AllRanges"/>.
    /// </summary>
    [Flags]
    public enum ShotRule
    {
        /// <summary>Not a contest mode; every shot counts under normal scoring.</summary>
        Any = 0,
        ThreePoint = 1 << 0,
        FourPoint = 1 << 1,
        SevenPoint = 1 << 2,

        /// <summary>The "all point contest" variant, which the authored data treats as its own flag.</summary>
        AllRanges = 1 << 3
    }
}
