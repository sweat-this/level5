using System.Collections.Generic;
using System.IO;
using Level5.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Coverage for #54: CPU shot-kind selection collapsed to four-pointers for almost every character.
///
/// The audit that preceded this fix found the issue's own diagnosis measured stale serialized
/// <c>CharacterProfile</c> fields rather than the runtime-resolved values ordinary CPU initialization
/// produces (<c>CharacterProfile.Start -> ApplyPreparedCpuMatchInitialization -> calculateAccuracyAttributeRatings</c>).
/// These tests exercise <see cref="CpuShotSelectionPolicy"/> and <see cref="CpuSevenPointEligibility"/>
/// directly (no MonoBehaviour needed), <see cref="CpuScoreDeficit"/> against constructed participants,
/// and the real authored CPU shooter prefabs through the actual production stat calculation - never
/// against raw serialized accuracy fields.
/// </summary>
public class Level5CpuShotSelectionTests
{
    private const string CpuShooterPrefabFolder = "Assets/Resources/Prefabs/characters/cpu_players";

    private static CpuShotSelectionContext Context(
        ShotKind preferredKind,
        float accuracyThree,
        float accuracyFour,
        float accuracySeven,
        bool canShootSeven,
        int scoreDeficit = 0)
    {
        return new CpuShotSelectionContext(
            preferredKind, accuracyThree, accuracyFour, accuracySeven, canShootSeven, scoreDeficit);
    }

    // ================================================================ normal archetypes

    [Test]
    public void ThreeSpecialistChoosesThreeWhenLegal()
    {
        CpuShotSelectionContext context = Context(ShotKind.Three, accuracyThree: 90, accuracyFour: 75, accuracySeven: 75, canShootSeven: true);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Three));
    }

    [Test]
    public void FourSpecialistChoosesFour()
    {
        CpuShotSelectionContext context = Context(ShotKind.Four, accuracyThree: 78, accuracyFour: 90, accuracySeven: 78, canShootSeven: true);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Four));
    }

    [Test]
    public void SevenSpecialistChoosesSevenWhenLegal()
    {
        CpuShotSelectionContext context = Context(ShotKind.Seven, accuracyThree: 78, accuracyFour: 78, accuracySeven: 90, canShootSeven: true);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Seven));
    }

    [Test]
    public void SevenSpecialistFallsBackSafelyWhenSevenIsUnavailable()
    {
        // Seven-type CPUs are authored with equal three/four accuracy (both level*0.15) - this is
        // the realistic shape, not a contrived tie.
        CpuShotSelectionContext context = Context(ShotKind.Seven, accuracyThree: 78, accuracyFour: 78, accuracySeven: 90, canShootSeven: false);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Four));
    }

    // ================================================================ ties / Arcade

    [TestCase(ShotKind.Three)]
    [TestCase(ShotKind.Four)]
    public void EqualAccuraciesResolveToThePreferredThreeOrFourKind(ShotKind preferred)
    {
        // Arcade/easy sets Accuracy2/3/4/7 all to 100 (CharacterProfile.Start). With seven legal too,
        // every kind ties - the authored preference alone must decide.
        CpuShotSelectionContext context = Context(preferred, accuracyThree: 100, accuracyFour: 100, accuracySeven: 100, canShootSeven: true);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(preferred));
    }

    [Test]
    public void EqualAccuraciesWithSevenPreferenceChooseSevenWhenLegal()
    {
        CpuShotSelectionContext context = Context(ShotKind.Seven, accuracyThree: 100, accuracyFour: 100, accuracySeven: 100, canShootSeven: true);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Seven));
    }

    [Test]
    public void EqualThreeAndFourWithSevenUnavailableFallsBackToFour()
    {
        CpuShotSelectionContext context = Context(ShotKind.Seven, accuracyThree: 100, accuracyFour: 100, accuracySeven: 100, canShootSeven: false);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Four));
    }

    [Test]
    public void ArcadeDoesNotCollapseEveryCpuIntoTheSameShotRegardlessOfPreference()
    {
        // The suspected real collapse #54 asked to characterize: with Accuracy7 >= Accuracy4 and
        // Accuracy7 >= Accuracy3 always true under Arcade's 100/100/100, does every CPU end up
        // shooting sevens regardless of who it is authored to be? It must not.
        CpuShotSelectionContext three = Context(ShotKind.Three, 100, 100, 100, canShootSeven: true);
        CpuShotSelectionContext four = Context(ShotKind.Four, 100, 100, 100, canShootSeven: true);
        CpuShotSelectionContext seven = Context(ShotKind.Seven, 100, 100, 100, canShootSeven: true);

        HashSet<ShotKind> results = new HashSet<ShotKind>
        {
            CpuShotSelectionPolicy.Select(in three),
            CpuShotSelectionPolicy.Select(in four),
            CpuShotSelectionPolicy.Select(in seven),
        };

        Assert.That(results, Is.EquivalentTo(new[] { ShotKind.Three, ShotKind.Four, ShotKind.Seven }),
            "Arcade's equal accuracies must let each authored preference through, not collapse to one shot");
    }

    [Test]
    public void UntiedPreferredKindFallsBackToFourEvenWhenFourIsNotATiedLeader()
    {
        // Pins a documented, currently-unreachable-by-authored-data edge in SelectBasePreference:
        // Three and Seven tie for the lead, Four is strictly behind, and the preferred kind is Four
        // (not itself a tied leader). The fallback is Four regardless of it not being an accuracy
        // leader - see SelectBasePreference's doc comment for why this is a deliberate, documented
        // choice rather than a bug.
        CpuShotSelectionContext context = Context(ShotKind.Four, accuracyThree: 90, accuracyFour: 70, accuracySeven: 90, canShootSeven: true);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Four));
    }

    // ================================================================ comeback

    // A three specialist with clearly separated accuracies (not a tie), so any non-Three result below
    // deficit 16 would prove the comeback override fired too early.
    private static CpuShotSelectionContext ThreeSpecialistWithDeficit(int deficit, bool canShootSeven = true)
    {
        return Context(ShotKind.Three, accuracyThree: 90, accuracyFour: 70, accuracySeven: 70, canShootSeven: canShootSeven, scoreDeficit: deficit);
    }

    [TestCase(0)]
    [TestCase(12)]
    [TestCase(13)]
    [TestCase(15)]
    public void BelowSixteenPointDeficitTheNormalPreferenceStands(int deficit)
    {
        // #54: the original code's 13-15 gap fell through to a Vector3.zero default instead of the
        // three-point preference this character actually has. It must not fall through here either.
        CpuShotSelectionContext context = ThreeSpecialistWithDeficit(deficit);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Three));
    }

    // #54: also an intentional behavior change from the pre-#54 code, which could still overwrite
    // to Seven in this deficit band when Accuracy7Pt was accuracy-dominant - its seven-point `if`
    // fired independently of the score gap whenever the accuracy comparison held. The override is
    // now unconditional for 16-20: mid-comeback always reaches for four, even over an
    // accuracy-favored seven. See SixteenToTwentyPointDeficitOverridesToFourEvenOverAnAccuracyDominantSeven
    // below for that specific case.
    [TestCase(16)]
    [TestCase(20)]
    public void SixteenToTwentyPointDeficitOverridesToFour(int deficit)
    {
        CpuShotSelectionContext context = ThreeSpecialistWithDeficit(deficit);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Four));
    }

    [Test]
    public void SixteenToTwentyPointDeficitOverridesToFourEvenOverAnAccuracyDominantSeven()
    {
        // The pre-#54 code's independent seven-point `if` could still overwrite to Seven here
        // whenever Accuracy7Pt was accuracy-dominant, since it checked the score gap on its own
        // clause. Confirmed against a Seven-preferred, Seven-dominant context that would otherwise
        // clearly select Seven (SelectBasePreference alone would return Seven for this context).
        CpuShotSelectionContext context = Context(
            ShotKind.Seven, accuracyThree: 50, accuracyFour: 60, accuracySeven: 90, canShootSeven: true, scoreDeficit: 18);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Four));
    }

    [Test]
    public void TwentyOnePlusDeficitOverridesToSevenWhenLegal()
    {
        CpuShotSelectionContext context = ThreeSpecialistWithDeficit(21, canShootSeven: true);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Seven));
    }

    [Test]
    public void TwentyOnePlusDeficitFallsBackToFourWhenSevenIsUnavailable()
    {
        CpuShotSelectionContext context = ThreeSpecialistWithDeficit(21, canShootSeven: false);
        Assert.That(CpuShotSelectionPolicy.Select(in context), Is.EqualTo(ShotKind.Four));
    }

    // ================================================================ seven-point eligibility

    [Test]
    public void ZeroOrNegativeSevenAccuracyIsNeverEligible()
    {
        Assert.IsFalse(CpuSevenPointEligibility.IsEligible(levelHasSevenPointers: true, range: 200, accuracySeven: 0));
        Assert.IsFalse(CpuSevenPointEligibility.IsEligible(levelHasSevenPointers: true, range: 200, accuracySeven: -5));
    }

    [Test]
    public void ALevelWithoutASevenPointLineIsNeverEligible()
    {
        // range=200, accuracySeven=75 -> rangePercent = 266%, well past the threshold - eligible only
        // because the arena supports it.
        Assert.IsFalse(CpuSevenPointEligibility.IsEligible(levelHasSevenPointers: false, range: 200, accuracySeven: 75));
    }

    [Test]
    public void RangePercentThresholdIsExclusive()
    {
        // range/accuracySeven*100 must be strictly greater than 70, matching the original
        // `rangePercent > 70`. This also pins the suspicious direction: raising accuracySeven with
        // range fixed *lowers* rangePercent, i.e. a more accurate seven-point shooter can become
        // ineligible. That inversion is a candidate follow-up (#54 does not fix it).
        Assert.IsFalse(CpuSevenPointEligibility.IsEligible(true, range: 70, accuracySeven: 100), "70% exactly must not pass a strict >");
        Assert.IsTrue(CpuSevenPointEligibility.IsEligible(true, range: 71, accuracySeven: 100), "just above 70% must pass");

        Assert.IsTrue(CpuSevenPointEligibility.IsEligible(true, range: 100, accuracySeven: 50), "lower accuracy raising the quotient still passes");
        Assert.IsFalse(CpuSevenPointEligibility.IsEligible(true, range: 100, accuracySeven: 200), "higher accuracy lowering the quotient below threshold fails eligibility");
    }

    // ================================================================ score-deficit ownership

    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            Object.DestroyImmediate(go);
        }
        spawned.Clear();
    }

    private PlayerIdentifier NewParticipant(int totalPoints, bool withStats = true)
    {
        GameObject go = new GameObject("participant");
        spawned.Add(go);
        PlayerIdentifier identifier = go.AddComponent<PlayerIdentifier>();
        if (withStats)
        {
            GameStats stats = go.AddComponent<GameStats>();
            stats.TotalPoints = totalPoints;
            identifier.gameStats = stats;
        }
        return identifier;
    }

    [Test]
    public void CpuTrailingOneOpponentComputesThePositiveDeficit()
    {
        PlayerIdentifier cpu = NewParticipant(40);
        PlayerIdentifier opponent = NewParticipant(58);
        List<PlayerIdentifier> participants = new List<PlayerIdentifier> { cpu, opponent };

        Assert.That(CpuScoreDeficit.Calculate(participants, cpu, cpuScore: 40), Is.EqualTo(18));
    }

    [Test]
    public void CpuLeadingComputesZeroDeficit()
    {
        PlayerIdentifier cpu = NewParticipant(70);
        PlayerIdentifier opponent = NewParticipant(40);
        List<PlayerIdentifier> participants = new List<PlayerIdentifier> { cpu, opponent };

        Assert.That(CpuScoreDeficit.Calculate(participants, cpu, cpuScore: 70), Is.EqualTo(0));
    }

    [Test]
    public void MultipleOpponentsUseTheHighestScoreRegardlessOfRosterPosition()
    {
        // The leader sits at index 2, not index 0 - the calculation must not assume roster position.
        PlayerIdentifier cpu = NewParticipant(20);
        PlayerIdentifier trailingOpponent = NewParticipant(25);
        PlayerIdentifier leadingOpponent = NewParticipant(55);
        List<PlayerIdentifier> participants = new List<PlayerIdentifier> { cpu, trailingOpponent, leadingOpponent };

        Assert.That(CpuScoreDeficit.Calculate(participants, cpu, cpuScore: 20), Is.EqualTo(35));
    }

    [Test]
    public void NullAndStatslessParticipantsAreIgnoredSafely()
    {
        PlayerIdentifier cpu = NewParticipant(20);
        PlayerIdentifier statsless = NewParticipant(0, withStats: false);
        PlayerIdentifier realOpponent = NewParticipant(33);
        List<PlayerIdentifier> participants = new List<PlayerIdentifier> { cpu, null, statsless, realOpponent };

        Assert.That(CpuScoreDeficit.Calculate(participants, cpu, cpuScore: 20), Is.EqualTo(13));
    }

    [Test]
    public void ANullParticipantListProducesZeroRatherThanThrowing()
    {
        PlayerIdentifier cpu = NewParticipant(20);
        Assert.That(CpuScoreDeficit.Calculate(null, cpu, cpuScore: 20), Is.EqualTo(0));
    }

    // ================================================================ authored CPU shooter assets

    private readonly struct AuthoredCpuCharacterization
    {
        public AuthoredCpuCharacterization(string name, int level, CpuBaseStats.ShooterType cpuType,
            float runtimeAccuracyThree, float runtimeAccuracyFour, float runtimeAccuracySeven)
        {
            Name = name;
            Level = level;
            CpuType = cpuType;
            RuntimeAccuracyThree = runtimeAccuracyThree;
            RuntimeAccuracyFour = runtimeAccuracyFour;
            RuntimeAccuracySeven = runtimeAccuracySeven;
        }

        public string Name { get; }
        public int Level { get; }
        public CpuBaseStats.ShooterType CpuType { get; }
        public float RuntimeAccuracyThree { get; }
        public float RuntimeAccuracyFour { get; }
        public float RuntimeAccuracySeven { get; }

        public ShotKind Select()
        {
            // Reuses the production mapping (AutoPlayerController.PreferredShotKind) rather than
            // re-deriving it, so a real change to that mapping breaks this test instead of two
            // independently-maintained copies quietly agreeing with each other but not production.
            ShotKind preferred = AutoPlayerController.PreferredShotKind(CpuType);
            CpuShotSelectionContext context = new CpuShotSelectionContext(
                preferred, RuntimeAccuracyThree, RuntimeAccuracyFour, RuntimeAccuracySeven,
                canShootSeven: true, scoreDeficit: 0);
            return CpuShotSelectionPolicy.Select(in context);
        }
    }

    /// <summary>
    /// Characterizes every real CPU shooter prefab through the actual production calculation
    /// (<c>CharacterProfile.calculateAccuracyAttributeRatings</c>), run on a temporary instantiated
    /// copy so the source asset is never touched. This is runtime-resolved data, not the raw
    /// serialized fields the original issue measured.
    /// </summary>
    private static List<AuthoredCpuCharacterization> CharacterizeAuthoredCpuShooters()
    {
        List<AuthoredCpuCharacterization> results = new List<AuthoredCpuCharacterization>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { CpuShooterPrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CharacterProfile sourceProfile = prefab != null ? prefab.GetComponentInChildren<CharacterProfile>(true) : null;
            if (sourceProfile == null)
            {
                continue;
            }

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                CharacterProfile profile = instance.GetComponentInChildren<CharacterProfile>(true);
                profile.calculateAccuracyAttributeRatings();

                results.Add(new AuthoredCpuCharacterization(
                    Path.GetFileNameWithoutExtension(path),
                    profile.Level,
                    profile.CpuType,
                    profile.Accuracy3Pt,
                    profile.Accuracy4Pt,
                    profile.Accuracy7Pt));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        return results;
    }

    [Test]
    public void EachAuthoredCpuArchetypeCanStillSelectItsOwnShotKind()
    {
        List<AuthoredCpuCharacterization> shooters = CharacterizeAuthoredCpuShooters();
        Assert.That(shooters, Is.Not.Empty, $"found no CPU shooter prefabs under {CpuShooterPrefabFolder}");

        foreach (CpuBaseStats.ShooterType archetype in new[]
                 {
                     CpuBaseStats.ShooterType.Three, CpuBaseStats.ShooterType.Four, CpuBaseStats.ShooterType.Seven,
                 })
        {
            ShotKind expected = AutoPlayerController.PreferredShotKind(archetype);

            List<AuthoredCpuCharacterization> ofType = shooters.FindAll(s => s.CpuType == archetype);
            Assert.That(ofType, Is.Not.Empty, $"no authored CPU shooter has cpuType {archetype}");

            bool anySelectsOwnKind = ofType.Exists(s => s.Select() == expected);
            Assert.IsTrue(anySelectsOwnKind,
                $"no {archetype} CPU (of {ofType.Count} authored) selects {expected} using its runtime-resolved accuracies");
        }
    }

    [Test]
    public void AuthoredCpuShootersDoNotAllCollapseIntoTheSameShotKind()
    {
        // The regression #54 reported: measured against stale serialized data, 21 of 22 CPU shooters
        // could only ever reach the four-point branch. Runtime-resolved data and the new policy must
        // not reproduce that collapse.
        List<AuthoredCpuCharacterization> shooters = CharacterizeAuthoredCpuShooters();
        Assert.That(shooters, Is.Not.Empty, $"found no CPU shooter prefabs under {CpuShooterPrefabFolder}");

        HashSet<ShotKind> distinctChoices = new HashSet<ShotKind>();
        foreach (AuthoredCpuCharacterization shooter in shooters)
        {
            distinctChoices.Add(shooter.Select());
        }

        Assert.That(distinctChoices.Count, Is.GreaterThan(1),
            "every authored CPU shooter selected the same shot kind using runtime-resolved accuracies");
    }
}
