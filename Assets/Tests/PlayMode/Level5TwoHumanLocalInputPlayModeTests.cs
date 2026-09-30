#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

/// <summary>
/// Two local humans in one real gameplay match, driven through the real spawn and input path.
///
/// There is no production launch source for two humans yet, so the match is composed here the way the
/// start menu composes one: the mode and arena the production launch chose are kept, the roster is
/// replaced with two <c>PlayerControlType.LocalHuman</c> slots built by <c>PlayerRoster.Build</c>, and the
/// result goes through <c>MatchCatalogs.Builder</c> (so <c>GameModeCompatibility</c>, including the
/// arena's multiplayer capability, still decides) before <c>ActiveMatch.Begin</c>. The device-side
/// preflight is asked first, like a launch source would.
///
/// Devices are virtual and added under <c>InputTestFixture</c>, which also removes the machine's real
/// ones, so <c>GameLevelManager</c>'s own device capture sees exactly what each test set up. These
/// tests do not substitute for the physical two-gamepad / keyboard+gamepad certification: see
/// docs/player-input-architecture.md.
/// </summary>
public class Level5TwoHumanLocalInputPlayModeTests
{
    private const float MoveSeconds = 1f;
    // Timing-based, so deliberately generous: the deterministic isolation evidence is the per-slot
    // controls.Player.movement reads asserted alongside these, which do not depend on frame pacing.
    private const float MovedAtLeast = 0.5f;
    private const float StayedWithin = 0.3f;
    private const float SettleSeconds = 1f;

    private sealed class Scenario
    {
        public MatchConfiguration Match;
        public PlayerRegistry Registry;
        public PlayerIdentifier First;
        public PlayerIdentifier Second;
    }

    // InputTestFixture is composed rather than inherited so teardown order is ours: the scenes (whose
    // OnScreenControls touch the Input System in OnDisable) are unloaded first, and only then is the
    // Input System restored.
    private InputTestFixture input;
    private LocalProfileTestDatabase matchDatabase;

    [SetUp]
    public void SetUpInput()
    {
        input = new InputTestFixture();
        input.Setup();
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
    }

    [UnityTearDown]
    public IEnumerator TearDownInput()
    {
        ActiveMatch.Clear();
        yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("two-human-input-blank");
        matchDatabase?.Dispose();
        matchDatabase = null;
        input.TearDown();
        input = null;
    }

    private void Set<TValue>(InputControl<TValue> control, TValue state) where TValue : struct
    {
        input.Set(control, state);
    }

    private void Press(ButtonControl button)
    {
        input.Press(button);
    }

    private void Release(ButtonControl button)
    {
        input.Release(button);
    }

    // ==================== launch composition ====================

    private static PlayerRoster TwoHumanRoster(MatchConfiguration production)
    {
        CharacterSelection first = production.Roster.GetBySlotId(0).Character;
        string secondName = first.ObjectName == "ashley" ? "baby" : "ashley";
        CharacterSelection second = new CharacterSelection(0, secondName, secondName, true, true);

        return PlayerRoster.Build(new[]
        {
            new PlayerRosterEntry(PlayerControlType.LocalHuman, first, "participant-one"),
            new PlayerRosterEntry(PlayerControlType.LocalHuman, second, "participant-two")
        });
    }

    private static MatchConfiguration BuildTwoHumanMatch(MatchConfiguration production)
    {
        return BuildMatch(production, TwoHumanRoster(production));
    }

    private static MatchConfiguration BuildMatch(MatchConfiguration production, PlayerRoster roster)
    {
        MatchBuildResult result = MatchCatalogs.Builder.Build(new MatchRequest(
            production.ModeId,
            production.Level.LevelId,
            roster,
            production.Modifiers,
            production.Cheerleader,
            "two-human input certification"));

        Assert.That(result.Succeeded, Is.True, "the match must pass GameModeCompatibility: " + result.Validation);
        return result.Configuration;
    }

    private static void BeginMatch(MatchConfiguration configuration)
    {
        ActiveMatch.Begin(configuration);

        // The one-way push every launch source does for consumers that still read the old globals.
        Type bridge = Assembly.Load("Assembly-CSharp").GetType("LegacyGameOptionsBridge", throwOnError: true);
        bridge.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { configuration });
    }

    /// <summary>
    /// Enters gameplay once through the start menu (which builds the catalogs and persistence the real
    /// path needs), then relaunches the same mode and arena with two local humans.
    /// </summary>
    private static IEnumerator LaunchTwoHumanMatch(Action<Scenario> onReady)
    {
        yield return LaunchMatch(TwoHumanRoster, onReady);
    }

    private static IEnumerator LaunchMatch(Func<MatchConfiguration, PlayerRoster> rosterFor, Action<Scenario> onReady)
    {
        yield return GameplayScenePlayModeHarness.EnterPlayableGameplayScene(_ => { });

        MatchConfiguration production = ActiveMatch.Configuration;
        Assert.That(production, Is.Not.Null, "the production launch must have made a match current");
        MatchConfiguration match = BuildMatch(production, rosterFor(production));
        TestContext.WriteLine(
            $"[two-human launch] mode={production.Mode.DisplayName} ({production.ModeId}) level={production.Level.DisplayName} "
            + $"scene={match.SceneName} allowsCpuShooters={match.Rules.AllowsCpuShooters} "
            + $"basketballCount={match.Rules.BasketballCount} addsImplicitDefender={match.Rules.AddsImplicitDefender}");

        Assert.That(
            PlayerControlsProvider.TryPreflightGameplayDevices(match.Roster.LocalHumanCount, out string reason),
            Is.True,
            "the device preflight must pass before a launch: " + reason);

        BeginMatch(match);
        yield return GameplayScenePlayModeHarness.LoadConfiguredGameplayScene(match.SceneName, _ => { });

        LevelRuntimeContext context = LevelRuntimeContext.instance;
        Assert.That(context, Is.Not.Null, "gameplay must have composed a LevelRuntimeContext");

        onReady(new Scenario
        {
            Match = match,
            Registry = context.Players,
            First = context.Players.GetBySlot(0),
            Second = context.Players.GetBySlot(1)
        });
    }

    // ==================== measurement helpers ====================

    private static Vector3 Ground(Vector3 position)
    {
        return new Vector3(position.x, 0f, position.z);
    }

    private static float Travelled(PlayerIdentifier participant, Vector3 from)
    {
        return Vector3.Distance(Ground(participant.player.transform.position), Ground(from));
    }

    private static IEnumerator Hold(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
    }

    // ==================== spawn / ownership ====================

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator TwoGamepadsSeatTwoDistinctHumansWithIndependentStatsBallsAndInputSlots()
    {
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);

        Assert.That(scenario.Registry.Count, Is.EqualTo(2), "PlayerRegistry holds exactly the two humans");
        PlayerIdentifier first = scenario.First;
        PlayerIdentifier second = scenario.Second;

        Assert.That(first, Is.Not.Null.And.Not.SameAs(second));
        Assert.That(first.isCpu, Is.False);
        Assert.That(second.isCpu, Is.False);
        Assert.That(first.pid, Is.EqualTo(0));
        Assert.That(second.pid, Is.EqualTo(1));
        Assert.That(first.player, Is.Not.Null.And.Not.SameAs(second.player), "two distinct actors");
        Assert.That(first.playerController, Is.Not.Null.And.Not.SameAs(second.playerController));
        Assert.That(scenario.Match.Roster.GetBySlotId(1).ParticipantId, Is.EqualTo("participant-two"));

        // two independent GameStats owners
        Assert.That(first.gameStats, Is.Not.Null);
        Assert.That(second.gameStats, Is.Not.Null.And.Not.SameAs(first.gameStats));
        Assert.That(second.gameStats.Stats, Is.Not.SameAs(first.gameStats.Stats));
        first.gameStats.TotalPoints = 9;
        Assert.That(second.gameStats.TotalPoints, Is.EqualTo(0), "one player's score never lands on the other");
        first.gameStats.TotalPoints = 0;

        // participant-specific basketballs
        Assert.That(first.basketBallController, Is.Not.Null);
        Assert.That(second.basketBallController, Is.Not.Null.And.Not.SameAs(first.basketBallController));
        Assert.That(first.basketBallController.OwnerActor, Is.SameAs(first.player));
        Assert.That(second.basketBallController.OwnerActor, Is.SameAs(second.player));
        Assert.That(first.basketBallController.ParticipantId, Is.EqualTo(0));
        Assert.That(second.basketBallController.ParticipantId, Is.EqualTo(1));
        Assert.That(first.basketBallController.IsPrimary, Is.True);
        Assert.That(second.basketBallController.IsPrimary, Is.False);
        Assert.That(((IBasketballRuntime)second.basketBallController).Stats, Is.SameAs(second.gameStats));

        // two local input slots, each bound to its own gamepad
        LocalGameplayDevicePlan plan = PlayerControlsProvider.GameplayDevicePlan;
        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.TwoGamepads));
        Assert.That(MatchRuntime.LocalInputSlotFor(0), Is.EqualTo(0));
        Assert.That(MatchRuntime.LocalInputSlotFor(1), Is.EqualTo(1));
        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { padA }));
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { padB }));

        // each actor stands on its own authored spawn point
        Vector3 spawn1 = GameObject.Find("player_spawn_location1").transform.position;
        Vector3 spawn2 = GameObject.Find("player_spawn_location2").transform.position;
        Assert.That(Vector3.Distance(Ground(first.player.transform.position), Ground(spawn1)), Is.LessThan(1f));
        Assert.That(Vector3.Distance(Ground(second.player.transform.position), Ground(spawn2)), Is.LessThan(1f));
    }

    // ==================== input isolation ====================

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator EachGamepadDrivesOnlyItsOwnPlayer()
    {
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        PlayerControls controlsA = PlayerControlsProvider.AcquireGameplayControls(0);
        PlayerControls controlsB = PlayerControlsProvider.AcquireGameplayControls(1);

        Vector3 firstStart = scenario.First.player.transform.position;
        Vector3 secondStart = scenario.Second.player.transform.position;

        Set(padA.leftStick, Vector2.right);
        yield return null;
        Assert.That(controlsA.Player.movement.ReadValue<Vector2>().x, Is.GreaterThan(0.5f));
        Assert.That(controlsB.Player.movement.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "pad A never reaches slot 1's controls");
        yield return Hold(MoveSeconds);
        Set(padA.leftStick, Vector2.zero);
        yield return Hold(SettleSeconds);

        Assert.That(Travelled(scenario.First, firstStart), Is.GreaterThan(MovedAtLeast), "pad A moved player A");
        Assert.That(Travelled(scenario.Second, secondStart), Is.LessThan(StayedWithin), "pad A did not move player B");

        firstStart = scenario.First.player.transform.position;
        Set(padB.leftStick, Vector2.left);
        yield return null;
        Assert.That(controlsB.Player.movement.ReadValue<Vector2>().x, Is.LessThan(-0.5f));
        Assert.That(controlsA.Player.movement.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "pad B never reaches slot 0's controls");
        yield return Hold(MoveSeconds);
        Set(padB.leftStick, Vector2.zero);
        yield return null;

        Assert.That(Travelled(scenario.Second, secondStart), Is.GreaterThan(MovedAtLeast), "pad B moved player B");
        Assert.That(Travelled(scenario.First, firstStart), Is.LessThan(StayedWithin), "pad B did not move player A");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator KeyboardDrivesPlayerOneAndTheGamepadDrivesPlayerTwoAndNeitherCrosses()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Gamepad pad = InputSystem.AddDevice<Gamepad>();
        keyboard.MakeCurrent();
        mouse.MakeCurrent();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);

        LocalGameplayDevicePlan plan = PlayerControlsProvider.GameplayDevicePlan;
        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.KeyboardMouseAndGamepad));
        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { keyboard, mouse }));
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { pad }));

        PlayerControls controls0 = PlayerControlsProvider.AcquireGameplayControls(0);
        PlayerControls controls1 = PlayerControlsProvider.AcquireGameplayControls(1);
        Vector3 firstStart = scenario.First.player.transform.position;
        Vector3 secondStart = scenario.Second.player.transform.position;

        Press(keyboard.dKey);
        yield return null;
        Assert.That(controls0.Player.movement.ReadValue<Vector2>().x, Is.GreaterThan(0.5f));
        Assert.That(controls1.Player.movement.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "the keyboard never reaches the gamepad player");
        yield return Hold(MoveSeconds);
        Release(keyboard.dKey);
        yield return Hold(SettleSeconds);

        Assert.That(Travelled(scenario.First, firstStart), Is.GreaterThan(MovedAtLeast), "the keyboard moved player 1");
        Assert.That(Travelled(scenario.Second, secondStart), Is.LessThan(StayedWithin), "the keyboard did not move player 2");

        firstStart = scenario.First.player.transform.position;
        Set(pad.leftStick, Vector2.left);
        yield return null;
        Assert.That(controls1.Player.movement.ReadValue<Vector2>().x, Is.LessThan(-0.5f));
        Assert.That(controls0.Player.movement.ReadValue<Vector2>(), Is.EqualTo(Vector2.zero), "the gamepad never reaches the keyboard player");
        yield return Hold(MoveSeconds);
        Set(pad.leftStick, Vector2.zero);
        yield return null;

        Assert.That(Travelled(scenario.Second, secondStart), Is.GreaterThan(MovedAtLeast), "the gamepad moved player 2");
        Assert.That(Travelled(scenario.First, firstStart), Is.LessThan(StayedWithin), "the gamepad did not move player 1");
    }

    // ==================== CPU gating is unchanged ====================

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator AModeThatDisallowsCpuShootersStillSpawnsNoCpuForAOneHumanRoster()
    {
        InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchMatch(
            production => PlayerRoster.Build(new[]
            {
                new PlayerRosterEntry(PlayerControlType.LocalHuman, production.Roster.GetBySlotId(0).Character),
                PlayerRosterEntry.Cpu(new CharacterSelection(0, "pony", "pony", true, true))
            }),
            s => scenario = s);

        Assert.That(scenario.Match.Rules.AllowsCpuShooters, Is.False, "this scenario needs a mode that disallows CPU shooters");
        Assert.That(scenario.Registry.Count, Is.EqualTo(1), "the CPU slot is still gated by the mode; only the human spawned");
        Assert.That(scenario.First.isCpu, Is.False);
        Assert.That(PlayerControlsProvider.GameplayDevicePlan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.SingleHuman));
    }

    // ==================== insufficient devices ====================

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator AKeyboardOnlyMachineIsRefusedByThePreflightEvenInsideARealGameplayScene()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        keyboard.MakeCurrent();

        yield return GameplayScenePlayModeHarness.EnterPlayableGameplayScene(_ => { });

        // A real gameplay scene carries touch_joystick.prefab, whose OnScreenStick registers a virtual
        // Gamepad. It must not be mistaken for a second player's controller.
        int onScreen = LocalGameplayDeviceAvailability.CaptureOnScreenGamepads().Count;
        TestContext.WriteLine($"[two-human devices] on-screen gamepads registered by the gameplay scene: {onScreen}");

        Assert.That(
            PlayerControlsProvider.TryPreflightGameplayDevices(2, out string reason),
            Is.False,
            "keyboard only, plus whatever on-screen gamepad the scene registered, cannot seat two players");
        StringAssert.StartsWith("Two local players need two gamepads", reason);
        StringAssert.Contains("0 gamepad(s)", reason);
        Assert.That(PlayerControlsProvider.TryPreflightGameplayDevices(1, out _), Is.True, "a solo launch is never refused");
    }

    // ==================== shared camera ====================
    //
    // The camera is observed black-box (Camera.main and viewport positions): cameraUpdater lives in the
    // game assembly, which these tests do not reference. The certified configuration is Total Points at
    // The Scrapyard, which is what the production launch selects by default.

    // Certified separation. The shared camera can only dolly back a bounded distance, so how far apart
    // two humans can be depends on the aspect ratio and on how close to the lens they stand. Ten world
    // units apart at ordinary depth fits at every aspect from 4:3 up (batch mode runs at 4:3, the
    // narrowest case); see docs/player-input-architecture.md for the wider numbers.
    private const float CertifiedSeparation = 10f;
    private const float CameraSettleSeconds = 2.5f;

    private static bool InFrameX(Vector3 viewport)
    {
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f;
    }

    private static void AssertBothInFrame(Camera camera, Scenario scenario, string when)
    {
        Vector3 first = camera.WorldToViewportPoint(scenario.First.player.transform.position);
        Vector3 second = camera.WorldToViewportPoint(scenario.Second.player.transform.position);

        Assert.That(InFrameX(first), Is.True, $"player 1 left the frame ({when}): viewport {first}");
        Assert.That(InFrameX(second), Is.True, $"player 2 left the frame ({when}): viewport {second}");
    }

    /// <summary>Holds a stick for a time, asserting on every frame that both humans stay in frame.</summary>
    private IEnumerator MoveWatchingBoth(Gamepad pad, Vector2 direction, float seconds, Camera camera, Scenario scenario, string label)
    {
        Set(pad.leftStick, direction);
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
            AssertBothInFrame(camera, scenario, label);
        }

        Set(pad.leftStick, Vector2.zero);
    }

    private static Vector3 Middle(Scenario scenario)
    {
        return (scenario.First.player.transform.position + scenario.Second.player.transform.position) * 0.5f;
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator TheSharedCameraFollowsThePairAsPlayerTwoWalksAwayCrossesPlayerOneAndReconverges()
    {
        InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        Assert.That(camera, Is.Not.Null, "the scene has a main camera");
        Vector3 restCamera = camera.transform.position;
        Vector3 firstStart = scenario.First.player.transform.position;
        TestContext.WriteLine($"[two-human camera] level={scenario.Match.Level.DisplayName} mode={scenario.Match.Mode.DisplayName} rest camera={restCamera} fov={camera.fieldOfView} aspect={camera.aspect}");

        Assert.That(scenario.Second.player.transform.position.x, Is.LessThan(firstStart.x), "player 2 spawns to the left of player 1");
        AssertBothInFrame(camera, scenario, "spawn");

        // Player 2 walks right, past player 1 (who stands still), to a meaningful separation.
        yield return MoveWatchingBoth(padB, Vector2.right, 3.3f, camera, scenario, "player 2 walking away");
        yield return Hold(CameraSettleSeconds);

        float separation = scenario.Second.player.transform.position.x - scenario.First.player.transform.position.x;
        TestContext.WriteLine($"[two-human camera] apart: separation={separation:0.0} camera={camera.transform.position}");
        Assert.That(separation, Is.GreaterThanOrEqualTo(CertifiedSeparation - 1f), "the walk must reach a meaningful separation");
        AssertBothInFrame(camera, scenario, "apart, settled");
        Assert.That(Travelled(scenario.First, firstStart), Is.LessThan(StayedWithin), "player 1 stood still throughout");
        Assert.That(
            Mathf.Abs(camera.transform.position.x - Middle(scenario).x),
            Is.LessThan(0.75f),
            "the camera is centred on the pair, not on slot 0");
        Assert.That(
            Mathf.Abs(camera.transform.position.x - scenario.First.player.transform.position.x),
            Is.GreaterThan(2f),
            "the camera did not stay on player 1");
        Assert.That(camera.transform.position.z, Is.LessThan(restCamera.z - 0.5f), "the camera pulled back to fit both");

        // ...and back again, crossing player 1 a second time.
        yield return MoveWatchingBoth(padB, Vector2.left, 3.6f, camera, scenario, "player 2 reconverging");
        yield return Hold(CameraSettleSeconds);

        TestContext.WriteLine($"[two-human camera] reconverged: camera={camera.transform.position}");
        AssertBothInFrame(camera, scenario, "reconverged");
        Assert.That(camera.transform.position.z, Is.EqualTo(restCamera.z).Within(0.6f), "the camera returned to its authored depth");
        Assert.That(Mathf.Abs(camera.transform.position.x - Middle(scenario).x), Is.LessThan(0.75f));
        Assert.That(Travelled(scenario.First, firstStart), Is.LessThan(StayedWithin), "player 1 still has not moved");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator PlayerOneWalkingAwayIsFramedToo()
    {
        // The mirror of the walk above: nothing about the camera may favour a slot.
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        Vector3 secondStart = scenario.Second.player.transform.position;

        yield return MoveWatchingBoth(padA, Vector2.left, 3f, camera, scenario, "player 1 walking away");
        yield return Hold(CameraSettleSeconds);

        AssertBothInFrame(camera, scenario, "player 1 apart, settled");
        Assert.That(Travelled(scenario.Second, secondStart), Is.LessThan(StayedWithin), "player 2 stood still throughout");
        Assert.That(Mathf.Abs(camera.transform.position.x - Middle(scenario).x), Is.LessThan(0.75f));
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator TheGoalInsetIsHeldClosedForTwoHumansEvenWhenPlayerOneIsFarFromTheRim()
    {
        // The goal inset is opened by player 0's distance from the rim, which says nothing about the
        // second human, so a two-human match never opens it.
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        Camera goal = null;
        foreach (Camera candidate in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (candidate.gameObject.scene.IsValid() && candidate.name.Contains("on_goal"))
            {
                goal = candidate;
            }
        }

        Assert.That(goal, Is.Not.Null, "the certified arena has a goal camera to hold closed");

        Set(padA.leftStick, Vector2.left);
        float end = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
            Assert.That(goal.gameObject.activeInHierarchy, Is.False, "the goal inset opened during a two-human match");
        }

        Set(padA.leftStick, Vector2.zero);
        Assert.That(scenario.First.player.transform.position.x, Is.LessThan(-8f), "player 1 is far enough from the rim to open the inset in a one-human match");
        AssertBothInFrame(camera, scenario, "player 1 far from the rim");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator PathologicalSeparationBoundsTheZoomInsteadOfFollowingWithoutLimit()
    {
        InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        Vector3 restCamera = camera.transform.position;

        // Far past anything the framing can hold: well over thirty world units.
        Set(padB.leftStick, Vector2.right);
        yield return Hold(9f);
        Set(padB.leftStick, Vector2.zero);
        yield return Hold(CameraSettleSeconds);

        Vector3 cameraPosition = camera.transform.position;
        float pulledBack = restCamera.z - cameraPosition.z;
        TestContext.WriteLine($"[two-human camera] pathological: separation={scenario.Second.player.transform.position.x - scenario.First.player.transform.position.x:0.0} camera={cameraPosition} pulledBack={pulledBack:0.0}");

        Assert.That(float.IsFinite(cameraPosition.x) && float.IsFinite(cameraPosition.y) && float.IsFinite(cameraPosition.z), Is.True);
        Assert.That(pulledBack, Is.GreaterThan(3f), "the camera did zoom out");
        Assert.That(pulledBack, Is.LessThan(8f), "the zoom stopped at its bound instead of following the separation");
        Assert.That(Mathf.Abs(cameraPosition.x - Middle(scenario).x), Is.LessThan(1.5f), "the camera still tracks the pair's centre");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator TheCameraDegradesFromTwoHumansToOneToNoneWithoutThrowing()
    {
        InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        Vector3 restCamera = camera.transform.position;
        GameObject firstActor = scenario.First.player;
        GameObject secondActor = scenario.Second.player;

        // Two -> one: player 2's actor becomes unavailable and the camera settles on player 1.
        secondActor.SetActive(false);
        yield return Hold(CameraSettleSeconds);
        Assert.That(
            Mathf.Abs(camera.transform.position.x - firstActor.transform.position.x),
            Is.LessThan(0.75f),
            "one live human: the camera frames that human");
        Assert.That(camera.transform.position.z, Is.EqualTo(restCamera.z).Within(0.6f));
        Assert.That(InFrameX(camera.WorldToViewportPoint(firstActor.transform.position)), Is.True);

        // One -> none: nothing to follow, so the camera holds still.
        firstActor.SetActive(false);
        Vector3 held = camera.transform.position;
        yield return Hold(1f);
        Assert.That(Vector3.Distance(camera.transform.position, held), Is.LessThan(0.01f), "no live human: the camera holds");

        // Both actors are available again: the pair is framed once more.
        firstActor.SetActive(true);
        secondActor.SetActive(true);
        yield return Hold(CameraSettleSeconds);
        AssertBothInFrame(camera, scenario, "both humans restored");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator TwoHumansPlayTheMatchToItsNormalEndWithTheirOwnStatsIntact()
    {
        // The match ends through GameRules' own clock, so its end-of-match work (which saves the
        // primary human's score) runs for real - against a throwaway database, never the developer's.
        // The database is swapped in only once gameplay is up: an empty profile database would change
        // which character the production launch picks by default.
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        matchDatabase = new LocalProfileTestDatabase();
        yield return matchDatabase.Open();
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        MatchController match = MatchController.instance;
        Assert.That(match, Is.Not.Null);
        Assert.That(match.IsPlaying, Is.True, "the two-human match is live");

        scenario.First.gameStats.TotalPoints = 12;
        scenario.Second.gameStats.TotalPoints = 7;

        // Play the pair apart and back so the shared camera works while the clock runs down.
        yield return MoveWatchingBoth(padB, Vector2.right, 2f, camera, scenario, "playing out the match");
        yield return MoveWatchingBoth(padA, Vector2.left, 1f, camera, scenario, "playing out the match");

        // Let the clock run out quickly rather than sit through a full round.
        Time.timeScale = 30f;
        float deadline = Time.realtimeSinceStartup + 90f;
        while (!match.IsOver && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Time.timeScale = 1f;
        Assert.That(match.IsOver, Is.True, "the match reached its end on its own clock");
        TestContext.WriteLine($"[two-human match] ended: reason={match.EndReason} phase={match.Phase}");

        deadline = Time.realtimeSinceStartup + 30f;
        while (match.Phase != MatchPhase.Completed && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        TestContext.WriteLine($"[two-human match] phase after end work: {match.Phase}");
        Assert.That(match.Phase, Is.EqualTo(MatchPhase.Completed), "the end-of-match work finished for a two-human match");
        Assert.That(scenario.First.gameStats.TotalPoints, Is.EqualTo(12), "player 1 kept its own score");
        Assert.That(scenario.Second.gameStats.TotalPoints, Is.EqualTo(7), "player 2 kept its own score");
    }

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator ACpuParticipantIsNeverAFramingTarget()
    {
        InputSystem.AddDevice<Gamepad>();
        InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        Vector3 restCamera = camera.transform.position;
        float expectedX = Middle(scenario).x;

        // A CPU participant registered far away. If the camera counted it, the pair's framing would
        // swing toward it and pull back; it must not.
        GameObject decoyActor = new GameObject("cpu-framing-decoy");
        decoyActor.transform.position = new Vector3(expectedX + 60f, 0f, scenario.First.player.transform.position.z);
        PlayerIdentifier decoy = decoyActor.AddComponent<PlayerIdentifier>();
        decoy.isCpu = true;
        decoy.pid = 2;
        decoy.player = decoyActor;
        decoy.autoPlayer = decoyActor;
        scenario.Registry.Add(decoy);

        try
        {
            yield return Hold(CameraSettleSeconds);

            Assert.That(Mathf.Abs(camera.transform.position.x - expectedX), Is.LessThan(0.75f), "the camera stayed on the two humans");
            Assert.That(camera.transform.position.z, Is.EqualTo(restCamera.z).Within(0.6f), "the CPU did not widen the framing");
            AssertBothInFrame(camera, scenario, "with a distant CPU registered");
        }
        finally
        {
            scenario.Registry.MutableParticipants.Remove(decoy);
            UnityEngine.Object.Destroy(decoyActor);
        }
    }

}
#endif
