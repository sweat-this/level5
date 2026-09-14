using System;
using Level5.Core.Match;
using UnityEngine;

/// <summary>
/// How a gameplay scene gets the rules it is being played under.
///
/// Normally that is the <see cref="ActiveMatch"/> configuration the launch path built and
/// validated. A gameplay scene can also be entered with no launch at all - pressing play on the
/// scene in the editor, or reloading one directly - and that has to keep working, so this falls
/// back to a <see cref="LegacyMatchRuntimeSnapshot"/> built fresh, on demand, by whoever the legacy
/// side has installed as <see cref="InstallLegacyFallbackReader"/>.
///
/// The fallback is read-only and one-shot per read. It never writes back into a configuration and
/// never becomes authoritative: when a real match is running, <see cref="ActiveMatch"/> is the
/// answer and the legacy side is only its shadow. <see cref="MatchRuntime"/> itself has no
/// <c>Assembly-CSharp</c> dependency - the legacy globals it used to read directly now arrive only
/// through that one narrow, replaceable seam.
/// </summary>
public static class MatchRuntime
{
    private static Func<LegacyMatchRuntimeSnapshot> legacyFallbackReader;

    public static MatchConfiguration Configuration => ActiveMatch.Configuration;

    public static bool HasConfiguration => ActiveMatch.IsActive;

    /// <summary>The resolved rules for this match, or the legacy fallback for a directly entered scene.</summary>
    public static ResolvedMatchRules Rules
    {
        get
        {
            MatchConfiguration configuration = Configuration;
            // Deliberately re-read each time rather than cached: the fallback exists to reflect the
            // legacy globals as they are now, and code outside the configuration still writes them.
            // A cached answer would be a third source of truth with its own staleness.
            return configuration != null ? configuration.Rules : LegacyFallback().Rules;
        }
    }

    /// <summary>The roster for this match, or the one the legacy fallback reconstructs.</summary>
    public static PlayerRoster Roster
    {
        get
        {
            MatchConfiguration configuration = Configuration;
            return configuration != null ? configuration.Roster : LegacyFallback().Roster;
        }
    }

    public static GameModeId ModeId => Configuration != null
        ? Configuration.ModeId
        : LegacyFallback().ModeId;

    /// <summary>The arena this match is in, or null when the scene was entered directly.</summary>
    public static LevelDefinition Level => Configuration != null ? Configuration.Level : null;

    // Arena facts gameplay reads. Each falls back to the legacy fallback, so this stays the one
    // place that knows the fallback exists rather than every caller having to.

    public static bool CustomCamera => Level != null && Level.CustomCamera;

    public static bool LevelRequiresTimeOfDay => Level != null
        ? Level.RequiresTimeOfDay
        : LegacyFallback().LevelRequiresTimeOfDay;

    public static bool LevelHasWeather => Level != null ? Level.HasWeather : LegacyFallback().LevelHasWeather;

    public static bool LevelHasSevenPointers => Level != null
        ? Level.HasSevenPointers
        : LegacyFallback().LevelHasSevenPointers;

    public static string LevelDisplayName => Level != null ? Level.DisplayName : LegacyFallback().LevelDisplayName;

    public static int LevelId => Level != null ? Level.LevelId : LegacyFallback().LevelId;

    /// <summary>The display name of the character in slot zero.</summary>
    public static string PrimaryCharacterDisplayName
    {
        get
        {
            MatchConfiguration configuration = Configuration;
            if (configuration != null)
            {
                PlayerSlot primary = configuration.Roster.GetBySlotId(0);
                if (primary != null && primary.Character != null && !string.IsNullOrEmpty(primary.Character.DisplayName))
                {
                    return primary.Character.DisplayName;
                }

                return LegacyFallback().PrimaryCharacterDisplayName;
            }

            LegacyMatchRuntimeSnapshot fallback = LegacyFallback();
            PlayerSlot legacyPrimary = fallback.Roster.GetBySlotId(0);
            return legacyPrimary != null && legacyPrimary.Character != null && !string.IsNullOrEmpty(legacyPrimary.Character.DisplayName)
                ? legacyPrimary.Character.DisplayName
                : fallback.PrimaryCharacterDisplayName;
        }
    }

    /// <summary>The stored mode number, for the call sites still comparing against <c>Modes</c>.</summary>
    public static int RawModeId => Configuration != null
        ? Configuration.Mode.RawModeId
        : LegacyFallback().RawModeId;

    public static string ModeDisplayName => Configuration != null
        ? Configuration.Mode.DisplayName
        : LegacyFallback().ModeDisplayName;

    /// <summary>How many participants are in the match.</summary>
    public static int ParticipantCount => Roster.Count;

    /// <summary>The cheerleader and the shooting bonuses she contributes.</summary>
    public static CheerleaderSelection Cheerleader => Configuration != null
        ? Configuration.Cheerleader
        : LegacyFallback().Cheerleader;

    /// <summary>The prefab name of the character in slot zero - the one the player is playing as.</summary>
    public static string PrimaryCharacterObjectName
    {
        get
        {
            MatchConfiguration configuration = Configuration;
            if (configuration != null)
            {
                PlayerSlot primary = configuration.Roster.GetBySlotId(0);
                if (primary != null && primary.Character != null && !string.IsNullOrEmpty(primary.Character.ObjectName))
                {
                    return primary.Character.ObjectName;
                }

                return LegacyFallback().PrimaryCharacterObjectName;
            }

            LegacyMatchRuntimeSnapshot fallback = LegacyFallback();
            PlayerSlot legacyPrimary = fallback.Roster.GetBySlotId(0);
            return legacyPrimary != null && legacyPrimary.Character != null && !string.IsNullOrEmpty(legacyPrimary.Character.ObjectName)
                ? legacyPrimary.Character.ObjectName
                : fallback.PrimaryCharacterObjectName;
        }
    }

    /// <summary>The id of the character in slot zero.</summary>
    public static int PrimaryCharacterId
    {
        get
        {
            MatchConfiguration configuration = Configuration;
            if (configuration != null)
            {
                PlayerSlot primary = configuration.Roster.GetBySlotId(0);
                if (primary != null && primary.Character != null && primary.Character.CharacterId != 0)
                {
                    return primary.Character.CharacterId;
                }

                return LegacyFallback().PrimaryCharacterId;
            }

            LegacyMatchRuntimeSnapshot fallback = LegacyFallback();
            PlayerSlot legacyPrimary = fallback.Roster.GetBySlotId(0);
            return legacyPrimary != null && legacyPrimary.Character != null && legacyPrimary.Character.CharacterId != 0
                ? legacyPrimary.Character.CharacterId
                : fallback.PrimaryCharacterId;
        }
    }

    /// <summary>
    /// Which local input device a participant listens to, or -1 when it is not a local human.
    /// The roster assigns these once at build time instead of counting past the CPUs on every ask.
    /// </summary>
    public static int LocalInputSlotFor(int slotId)
    {
        MatchConfiguration configuration = Configuration;
        if (configuration != null)
        {
            PlayerSlot slot = configuration.Roster.GetBySlotId(slotId);
            if (slot != null)
            {
                return slot.LocalInputSlot ?? -1;
            }

            // Configured, but this slot is not in this match's roster: the same legacy-formula
            // fallback the unconfigured path below uses. Preserved from before this move, not
            // introduced by it - see PrimaryCharacterDisplayName/ObjectName/Id for the identical,
            // pre-existing, ungated-on-configuration shape.
            return LegacyFallback().GetHumanPlayerInputSlot(slotId);
        }

        LegacyMatchRuntimeSnapshot fallback = LegacyFallback();
        PlayerSlot legacySlot = fallback.Roster.GetBySlotId(slotId);
        return legacySlot != null ? legacySlot.LocalInputSlot ?? -1 : fallback.GetHumanPlayerInputSlot(slotId);
    }

    /// <summary>
    /// Warns once per scene when gameplay is running without a validated configuration, so the
    /// difference between "launched from the menu" and "entered directly" is visible in the log
    /// rather than being something to guess at from behaviour.
    /// </summary>
    public static void WarnIfUnconfigured(UnityEngine.Object context)
    {
        if (HasConfiguration)
        {
            return;
        }

        Debug.LogWarning(
            "This scene is running without a validated match configuration, so its rules are being "
            + "read from the legacy GameOptions globals. That is expected when playing a gameplay "
            + "scene directly; launching from the start menu builds a configuration.",
            context);
    }

    /// <summary>
    /// Installs the reader the legacy fallback resolves through. Production installs the real
    /// <c>GameOptions</c>-backed reader before any scene <c>Awake()</c> can need it (see
    /// <c>GameOptions</c>'s own <c>RuntimeInitializeOnLoadMethod</c> bootstrap); a test installs it
    /// explicitly so it exercises the same production mapping rather than a fake.
    /// </summary>
    public static void InstallLegacyFallbackReader(Func<LegacyMatchRuntimeSnapshot> reader)
    {
        legacyFallbackReader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// Removes whatever reader is currently installed. Called once per runtime lifecycle
    /// (<see cref="RuntimeInitializeLoadType.SubsystemRegistration"/>, which runs before the legacy
    /// side's own <c>BeforeSceneLoad</c> re-installs it - see the ordering this depends on) and by
    /// tests that need to prove the missing-reader behaviour or isolate themselves from another
    /// test's installed reader.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetLegacyFallbackReader()
    {
        legacyFallbackReader = null;
    }

    /// <summary>
    /// Builds a fresh <see cref="LegacyMatchRuntimeSnapshot"/> from whatever the installed reader
    /// currently reports. Never cached across calls - see the class summary - so each property above
    /// calls this itself rather than sharing one snapshot field.
    /// </summary>
    private static LegacyMatchRuntimeSnapshot LegacyFallback()
    {
        if (legacyFallbackReader == null)
        {
            throw new InvalidOperationException(
                "MatchRuntime has no validated match configuration and no legacy fallback reader is "
                + "installed, so it has no rules to answer with. Production installs one before any "
                + "scene loads; a test reading this without ActiveMatch configured must install one "
                + "explicitly (see MatchRuntime.InstallLegacyFallbackReader).");
        }

        return legacyFallbackReader();
    }
}
