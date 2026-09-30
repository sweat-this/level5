#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The production Local Versus screen and its pause menu, driven only by a gamepad through the real
/// <c>InputSystemUIInputModule</c> (the same route <c>UiSelectionAdapter</c> sets up) - no
/// <c>onClick.Invoke()</c> shortcuts. Devices are virtual, added under <c>InputTestFixture</c>, so this
/// certifies the navigation contract (every control reachable, no focus trap, actions match labels), not
/// a physical controller: see docs/versus-architecture.md, "Local Versus certification".
///
/// Navigation on these screens is Unity's automatic geometric navigation, so reachability is computed
/// from the same <c>Selectable.FindSelectableOn*</c> the module resolves a d-pad press through, and every
/// step of a journey is then made with a real d-pad press and checked against that prediction.
/// </summary>
public class LocalVersusGamepadNavigationPlayModeTests
{
    private const float SceneTimeoutSeconds = 90f;

    private enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    private InputTestFixture input;
    private Gamepad pad;
    private string root;

    [SetUp]
    public void SetUp()
    {
        // Controls cached by an earlier fixture belong to the Input System state this fixture is about to replace.
        ResetControlsProvider();
        input = new InputTestFixture();
        input.Setup();
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();

        root = Path.Combine(Path.GetTempPath(), "level5-local-versus-pad-" + Guid.NewGuid().ToString("N"));
        VersusRuntime.Override(new FileVersusSeriesRepository(root));
        LocalVersusNavigationState.Clear();
        pad = InputSystem.AddDevice<Gamepad>();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;
        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();

        // the scenes (whose controls touch the Input System in OnDisable) go before the Input System is restored
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        yield return null;
        yield return null;
        input.TearDown();
        ResetControlsProvider();

        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator EveryControlIsReachableAndCreateFormatAndPlayWorkFromTheGamepad()
    {
        yield return OpenLocalVersusWithTheGamepad();

        // before any series: the create form and Back are the only live controls
        HashSet<string> empty = ReachableFromSelection();
        Debug.Log("[LV-PAD] reachable with no series: " + string.Join(", ", empty));
        foreach (string name in new[] { "player1NameInputField", "player2NameInputField", "rulesetButton", "formatButton", "createButton", "backButton" })
        {
            Assert.That(empty, Does.Contain(name), name + " is unreachable with the d-pad");
        }

        foreach (string name in new[] { "playTurnButton", "characterButton", "levelButton", "seriesSelectButton" })
        {
            Assert.That(empty, Does.Not.Contain(name), "the disabled " + name + " must not be a navigation stop");
        }

        // Format cycles through the offered lengths from the gamepad
        yield return NavigateTo("formatButton");
        string before = TextOf("formatButton");
        yield return Tap(pad.buttonSouth);
        Assert.That(TextOf("formatButton"), Is.Not.EqualTo(before), "Submit on Format did not cycle it");
        for (int step = 0; step < 3; step++)
        {
            yield return Tap(pad.buttonSouth);
        }

        Assert.That(TextOf("formatButton"), Is.EqualTo(before), "four presses return to the starting format (1, 3, 5, 7)");

        // Create from the gamepad stores exactly one series
        yield return NavigateTo("createButton");
        yield return Tap(pad.buttonSouth);
        Assert.That(new FileVersusSeriesRepository(root).ListSummaries(), Has.Count.EqualTo(1), "Create from the gamepad stored one series");
        SeriesId id = new FileVersusSeriesRepository(root).ListSummaries()[0].Id;

        // the turn controls join the navigation graph
        HashSet<string> withSeries = ReachableFromSelection();
        Debug.Log("[LV-PAD] reachable with a series: " + string.Join(", ", withSeries));
        foreach (string name in new[] { "characterButton", "levelButton", "playTurnButton", "createButton", "backButton", "formatButton", "player1NameInputField" })
        {
            Assert.That(withSeries, Does.Contain(name), name + " is unreachable with the d-pad once a series exists");
        }

        // Character and Arena cycle from the gamepad
        yield return NavigateTo("characterButton");
        string character = TextOf("characterButton");
        yield return Tap(pad.buttonSouth);
        Assert.That(TextOf("characterButton"), Is.Not.EqualTo(character), "Submit on Character did not cycle it");
        yield return NavigateTo("levelButton");
        string level = TextOf("levelButton");
        yield return Tap(pad.buttonSouth);
        Assert.That(TextOf("levelButton"), Is.Not.EqualTo(level), "Submit on Arena did not cycle it");

        // Play Turn from the gamepad launches the gameplay scene for the participant the button names
        yield return NavigateTo("playTurnButton");
        string playLabel = TextOf("playTurnButton");
        Assert.That(playLabel, Does.StartWith("Play Turn: "));
        yield return Tap(pad.buttonSouth);
        yield return WaitForGameplay();
        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.SeriesId, Is.EqualTo(id));
        string upName = new FileVersusSeriesRepository(root).Load(id).Participants.Find(ActiveVersusAttempt.ParticipantId).DisplayName;
        Assert.That(playLabel, Is.EqualTo("Play Turn: " + upName), "the button named the participant whose turn it launched");
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator AGamepadThatMovesOntoANameFieldIsNotTrappedInIt()
    {
        // Found in certification: selecting a field with the d-pad started editing it, and an active
        // field swallowed the d-pad, the stick and Cancel - only Submit let go. Selecting must not edit.
        yield return OpenLocalVersusWithTheGamepad();
        yield return NavigateTo("player1NameInputField");
        Assert.That(IsEditing("player1NameInputField"), Is.False, "moving onto a field must not start editing it");

        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("player2NameInputField"), "the d-pad moves on from the first field");
        Assert.That(IsEditing("player2NameInputField"), Is.False);

        // ...and editing is still a deliberate act, released the same way
        yield return Tap(pad.buttonSouth);
        Assert.That(IsEditing("player2NameInputField"), Is.True, "Submit starts editing the selected field");
        yield return Tap(pad.buttonSouth);
        Assert.That(IsEditing("player2NameInputField"), Is.False, "Submit finishes editing");
        yield return Tap(pad.dpad.down);
        Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.Not.EqualTo("player2NameInputField"), "and the d-pad moves on again");
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator BackReturnsToTheStartScreenFromTheGamepad()
    {
        yield return OpenLocalVersusWithTheGamepad();
        yield return NavigateTo("backButton");
        yield return Tap(pad.buttonSouth);

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline && SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_local_versus)
        {
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo(Constants.SCENE_NAME_level_00_local_versus), "Back never left the screen");
        float until = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < until && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_start)
        {
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Constants.SCENE_NAME_level_00_start), "Back returns to Start");
    }
    [UnityTest]
    [Timeout(600000)]
    public IEnumerator ThePauseMenuIsNavigableByGamepadAndItsQuitTurnLabelForfeitsWhatItSays()
    {
        yield return OpenLocalVersusWithTheGamepad();
        yield return NavigateTo("createButton");
        yield return Tap(pad.buttonSouth);
        SeriesId id = new FileVersusSeriesRepository(root).ListSummaries()[0].Id;
        ParticipantId quitter = ExpectedNext(new FileVersusSeriesRepository(root).Load(id));
        yield return NavigateTo("playTurnButton");
        yield return Tap(pad.buttonSouth);
        yield return WaitForGameplay();

        // a match opens on a hold ("press Start to begin"); the gamepad's Start begins it, and Select (Back) pauses it
        Assert.That(Time.timeScale, Is.EqualTo(0f), "the match opens held");
        yield return Tap(pad.startButton);
        yield return Frames(10);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "the gamepad Start button began the match");
        yield return Tap(pad.selectButton);
        yield return Frames(10);
        Assert.That(Time.timeScale, Is.EqualTo(0f), "the gamepad Select (Back) button paused the match");

        HashSet<string> stops = ReachableFromSelection();
        Debug.Log("[LV-PAD] pause stops: " + string.Join(", ", stops));
        Assert.That(stops, Has.Count.GreaterThanOrEqualTo(3), "the pause menu is navigable by d-pad");

        // the start-screen action's live label says what it will do, and doing it does that
        Text quitTurn = null;
        foreach (Text label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (label.text == Pause.LocalVersusForfeitLabel)
            {
                quitTurn = label;
            }
        }

        Assert.That(quitTurn, Is.Not.Null, "mid-turn the pause menu's start-screen action reads '" + Pause.LocalVersusForfeitLabel + "'");
        yield return NavigateTo(quitTurn.GetComponentInParent<Button>().gameObject);
        yield return Tap(pad.buttonSouth);

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.Not.EqualTo(Constants.SCENE_NAME_level_00_start));
            yield return null;
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus), "Quit Turn returned to the series screen");
        yield return Frames(3);
        VersusSeries stored = new FileVersusSeriesRepository(root).Load(id);
        Assert.That(stored.Games[0].Status, Is.EqualTo(VersusGameStatus.Forfeited), "the label said Quit Turn (Forfeit) and that is what happened");
        Assert.That(stored.Games[0].Result.WinnerId, Is.EqualTo(stored.Participants.Opponent(quitter).Id));
    }

    private static void ResetControlsProvider()
    {
        // The same method Unity runs at SubsystemRegistration: disposes every cached controls instance.
        MethodInfo reset = typeof(PlayerControlsProvider).GetMethod("ResetState", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(reset, Is.Not.Null, "PlayerControlsProvider.ResetState must exist");
        reset.Invoke(null, null);
    }
    // ------------------------------------------------------------------ steps

    private IEnumerator OpenLocalVersusWithTheGamepad()
    {
        SceneManager.LoadScene(Constants.SCENE_NAME_level_00_start);
        yield return Frames(3);

        // The shipped entry is the authored Local Versus button on the Start screen.
        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        while (Time.realtimeSinceStartup < deadline && SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
        {
            if (SceneManager.GetActiveScene().name == Constants.SCENE_NAME_level_00_start && EventSystem.current != null)
            {
                GameObject entry = GameObject.Find("local_versus_menu");
                if (entry != null)
                {
                    // The Start screen's own input routing is not what is under test (and depends on which
                    // fixture ran before this one), so the shipped entry is submitted directly, as the smoke
                    // fixture does; everything from the Local Versus screen on is driven by the gamepad.
                    ExecuteEvents.Execute(entry, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
                }
            }

            yield return Frames(5);
        }

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus), "the Local Versus screen never opened");
        yield return Frames(3);
    }

    /// <summary>Every interactable control reachable from the current selection through d-pad moves.</summary>
    private static HashSet<string> ReachableFromSelection()
    {
        HashSet<string> names = new HashSet<string>();
        foreach (Selectable selectable in Reachable(CurrentSelectable()))
        {
            names.Add(selectable.name);
        }

        return names;
    }

    private static Selectable CurrentSelectable()
    {
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        Assert.That(selected, Is.Not.Null, "nothing is selected");
        return selected.GetComponent<Selectable>();
    }

    private static Selectable Neighbor(Selectable from, Direction direction)
    {
        switch (direction)
        {
            case Direction.Up:
                return from.FindSelectableOnUp();
            case Direction.Down:
                return from.FindSelectableOnDown();
            case Direction.Left:
                return from.FindSelectableOnLeft();
            default:
                return from.FindSelectableOnRight();
        }
    }

    private static IEnumerable<Selectable> Reachable(Selectable start)
    {
        HashSet<Selectable> seen = new HashSet<Selectable> { start };
        Queue<Selectable> queue = new Queue<Selectable>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            Selectable current = queue.Dequeue();
            foreach (Direction direction in (Direction[])Enum.GetValues(typeof(Direction)))
            {
                Selectable next = Neighbor(current, direction);
                if (next != null && next.IsInteractable() && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return seen;
    }

    /// <summary>Shortest sequence of d-pad moves from one control to another, or null when there is none.</summary>
    private static List<KeyValuePair<Direction, Selectable>> PathTo(Selectable from, Selectable target)
    {
        Dictionary<Selectable, KeyValuePair<Selectable, Direction>> cameFrom = new Dictionary<Selectable, KeyValuePair<Selectable, Direction>>();
        Queue<Selectable> queue = new Queue<Selectable>();
        queue.Enqueue(from);
        cameFrom[from] = default;
        while (queue.Count > 0 && !cameFrom.ContainsKey(target))
        {
            Selectable current = queue.Dequeue();
            foreach (Direction direction in (Direction[])Enum.GetValues(typeof(Direction)))
            {
                Selectable next = Neighbor(current, direction);
                if (next != null && next.IsInteractable() && !cameFrom.ContainsKey(next))
                {
                    cameFrom[next] = new KeyValuePair<Selectable, Direction>(current, direction);
                    queue.Enqueue(next);
                }
            }
        }

        if (!cameFrom.ContainsKey(target))
        {
            return null;
        }

        List<KeyValuePair<Direction, Selectable>> path = new List<KeyValuePair<Direction, Selectable>>();
        for (Selectable node = target; node != from; node = cameFrom[node].Key)
        {
            path.Insert(0, new KeyValuePair<Direction, Selectable>(cameFrom[node].Value, node));
        }

        return path;
    }

    private IEnumerator NavigateTo(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        Assert.That(target, Is.Not.Null, objectName + " is not in the scene");
        yield return NavigateTo(target);
    }

    /// <summary>Moves the selection to a control using real d-pad presses, checking every step lands where the graph says.</summary>
    private IEnumerator NavigateTo(GameObject target)
    {
        List<KeyValuePair<Direction, Selectable>> path = PathTo(CurrentSelectable(), target.GetComponent<Selectable>());
        Assert.That(path, Is.Not.Null, target.name + " cannot be reached from " + EventSystem.current.currentSelectedGameObject.name + " with the d-pad");
        foreach (KeyValuePair<Direction, Selectable> step in path)
        {
            yield return Tap(DpadFor(step.Key));
            Assert.That(
                EventSystem.current.currentSelectedGameObject,
                Is.EqualTo(step.Value.gameObject),
                "a d-pad " + step.Key + " press did not land on " + step.Value.name);
        }
    }

    private ButtonControl DpadFor(Direction direction)
    {
        switch (direction)
        {
            case Direction.Up:
                return pad.dpad.up;
            case Direction.Down:
                return pad.dpad.down;
            case Direction.Left:
                return pad.dpad.left;
            default:
                return pad.dpad.right;
        }
    }

    private IEnumerator Tap(ButtonControl button)
    {
        input.Set(button, 1f);
        yield return Frames(2);
        input.Set(button, 0f);
        yield return Frames(3);
    }

    private static IEnumerator Frames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            yield return null;
        }
    }

    private static IEnumerator WaitForGameplay()
    {
        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        PlayerController player = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (SceneManager.GetActiveScene().name != Constants.SCENE_NAME_level_00_local_versus)
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                if (player != null)
                {
                    break;
                }
            }

            yield return null;
        }

        Assert.That(player, Is.Not.Null, "gameplay never started from Play Turn");
        yield return Frames(5);
    }

    private static bool IsEditing(string fieldName)
    {
        foreach (Component component in GameObject.Find(fieldName).GetComponents<Component>())
        {
            if (component != null && component.GetType().Name == "TMP_InputField")
            {
                return (bool)component.GetType().GetProperty("isFocused").GetValue(component);
            }
        }

        Assert.Fail(fieldName + " has no TMP_InputField");
        return false;
    }

    private static ParticipantId ExpectedNext(VersusSeries series)
    {
        VersusGame game = series.CurrentGame;
        ParticipantId first = series.Participants.At(game.FirstAttemptParticipantIndex).Id;
        return series.ViewFor(first).CurrentGame.OwnAttemptState == AttemptState.Completed
            ? series.Participants.Opponent(first).Id
            : first;
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
}
#endif
