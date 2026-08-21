using System;
using UnityEngine;

namespace Level5.Core.Match
{
    /// <summary>
    /// One player's match statistics, and the logic that maintains them.
    ///
    /// Lifted out of the <c>GameStats</c> MonoBehaviour, which owned this state, the shot-counter
    /// arithmetic behind it, and a scene presence, all at once. Everything here is plain data and
    /// plain arithmetic: it needs no <c>GameObject</c>, no running match and no singleton, which is
    /// what lets it be tested directly instead of through an <c>AddComponent</c> per test case.
    ///
    /// This is a characterization extraction. Every counter, every ordering constraint and every
    /// oddity is preserved exactly as <c>GameStats</c> had it - see <see cref="ApplyMadeShot"/> and
    /// <see cref="CalculateConsecutiveShot"/>, where the oddities are called out rather than fixed.
    ///
    /// <c>[Serializable]</c> with private backing fields so Unity serializes it inline on the
    /// <c>GameStats</c> facade. The inspector still shows live stats during play, from one owner,
    /// with no mirrored second copy of the state.
    ///
    /// Deliberately *not* here: anything that reads a scene type or a match-wide singleton.
    /// <c>BuildExperienceInput</c> stays on <c>GameStats</c> because it reads <c>MatchRuntime</c>,
    /// which lives in Assembly-CSharp and which this assembly cannot reference. That boundary is the
    /// point: the compiler now rejects any future attempt to make the stats owner reach into a scene.
    /// </summary>
    [Serializable]
    public class MatchStats
    {
        [SerializeField] private int _experienceGained;
        [SerializeField] private int _totalPoints;
        [SerializeField] private int _bonusPoints;

        [SerializeField] private int _twoPointerMade;
        [SerializeField] private int _threePointerMade;
        [SerializeField] private int _fourPointerMade;
        [SerializeField] private int _sevenPointerMade;
        [SerializeField] private int _moneyBallMade;

        [SerializeField] private int _twoPointerAttempts;
        [SerializeField] private int _threePointerAttempts;
        [SerializeField] private int _fourPointerAttempts;
        [SerializeField] private int _sevenPointerAttempts;
        [SerializeField] private int _moneyBallAttempts;

        [SerializeField] private int _shotAttempt;
        [SerializeField] private int _shotMade;
        [SerializeField] private float _longestShotMade;
        [SerializeField] private float _totalDistance;

        [SerializeField] private float _makeThreePointersLowTime;
        [SerializeField] private float _makeFourPointersLowTime;
        [SerializeField] private float _makeAllPointersLowTime;

        [SerializeField] private float _makeThreePointersMoneyBallLowTime;
        [SerializeField] private float _makeFourPointersMoneyBallLowTime;
        [SerializeField] private float _makeAllPointersMoneyBallLowTime;

        [SerializeField] private int _criticalRolled;
        [SerializeField] private int _mostConsecutiveShots;

        [SerializeField] private int _enemiesKilled;
        [SerializeField] private int _minionsKilled;
        [SerializeField] private int _bossKilled;

        [SerializeField] private int _sniperShots;
        [SerializeField] private int _sniperHits;

        [SerializeField] private float _timePlayed;
        [SerializeField] private int _blockedShots;

        // The streak tracker's own state. Predictive rather than counted - see
        // CalculateConsecutiveShot. _expectedShotMade/_expectedShotAttempts start at 1 because the
        // first made shot is compared against "one made, one attempted".
        [SerializeField] private int _consecutiveShotsMade;
        [SerializeField] private int _currentShotMade;
        [SerializeField] private int _currentShotAttempts;
        [SerializeField] private int _expectedShotMade = 1;
        [SerializeField] private int _expectedShotAttempts = 1;

        [SerializeField] private int _campaignWins;
        [SerializeField] private int _campaignLosses;
        [SerializeField] private int _campaignTies;
        [SerializeField] private int _campaignGamesPlayed;

        public int ExperienceGained { get => _experienceGained; set => _experienceGained = value; }
        public int TotalPoints { get => _totalPoints; set => _totalPoints = value; }
        public int BonusPoints { get => _bonusPoints; set => _bonusPoints = value; }

        public int TwoPointerMade { get => _twoPointerMade; set => _twoPointerMade = value; }
        public int ThreePointerMade { get => _threePointerMade; set => _threePointerMade = value; }
        public int FourPointerMade { get => _fourPointerMade; set => _fourPointerMade = value; }
        public int SevenPointerMade { get => _sevenPointerMade; set => _sevenPointerMade = value; }
        public int MoneyBallMade { get => _moneyBallMade; set => _moneyBallMade = value; }

        public int TwoPointerAttempts { get => _twoPointerAttempts; set => _twoPointerAttempts = value; }
        public int ThreePointerAttempts { get => _threePointerAttempts; set => _threePointerAttempts = value; }
        public int FourPointerAttempts { get => _fourPointerAttempts; set => _fourPointerAttempts = value; }
        public int SevenPointerAttempts { get => _sevenPointerAttempts; set => _sevenPointerAttempts = value; }
        public int MoneyBallAttempts { get => _moneyBallAttempts; set => _moneyBallAttempts = value; }

        public int ShotAttempt { get => _shotAttempt; set => _shotAttempt = value; }
        public int ShotMade { get => _shotMade; set => _shotMade = value; }
        public float LongestShotMade { get => _longestShotMade; set => _longestShotMade = value; }
        public float TotalDistance { get => _totalDistance; set => _totalDistance = value; }

        public float MakeThreePointersLowTime { get => _makeThreePointersLowTime; set => _makeThreePointersLowTime = value; }
        public float MakeFourPointersLowTime { get => _makeFourPointersLowTime; set => _makeFourPointersLowTime = value; }
        public float MakeAllPointersLowTime { get => _makeAllPointersLowTime; set => _makeAllPointersLowTime = value; }

        public float MakeThreePointersMoneyBallLowTime { get => _makeThreePointersMoneyBallLowTime; set => _makeThreePointersMoneyBallLowTime = value; }
        public float MakeFourPointersMoneyBallLowTime { get => _makeFourPointersMoneyBallLowTime; set => _makeFourPointersMoneyBallLowTime = value; }
        public float MakeAllPointersMoneyBallLowTime { get => _makeAllPointersMoneyBallLowTime; set => _makeAllPointersMoneyBallLowTime = value; }

        public int CriticalRolled { get => _criticalRolled; set => _criticalRolled = value; }
        public int MostConsecutiveShots { get => _mostConsecutiveShots; set => _mostConsecutiveShots = value; }

        public int EnemiesKilled { get => _enemiesKilled; set => _enemiesKilled = value; }
        public int MinionsKilled { get => _minionsKilled; set => _minionsKilled = value; }
        public int BossKilled { get => _bossKilled; set => _bossKilled = value; }

        public int SniperShots { get => _sniperShots; set => _sniperShots = value; }
        public int SniperHits { get => _sniperHits; set => _sniperHits = value; }

        public float TimePlayed { get => _timePlayed; set => _timePlayed = value; }

        /// <summary>
        /// Blocks credited against this player. The setter is public because
        /// <c>CollisionCheckDefense</c> does <c>gameStats.blockedShots++</c>; on <c>GameStats</c> it
        /// was <c>{ get; internal set; }</c>, which only compiled because both types were in
        /// Assembly-CSharp. As an auto-property it was also never serialized; as a backing field here
        /// it is, which is a change in what the six assets store, not in behaviour.
        /// </summary>
        public int BlockedShots { get => _blockedShots; set => _blockedShots = value; }

        public int ConsecutiveShotsMade { get => _consecutiveShotsMade; set => _consecutiveShotsMade = value; }

        public int CampaignWins { get => _campaignWins; set => _campaignWins = value; }
        public int CampaignLosses { get => _campaignLosses; set => _campaignLosses = value; }
        public int CampaignTies { get => _campaignTies; set => _campaignTies = value; }
        public int CampaignGamesPlayed { get => _campaignGamesPlayed; set => _campaignGamesPlayed = value; }

        /// <summary>Made shots as a percentage of attempts, or 0 when nothing has been attempted.</summary>
        public float TotalPointAccuracy
        {
            get
            {
                if (ShotAttempt > 0)
                {
                    float accuracy = (float)ShotMade / ShotAttempt;
                    return accuracy * 100;
                }

                return 0;
            }
        }

        /// <summary>
        /// Scores one made shot and updates the made-shot counters and consecutive-streak state behind
        /// it, in the one order both require (AUD-065). <paramref name="input"/>'s <c>ConsecutiveShotsMade</c>
        /// is overwritten with this shot's own contribution before the final score is computed - the
        /// caller's value is only a placeholder.
        ///
        /// <c>CountedAs</c>/<c>MoneyBallMade</c> never depend on <c>ConsecutiveShotsMade</c> (see
        /// <see cref="ShotScoring.Score"/>), so scoring twice - once to learn which counter this shot
        /// moves, again after the streak is updated - changes only <c>Points</c>, and only in the
        /// open-play streak-bonus mode. Every other mode gets an identical result both times.
        ///
        /// <paramref name="wasTwoPointAttempt"/> is the whole of this method's former dependency on
        /// the scene: <c>GameStats.ApplyMadeShot</c> took a <c>BasketBallState</c> and read exactly
        /// one member off it, <c>TwoAttempt</c>. It must still be the value as of launch, before
        /// <c>BasketballState.ResetShotAttemptSnapshot</c> clears it.
        /// </summary>
        public ShotScore ApplyMadeShot(bool wasTwoPointAttempt, ShotScoringInput input)
        {
            ShotScore provisional = ShotScoring.Score(input);

            switch (provisional.CountedAs)
            {
                case ShotKind.Two:
                    TwoPointerMade++;
                    break;
                case ShotKind.Three:
                    ThreePointerMade++;
                    break;
                case ShotKind.Four:
                    FourPointerMade++;
                    break;
                case ShotKind.Seven:
                    SevenPointerMade++;
                    break;
            }

            ShotMade = TwoPointerMade + ThreePointerMade + FourPointerMade + SevenPointerMade;

            // Must run after ShotMade is finalized above (CalculateConsecutiveShot compares it against
            // the total it predicted last time) and before BasketballState.ResetShotAttemptSnapshot
            // clears TwoAttempt (called by the shotMade() caller once this method returns) - AUD-065.
            CalculateConsecutiveShot(wasTwoPointAttempt);
            input.ConsecutiveShotsMade = ConsecutiveShotsMade;

            return ShotScoring.Score(input);
        }

        /// <summary>
        /// Advances the consecutive-made-shots streak.
        ///
        /// **Preserved oddity - predictive, not counted.** This does not count made shots; it compares
        /// the live totals against the totals it predicted when it last ran. A shot continues the
        /// streak only when both the made and the attempted totals advanced by exactly the predicted
        /// one, which is how a miss (attempts moved, made did not) breaks it.
        ///
        /// **Preserved oddity - a broken streak resets to 1, not 0.** Both branches then set the same
        /// <c>_expected*</c> values, so the two differ only in that reset. It looks like a bug and is
        /// left exactly as it was.
        ///
        /// **Preserved oddity - a made two-pointer never extends a streak.** It falls into the else
        /// branch and resets the count, rather than being ignored.
        /// </summary>
        public void CalculateConsecutiveShot(bool wasTwoPointAttempt)
        {
            // get current state of shots made/attempted
            _currentShotMade = ShotMade;
            _currentShotAttempts = ShotAttempt;

            // if current is == expected made/attempt, increment consecutive and not a 2 point shot
            if (_currentShotMade == _expectedShotMade
                && _currentShotAttempts == _expectedShotAttempts
                && !wasTwoPointAttempt)
            {
                _consecutiveShotsMade++;
                // increment expected values for next shot
                _expectedShotMade = _currentShotMade + 1;
                _expectedShotAttempts = _currentShotAttempts + 1;
            }
            // else, not consecutive shot. get current, increment for next expected consecutive shot
            else
            {
                _consecutiveShotsMade = 1;
                // increment expected values for next shot
                _expectedShotMade = _currentShotMade + 1;
                _expectedShotAttempts = _currentShotAttempts + 1;
            }
            // if current consecutive greater than previous high consecutive
            if (_consecutiveShotsMade > MostConsecutiveShots)
            {
                MostConsecutiveShots = _consecutiveShotsMade;
            }
        }

        /// <summary>
        /// Folds one finished session into this record, which is how a campaign total is built up.
        ///
        /// The field list is <c>PlayerData.updateCampaignStats</c>, moved verbatim, and it is
        /// deliberately not "sum everything except two maxima". Four rules apply, and the third is
        /// the one that gets lost:
        ///
        /// - **SUM** - the 21 counters below.
        /// - **MAX** - <see cref="LongestShotMade"/> and <see cref="MostConsecutiveShots"/>. A career
        ///   best is not the sum of session bests.
        /// - **NOT ACCUMULATED** - <see cref="ExperienceGained"/>, <see cref="BonusPoints"/>,
        ///   <see cref="BlockedShots"/> and all six <c>Make*LowTime</c> fields. The original omits
        ///   them, and the omission is preserved rather than fixed: summing the low-time fields would
        ///   be meaningless (they are best-times, so a career value would be a MIN if it were wanted
        ///   at all), and whether experience and bonus points belong in a campaign total is a scoring
        ///   question, not a refactoring one.
        /// - **DERIVED** - <see cref="TotalPointAccuracy"/> and the streak-prediction fields are
        ///   computed or session-local, and are never folded.
        ///
        /// The <c>Campaign*</c> tallies are also untouched: they belong to the accumulator itself,
        /// and <c>EndRoundMenuManager</c> increments them directly.
        ///
        /// Adding a counter to this type therefore means choosing one of those four rules on purpose.
        /// </summary>
        public void Accumulate(MatchStats session)
        {
            if (session == null)
            {
                return;
            }

            TotalPoints += session.TotalPoints;
            TotalDistance += session.TotalDistance;
            ThreePointerMade += session.ThreePointerMade;
            FourPointerMade += session.FourPointerMade;
            SevenPointerMade += session.SevenPointerMade;
            ThreePointerAttempts += session.ThreePointerAttempts;
            FourPointerAttempts += session.FourPointerAttempts;
            SevenPointerAttempts += session.SevenPointerAttempts;
            LongestShotMade = Math.Max(session.LongestShotMade, LongestShotMade);
            TimePlayed += session.TimePlayed;
            CriticalRolled += session.CriticalRolled;
            EnemiesKilled += session.EnemiesKilled;
            BossKilled += session.BossKilled;
            MinionsKilled += session.MinionsKilled;
            MoneyBallMade += session.MoneyBallMade;
            MoneyBallAttempts += session.MoneyBallAttempts;
            ShotMade += session.ShotMade;
            ShotAttempt += session.ShotAttempt;
            SniperHits += session.SniperHits;
            SniperShots += session.SniperShots;
            TwoPointerMade += session.TwoPointerMade;
            TwoPointerAttempts += session.TwoPointerAttempts;
            MostConsecutiveShots = Math.Max(session.MostConsecutiveShots, MostConsecutiveShots);
        }
    }
}
