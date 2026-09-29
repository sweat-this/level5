#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Level5.BackendV2;
using Level5.Core.Match;
using Level5.Core.Versus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The production Local Versus loop through the real scenes, with no test doubles between the player
/// and the screens:
///
/// <code>
/// Start -> Local Versus -> create best of 3 -> Play Turn -> gameplay -> result -> Continue Series
///       -> loading scene -> Local Versus -> next side's turn ... -> completed
/// </code>
///
/// Only two things are driven from the test because there is no player to do them: the match-end
/// result is reported through <see cref="VersusMatchReporter"/> (the exact call <c>GameRules</c> makes),
/// and "Continue Series" is pressed by starting <c>Pause.loadstartScreen</c> (the coroutine the pause
/// menu's start-screen button runs). Everything else - the Start button, the Local Versus screen's own
/// buttons, the launcher, the scene loads, the loading scene's routing - is the shipped path.
///
/// <c>StartManager</c>, <c>Pause</c> and the Local Versus controller live in <c>Assembly-CSharp</c>, so
/// this fixture reaches them by scene object name and runtime type name, like
/// <see cref="GameplayScenePlayModeHarness"/>.
/// </summary>
public class LocalVersusProductionSmokePlayModeTests
{
    private const float SceneTimeoutSeconds = 90f;

    private string root;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "level5-local-versus-smoke-" + Guid.NewGuid().ToString("N"));
        VersusRuntime.Override(new FileVersusSeriesRepository(root));
        BackendV2SessionStore.Clear();
        LocalVersusNavigationState.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusCatalogs.Reset();

        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [UnityTest]
    [Timeout(900000)]
    public IEnumerator ABestOf3IsPlayedToCompletionThroughTheProductionScreens()
    {
        yield return OpenLocalVersusFromStart();
        Assert.That(BackendV2SessionStore.Current, Is.Null, "no online session exists or is required");

        FindButton("createButton").onClick.Invoke();
        yield return null;

        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1), "Create Series stored one series");
        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        Assert.That(VersusRuntime.Coordinator.Load(id).Snapshot.GameCount, Is.EqualTo(3));

        int turns = 0;
        ParticipantId lastLaunched = default;
        while (VersusRuntime.Coordinator.Load(id).IsActive && turns < 6)
        {
            VersusSeries before = VersusRuntime.Coordinator.Load(id);
            ParticipantId expected = ExpectedNext(before);
            string expectedName = before.Participants.Find(expected).DisplayName;
            Assert.That(TextOf("seriesDetail"), Does.Contain(expectedName + " is up."), "the screen names the side the series says is up");

            // participant one wins every game
            int score = expected == before.Participants.First.Id ? 30 : 10;
            yield return PlayTurnAndContinue(id, expected, score);
            turns++;
            lastLaunched = expected;
        }

        VersusSeries finished = VersusRuntime.Coordinator.Load(id);
        Assert.That(finished.Status, Is.EqualTo(SeriesStatus.Completed));
        Assert.That(turns, Is.EqualTo(4), "2-0: two games, two turns each");
        Assert.That(finished.Result.WinnerId, Is.EqualTo(finished.Participants.First.Id));
        Assert.That(lastLaunched.HasValue, Is.True);
        Assert.That(TextOf("seriesDetail"), Does.Contain("wins the series."), "the completed series renders its terminal state");
        Assert.That(FindButton("playTurnButton").interactable, Is.False, "a finished series offers no turn");
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False, "the return hint was consumed on arrival");
        Assert.That(BackendV2SessionStore.Current, Is.Null);
    }

    [UnityTest]
    [Timeout(600000)]
    public IEnumerator ASeriesResumesFromItsFileAfterARestartBetweenTurns()
    {
        yield return OpenLocalVersusFromStart();
        FindButton("createButton").onClick.Invoke();
        yield return null;

        SeriesId id = VersusRuntime.Coordinator.ListSeries()[0].Id;
        VersusSeries created = VersusRuntime.Coordinator.Load(id);
        ParticipantId first = ExpectedNext(created);
        ParticipantId second = created.Participants.Opponent(first).Id;

        // turn one is played and reported, then the application "quits" before Continue Series
        yield return PlayTurn(id, first, 30);

        LocalVersusNavigationState.Clear();
        ActiveVersusAttempt.Clear();
        ActiveMatch.Clear();
        VersusRuntime.Reset();
        VersusRuntime.Override(new FileVersusSeriesRepository(root));

        yield return OpenLocalVersusFromStart();

        Assert.That(VersusRuntime.Coordinator.ListSeries(), Has.Count.EqualTo(1), "the series survived on disk");
        VersusSeries resumed = VersusRuntime.Coordinator.Load(id);
        Assert.That(ExpectedNext(resumed), Is.EqualTo(second));
        Assert.That(
            TextOf("seriesDetail"),
            Does.Contain(resumed.Participants.Find(second).DisplayName + " is up."),
            "the restarted screen shows the same series with the other side up, from the file alone");
        Assert.That(FindButton("playTurnButton").interactable, Is.True, "and it is resumable");
    }

    // ------------------------------------------------------------------ steps

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

    /// <summary>One turn played and reported, stopping at the match-end summary (before Continue Series).</summary>
    private IEnumerator PlayTurn(SeriesId id, ParticipantId expected, int score)
    {
        Button play = FindButton("playTurnButton");
        Assert.That(play.interactable, Is.True, "Play Turn is not available: " + TextOf("turnMessage"));
        play.onClick.Invoke();

        float deadline = Time.realtimeSinceStartup + SceneTimeoutSeconds;
        PlayerController player = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            string active = SceneManager.GetActiveScene().name;
            if (active != Constants.SCENE_NAME_level_00_local_versus)
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
        yield return null;

        Assert.That(ActiveVersusAttempt.IsActive, Is.True);
        Assert.That(ActiveVersusAttempt.SeriesId, Is.EqualTo(id));
        Assert.That(ActiveVersusAttempt.ParticipantId, Is.EqualTo(expected));
        Assert.That(ActiveMatch.Configuration.Roster.Count, Is.EqualTo(1));
        Assert.That(ActiveMatch.Configuration.Roster.LocalHumanCount, Is.EqualTo(1));
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True);
        Assert.That(BackendV2SessionStore.Current, Is.Null);

        bool relabelled = false;
        foreach (Text label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            relabelled |= label.text == Pause.LocalVersusForfeitLabel;
        }

        Assert.That(relabelled, Is.True, "the pause menu's start-screen action reads the forfeit label while a local-versus turn is in progress");

        GameStats stats = UnityEngine.Object.FindAnyObjectByType<GameStats>();
        Assert.That(stats, Is.Not.Null, "the gameplay scene has no GameStats to report from");
        stats.TotalPoints = score;
        stats.ShotMade = 5;
        stats.ShotAttempt = 10;
        Assert.That(
            VersusMatchReporter.TryReport(stats, ActiveMatch.Configuration.ModeId, 90f),
            Is.True,
            "the turn's result was not stored");
        Assert.That(ActiveVersusAttempt.IsActive, Is.False, "a stored result clears the attempt");
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.True, "the return hint outlives the attempt");
    }

    private IEnumerator PlayTurnAndContinue(SeriesId id, ParticipantId expected, int score)
    {
        yield return PlayTurn(id, expected, score);

        // "Continue Series": the pause menu's start-screen action.
        MonoBehaviour pause = FindBehaviourByTypeName("Pause");
        Assert.That(pause, Is.Not.Null, "the gameplay scene has no Pause component");
        MethodInfo method = pause.GetType().GetMethod("loadstartScreen", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        pause.StartCoroutine((IEnumerator)method.Invoke(pause, null));

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

        Assert.That(
            SceneManager.GetActiveScene().name,
            Is.EqualTo(Constants.SCENE_NAME_level_00_local_versus),
            "Continue Series never returned to Local Versus");
        yield return null;
        yield return null;
        Assert.That(LocalVersusNavigationState.ReturnPending, Is.False, "the screen consumed the return hint");
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Independent oracle for whose turn it is, read from the stored series.</summary>
    private static ParticipantId ExpectedNext(VersusSeries series)
    {
        VersusGame game = series.CurrentGame;
        ParticipantId first = series.Participants.At(game.FirstAttemptParticipantIndex).Id;
        return series.ViewFor(first).CurrentGame.OwnAttemptState == AttemptState.Completed
            ? series.Participants.Opponent(first).Id
            : first;
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
}
#endif
