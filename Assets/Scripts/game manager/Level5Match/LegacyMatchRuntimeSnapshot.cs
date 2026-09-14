using System;
using Level5.Core.Match;

/// <summary>
/// The resolved answers <see cref="MatchRuntime"/> needs when a gameplay scene has no validated
/// <see cref="ActiveMatch"/> configuration - the editor's play-from-scene workflow, or a direct scene
/// reload.
///
/// This is not a second copy of <c>GameOptions</c>: it holds the same shape of answer a validated
/// <see cref="MatchConfiguration"/> would have supplied (resolved rules, a roster, typed mode/level
/// identity), not dozens of raw legacy booleans. The legacy owner (<c>GameOptions</c>, still
/// <c>Assembly-CSharp</c>) builds one of these on request; <see cref="MatchRuntime"/> only ever sees
/// this narrow result, which is what lets it live in <c>Level5.Match</c> with zero
/// <c>Assembly-CSharp</c> dependency of its own.
///
/// Deliberately transient: a caller builds a fresh one each time the legacy fallback is consulted
/// (see <see cref="MatchRuntime"/>'s reader), rather than one being cached for a scene's lifetime, so
/// a legacy global that changes mid-scene is reflected on the next read.
///
/// <see cref="Rules"/> and <see cref="Roster"/> are the expensive answers here - building either loops
/// the roster and allocates. They are computed lazily, on first access to this instance, so a caller
/// that only wants a cheap field (<see cref="LevelDisplayName"/>, say) does not pay to reconstruct both
/// just because one snapshot object happens to answer everything <see cref="MatchRuntime"/> might ask.
/// </summary>
public sealed class LegacyMatchRuntimeSnapshot
{
    private readonly Lazy<ResolvedMatchRules> rules;
    private readonly Lazy<PlayerRoster> roster;
    private readonly Func<int, int> getHumanPlayerInputSlot;

    public LegacyMatchRuntimeSnapshot(
        Func<ResolvedMatchRules> rules,
        Func<PlayerRoster> roster,
        GameModeId modeId,
        int rawModeId,
        string modeDisplayName,
        string levelDisplayName,
        int levelId,
        bool levelRequiresTimeOfDay,
        bool levelHasWeather,
        bool levelHasSevenPointers,
        string primaryCharacterDisplayName,
        string primaryCharacterObjectName,
        int primaryCharacterId,
        CheerleaderSelection cheerleader,
        Func<int, int> getHumanPlayerInputSlot)
    {
        this.rules = new Lazy<ResolvedMatchRules>(rules ?? throw new ArgumentNullException(nameof(rules)));
        this.roster = new Lazy<PlayerRoster>(roster ?? throw new ArgumentNullException(nameof(roster)));
        ModeId = modeId;
        RawModeId = rawModeId;
        ModeDisplayName = modeDisplayName ?? string.Empty;
        LevelDisplayName = levelDisplayName ?? string.Empty;
        LevelId = levelId;
        LevelRequiresTimeOfDay = levelRequiresTimeOfDay;
        LevelHasWeather = levelHasWeather;
        LevelHasSevenPointers = levelHasSevenPointers;
        PrimaryCharacterDisplayName = primaryCharacterDisplayName ?? string.Empty;
        PrimaryCharacterObjectName = primaryCharacterObjectName ?? string.Empty;
        PrimaryCharacterId = primaryCharacterId;
        Cheerleader = cheerleader ?? CheerleaderSelection.None;
        this.getHumanPlayerInputSlot = getHumanPlayerInputSlot ?? throw new ArgumentNullException(nameof(getHumanPlayerInputSlot));
    }

    public ResolvedMatchRules Rules => rules.Value;

    public PlayerRoster Roster => roster.Value;

    public GameModeId ModeId { get; }

    /// <summary>The stored mode number, for the call sites still comparing against <c>Modes</c>.</summary>
    public int RawModeId { get; }

    public string ModeDisplayName { get; }

    public string LevelDisplayName { get; }

    public int LevelId { get; }

    public bool LevelRequiresTimeOfDay { get; }

    public bool LevelHasWeather { get; }

    public bool LevelHasSevenPointers { get; }

    /// <summary>
    /// The legacy globals' own idea of the primary character, independent of <see cref="Roster"/>.
    /// <see cref="MatchRuntime"/> reads this even when a configuration is active, as the fallback for
    /// a primary roster slot that carries no character of its own - see its callers.
    /// </summary>
    public string PrimaryCharacterDisplayName { get; }

    public string PrimaryCharacterObjectName { get; }

    public int PrimaryCharacterId { get; }

    /// <summary>Only the identity survives as a global; a scene entered without a launch has no cheerleader bonuses.</summary>
    public CheerleaderSelection Cheerleader { get; }

    /// <summary>Which local input device a slot the roster does not model listens to, or -1 when it is a CPU.</summary>
    public int GetHumanPlayerInputSlot(int slotId) => getHumanPlayerInputSlot(slotId);
}
