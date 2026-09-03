using System.Collections.Generic;
using Level5.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

// AUD-010 Phase 1c: ApplyMarkerAndMoneyBallOnShoot's untouched GameRules.instance.MoneyBallEnabled
// read needs a non-null GameRules to see, but GameRules.Awake() pulls in MatchController,
// MatchHudPresenter, ProgressionService and MatchSession - the exact dependency chain
// Level5BasketballMarkerOwnershipTests' file header explains why no test in that suite instantiates
// GameRules. Adding the component to a GameObject that is still inactive defers Awake() (Unity only
// runs it once the GameObject is first activated), so MakeGameRules below never triggers it - the
// bare component is enough to read/write MoneyBallEnabled, which is a plain property, not lifecycle
// state.

/// <summary>
/// AUD-017: <see cref="BasketballShotPipeline"/> is the single shot-launch computation
/// <see cref="BasketBall"/> (human) and <see cref="BasketBallAuto"/> (CPU) now both call, replacing
/// what used to be two independently-maintained copies. These tests exercise it directly - it had
/// no coverage of its own before the extraction, only the arithmetic it delegates to
/// (<see cref="ShotModifiers"/>, covered by <see cref="Level5ShotModifierTests"/>).
/// </summary>
public class Level5BasketballShotPipelineTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
        GameRules.instance = null;
        ActiveMatch.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    /// <summary>
    /// A bare GameRules instance, assigned directly to the public static <c>instance</c> field
    /// rather than through Awake() - see the header comment on why that is safe here and what it
    /// deliberately avoids standing up.
    /// </summary>
    private GameRules MakeGameRules(bool moneyBallEnabled = false)
    {
        GameObject go = Spawn("game-rules");
        go.SetActive(false); // defers Awake() - see the header comment
        GameRules gameRules = go.AddComponent<GameRules>();
        gameRules.MoneyBallEnabled = moneyBallEnabled;
        GameRules.instance = gameRules;
        return gameRules;
    }

    private BasketBallShotMarker MakeMarker(string name, int maxShotAttempt = 5)
    {
        BasketBallShotMarker marker = Spawn(name).AddComponent<BasketBallShotMarker>();
        marker.MaxShotAttempt = maxShotAttempt;
        return marker;
    }

    private sealed class FakeShooterActor : IShooterActor
    {
        public bool HasBasketball { get; set; }
        public bool FacingFront => true;
        public bool Grounded => true;
        public bool InAir { get; set; }
        public bool InDunkState => false;
        public float DistanceFromRim => 0f;
        public ShooterAttributes ShooterAttributes => default;
        public int Clutch => 0;
        public float ShotMeterSliderValue => 0f;
        public bool ShotMeterEnded => true;
        public void SetAnimBool(string name, bool value) { }
        public void SetAnimTrigger(string name) { }
        public void LockCallBallToPlayer(bool locked) { }
        public void DisplayShotMeterMessage(string message) { }
        public void EndShootCycle() { }
    }

    private sealed class FakeRuntime : IBasketballRuntime
    {
        public int ParticipantId { get; set; }
        public bool IsCpu { get; set; }
        public bool IsPrimary { get; set; }
        public GameObject OwnerActor { get; set; }
        public IShooterActor Actor { get; set; } = new FakeShooterActor();
        public BasketBallState State { get; set; }
        public GameStats Stats { get; set; }
        public float LastShotDistance => 0f;

        public void BindOwner(int participantId, bool isCpu, bool isPrimary, GameObject ownerActor, IShooterActor actor)
        {
        }
    }

    private CharacterProfile MakeProfile(int luck, int range, int release, int shootAngle)
    {
        CharacterProfile profile = Spawn("profile").AddComponent<CharacterProfile>();
        profile.Luck = luck;
        profile.Range = range;
        profile.Release = release;
        profile.ShootAngle = shootAngle;
        profile.Accuracy2Pt = 80;
        profile.Accuracy3Pt = 70;
        return profile;
    }

    private BasketBallState MakeState(bool twoPoints)
    {
        BasketBallState state = Spawn("basketball-state").AddComponent<BasketBallState>();
        state.TwoPoints = twoPoints;
        state.ThreePoints = !twoPoints;
        state.BasketBallTarget = Spawn("target");
        state.BasketBallTarget.transform.position = new Vector3(0f, 0f, 20f);
        return state;
    }

    private GameStats MakeStats()
    {
        return Spawn("stats").AddComponent<GameStats>();
    }

    /// <summary>
    /// luck = 100 always rolls critical (<see cref="PercentChance.Succeeds"/> treats >=100 as
    /// certain regardless of the draw), and a range far beyond the shot distance always reaches the
    /// rim with no roll - so this case needs no RNG seeding to be deterministic. Both conditions
    /// zero every modifier, so the shot must resolve as a swish with no aim/release/range error.
    /// </summary>
    [Test]
    public void CertainLuckAndInRangeShotIsAlwaysASwishWithNoModifiers()
    {
        CharacterProfile profile = MakeProfile(luck: 100, range: 10000, release: 50, shootAngle: 45);
        BasketBallState state = MakeState(twoPoints: true);
        GameStats stats = MakeStats();
        GameObject ball = Spawn("ball");

        BasketballShotPipeline.LaunchComputation result = BasketballShotPipeline.ComputeLaunch(
            ball.transform,
            Vector3.zero,
            state.BasketBallTarget.transform.position,
            ShooterAttributesMapper.From(profile),
            state,
            stats,
            lastShotDistance: 10f,
            shotMeterSliderValue: 50f);

        Assert.That(result.Critical, Is.True);
        Assert.That(result.IsSwish, Is.True);
        Assert.That(result.ShotMeterMessage, Is.EqualTo("swish + critical"));
        Assert.That(stats.CriticalRolled, Is.EqualTo(1), "the critical roll must land on the GameStats instance passed in, not a stray global");
    }

    /// <summary>
    /// luck = 0 never rolls critical, and slider >= 95 skips the accuracy-modifier draw entirely -
    /// only the release roll remains. release = 100 always shoots clean
    /// (<see cref="ShotModifiers.ReleaseModifier"/> returns 0 when rolledClean), so this is also
    /// deterministic without seeding: X and Y both land on 0, and an in-range shot keeps Z at 0 too.
    /// </summary>
    [Test]
    public void HighSliderWithCertainCleanReleaseAndInRangeShotIsAlsoASwish()
    {
        CharacterProfile profile = MakeProfile(luck: 0, range: 10000, release: 100, shootAngle: 45);
        BasketBallState state = MakeState(twoPoints: true);
        GameStats stats = MakeStats();
        GameObject ball = Spawn("ball");

        BasketballShotPipeline.LaunchComputation result = BasketballShotPipeline.ComputeLaunch(
            ball.transform,
            Vector3.zero,
            state.BasketBallTarget.transform.position,
            ShooterAttributesMapper.From(profile),
            state,
            stats,
            lastShotDistance: 10f,
            shotMeterSliderValue: 95f);

        Assert.That(result.Critical, Is.False);
        Assert.That(result.IsSwish, Is.True);
        Assert.That(result.ShotMeterMessage, Is.EqualTo("swish"));
        Assert.That(stats.CriticalRolled, Is.EqualTo(0));
    }

    /// <summary>
    /// luck = 0 and release = 0 (never clean) with a slider under 95 forces every modifier branch to
    /// actually run. Not asserting exact floats here - that arithmetic is Level5ShotModifierTests'
    /// job - just that the pipeline reaches the "&lt; 95" branch, stops calling it a swish, and still
    /// produces a launch velocity with a forward (Z) component.
    /// </summary>
    [Test]
    public void LowSliderWithNoCleanRollsProducesANonSwishLaunchWithForwardVelocity()
    {
        CharacterProfile profile = MakeProfile(luck: 0, range: 10000, release: 0, shootAngle: 45);
        BasketBallState state = MakeState(twoPoints: true);
        GameStats stats = MakeStats();
        GameObject ball = Spawn("ball");

        BasketballShotPipeline.LaunchComputation result = BasketballShotPipeline.ComputeLaunch(
            ball.transform,
            Vector3.zero,
            state.BasketBallTarget.transform.position,
            ShooterAttributesMapper.From(profile),
            state,
            stats,
            lastShotDistance: 10f,
            shotMeterSliderValue: 10f);

        Assert.That(result.Critical, Is.False);
        Assert.That(result.ShotMeterMessage, Does.Contain("< 95"));
        Assert.That(result.ShotMeterMessage, Does.Contain("+ release modifier"));
        Assert.That(result.GlobalVelocity.z, Is.GreaterThan(0f));
    }

    [Test]
    public void UpdateScoreTextReportsCountsAndPercentagesFromTheSameGameStatsInstance()
    {
        GameStats stats = MakeStats();
        stats.ShotMade = 3;
        stats.ShotAttempt = 4;
        stats.TwoPointerMade = 2;
        stats.TwoPointerAttempts = 2;
        stats.ThreePointerMade = 1;
        stats.ThreePointerAttempts = 2;
        Text scoreText = Spawn("score-text").AddComponent<UnityEngine.UI.Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BasketballShotPipeline.UpdateScoreText(scoreText, stats, lastShotDistance: 5f);

        Assert.That(scoreText.text, Does.Contain("shots  : 3 / 4"));
        Assert.That(scoreText.text, Does.Contain("2 pointers : 2 / 2  100.00%"));
        Assert.That(scoreText.text, Does.Contain("3 pointers : 1 / 2  50.00%"));
    }

    /// <summary>
    /// Code review on the Phase 1c migration: a missing CharacterProfile used to throw at this call
    /// site; ShooterAttributesMapper.From now returns an inert default instead (moved from
    /// ShooterAttributesFactory in the player↔basketball cycle-cut slice). That fallback predates
    /// this migration (Phase 1a) and is deliberately preserved - not thrown here - but it must not go
    /// silent, since a real missing profile is a setup bug worth seeing in the console.
    /// </summary>
    [Test]
    public void MissingCharacterProfileLogsAndFallsBackToAnInertShooter()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ShooterAttributesMapper.From"));

        ShooterAttributes shooter = ShooterAttributesMapper.From(null);

        Assert.That(shooter.DisplayName, Is.Null);
        Assert.That(shooter.AccuracyFor(ShotKind.Two), Is.EqualTo(0f));
    }

    /// <summary>Same as above for the other half of the seam: a missing BasketBallState.</summary>
    [Test]
    public void MissingBasketBallStateLogsAndFallsBackToNoShotKind()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("BasketballShotPipeline.KindFromPointFlags"));

        ShotKind kind = BasketballShotPipeline.KindFromPointFlags(null);

        Assert.That(kind, Is.EqualTo(ShotKind.None));
    }

    /// <summary>
    /// Code review on the Phase 1c migration: an inert (zeroed) ShooterAttributes has ShootAngle 0,
    /// so tanAlpha is 0. With a target above the release point - the ordinary case, the rim is
    /// higher than the ball - that leaves Sqrt(G * R^2 / (2 * H)) with a negative radicand, since G
    /// (gravity) is always negative and H is positive. Sqrt of a negative number is NaN, not an
    /// exception, and it would have flowed straight into the launch velocity applied to the ball's
    /// Rigidbody. The fix clamps the radicand at 0 instead of letting it go negative, so a
    /// degenerate shooter produces a shot that goes nowhere rather than NaN physics state.
    /// </summary>
    [Test]
    public void ADegenerateShooterProducesAZeroVelocityShotRatherThanNaN()
    {
        BasketBallState state = MakeState(twoPoints: true);
        state.BasketBallTarget.transform.position = new Vector3(0f, 5f, 20f);
        GameStats stats = MakeStats();
        GameObject ball = Spawn("ball");

        BasketballShotPipeline.LaunchComputation result = BasketballShotPipeline.ComputeLaunch(
            ball.transform,
            Vector3.zero,
            state.BasketBallTarget.transform.position,
            default(ShooterAttributes),
            state,
            stats,
            lastShotDistance: 10f,
            shotMeterSliderValue: 50f);

        Assert.That(float.IsNaN(result.GlobalVelocity.x), Is.False, "x component must not be NaN");
        Assert.That(float.IsNaN(result.GlobalVelocity.y), Is.False, "y component must not be NaN");
        Assert.That(float.IsNaN(result.GlobalVelocity.z), Is.False, "z component must not be NaN");
    }

    // ==================== AUD-010 Phase 1c: ApplyMarkerAndMoneyBallOnShoot's marker-required gate ====================
    //
    // GameOptionsSnapshot + ActiveMatch.Clear() drives MatchRuntime.Rules through the legacy-globals
    // fallback exactly like Level5BasketballMarkerOwnershipTests' IsPointContestMode tests, since none
    // of these tests build a validated MatchConfiguration.

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_MarkerModeDisabled_RegistersNoMarkerAttempt()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeRequiresShotMarkers3s = false;
            GameOptions.gameModeRequiresShotMarkers4s = false;
            GameOptions.gameModeRequiresShotMarkers7s = false;
            MakeGameRules();

            BasketBallShotMarker marker = MakeMarker("marker");
            BasketBallState state = MakeState(twoPoints: true);
            state.EnterShotMarker(marker);
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = MakeStats() };

            BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime);

            Assert.That(marker.ShotAttempt, Is.EqualTo(0), "a mode that does not require markers must not register an attempt");
            Assert.That(state.OnShootShotMarker, Is.Null, "the launch-time marker snapshot must not be taken when markers are not required");
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_MarkerModeRequired_RegistersAttemptOnTheExactCurrentMarker()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeRequiresShotMarkers3s = true;
            GameOptions.gameModeRequiresShotMarkers4s = false;
            GameOptions.gameModeRequiresShotMarkers7s = false;
            MakeGameRules();

            BasketBallShotMarker onMarker = MakeMarker("on-marker");
            BasketBallShotMarker otherMarker = MakeMarker("other-marker");
            BasketBallState state = MakeState(twoPoints: true);
            state.EnterShotMarker(onMarker);
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = MakeStats() };

            BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime);

            Assert.That(onMarker.ShotAttempt, Is.EqualTo(1), "the marker the participant is standing on must receive the attempt");
            Assert.That(otherMarker.ShotAttempt, Is.EqualTo(0), "a different marker must not be credited");
            Assert.AreSame(onMarker, state.OnShootShotMarker);
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_NoMarkerOccupancy_RegistersNoMarkerAccountingEvenWhenMarkersRequired()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeRequiresShotMarkers3s = true;
            MakeGameRules();

            BasketBallShotMarker marker = MakeMarker("marker");
            BasketBallState state = MakeState(twoPoints: true);
            // deliberately not entering a marker - PlayerOnMarker stays false
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = MakeStats() };

            BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime);

            Assert.That(marker.ShotAttempt, Is.EqualTo(0));
            Assert.That(state.OnShootShotMarker, Is.Null);
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_PlayerOnMarkerTrueWithNoCurrentMarker_LogsAndSkipsWithoutThrowing()
    {
        // BasketBallState.EnterShotMarker is the only production writer of PlayerOnMarker=true, and it
        // always sets CurrentShotMarker alongside it - so this is an unreachable ownership/composition
        // bug path, exercised here only to pin the defensive log-and-skip behavior.
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeRequiresShotMarkers3s = true;
            MakeGameRules();

            BasketBallState state = MakeState(twoPoints: true);
            state.PlayerOnMarker = true; // CurrentShotMarker deliberately left null
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = MakeStats(), ParticipantId = 3 };

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("participant 3 has PlayerOnMarker true but no CurrentShotMarker"));

            Assert.DoesNotThrow(() => BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime));
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_ThreePointContestFinalAttempt_IncrementsMoneyBallAttempts()
    {
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeRequiresShotMarkers3s = true;
            GameOptions.gameModeThreePointContest = true;
            MakeGameRules(moneyBallEnabled: false);

            BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 5);
            BasketBallState state = MakeState(twoPoints: true);
            GameStats stats = MakeStats();
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

            for (int i = 0; i < 5; i++)
            {
                state.EnterShotMarker(marker);
                BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime);
            }

            Assert.That(marker.ShotAttempt, Is.EqualTo(5));
            Assert.That(stats.Stats.MoneyBallAttempts, Is.EqualTo(1), "the fifth attempt in a three-point contest must credit exactly one money-ball attempt");
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_OrdinaryMarkerMode_FifthAttemptDoesNotCreditMoneyBallAttempt()
    {
        // Contrast with the contest test above: same fifth-attempt marker count, but no contest rule
        // active, so the ShotAttempt == 5 branch's contest-flag check must stay false.
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeRequiresShotMarkers3s = true;
            // explicitly zeroed, not left to ambient/default state - matches
            // Level5BasketballMarkerOwnershipTests.IsPointContestMode_OrdinaryShootingMode_ReturnsFalse
            GameOptions.gameModeThreePointContest = false;
            GameOptions.gameModeFourPointContest = false;
            GameOptions.gameModeSevenPointContest = false;
            MakeGameRules(moneyBallEnabled: false);

            BasketBallShotMarker marker = MakeMarker("marker", maxShotAttempt: 5);
            BasketBallState state = MakeState(twoPoints: true);
            GameStats stats = MakeStats();
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

            for (int i = 0; i < 5; i++)
            {
                state.EnterShotMarker(marker);
                BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime);
            }

            Assert.That(marker.ShotAttempt, Is.EqualTo(5));
            Assert.That(stats.Stats.MoneyBallAttempts, Is.EqualTo(0));
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_MoneyBallEnabled_StillSetsFlagAndCreditsAttemptIndependentlyOfMarkerRule()
    {
        // GameRules.instance.MoneyBallEnabled is untouched by this migration - still read directly,
        // still additive with the marker-contest money-ball credit.
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            GameOptions.gameModeRequiresShotMarkers3s = true;
            MakeGameRules(moneyBallEnabled: true);

            BasketBallShotMarker marker = MakeMarker("marker");
            BasketBallState state = MakeState(twoPoints: true);
            state.EnterShotMarker(marker);
            GameStats stats = MakeStats();
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = stats };

            BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime);

            Assert.IsTrue(state.MoneyBallEnabledOnShoot);
            Assert.That(stats.Stats.MoneyBallAttempts, Is.EqualTo(1));
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }

    [Test]
    public void ApplyMarkerAndMoneyBallOnShoot_DirectEntryLegacyFallback_MarkerRequiredGateMatchesLegacyGlobals()
    {
        // Section 9 / direct-entry support: with no validated MatchConfiguration active,
        // MatchRuntime.Rules reconstructs ResolvedMatchRules from the legacy GameOptions globals on
        // every call - the same path GameRules.Start used to seed positionMarkersRequired from.
        GameOptionsSnapshot snapshot = GameOptionsSnapshot.Capture();
        try
        {
            ActiveMatch.Clear();
            Assert.IsFalse(ActiveMatch.IsActive, "this test exercises the legacy-globals fallback, not a validated configuration");
            GameOptions.gameModeRequiresShotMarkers3s = false;
            GameOptions.gameModeRequiresShotMarkers4s = true;
            GameOptions.gameModeRequiresShotMarkers7s = false;
            MakeGameRules();

            BasketBallShotMarker marker = MakeMarker("marker");
            BasketBallState state = MakeState(twoPoints: false);
            state.FourPoints = true;
            state.EnterShotMarker(marker);
            FakeRuntime runtime = new FakeRuntime { State = state, Stats = MakeStats() };

            BasketballShotPipeline.ApplyMarkerAndMoneyBallOnShoot(runtime);

            Assert.That(marker.ShotAttempt, Is.EqualTo(1), "a mode requiring four-point markers, entered directly with no launch configuration, must still gate on the legacy globals' equivalent rule");
        }
        finally
        {
            snapshot.Restore();
            ActiveMatch.Clear();
        }
    }
}
