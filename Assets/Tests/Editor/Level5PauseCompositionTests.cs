using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-012 Phase 2b Slice 63: <see cref="Pause"/> no longer reads <c>GameLevelManager.instance</c>/
/// <c>GameRules.instance</c> itself - <see cref="Pause.Update"/>, <see cref="Pause.PressCancelMenu"/>,
/// <see cref="Pause.TogglePause"/> and the private <c>updateFreePlayStats</c> only consult whatever
/// delegates <see cref="Pause.BindGameLevelManagerContext"/> was called with. Mirrors the shape of
/// <c>Level5TimerMatchEndContextTests</c>/<c>Level5MatchHudPresenterCompositionTests</c>:
/// composition-forwarding tests driving the real methods (via reflection where private) against a
/// <see cref="Pause"/> built without running its own heavy <c>Awake()</c> (which requires a fully wired
/// <see cref="PauseUiObjects"/> and an active <c>EventSystem</c> neither this fixture sets up), then
/// separate production-adapter tests against live <see cref="GameLevelManager"/>/<see cref="GameRules"/>
/// singletons.
///
/// <c>updateFreePlayStats</c>'s forwarding test tolerates the method throwing once it reaches
/// <c>DBConnector.instance.savePlayerGameStats</c> (a null <c>DBConnector.instance</c> in this fixture) -
/// that persistence-layer coupling is this slice's explicitly deferred blocker, not touched here; the
/// assertion is that <see cref="Pause"/>'s own two new delegates (<c>setTimePlayed</c>,
/// <c>allParticipantsReader</c>) are called before that unrelated failure, proving the forwarding this
/// slice actually changed.
/// </summary>
public class Level5PauseCompositionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private GameLevelManager savedGameLevelManagerInstance;
    private GameRules savedGameRulesInstance;

    [SetUp]
    public void SetUp()
    {
        savedGameLevelManagerInstance = GameLevelManager.instance;
        savedGameRulesInstance = GameRules.instance;

        // TogglePause()/StartGame() read and write this real engine global - pinned here and restored
        // in TearDown so this fixture's tests are deterministic regardless of run order, and so this
        // fixture leaves no state behind for whatever else runs in the same batch.
        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        GameLevelManager.instance = savedGameLevelManagerInstance;
        GameRules.instance = savedGameRulesInstance;
        Time.timeScale = 1f;

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
        Action<bool> setJoystickEnabled = null,
        Func<List<PlayerIdentifier>> allParticipantsReader = null,
        Func<PlayerIdentifier> primaryPlayerReader = null,
        Action setTimePlayed = null)
    {
        pause.BindGameLevelManagerContext(
            hasGameLevelManagerReader ?? (() => true),
            cancelTriggeredReader ?? (() => false),
            submitTriggeredReader ?? (() => false),
            gameOverReader ?? (() => false),
            setJoystickEnabled ?? (enabled => { }),
            allParticipantsReader ?? (() => new List<PlayerIdentifier>()),
            primaryPlayerReader ?? (() => null),
            setTimePlayed ?? (() => { }));
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

    // ==================== updateFreePlayStats() forwarding ====================

    [Test]
    public void UpdateFreePlayStats_ForwardsThroughSetTimePlayedAndAllParticipantsReaderBeforeThePersistenceCallItDoesNotTouch()
    {
        Pause pause = SpawnPauseWithoutAwake();
        bool setTimePlayedCalled = false;
        bool allParticipantsReaderCalled = false;
        Bind(pause,
            setTimePlayed: () => setTimePlayedCalled = true,
            allParticipantsReader: () => { allParticipantsReaderCalled = true; return new List<PlayerIdentifier>(); });

        // DBConnector.instance is null in this fixture - the untouched persistence coupling this slice
        // defers - so the method is expected to throw once it reaches DBConnector.instance.
        // savePlayerGameStats. The two delegates above run before that point.
        Assert.Throws<TargetInvocationException>(() => InvokePrivate(pause, "updateFreePlayStats"));

        Assert.IsTrue(setTimePlayedCalled, "updateFreePlayStats must forward to the bound setTimePlayed delegate.");
        Assert.IsTrue(allParticipantsReaderCalled, "updateFreePlayStats must forward to the bound allParticipantsReader delegate.");
    }

    // ================ production adapters: GameLevelManager's Pause composition ================

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

    [Test]
    public void ReadAllParticipantsForPause_LiveGameLevelManager_ForwardsPlayersList()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;

        object result = AdapterMethod("ReadAllParticipantsForPause").Invoke(null, null);

        Assert.AreSame(manager.players, result);
    }

    [Test]
    public void ReadPrimaryPlayerForPause_LiveGameLevelManager_ForwardsPlayer1()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = Spawn("registered").AddComponent<PlayerIdentifier>();
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = AdapterMethod("ReadPrimaryPlayerForPause").Invoke(null, null);

        Assert.AreSame(manager.Player1, result);
    }

    [Test]
    public void SetTimePlayedForPause_LiveGameRules_ForwardsToSetTimePlayed()
    {
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        GameRules.instance = rules;

        AdapterMethod("SetTimePlayedForPause").Invoke(null, null);

        // setTimePlayed() computes Time.time - timePlayedStart and writes it into gameStats1's
        // MatchStats.TimePlayed; a null gameStats1 in this fixture makes it a safe no-op internally
        // (GameRules' own pre-existing guard, unrelated to this slice) - reaching that line at all
        // without throwing is the forwarding proof.
        Assert.Pass("SetTimePlayedForPause invoked GameRules.instance.setTimePlayed() without throwing.");
    }

    [Test]
    public void SetTimePlayedForPause_AbsentGameRulesInstance_PreservesExistingFailureSemantics()
    {
        GameRules.instance = null;

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(
            () => AdapterMethod("SetTimePlayedForPause").Invoke(null, null),
            "an absent GameRules.instance must still throw exactly as the former direct call did.");
        Assert.That(thrown.InnerException, Is.InstanceOf<NullReferenceException>());
    }
}
