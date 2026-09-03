using Assets.Scripts.Utility;
using UnityEngine;
using Level5.Core;
using Level5.Core.Match;

/// <summary>
/// Compatibility facade over <see cref="MatchStats"/>, which is now the single owner of match-stats
/// state (phase 1b).
///
/// This type used to be the owner: ~40 serialized counters, the made-shot and streak arithmetic, and
/// a scene presence, all in one MonoBehaviour that also served as the campaign accumulator and as the
/// all-time-stats DTO. The state and the arithmetic moved to <c>Level5.Core.Match.MatchStats</c>,
/// which needs no <c>GameObject</c> and cannot reach into a scene. Everything here delegates.
///
/// It keeps its exact previous public surface so that none of the ~147 existing call sites change in
/// this slice - including the shapes that are not properties: <see cref="getTotalPointAccuracy"/> is
/// still a method, and the four <c>campaign*</c> members are still lower-cased. Phase 1c migrates
/// consumers onto <see cref="Stats"/> one at a time; this type is retired once that finishes. This
/// type's last scene-type dependency - a made-shot seam that took the basketball's launch-time shot
/// state as a parameter, purely to read one bool off it - is gone as of AUD-010 Phase 1c: the live
/// made-shot path now calls <see cref="Stats"/>.ApplyMadeShot(bool, ShotScoringInput) directly, with
/// the caller reading that bool itself before passing it in. No member of this type takes a scene
/// type as a parameter or return value.
///
/// <see cref="BuildExperienceInput"/> used to read <c>MatchRuntime</c> directly, because
/// <c>MatchRuntime</c> lives in Assembly-CSharp and <c>Level5.Core</c> cannot reference it. AUD-010
/// Phase 2b0 removed that read: composition (<see cref="SpawnCoordinator.GiveBall"/>) now binds the
/// scene's already-resolved <see cref="ResolvedMatchRules"/> once, through <see cref="BindMatchRules"/>,
/// and match-XP calculation reads that bound reference instead. This type still cannot become a
/// second match-configuration owner - it holds one reference to the rules the scene already resolved,
/// nothing more.
/// </summary>
public class GameStats : MonoBehaviour
{
    /// <summary>
    /// The one copy of the state. Serialized inline, so the inspector still shows live stats during
    /// play - from the owner, with no mirrored second copy to drift out of sync.
    /// </summary>
    [SerializeField] private MatchStats _stats = new MatchStats();

    /// <summary>
    /// The rules this match is being played under, bound once by composition. Not serialized: it is
    /// runtime-only, set after the component already exists (see <see cref="BindMatchRules"/>), and
    /// <see cref="ResolvedMatchRules"/> is not itself <c>[Serializable]</c>.
    ///
    /// Null for every <c>GameStats</c> that is not a live match participant - the campaign
    /// accumulator (<c>PlayerData.campaignGameStats</c>), the all-time-stats DTO
    /// (<c>DBHelper.getAllTimeStats</c>) and test doubles all remain valid without ever calling
    /// <see cref="BindMatchRules"/>; only match-XP calculation requires it.
    /// </summary>
    private ResolvedMatchRules matchRules;

    /// <summary>
    /// The seam phase 1c migrates consumers onto.
    ///
    /// Every delegating member below goes through this rather than through <c>_stats</c> directly, so
    /// a component deserialized from an asset authored before this field existed cannot surface as a
    /// null dereference at runtime.
    /// </summary>
    public MatchStats Stats => _stats ??= new MatchStats();

    /// <summary>Whether <see cref="BindMatchRules"/> has already been accepted. Read-only diagnostic
    /// for composition tests and callers that want to check before asking for match XP.</summary>
    public bool HasBoundMatchRules => matchRules != null;

    /// <summary>
    /// Binds the rules this match is being played under. Composition
    /// (<see cref="SpawnCoordinator.GiveBall"/>) calls this once, immediately after instantiating a
    /// match participant's basketball, for every <c>GameStats</c> that can later be asked for match
    /// XP - a generic/campaign/DB/versus-only <c>GameStats</c> is never bound and remains a valid
    /// stats facade regardless.
    ///
    /// A null argument or a second bind attempt is rejected with an actionable error rather than
    /// silently replacing or discarding the existing state, matching the rebind guard
    /// <see cref="IBasketballRuntime.BindOwner"/> already uses for the same composition step.
    /// </summary>
    public void BindMatchRules(ResolvedMatchRules rules)
    {
        if (rules == null)
        {
            Debug.LogError("GameStats.BindMatchRules was called with null rules; this GameStats remains unbound.", this);
            return;
        }

        if (matchRules != null)
        {
            Debug.LogError("GameStats.BindMatchRules was already called once; keeping the original rules.", this);
            return;
        }

        matchRules = rules;
    }

    public float getTotalPointAccuracy()
    {
        return Stats.TotalPointAccuracy;
    }

    public int getExperienceGainedFromSession()
    {
        if (matchRules == null)
        {
            Debug.LogError("GameStats.getExperienceGainedFromSession was called with no match rules bound; returning 0.", this);
            ExperienceGained = 0;
            return 0;
        }

        ExperienceGained = MatchExperience.Calculate(BuildExperienceInput());
        return ExperienceGained;
    }

    // the award itself lives in Level5.Core so it can be unit tested without a scene.
    // this method's only job is reading the session's stats and mode flags into plain data.
    public MatchExperienceInput BuildExperienceInput()
    {
        if (matchRules == null)
        {
            Debug.LogError("GameStats.BuildExperienceInput was called with no match rules bound; returning an inert input.", this);
            return default;
        }

        return new MatchExperienceInput
        {
            ShotAttempts = ShotAttempt,
            TwoPointerMade = TwoPointerMade,
            ThreePointerMade = ThreePointerMade,
            FourPointerMade = FourPointerMade,
            SevenPointerMade = SevenPointerMade,
            TotalDistance = TotalDistance,
            MostConsecutiveShots = MostConsecutiveShots,
            TotalPoints = TotalPoints,

            SniperShots = SniperShots,
            SniperHits = SniperHits,

            MinionsKilled = MinionsKilled,
            BossKilled = BossKilled,

            TrafficEnabled = matchRules.TrafficEnabled,
            EnemiesEnabled = matchRules.EnemiesEnabled || matchRules.EnemiesOnly,
            HardcoreEnabled = matchRules.Hardcore,
            SniperEnabled = matchRules.SniperEnabled,
            ArcadeMode = matchRules.ArcadeMode,
            DifficultySelected = MatchDifficulties.ToInt(matchRules.Difficulty)
        };
    }

    public float MakeThreePointersMoneyBallLowTime
    {
        get => Stats.MakeThreePointersMoneyBallLowTime;
        set => Stats.MakeThreePointersMoneyBallLowTime = value;
    }

    public float MakeFourPointersMoneyBallLowTime
    {
        get => Stats.MakeFourPointersMoneyBallLowTime;
        set => Stats.MakeFourPointersMoneyBallLowTime = value;
    }

    public float MakeAllPointersMoneyBallLowTime
    {
        get => Stats.MakeAllPointersMoneyBallLowTime;
        set => Stats.MakeAllPointersMoneyBallLowTime = value;
    }

    public float MakeThreePointersLowTime
    {
        get => Stats.MakeThreePointersLowTime;
        set => Stats.MakeThreePointersLowTime = value;
    }

    public float MakeFourPointersLowTime
    {
        get => Stats.MakeFourPointersLowTime;
        set => Stats.MakeFourPointersLowTime = value;
    }

    public float MakeAllPointersLowTime
    {
        get => Stats.MakeAllPointersLowTime;
        set => Stats.MakeAllPointersLowTime = value;
    }

    public int CriticalRolled
    {
        get => Stats.CriticalRolled;
        set => Stats.CriticalRolled = value;
    }

    public int ShotAttempt
    {
        get => Stats.ShotAttempt;
        set => Stats.ShotAttempt = value;
    }

    public int ShotMade
    {
        get => Stats.ShotMade;
        set => Stats.ShotMade = value;
    }

    public float LongestShotMade
    {
        get => Stats.LongestShotMade;
        set => Stats.LongestShotMade = value;
    }

    public float TotalDistance
    {
        get => Stats.TotalDistance;
        set => Stats.TotalDistance = value;
    }

    public int TotalPoints
    {
        get => Stats.TotalPoints;
        set => Stats.TotalPoints = value;
    }

    public int TwoPointerMade
    {
        get => Stats.TwoPointerMade;
        set => Stats.TwoPointerMade = value;
    }

    public int ThreePointerMade
    {
        get => Stats.ThreePointerMade;
        set => Stats.ThreePointerMade = value;
    }

    public int FourPointerMade
    {
        get => Stats.FourPointerMade;
        set => Stats.FourPointerMade = value;
    }

    public int TwoPointerAttempts
    {
        get => Stats.TwoPointerAttempts;
        set => Stats.TwoPointerAttempts = value;
    }

    public int ThreePointerAttempts
    {
        get => Stats.ThreePointerAttempts;
        set => Stats.ThreePointerAttempts = value;
    }

    public int FourPointerAttempts
    {
        get => Stats.FourPointerAttempts;
        set => Stats.FourPointerAttempts = value;
    }

    public int SevenPointerMade
    {
        get => Stats.SevenPointerMade;
        set => Stats.SevenPointerMade = value;
    }

    public int SevenPointerAttempts
    {
        get => Stats.SevenPointerAttempts;
        set => Stats.SevenPointerAttempts = value;
    }

    public int MoneyBallMade
    {
        get => Stats.MoneyBallMade;
        set => Stats.MoneyBallMade = value;
    }

    public int MoneyBallAttempts
    {
        get => Stats.MoneyBallAttempts;
        set => Stats.MoneyBallAttempts = value;
    }

    public float TimePlayed
    {
        get => Stats.TimePlayed;
        set => Stats.TimePlayed = value;
    }

    public int MostConsecutiveShots
    {
        get => Stats.MostConsecutiveShots;
        set => Stats.MostConsecutiveShots = value;
    }

    public int ExperienceGained
    {
        get => Stats.ExperienceGained;
        set => Stats.ExperienceGained = value;
    }

    public int EnemiesKilled { get => Stats.EnemiesKilled; set => Stats.EnemiesKilled = value; }
    public int BossKilled { get => Stats.BossKilled; set => Stats.BossKilled = value; }
    public int MinionsKilled { get => Stats.MinionsKilled; set => Stats.MinionsKilled = value; }
    public int BonusPoints { get => Stats.BonusPoints; set => Stats.BonusPoints = value; }
    public int SniperShots { get => Stats.SniperShots; set => Stats.SniperShots = value; }
    public int SniperHits { get => Stats.SniperHits; set => Stats.SniperHits = value; }
    public int ConsecutiveShotsMade { get => Stats.ConsecutiveShotsMade; set => Stats.ConsecutiveShotsMade = value; }

    // was { get; internal set; } - an auto-property, so never serialized, and writable only because
    // CollisionCheckDefense happened to share this assembly. Now a real counter on the owner.
    public int blockedShots { get => Stats.BlockedShots; set => Stats.BlockedShots = value; }

    // these four were public fields, incremented directly by EndRoundMenuManager and read by
    // HighScoreModel and DBHelper. Field -> property is source-compatible for every one of those
    // uses (nothing passes them by ref or out), and it is what lets the state live on the owner.
    public int campaignWins { get => Stats.CampaignWins; set => Stats.CampaignWins = value; }
    public int campaignLosses { get => Stats.CampaignLosses; set => Stats.CampaignLosses = value; }
    public int campaignTies { get => Stats.CampaignTies; set => Stats.CampaignTies = value; }
    public int campaignGamesPlayed { get => Stats.CampaignGamesPlayed; set => Stats.CampaignGamesPlayed = value; }
}
