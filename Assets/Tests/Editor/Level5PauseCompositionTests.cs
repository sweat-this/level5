using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// AUD-012 Phase 2b Slice 63/67: <see cref="Pause"/> no longer reads <c>GameLevelManager.instance</c>/
/// <c>GameRules.instance</c> (Slice 63) or <c>DBConnector</c>/<c>DBHelper</c>/<c>PlayerData</c>/
/// <c>HighScoreModel</c>/<c>PendingMatchPersistenceStore</c>/<c>ProgressionService</c> (Slice 67) itself -
/// <see cref="Pause.Update"/>, <see cref="Pause.PressCancelMenu"/>, <see cref="Pause.TogglePause"/>,
/// <see cref="Pause.Quit"/>, <see cref="Pause.loadstartScreen"/>, <see cref="Pause.reloadScene"/> and the
/// private <c>updateFreePlayStats</c>/<c>WaitForDatabaseUnlock</c> only consult whatever delegates
/// <see cref="Pause.BindGameLevelManagerContext"/>/<see cref="Pause.BindPersistenceContext"/> were called
/// with. Mirrors the shape of <c>Level5MatchHudPresenterCompositionTests</c>: composition-forwarding
/// tests driving the real methods (via reflection where private) against a <see cref="Pause"/> built
/// without running its own heavy <c>Awake()</c> (which requires a fully wired <see cref="PauseUiObjects"/>
/// and an active <c>EventSystem</c> neither this fixture sets up), then separate production-adapter tests
/// against live <see cref="GameLevelManager"/>/<see cref="GameRules"/>/<see cref="DBConnector"/> singletons.
/// </summary>
public class Level5PauseCompositionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private GameLevelManager savedGameLevelManagerInstance;
    private GameRules savedGameRulesInstance;
    private DBConnector savedDBConnectorInstance;
    private DBHelper savedDBHelperInstance;
    private PlayerData savedPlayerDataInstance;
    private string pendingPersistencePath;
    private bool pendingPersistenceFileExisted;
    private string pendingPersistenceBackup;

    [SetUp]
    public void SetUp()
    {
        savedGameLevelManagerInstance = GameLevelManager.instance;
        savedGameRulesInstance = GameRules.instance;
        savedDBConnectorInstance = DBConnector.instance;
        savedDBHelperInstance = DBHelper.instance;
        savedPlayerDataInstance = PlayerData.instance;
        ActiveMatch.Clear();

        // TogglePause()/StartGame() read and write this real engine global - pinned here and restored
        // in TearDown so this fixture's tests are deterministic regardless of run order, and so this
        // fixture leaves no state behind for whatever else runs in the same batch.
        Time.timeScale = 1f;

        // A couple of production-adapter tests below exercise PendingMatchPersistenceStore for real
        // (it writes to Application.persistentDataPath, the same behavior it always had - this slice
        // only relocated who calls it). Backed up and restored so this fixture never permanently
        // changes whatever pending-persistence state another test run or a real session left behind.
        pendingPersistencePath = Path.Combine(Application.persistentDataPath, "pending-match-persistence.json");
        pendingPersistenceFileExisted = File.Exists(pendingPersistencePath);
        pendingPersistenceBackup = pendingPersistenceFileExisted ? File.ReadAllText(pendingPersistencePath) : null;
    }

    [TearDown]
    public void TearDown()
    {
        GameLevelManager.instance = savedGameLevelManagerInstance;
        GameRules.instance = savedGameRulesInstance;
        DBConnector.instance = savedDBConnectorInstance;
        DBHelper.instance = savedDBHelperInstance;
        PlayerData.instance = savedPlayerDataInstance;
        ActiveMatch.Clear();
        Time.timeScale = 1f;
        LogAssert.ignoreFailingMessages = false;

        if (pendingPersistenceFileExisted)
        {
            File.WriteAllText(pendingPersistencePath, pendingPersistenceBackup);
        }
        else if (File.Exists(pendingPersistencePath))
        {
            File.Delete(pendingPersistencePath);
        }

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private GameLevelManager SpawnManagerWithoutAwake(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<GameLevelManager>();
    }

    /// <summary>Mirrors Level5SpawnCoordinatorAnimationEventsCompositionTests.SpawnManagerWithoutAwake:
    /// an inactive GameObject defers every component's Awake() - Pause's own Awake() requires a fully
    /// wired PauseUiObjects and an active EventSystem this fixture never sets up.</summary>
    private Pause SpawnPauseWithoutAwake()
    {
        GameObject go = new GameObject("pause");
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<Pause>();
    }

    /// <summary>
    /// A live <see cref="DBConnector"/> whose private <c>dbHelper</c> field is left null (the inactive-
    /// GameObject trick defers <c>Awake()</c>'s <c>GetComponent&lt;DBHelper&gt;()</c> resolution
    /// indefinitely, and no <see cref="DBHelper"/> component is ever added). Every
    /// <c>DBConnector</c> save method already guards on <c>dbHelper != null</c> and returns false rather
    /// than touching SQLite when it is absent, so this reaches the exact same "save failed, queue it"
    /// branch <see cref="PersistFreePlayStatsForPause"/> production-adapter tests below need, with no
    /// real database file involved.
    /// </summary>
    private DBConnector SpawnDatabaseConnectorWithoutDbHelper(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<DBConnector>();
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    private static object InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"{target.GetType().Name} must declare a method named '{methodName}'");
        return method.Invoke(target, args);
    }

    /// <summary>
    /// Wires every field <see cref="Pause.TogglePause"/> (via <c>setBackgroundFade</c>/
    /// <c>setPauseScreen</c>) dereferences unconditionally, so it can run without first running
    /// <see cref="Pause.Awake"/> (which resolves them from a fully wired <see cref="PauseUiObjects"/>
    /// this fixture does not build). None of these fields are this slice's concern - they are unrelated
    /// pre-existing UI wiring <c>TogglePause</c> already required before this slice touched it.
    /// </summary>
    private void WireMinimalPauseUi(Pause pause)
    {
        SetPrivateField(pause, "fadeTexture", Spawn("fade").AddComponent<Image>());
        SetPrivateField(pause, "loadSceneText", Spawn("load-scene-text").AddComponent<Text>());
        SetPrivateField(pause, "loadStartScreenText", Spawn("load-start-text").AddComponent<Text>());
        SetPrivateField(pause, "quitGameText", Spawn("quit-text").AddComponent<Text>());
        SetPrivateField(pause, "cancelMenuText", Spawn("cancel-text").AddComponent<Text>());
        SetPrivateField(pause, "loadSceneButton", Spawn("load-scene-button").AddComponent<Button>());
        SetPrivateField(pause, "loadStartScreenButton", Spawn("load-start-button").AddComponent<Button>());
        SetPrivateField(pause, "cancelMenuButton", Spawn("cancel-button").AddComponent<Button>());
        SetPrivateField(pause, "quitGameButton", Spawn("quit-button").AddComponent<Button>());
        SetPrivateField(pause, "toggleFpsText", Spawn("fps-text").AddComponent<Text>());
        SetPrivateField(pause, "toggleMaxStatsText", Spawn("max-stats-text").AddComponent<Text>());
        SetPrivateField(pause, "toggleUiStatsText", Spawn("ui-stats-text").AddComponent<Text>());
    }

    private void Bind(
        Pause pause,
        Func<bool> hasGameLevelManagerReader = null,
        Func<bool> cancelTriggeredReader = null,
        Func<bool> submitTriggeredReader = null,
        Func<bool> gameOverReader = null,
        Action<bool> setJoystickEnabled = null)
    {
        pause.BindGameLevelManagerContext(
            hasGameLevelManagerReader ?? (() => true),
            cancelTriggeredReader ?? (() => false),
            submitTriggeredReader ?? (() => false),
            gameOverReader ?? (() => false),
            setJoystickEnabled ?? (enabled => { }));
    }

    private void BindPersistence(
        Pause pause,
        Func<bool> hasDatabaseReader = null,
        Func<bool> databaseLockedReader = null,
        Func<bool> hasPlayerDataReader = null,
        Action reloadPlayerData = null,
        Action<string> persistFreePlayStats = null)
    {
        pause.BindPersistenceContext(
            hasDatabaseReader ?? (() => true),
            databaseLockedReader ?? (() => false),
            hasPlayerDataReader ?? (() => true),
            reloadPlayerData ?? (() => { }),
            persistFreePlayStats ?? (resultId => { }));
    }

    /// <summary>Builds a validated FreePlay (<see cref="GameModeId.FreePlay"/> = 99) <see cref="MatchConfiguration"/>
    /// and installs it through <see cref="ActiveMatch.Begin"/>, so <c>MatchRuntime.RawModeId == 99</c> -
    /// the exact condition <see cref="Pause.Quit"/>/<see cref="Pause.loadstartScreen"/>/
    /// <see cref="Pause.reloadScene"/>'s unchanged Free Play gate checks - without needing to fabricate
    /// a display-name string match instead.</summary>
    private static void BeginFreePlayMatch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.FreePlay);
        LevelDefinition level = TestDefinitions.Level(1, objectName: "level_free_play");
        ActiveMatch.Begin(Configure(mode, level));
    }

    /// <summary>A non-FreePlay match, so the same gate's <c>RawModeId == 99</c>/"free" display-name check
    /// is false - proves the gate actually discriminates rather than always passing.</summary>
    private static void BeginNonFreePlayMatch()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.VersusCpu);
        LevelDefinition level = TestDefinitions.Level(2, objectName: "level_versus");
        ActiveMatch.Begin(Configure(mode, level));
    }

    private static MatchConfiguration Configure(GameModeDefinition mode, LevelDefinition level)
    {
        PlayerRoster roster = TestDefinitions.SoloRoster();
        MatchConfigurationBuilder builder = new MatchConfigurationBuilder(
            new GameModeCatalog(new[] { mode }),
            new LevelDefinitionCatalog(new[] { level }));
        MatchBuildResult result = builder.Build(new MatchRequest(mode.Id, level.LevelId, roster, MatchModifiers.Default));
        Assert.That(result.Succeeded, Is.True, result.Validation.ToString());
        return result.Configuration;
    }

    // ==================== Update() forwarding ====================

    [Test]
    public void Update_CancelTriggeredAndNotGameOver_TogglesPauseAndForwardsJoystickEnabled()
    {
        Pause pause = SpawnPauseWithoutAwake();
        WireMinimalPauseUi(pause);
        bool? joystickEnabledArgument = null;
        Bind(pause,
            cancelTriggeredReader: () => true,
            gameOverReader: () => false,
            setJoystickEnabled: enabled => joystickEnabledArgument = enabled);

        InvokePrivate(pause, "Update");

        Assert.IsTrue(pause.Paused, "cancel triggered with no game over must toggle the pause state on.");
        Assert.That(joystickEnabledArgument, Is.EqualTo(false),
            "TogglePause's pause-on branch must forward false through setJoystickEnabled.");
    }

    [Test]
    public void Update_CancelTriggeredButGameOver_DoesNotTogglePause()
    {
        Pause pause = SpawnPauseWithoutAwake();
        WireMinimalPauseUi(pause);
        Bind(pause,
            cancelTriggeredReader: () => true,
            gameOverReader: () => true);

        InvokePrivate(pause, "Update");

        Assert.IsFalse(pause.Paused,
            "a bound gameOverReader returning true must block the toggle exactly as the former GameLevelManager.instance.GameOver read did.");
    }

    [Test]
    public void Update_SubmitTriggeredWhileStartOnPause_InvokesStartGame()
    {
        Pause pause = SpawnPauseWithoutAwake();
        WireMinimalPauseUi(pause);
        SetPrivateField(pause, "startOnPause", true);
        Bind(pause, submitTriggeredReader: () => true);

        InvokePrivate(pause, "Update");

        Assert.IsFalse((bool)GetPrivateField(pause, "startOnPause"),
            "a bound submitTriggeredReader returning true while startOnPause must reach StartGame(), which clears startOnPause.");
    }

    // ==================== PressCancelMenu() forwarding ====================

    [Test]
    public void PressCancelMenu_NoLiveGameLevelManager_NeverConsultsGameOverReader()
    {
        Pause pause = SpawnPauseWithoutAwake();
        WireMinimalPauseUi(pause);
        SetPrivateField(pause, "paused", true);
        Time.timeScale = 0f; // consistent with paused=true - TogglePause() decides direction from this, not the paused field
        bool gameOverReaderCalled = false;
        Bind(pause,
            hasGameLevelManagerReader: () => false,
            gameOverReader: () => { gameOverReaderCalled = true; return true; });

        InvokePrivate(pause, "PressCancelMenu");

        Assert.IsFalse(gameOverReaderCalled,
            "hasGameLevelManagerReader returning false must short-circuit before gameOverReader is ever consulted - matching the former GameLevelManager.instance != null && ... short-circuit.");
        Assert.IsFalse(pause.Paused, "with no live GameLevelManager, gameOver is treated as false, so the menu still toggles off.");
    }

    [Test]
    public void PressCancelMenu_LiveGameLevelManagerAndGameOver_DoesNotToggle()
    {
        Pause pause = SpawnPauseWithoutAwake();
        WireMinimalPauseUi(pause);
        SetPrivateField(pause, "paused", true);
        Bind(pause,
            hasGameLevelManagerReader: () => true,
            gameOverReader: () => true);

        InvokePrivate(pause, "PressCancelMenu");

        Assert.IsTrue(pause.Paused, "a live, game-over GameLevelManager must block the cancel-menu toggle.");
    }

    // ==================== AUD-012 Phase 2b Slice 67: updateFreePlayStats() forwarding ====================

    [Test]
    public void UpdateFreePlayStats_ForwardsToPersistFreePlayStatsWithTheCapturedResultId()
    {
        Pause pause = SpawnPauseWithoutAwake();
        SetPrivateField(pause, "freePlayProgressionResultId", "captured-result-id");
        int callCount = 0;
        string receivedResultId = null;
        BindPersistence(pause, persistFreePlayStats: resultId => { callCount++; receivedResultId = resultId; });

        InvokePrivate(pause, "updateFreePlayStats");

        Assert.That(callCount, Is.EqualTo(1));
        Assert.That(receivedResultId, Is.EqualTo("captured-result-id"),
            "updateFreePlayStats must forward the exact result id captured once in Awake() (MatchSession.EnsureCurrentMatch()), not resolve a replacement at call time.");
    }

    // ==================== AUD-012 Phase 2b Slice 67: Quit() database/mode gate forwarding ====================

    /// <summary>
    /// Drives the coroutine with exactly one <c>MoveNext()</c>: the free-play persistence call happens
    /// synchronously before <c>Quit()</c>'s <c>yield return WaitForDatabaseUnlock()</c>, so this observes
    /// it without ever running <see cref="Pause.WaitForDatabaseUnlock"/> or <c>QuitApplication</c>'s
    /// <c>Application.Quit()</c> (both are covered separately, and neither is usable/meaningful outside
    /// Play Mode).
    /// </summary>
    [Test]
    public void Quit_DatabasePresentFreePlayMode_CallsPersistFreePlayStatsBeforeYielding()
    {
        BeginFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();
        bool persistCalled = false;
        BindPersistence(pause, hasDatabaseReader: () => true, persistFreePlayStats: resultId => persistCalled = true);

        IEnumerator routine = pause.Quit();
        routine.MoveNext();

        Assert.IsTrue(persistCalled, "Quit() must call updateFreePlayStats() when the database is present and the mode is Free Play.");
    }

    [Test]
    public void Quit_DatabaseAbsent_DoesNotCallPersistFreePlayStats()
    {
        BeginFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();
        bool persistCalled = false;
        BindPersistence(pause, hasDatabaseReader: () => false, persistFreePlayStats: resultId => persistCalled = true);

        IEnumerator routine = pause.Quit();
        routine.MoveNext();

        Assert.IsFalse(persistCalled,
            "hasDatabaseReader returning false must block the Free Play persistence call, matching the former DBConnector.instance != null guard.");
    }

    [Test]
    public void Quit_DatabasePresentButNotFreePlayMode_DoesNotCallPersistFreePlayStats()
    {
        BeginNonFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();
        bool persistCalled = false;
        BindPersistence(pause, hasDatabaseReader: () => true, persistFreePlayStats: resultId => persistCalled = true);

        IEnumerator routine = pause.Quit();
        routine.MoveNext();

        Assert.IsFalse(persistCalled,
            "a non-Free-Play mode must not persist Free Play stats, even with the database present - the unchanged MatchRuntime.ModeDisplayName/RawModeId gate.");
    }

    /// <summary>
    /// Regression coverage for a scenario code review found: <c>minigame_racing.unity</c> authors its own
    /// inline <c>Pause</c> with no companion <c>GameLevelManager</c> (it uses its own
    /// <c>RacingGameManager</c> instead), so <see cref="Pause.BindPersistenceContext"/> is never called
    /// there. Before this slice, <c>Quit()</c>'s database gate was a direct, null-safe
    /// <c>DBConnector.instance != null</c> check that worked in any scene. This proves the persistence
    /// delegates' safe-default initializers preserve that: an entirely unbound <see cref="Pause"/> must
    /// still run <c>Quit()</c> without throwing, treating the database as unavailable rather than
    /// dereferencing a null delegate.
    /// </summary>
    [Test]
    public void Quit_PersistenceContextNeverBound_DoesNotThrowAndTreatsDatabaseAsUnavailable()
    {
        BeginFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();

        IEnumerator routine = pause.Quit();

        Assert.DoesNotThrow(() => routine.MoveNext(),
            "an unbound Pause (no GameLevelManager in the scene) must not throw when hasDatabaseReader's default is consulted.");
    }

    // ==================== AUD-012 Phase 2b Slice 67: reloadScene() reload-order forwarding ====================

    /// <summary>
    /// <c>reloadScene()</c>'s final statement unconditionally calls
    /// <c>SceneTransition.LoadScene(SceneManager.GetActiveScene().name)</c>, which is not usable outside
    /// Play Mode. This fixture's reload-forwarding tests only care about the evidence gathered before
    /// that point (the exact same scope the pre-existing <c>updateFreePlayStats</c> forwarding test used,
    /// which likewise tolerated a downstream failure it wasn't testing) - so the call is tolerated with
    /// <see cref="LogAssert.ignoreFailingMessages"/> plus a catch-all, the same pattern
    /// <c>Level5BasketballShotMarkerSessionTests</c> uses for an equivalent Play-Mode-only Unity API.
    /// </summary>
    private static void InvokeReloadSceneTolerant(Pause pause)
    {
        LogAssert.ignoreFailingMessages = true;
        try
        {
            pause.reloadScene();
        }
        catch (Exception)
        {
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
        }
    }

    [Test]
    public void ReloadScene_DatabasePresentFreePlayModeAndPlayerDataPresent_PersistsThenReloadsTwice()
    {
        BeginFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();
        List<string> events = new List<string>();
        BindPersistence(pause,
            hasDatabaseReader: () => true,
            hasPlayerDataReader: () => true,
            reloadPlayerData: () => events.Add("reload"),
            persistFreePlayStats: resultId => events.Add("persist"));

        InvokeReloadSceneTolerant(pause);

        Assert.That(events, Is.EqualTo(new[] { "persist", "reload", "reload" }),
            "reloadScene() must persist once, then reload twice - the former unguarded reload inside "
            + "the DB+FreePlay block, then the later independently-guarded reload - in that exact order.");
    }

    [Test]
    public void ReloadScene_DatabasePresentFreePlayModeButPlayerDataAbsent_ReloadsOnlyOnceFromTheUnguardedCall()
    {
        BeginFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();
        int reloadCallCount = 0;
        BindPersistence(pause,
            hasDatabaseReader: () => true,
            hasPlayerDataReader: () => false,
            reloadPlayerData: () => reloadCallCount++);

        InvokeReloadSceneTolerant(pause);

        Assert.That(reloadCallCount, Is.EqualTo(1),
            "with hasPlayerDataReader false, only the first, former-unguarded reload (inside the DB+FreePlay "
            + "block) must run - the second, former-guarded reload must be skipped, proving the two reload "
            + "call sites keep their distinct former guarding rather than being deduplicated.");
    }

    [Test]
    public void ReloadScene_DatabaseAbsent_NeitherPersistsNorRunsTheFirstReload()
    {
        BeginFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();
        bool persistCalled = false;
        int reloadCallCount = 0;
        BindPersistence(pause,
            hasDatabaseReader: () => false,
            hasPlayerDataReader: () => false,
            reloadPlayerData: () => reloadCallCount++,
            persistFreePlayStats: resultId => persistCalled = true);

        InvokeReloadSceneTolerant(pause);

        Assert.IsFalse(persistCalled);
        Assert.That(reloadCallCount, Is.EqualTo(0));
    }

    [Test]
    public void ReloadScene_NotFreePlayModeButPlayerDataPresent_SkipsPersistenceButStillRunsTheSecondReload()
    {
        BeginNonFreePlayMatch();
        Pause pause = SpawnPauseWithoutAwake();
        bool persistCalled = false;
        int reloadCallCount = 0;
        BindPersistence(pause,
            hasDatabaseReader: () => true,
            hasPlayerDataReader: () => true,
            reloadPlayerData: () => reloadCallCount++,
            persistFreePlayStats: resultId => persistCalled = true);

        InvokeReloadSceneTolerant(pause);

        Assert.IsFalse(persistCalled, "a non-Free-Play mode must not persist Free Play stats.");
        Assert.That(reloadCallCount, Is.EqualTo(1),
            "the second reload is gated only on hasPlayerDataReader, independent of mode - it must still run.");
    }

    // ==================== AUD-012 Phase 2b Slice 67: WaitForDatabaseUnlock() polling ====================

    [Test]
    public void WaitForDatabaseUnlock_NotLocked_CompletesImmediatelyWithoutWarning()
    {
        Pause pause = SpawnPauseWithoutAwake();
        BindPersistence(pause, databaseLockedReader: () => false);

        IEnumerator routine = (IEnumerator)InvokePrivate(pause, "WaitForDatabaseUnlock");

        Assert.That(routine.MoveNext(), Is.False,
            "with the database never locked, the poll loop must never yield - the coroutine completes on the first MoveNext.");
    }

    /// <summary>
    /// A four-call response sequence (true, true, false, true) models: two poll iterations that see the
    /// database locked (each yields once), a third iteration that sees it unlocked (exits the while
    /// loop), then the post-loop timeout-warning check independently seeing it locked again - proving
    /// that check is its own, separate <c>databaseLockedReader()</c> call rather than reusing the loop's
    /// last result. This exercises the exact warning-after-timeout code path without an actual 8-second
    /// real-time wait (<c>DatabaseWaitTimeoutSeconds</c> is unchanged production behavior this slice did
    /// not touch, verified separately by <see cref="WaitForDatabaseUnlock_TimeoutConstant_IsUnchanged"/>).
    ///
    /// Code review, 2026-09-15: in the real coroutine the loop's last (false) evaluation and the
    /// post-loop check happen synchronously with no yield between them, so the two reads are provably
    /// redundant there - this is preserved legacy behavior from the pre-Slice-67 compound check
    /// (<c>DBHelper.instance != null &amp;&amp; DBHelper.instance.DatabaseLocked</c>, evaluated at both
    /// call sites), not a bug this slice introduced or should "fix" mid-migration. This test's exact
    /// <c>callIndex == 4</c> assertion is asserting today's call count, not a load-bearing contract - if a
    /// future change collapses the loop's last read into the post-loop check (e.g. capturing it in a
    /// local), update this test's expected count rather than treating it as a regression.
    /// </summary>
    [Test]
    public void WaitForDatabaseUnlock_LockedThenUnlocked_PollsThenCompletes()
    {
        Pause pause = SpawnPauseWithoutAwake();
        bool[] responses = { true, true, false, true };
        int callIndex = 0;
        BindPersistence(pause, databaseLockedReader: () => responses[callIndex++]);

        IEnumerator routine = (IEnumerator)InvokePrivate(pause, "WaitForDatabaseUnlock");

        Assert.That(routine.MoveNext(), Is.True, "iteration 1: locked, so the loop must yield.");
        Assert.That(routine.MoveNext(), Is.True, "iteration 2: locked, so the loop must yield again.");
        LogAssert.Expect(LogType.Warning, "Pause timed out waiting for the local database; continuing navigation.");
        Assert.That(routine.MoveNext(), Is.False,
            "iteration 3: unlocked, so the loop exits; the post-loop check (call 4) independently sees "
            + "locked again and must log the timeout warning before completing.");
        Assert.That(callIndex, Is.EqualTo(4), "databaseLockedReader must be called exactly once per loop check plus once for the post-loop warning check.");
    }

    [Test]
    public void WaitForDatabaseUnlock_TimeoutConstant_IsUnchanged()
    {
        FieldInfo field = typeof(Pause).GetField("DatabaseWaitTimeoutSeconds", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "Pause must declare DatabaseWaitTimeoutSeconds");
        Assert.That(field.GetValue(null), Is.EqualTo(8f));
    }

    // ================ production adapters: GameLevelManager's Pause composition (Slice 63) ================

    private static MethodInfo AdapterMethod(string name)
    {
        MethodInfo method = typeof(GameLevelManager).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, $"GameLevelManager.{name} must exist as a production Pause adapter");
        return method;
    }

    [Test]
    public void HasGameLevelManagerForPause_ReflectsCurrentInstance()
    {
        GameLevelManager.instance = null;
        Assert.IsFalse((bool)AdapterMethod("HasGameLevelManagerForPause").Invoke(null, null));

        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        Assert.IsTrue((bool)AdapterMethod("HasGameLevelManagerForPause").Invoke(null, null));
    }

    [Test]
    public void ReadGameOverForPause_LiveGameLevelManager_ForwardsCurrentValue()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        manager.GameOver = true;

        Assert.IsTrue((bool)AdapterMethod("ReadGameOverForPause").Invoke(null, null));
    }

    // ================ production adapters: GameLevelManager's Pause persistence composition (Slice 67) ================

    [Test]
    public void HasDatabaseForPause_ReflectsCurrentInstance_IncludingAfterReplacement()
    {
        DBConnector.instance = null;
        Assert.IsFalse((bool)AdapterMethod("HasDatabaseForPause").Invoke(null, null));

        DBConnector first = SpawnDatabaseConnectorWithoutDbHelper("db-connector-1");
        DBConnector.instance = first;
        Assert.IsTrue((bool)AdapterMethod("HasDatabaseForPause").Invoke(null, null));

        // "replacement singleton liveness": a later singleton swap must be observed immediately, proving
        // this adapter resolves DBConnector.instance fresh on every call rather than capturing it once.
        DBConnector.instance = null;
        Assert.IsFalse((bool)AdapterMethod("HasDatabaseForPause").Invoke(null, null),
            "the adapter must reflect a replaced (here: cleared) DBConnector.instance live, not a stale capture.");
    }

    [Test]
    public void DatabaseLockedForPause_NoDBHelper_ReturnsFalse()
    {
        DBHelper.instance = null;
        Assert.IsFalse((bool)AdapterMethod("DatabaseLockedForPause").Invoke(null, null));
    }

    [Test]
    public void DatabaseLockedForPause_LiveDBHelper_ForwardsCurrentLockedValue()
    {
        // DBHelper.Awake() destroys its own GameObject when DBHelper.instance is already non-null and
        // is not this component - nulled first so AddComponent's real Awake() assigns instance = this
        // instead of hitting that duplicate-instance guard (which also logs outside Play Mode).
        DBHelper.instance = null;
        DBHelper helper = Spawn("db-helper").AddComponent<DBHelper>();
        DBHelper.instance = helper;
        helper.DatabaseLocked = true;

        Assert.IsTrue((bool)AdapterMethod("DatabaseLockedForPause").Invoke(null, null));

        helper.DatabaseLocked = false;
        Assert.IsFalse((bool)AdapterMethod("DatabaseLockedForPause").Invoke(null, null));
    }

    [Test]
    public void HasPlayerDataForPause_ReflectsCurrentInstance()
    {
        PlayerData.instance = null;
        Assert.IsFalse((bool)AdapterMethod("HasPlayerDataForPause").Invoke(null, null));
    }

    [Test]
    public void ReloadPlayerDataForPause_NoLivePlayerData_ThrowsExactlyAsTheFormerBareDereferenceDid()
    {
        PlayerData.instance = null;

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
            () => AdapterMethod("ReloadPlayerDataForPause").Invoke(null, null));
        Assert.That(thrown.InnerException, Is.InstanceOf<NullReferenceException>());
    }

    [Test]
    public void PersistFreePlayStatsForPause_NoLiveGameRules_ThrowsAtSetTimePlayedBeforeAnythingElse()
    {
        GameRules.instance = null;

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
            () => AdapterMethod("PersistFreePlayStatsForPause").Invoke(null, new object[] { "result-id" }),
            "GameRules.setTimePlayed() must still be the first thing this adapter does - an absent "
            + "GameRules.instance must throw exactly as the former direct call did.");
        Assert.That(thrown.InnerException, Is.InstanceOf<NullReferenceException>());
    }

    /// <summary>
    /// With no participant registered, <c>GameLevelManager.Player1</c> is null - proving the "null
    /// player/gameStats early return" is actually reached, this must complete without throwing even
    /// though the score save ahead of it already ran (against a real, dbHelper-less DBConnector so that
    /// save itself cannot throw either - see <see cref="SpawnDatabaseConnectorWithoutDbHelper"/>).
    /// </summary>
    [Test]
    public void PersistFreePlayStatsForPause_NoPrimaryPlayer_ReturnsEarlyWithoutThrowing()
    {
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        GameRules.instance = rules;
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        DBConnector.instance = SpawnDatabaseConnectorWithoutDbHelper("db-connector");

        Assert.DoesNotThrow(() => AdapterMethod("PersistFreePlayStatsForPause").Invoke(null, new object[] { "result-id" }));
    }

    /// <summary>
    /// The primary end-to-end proof: a live primary player with <c>gameStats</c>, a real (but
    /// dbHelper-less) <see cref="DBConnector"/>, and no exception anywhere along
    /// setTimePlayed -&gt; HighScoreModel conversion -&gt; score save/queue -&gt; primary player lookup -&gt;
    /// all-time save/queue -&gt; ProgressionService.ApplyMatchResult. Three concrete effects are checked,
    /// not merely that nothing threw: <c>GameRules.setTimePlayed()</c> actually reached and overwrote the
    /// primary player's <c>MatchStats.TimePlayed</c> (a sentinel value proves the mutation, the same
    /// proof the retired <c>SetTimePlayedForPause_LiveGameRules_ForwardsToSetTimePlayed</c> test used);
    /// the failed score save reached <c>PendingMatchPersistenceStore.QueueScore</c> (its <c>Scoreid</c>
    /// appears in the real pending-persistence file); and the failed all-time-stats save reached
    /// <c>QueueAllTime</c> with the exact supplied <c>resultId</c>. Both queue files are the real,
    /// backed-up/restored pending-persistence file this fixture's SetUp/TearDown manage.
    /// </summary>
    [Test]
    public void PersistFreePlayStatsForPause_LivePrimaryPlayerAndDatabase_MutatesTimePlayedAndQueuesBothFailedSaves()
    {
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        GameRules.instance = rules;
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = Spawn("primary-player").AddComponent<PlayerIdentifier>();
        player.gameStats = Spawn("primary-player-stats").AddComponent<GameStats>();
        player.gameStats.Stats.TimePlayed = -999f; // sentinel: setTimePlayed() must overwrite this
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);
        DBConnector.instance = SpawnDatabaseConnectorWithoutDbHelper("db-connector");

        Assert.DoesNotThrow(() =>
            AdapterMethod("PersistFreePlayStatsForPause").Invoke(null, new object[] { "queue-proof-result-id" }));

        Assert.That(player.gameStats.Stats.TimePlayed, Is.Not.EqualTo(-999f),
            "PersistFreePlayStatsForPause must reach GameRules.instance.setTimePlayed(), which overwrites the primary player's MatchStats.TimePlayed.");

        string queuedContent = File.Exists(pendingPersistencePath) ? File.ReadAllText(pendingPersistencePath) : string.Empty;
        Assert.That(queuedContent, Does.Contain("\"Scoreid\":"),
            "a failed score save (no live dbHelper) must be queued through PendingMatchPersistenceStore.QueueScore.");
        Assert.That(queuedContent, Does.Contain("\"resultId\": \"queue-proof-result-id\""),
            "a failed all-time-stats save (no live dbHelper) must be queued through "
            + "PendingMatchPersistenceStore.QueueAllTime with the exact supplied resultId.");
    }
}
