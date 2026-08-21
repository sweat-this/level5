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
/// still a method, the four <c>campaign*</c> members are still lower-cased, and
/// <see cref="ApplyMadeShot"/> still takes a <see cref="BasketBallState"/>. Phase 1c migrates
/// consumers onto <see cref="Stats"/> one at a time; this type is retired once that finishes.
///
/// <see cref="BuildExperienceInput"/> stays here rather than moving, because it reads
/// <c>MatchRuntime</c>, which lives in Assembly-CSharp and which <c>Level5.Core</c> cannot reference.
/// That is the assembly boundary doing its job, not a gap in the extraction.
/// </summary>
public class GameStats : MonoBehaviour
{
    /// <summary>
    /// The one copy of the state. Serialized inline, so the inspector still shows live stats during
    /// play - from the owner, with no mirrored second copy to drift out of sync.
    /// </summary>
    [SerializeField] private MatchStats _stats = new MatchStats();

    /// <summary>
    /// The seam phase 1c migrates consumers onto.
    ///
    /// Every delegating member below goes through this rather than through <c>_stats</c> directly, so
    /// a component deserialized from an asset authored before this field existed cannot surface as a
    /// null dereference at runtime.
    /// </summary>
    public MatchStats Stats => _stats ??= new MatchStats();

    /// <summary>
    /// Advances the consecutive-made-shots streak. No caller outside this type uses it; kept for the
    /// duration of 1b so that this slice changes no external surface at all.
    /// </summary>
    public void calculateConsecutiveShot(BasketBallState basketBallState)
    {
        Stats.CalculateConsecutiveShot(basketBallState.TwoAttempt);
    }

    /// <summary>
    /// Scores one made shot and updates the counters and streak state behind it, in the one order
    /// both require (AUD-065). See <see cref="MatchStats.ApplyMadeShot"/> for the ordering constraint
    /// and the reason this scores twice.
    ///
    /// <paramref name="basketBallState"/> is read for exactly one member, <c>TwoAttempt</c> - that
    /// single bool was this logic's entire dependency on the scene, and reducing it to a parameter is
    /// what let the arithmetic move. The overload stays so no caller changes in this slice.
    /// </summary>
    public ShotScore ApplyMadeShot(BasketBallState basketBallState, ShotScoringInput input)
    {
        return Stats.ApplyMadeShot(basketBallState.TwoAttempt, input);
    }

    public float getTotalPointAccuracy()
    {
        return Stats.TotalPointAccuracy;
    }

    public int getExperienceGainedFromSession()
    {
        ExperienceGained = MatchExperience.Calculate(BuildExperienceInput());
        return ExperienceGained;
    }

    // the award itself lives in Level5.Core so it can be unit tested without a scene.
    // this method's only job is reading the session's stats and mode flags into plain data.
    //
    // it stays on this side of the assembly boundary because MatchRuntime is an Assembly-CSharp
    // static that Level5.Core cannot see - the same constraint ShooterAttributesMapper has.
    public MatchExperienceInput BuildExperienceInput()
    {
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

            TrafficEnabled = MatchRuntime.Rules.TrafficEnabled,
            EnemiesEnabled = MatchRuntime.Rules.EnemiesEnabled || MatchRuntime.Rules.EnemiesOnly,
            HardcoreEnabled = MatchRuntime.Rules.Hardcore,
            SniperEnabled = MatchRuntime.Rules.SniperEnabled,
            ArcadeMode = MatchRuntime.Rules.ArcadeMode,
            DifficultySelected = MatchDifficulties.ToInt(MatchRuntime.Rules.Difficulty)
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
