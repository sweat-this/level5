#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Level5.BackendV2;
using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The first production local-simultaneous slice through the real scenes: Most Points, two local
/// humans, The Scrapyard, Best of 3.
///
/// <code>
/// Start -> Local Versus -> Mode: Local Simultaneous -> Create -> Play Game (two humans, one match)
///       -> both scores on the HUD -> game ends through GameRules -> one atomic pair saved
///       -> Continue Series -> Local Versus -> next game ... -> completed -> restart -> history intact
/// </code>
///
/// Two virtual gamepads stand in for the two humans (under <c>InputTestFixture</c>, which also removes
/// the machine's real devices) so the launcher's own device preflight and the gameplay scene's own
/// device plan see exactly what the test set up. They do not substitute for the physical
/// two-gamepad / keyboard+gamepad certification: see docs/player-input-architecture.md.
///
/// The only things driven from the test are the two scores (there is no player to shoot) and the
/// match ending, which goes through the real <c>GameRules.RequestGameOver</c> and its real match-end
/// retry loop; "Continue Series" is pressed by starting <c>Pause.loadstartScreen</c>. Reaches
/// <c>Assembly-CSharp</c> types by object name and runtime type name, like
/// <see cref="LocalVersusProductionSmokePlayModeTests"/>.
/// </summary>
public class LocalSimultaneousVersusProductionPlayModeTests
{
    private const float SceneTimeoutSeconds = 90f;

    private string root;
    private InputTestFixture input;
    private Gamepad padA;
    private Gamepad padB;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "level5-local-simultaneous-" + Guid.NewGuid().ToString("N"));
        input = new InputTestFixture();
        input.Setup();
        padA = InputSystem.AddDevice<Gamepad>();
        padB = InputSystem.AddDevice<Gamepad>();

        VersusRuntime.Override(new FileVersusSeriesRepository(root));
        BackendV2SessionStore.Clear();
        LocalVersusNavigationState.Clear();
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        BackendV2SessionStore.Clear();
        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();

        yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("local-simultaneous-blank");
        input.TearDown();
        input = null;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [UnityTest]
    [Timeout(1200000)]
    public IEnumerator ABestOf3SimultaneousSeriesIsPlayedToCompletionThroughTheProductionScreens()
    {
        yield return OpenLocalVersusFromStart();

        // create it as a same-time series: only Most Points is offered, and nothing has a next participant
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        Assert.That(TextOf("modeButton"), Does.Contain("Local Simultaneous"));
        Assert.That(TextOf("rulesetButton"), Does.Contain("Most Points"));
        Assert.That(FindButton("rulesetButton").interactable, Is.False, "only one ruleset supports simultaneous play");

        FindButton("createButton").onClick.Invoke();
        yield return null;

        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1));
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        VersusSeries created = Durable(id);
        Assert.That(created.Mode, Is.EqualTo(VersusMode.LocalSimultaneous));
        Assert.That(created.Snapshot.GameCount, Is.EqualTo(3));
        Assert.That(created.Snapshot.GameAt(0).Id.Value, Is.EqualTo("most-points"));
        Assert.That(TextOf("playTurnButton"), Is.EqualTo("Play Game"));
        Assert.That(FindButton("character2Button").gameObject.activeInHierarchy, Is.True, "Player 2's selector is shown");
        Assert.That(TextOf("character2Button"), Does.StartWith("Player 2 ("));
        Assert.That(TextOf("characterButton"), Does.StartWith("Player 1 ("));
        Assert.That(TextOf("seriesDetail"), Does.Contain("Both players play at once."));
        Assert.That(TextOf("levelButton"), Does.Contain("Scrapyard"), "the only certified multiplayer arena");

        // game 1: Player 2 outscores Player 1, so the second participant wins - identity by slot, not by rank
        yield return PlayGame(id, firstScore: 12, secondScore: 30);
        VersusSeries afterOne = Durable(id);
        Assert.That(afterOne.Games[0].Result.WinnerId, Is.EqualTo(afterOne.Participants.Second.Id));
        Assert.That(afterOne.Score.SecondWins, Is.EqualTo(1));
        Assert.That(afterOne.CurrentGame.Index, Is.EqualTo(1));
        string first = afterOne.Participants.First.DisplayName;
        string second = afterOne.Participants.Second.DisplayName;
        Assert.That(TextOf("seriesDetail"), Does.Contain(first + " 0 - 1 " + second));
        Assert.That(TextOf("seriesDetail"), Does.Contain("Game 2 of 3"));
        Assert.That(FindButton("playTurnButton").interactable, Is.True, "the series carries on");

        // game 2 and 3: Player 1 wins both
        yield return PlayGame(id, firstScore: 40, secondScore: 15);
        Assert.That(Durable(id).IsActive, Is.True, "level at one game each");
        yield return PlayGame(id, firstScore: 33, secondScore: 21);

        VersusSeries finished = Durable(id);
        Assert.That(finished.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(finished.Result.WinnerId, Is.EqualTo(finished.Participants.First.Id));
        Assert.That(finished.Score.FirstWins, Is.EqualTo(2));
        Assert.That(finished.Score.SecondWins, Is.EqualTo(1));
        Assert.That(TextOf("seriesDetail"), Does.Contain("wins the series."));
        Assert.That(FindButton("playTurnButton").interactable, Is.False, "a finished series offers no game");

        // restart the application: the completed series and its history come back from the file alone
        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusRuntime.Override(new FileVersusSeriesRepository(root));
        yield return OpenLocalVersusFromStart();

        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1));
        VersusSeries restored = Durable(id);
        Assert.That(restored.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(restored.Games[0].Result.WinnerId, Is.EqualTo(restored.Participants.Second.Id));
        Assert.That(restored.Games[1].Result.WinnerId, Is.EqualTo(restored.Participants.First.Id));
        Assert.That(TextOf("seriesDetail"), Does.Contain("wins the series."));
    }

    [UnityTest]
    [Timeout(900000)]
    public IEnumerator ALaunchedGameSeatsTwoHumansInSeriesOrderAndShowsBothScores()
    {
        yield return OpenLocalVersusFromStart();
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        FindButton("createButton").onClick.Invoke();
        yield return null;
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        VersusSeries created = Durable(id);

        yield return LaunchGame(id);

        // the roster is exactly two humans, slot 0 the first participant and slot 1 the second
        MatchConfiguration match = ActiveMatch.Configuration;
        Assert.That(match.Roster.Count, Is.EqualTo(2));
        Assert.That(match.Roster.LocalHumanCount, Is.EqualTo(2));
        Assert.That(match.Roster.GetBySlotId(0).ParticipantId, Is.EqualTo(created.Participants.First.Id.Value));
        Assert.That(match.Roster.GetBySlotId(1).ParticipantId, Is.EqualTo(created.Participants.Second.Id.Value));
        Assert.That(ActiveVersusAttempt.ParticipantId, Is.EqualTo(created.Participants.First.Id));
        Assert.That(ActiveVersusAttempt.SecondParticipantId, Is.EqualTo(created.Participants.Second.Id));

        // the gameplay scene composed two distinct players, each on its own gamepad
        PlayerRegistry players = LevelRuntimeContext.instance.Players;
        PlayerIdentifier playerOne = players.GetBySlot(0);
        PlayerIdentifier playerTwo = players.GetBySlot(1);
        Assert.That(players.Count, Is.EqualTo(2));
        Assert.That(playerOne.gameStats, Is.Not.Null.And.Not.SameAs(playerTwo.gameStats));
        LocalGameplayDevicePlan plan = PlayerControlsProvider.GameplayDevicePlan;
        Assert.That(plan.Layout, Is.EqualTo(LocalGameplayDeviceLayout.TwoGamepads));
        Assert.That(plan.DevicesFor(0), Is.EqualTo(new InputDevice[] { padA }), "slot 0 listens to the first gamepad only");
        Assert.That(plan.DevicesFor(1), Is.EqualTo(new InputDevice[] { padB }), "slot 1 listens to the second gamepad only");
        Assert.That(MatchRuntime.LocalInputSlotFor(0), Is.EqualTo(0));
        Assert.That(MatchRuntime.LocalInputSlotFor(1), Is.EqualTo(1));

        // both scores are on the HUD, in roster order, not sorted by who is ahead
        playerOne.gameStats.TotalPoints = 12;
        playerTwo.gameStats.TotalPoints = 30;
        yield return null;
        yield return null;
        Assert.That(LabelNamed("display_p1_score"), Does.StartWith("Player 1").And.Contain("points : 12"));
        Assert.That(LabelNamed("display_p2_score"), Does.StartWith("Player 2").And.Contain("points : 30"));

        // a leave request mid-game is refused: nobody is forfeited, restart is unavailable
        string gameplayScene = SceneManager.GetActiveScene().name;
        Assert.That(VersusQuitPolicy.SimultaneousGameInProgress, Is.True);
        Assert.That(AnyLabelReads(Pause.LocalVersusSimultaneousExitLabel), Is.True,
            "the pause menu says exit is unavailable instead of offering a forfeit");
        Assert.That(AnyLabelReads(Pause.LocalVersusForfeitLabel), Is.False);

        StartPauseAction("loadstartScreen");
        yield return AssertStaysInGameplay(gameplayScene, "Start/Menu must not leave a simultaneous game");
        Assert.That(AnyLabelReads(Pause.ExitUnavailableSimultaneousMessage), Is.True, "the refused button says why");
        StartPauseAction("Quit");
        yield return AssertStaysInGameplay(gameplayScene, "Quit must not run while a simultaneous game is live");
        MonoBehaviour pause = FindBehaviourByTypeName("Pause");
        pause.GetType().GetMethod("reloadScene", BindingFlags.Public | BindingFlags.Instance).Invoke(pause, null);
        yield return AssertStaysInGameplay(gameplayScene, "Restart is refused while a simultaneous game is active");

        VersusSeries stored = Durable(id);
        Assert.That(stored.Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "nobody was forfeited");
        Assert.That(stored.ViewFor(created.Participants.First.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
        Assert.That(stored.ViewFor(created.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.EqualTo(AttemptState.Started));
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
    }

    [UnityTest]
    [Timeout(900000)]
    public IEnumerator AFinishedGameIsCompetitionOnlyAndItsPairIsSavedAtomically()
    {
        Guid playerId = Guid.NewGuid();
        FailableRepository repository = new FailableRepository(new FileVersusSeriesRepository(root));
        VersusRuntime.Override(repository);

        yield return OpenLocalVersusFromStart();
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        FindButton("createButton").onClick.Invoke();
        yield return null;
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        VersusSeries created = Durable(id);
        yield return LaunchGame(id);

        PlayerRegistry players = LevelRuntimeContext.instance.Players;
        players.GetBySlot(0).gameStats.TotalPoints = 44;
        players.GetBySlot(1).gameStats.TotalPoints = 16;

        // a signed-in account must not receive a two-player match as slot 0's ordinary result
        BackendV2SessionStore.Set(new BackendV2Session(
            "access-token", DateTimeOffset.UtcNow.AddHours(1), playerId, "refresh-token", DateTimeOffset.UtcNow.AddDays(30)));

        // the real match-end loop, with a disk that refuses the pair
        MonoBehaviour rules = FindBehaviourByTypeName("GameRules");
        Assert.That(rules, Is.Not.Null, "the gameplay scene has no GameRules");
        repository.FailSaves = true;
        int refusedBefore = repository.RefusedSaves;
        rules.GetType().GetMethod("RequestGameOver", BindingFlags.Public | BindingFlags.Instance).Invoke(rules, null);

        float deadline = Time.realtimeSinceStartup + 30f;
        while (repository.RefusedSaves < refusedBefore + 2 && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(repository.RefusedSaves, Is.GreaterThanOrEqualTo(refusedBefore + 2),
            "GameRules did not retry the simultaneous result while the save was failing");
        Assert.That(ActiveVersusAttempt.IsActive, Is.True, "a refused save leaves both attempts outstanding");
        VersusSeries pending = Durable(id);
        Assert.That(pending.Games[0].Status, Is.EqualTo(VersusGameStatus.Active));
        Assert.That(pending.ViewFor(created.Participants.First.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed));
        Assert.That(pending.ViewFor(created.Participants.Second.Id).CurrentGame.OwnAttemptState, Is.Not.EqualTo(AttemptState.Completed),
            "neither half of the pair is durable");
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.False, "leaving waits for the pair");
        Assert.That(Durable(id).Games[0].Status, Is.EqualTo(VersusGameStatus.Active), "and nobody is forfeited");

        repository.FailSaves = false;
        deadline = Time.realtimeSinceStartup + 30f;
        while (ActiveVersusAttempt.IsActive && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "the next pass saved the pair and cleared the context");
        VersusSeries stored = Durable(id);
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(stored.Participants.First.Id), "44 to 16");
        Assert.That(stored.Score.FirstWins, Is.EqualTo(1), "recorded exactly once");
        Assert.That(VersusQuitPolicy.TryPrepareForExplicitExit(), Is.True, "once recorded, leaving is ordinary");

        Assert.That(PendingMatchResultStore.GetRetryable(playerId), Is.Empty,
            "a simultaneous game never enters general Backend V2 match-result submission");
    }

    [UnityTest]
    [Timeout(900000)]
    public IEnumerator TwoHumansMayPlayTheSameCharacterAndStillGetSeparateActorsAndResults()
    {
        yield return OpenLocalVersusFromStart();
        FindButton("modeButton").onClick.Invoke();
        yield return null;
        FindButton("createButton").onClick.Invoke();
        yield return null;
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        VersusSeries created = Durable(id);

        // cycle Player 2's selector until it names the same character as Player 1's
        string one = CharacterNameIn(TextOf("characterButton"));
        for (int press = 0; press < 300 &&CharacterNameIn(TextOf("character2Button")) != one; press++)
        {
            FindButton("character2Button").onClick.Invoke();
            yield return null;
        }

        Assert.That(CharacterNameIn(TextOf("character2Button")), Is.EqualTo(one), "both selectors name the same character");

        yield return LaunchGame(id);

        MatchConfiguration match = ActiveMatch.Configuration;
        Assert.That(match.Roster.GetBySlotId(0).Character.CharacterId, Is.EqualTo(match.Roster.GetBySlotId(1).Character.CharacterId));
        PlayerRegistry players = LevelRuntimeContext.instance.Players;
        PlayerIdentifier playerOne = players.GetBySlot(0);
        PlayerIdentifier playerTwo = players.GetBySlot(1);
        Assert.That(players.Count, Is.EqualTo(2));
        Assert.That(playerOne.player, Is.Not.Null.And.Not.SameAs(playerTwo.player), "two distinct actors");
        Assert.That(playerOne.playerController, Is.Not.Null.And.Not.SameAs(playerTwo.playerController));
        Assert.That(playerOne.gameStats, Is.Not.Null.And.Not.SameAs(playerTwo.gameStats));
        Assert.That(playerOne.basketBallController, Is.Not.Null.And.Not.SameAs(playerTwo.basketBallController));

        playerOne.gameStats.TotalPoints = 10;
        playerTwo.gameStats.TotalPoints = 25;
        MonoBehaviour rules = FindBehaviourByTypeName("GameRules");
        rules.GetType().GetMethod("RequestGameOver", BindingFlags.Public | BindingFlags.Instance).Invoke(rules, null);

        float deadline = Time.realtimeSinceStartup + 30f;
        while (ActiveVersusAttempt.IsActive && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "the result was recorded");
        VersusSeries stored = Durable(id);
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(created.Participants.Second.Id),
            "25 to 10 goes to the second participant even with identical characters");
    }

    // ------------------------------------------------------------------ steps

    /// <summary>One whole game: Play Game, set the two scores, end through GameRules, wait for the pair to land, Continue Series.</summary>
    private IEnumerator PlayGame(SeriesId id, int firstScore, int secondScore)
    {
        yield return LaunchGame(id);

        PlayerRegistry players = LevelRuntimeContext.instance.Players;
        players.GetBySlot(0).gameStats.TotalPoints = firstScore;
        players.GetBySlot(0).gameStats.ShotMade = 5;
        players.GetBySlot(0).gameStats.ShotAttempt = 10;
        players.GetBySlot(1).gameStats.TotalPoints = secondScore;
        players.GetBySlot(1).gameStats.ShotMade = 5;
        players.GetBySlot(1).gameStats.ShotAttempt = 10;

        MonoBehaviour rules = FindBehaviourByTypeName("GameRules");
        Assert.That(rules, Is.Not.Null, "the gameplay scene has no GameRules");
        rules.GetType().GetMethod("RequestGameOver", BindingFlags.Public | BindingFlags.Instance).Invoke(rules, null);

        float deadline = Time.realtimeSinceStartup + 30f;
        while (ActiveVersusAttempt.IsActive && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "GameRules never recorded the simultaneous result");
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True, "the return hint outlives the attempt");

        yield return ContinueSeries();
    }

    /// <summary>Presses Play Game and waits for the live gameplay scene with both humans seated.</summary>
    private IEnumerator LaunchGame(SeriesId id)
    {
        Button play = FindButton("playTurnButton");
        Assert.That(play.interactable, Is.True, "Play Game is not available: " + TextOf("turnMessage"));
        play.onClick.Invoke();

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        bool seated = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus
                && LevelRuntimeContext.instance != null
                && LevelRuntimeContext.instance.Players != null
                && LevelRuntimeContext.instance.Players.Count == 2)
            {
                seated = true;
                break;
            }

            yield return null;
        }

        Assert.That(seated, Is.True, "gameplay never started two humans from Play Game");
        yield return null;

        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.IsSimultaneous, Is.True);
        Assert.That(ActiveVersusAttempt.SeriesId, Is.EqualTo(id));
        Assert.That(ActiveMatch.Configuration.Roster.LocalHumanCount, Is.EqualTo(2));
        Assert.That(ActiveMatch.Configuration.Level.DisplayName, Does.Contain("Scrapyard"));
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True);
    }

    private IEnumerator ContinueSeries()
    {
        StartPauseAction("loadstartScreen");

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline
            && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
        {
            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.Not.EqualTo(Constants.SCENE_NAME_level_00_start),
                "Continue Series went to the Start screen instead of Local Versus");
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus));
        yield return null;
        yield return null;
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False, "the screen consumed the return hint");
    }

    private IEnumerator OpenLocalVersusFromStart()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        yield return null;

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline
            && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
        {
            if (SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start && EventSystem.current != null)
            {
                GameObject entry = GameObject.Find("local_versus_menu");
                if (entry != null)
                {
                    ExecuteEvents.Execute(entry, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                }
            }

            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }
        }

        Assert.That(
            SceneManager.GetActiveScene().name,
            Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus),
            "the Start screen's Local Versus entry never opened the Local Versus screen");

        yield return null;
        yield return null;

        MonoBehaviour controller = RealScenePlayModeTestSupport.FindActiveBehaviourInScene(
            SceneManager.GetActiveScene(), "LocalVersusController");
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.enabled, Is.True, "LocalVersusController disabled itself");
    }

    // ------------------------------------------------------------------ helpers

    private static void StartPauseAction(string methodName)
    {
        MonoBehaviour pause = FindBehaviourByTypeName("Pause");
        Assert.That(pause, Is.Not.Null, "the gameplay scene has no Pause component");
        MethodInfo method = pause.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, methodName);
        pause.StartCoroutine((IEnumerator)method.Invoke(pause, null));
    }

    private static IEnumerator AssertStaysInGameplay(string gameplayScene, string because)
    {
        float until = Time.realtimeSinceStartup + 1.5f;
        for (int frame = 0; frame < 10 || Time.realtimeSinceStartup < until; frame++)
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(gameplayScene), because);
            yield return null;
        }
    }

    private static bool AnyLabelReads(string text)
    {
        foreach (Text label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label.text == text)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The character name in a selector label such as "Player 1 (Ann): Dr Blood".</summary>
    private static string CharacterNameIn(string selectorText)
    {
        int at = selectorText.IndexOf("): ", StringComparison.Ordinal);
        return at < 0 ? selectorText : selectorText.Substring(at + 3);
    }

    /// <summary>The legacy uGUI label with this object name, or empty.</summary>
    private static string LabelNamed(string objectName)
    {
        foreach (Text label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label.gameObject.name == objectName)
            {
                return label.text;
            }
        }

        return string.Empty;
    }

    private VersusSeries Durable(SeriesId id)
    {
        VersusSeries series = new FileVersusSeriesRepository(root).Load(id);
        Assert.That(series, Is.Not.Null, "the series is not on disk");
        return series;
    }

    private static Button FindButton(string objectName)
    {
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Button button in sceneRoot.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == objectName)
                {
                    return button;
                }
            }
        }

        Assert.Fail("no button named '" + objectName + "' in " + SceneManager.GetActiveScene().name);
        return null;
    }

    /// <summary>Reads a TextMeshPro label by object name without referencing the TMP assembly.</summary>
    private static string TextOf(string objectName)
    {
        foreach (GameObject sceneRoot in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform t in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != objectName)
                {
                    continue;
                }

                foreach (Component component in t.GetComponents<Component>())
                {
                    PropertyInfo text = component == null ? null : component.GetType().GetProperty("text");
                    if (text != null && text.PropertyType == typeof(string) && component.GetType().Name.StartsWith("TextMeshPro"))
                    {
                        return (string)text.GetValue(component);
                    }
                }
            }
        }

        return string.Empty;
    }

    private static MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour != null && behaviour.GetType().Name == typeName)
            {
                return behaviour;
            }
        }

        return null;
    }

    /// <summary>The real on-disk store, until told to refuse saves. Loads always read the disk.</summary>
    private sealed class FailableRepository : IVersusSeriesRepository
    {
        private readonly IVersusSeriesRepository inner;

        public FailableRepository(IVersusSeriesRepository inner)
        {
            this.inner = inner;
        }

        public bool FailSaves { get; set; }

        public int RefusedSaves { get; private set; }

        public bool Save(VersusSeries series)
        {
            if (FailSaves)
            {
                RefusedSaves++;
                return false;
            }

            return inner.Save(series);
        }

        public VersusSeries Load(SeriesId id) => inner.Load(id);

        public bool Exists(SeriesId id) => inner.Exists(id);

        public IReadOnlyList<SeriesSummary> ListSummaries() => inner.ListSummaries();

        public bool Delete(SeriesId id) => inner.Delete(id);

        public bool Archive(SeriesId id) => inner.Archive(id);
    }
}
#endif
