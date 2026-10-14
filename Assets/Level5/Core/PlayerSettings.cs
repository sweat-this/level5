using System;

namespace Level5.Core
{
    /// <summary>Application-wide local preferences, independent of accounts and matches.</summary>
    public sealed class PlayerSettings
    {
        public static PlayerSettings Defaults { get; } = new PlayerSettings(1f, 1f, 1f);

        public float Master { get; }
        public float Music { get; }
        public float Sfx { get; }

        public PlayerSettings(float master, float music, float sfx)
        {
            Master = Normalize(master);
            Music = Normalize(music);
            Sfx = Normalize(sfx);
        }

        public static float Normalize(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 1f : Math.Max(0f, Math.Min(1f, value));
    }
}
