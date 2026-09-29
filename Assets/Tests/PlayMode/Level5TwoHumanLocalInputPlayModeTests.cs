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

    // ==================== camera / HUD characterization ====================

    [UnityTest]
    [Timeout(300000)]
    public IEnumerator BothHumansAreVisibleToTheSharedCameraAtSpawnAndItsFollowBehaviourIsRecorded()
    {
        Gamepad padA = InputSystem.AddDevice<Gamepad>();
        Gamepad padB = InputSystem.AddDevice<Gamepad>();

        Scenario scenario = null;
        yield return LaunchTwoHumanMatch(s => scenario = s);
        yield return Hold(0.5f);

        Camera camera = Camera.main;
        Assert.That(camera, Is.Not.Null, "the scene has a main camera");

        Vector3 first = camera.WorldToViewportPoint(scenario.First.player.transform.position);
        Vector3 second = camera.WorldToViewportPoint(scenario.Second.player.transform.position);
        TestContext.WriteLine($"[two-human camera] level={scenario.Match.Level.DisplayName} mode={scenario.Match.Mode.DisplayName}");
        TestContext.WriteLine($"[two-human camera] spawn viewport P1={first} P2={second}");

        Assert.That(InFrame(first), Is.True, "player 1 is on screen at spawn: " + first);
        Assert.That(InFrame(second), Is.True, "player 2 is on screen at spawn: " + second);

        // The camera follows slot 0 only (cameraUpdater reads the pid-0 participant). Walk player 2 away
        // while player 1 stands still and record where player 2 ends up in frame.
        Set(padB.leftStick, Vector2.right);
        yield return Hold(3f);
        Set(padB.leftStick, Vector2.zero);
        yield return null;

        Vector3 secondAfter = camera.WorldToViewportPoint(scenario.Second.player.transform.position);
        Vector3 firstAfter = camera.WorldToViewportPoint(scenario.First.player.transform.position);
        TestContext.WriteLine($"[two-human camera] after P2 walks: viewport P1={firstAfter} P2={secondAfter} (P1 stood still)");
    }

    private static bool InFrame(Vector3 viewport)
    {
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }
}
#endif
