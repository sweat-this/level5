using System.Collections.Generic;
using System.Reflection;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// AUD-012 Phase 2b Slice 53: <see cref="LevelRuntimeContext"/> moved into <c>Level5.Match</c>
/// source-identically. These tests protect the composition/lifecycle contract
/// <see cref="GameLevelManager"/> depends on across that move - the registry existing before
/// adoption, adoption preserving reference identity rather than copying, a null adoption leaving the
/// active registry alone, the singleton clearing itself on teardown, and match-end requests reaching
/// a resolved <see cref="MatchController"/> - the same shape <see cref="Level5MatchLifecycleTests"/>
/// establishes for <c>MatchController</c> itself.
/// </summary>
public class Level5LevelRuntimeContextTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        ActiveMatch.Clear();

        // A gameplay scene already open in the editor session (or left behind by an unrelated
        // fixture run outside this file's own TearDown) can leave this claimed - the same reason
        // BasketBall/GameRules/BasketBallAuto reset their own statics around their tests.
        LevelRuntimeContext.instance = null;
    }

    [TearDown]
    public void TearDown()
    {
        ActiveMatch.Clear();
        LevelRuntimeContext.instance = null;

        foreach (GameObject go in spawned)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
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

    /// <summary>
    /// <c>[DefaultExecutionOrder(-100)]</c> on <see cref="LevelRuntimeContext"/> - the only script in
    /// the project using a custom execution order - defers Unity's own <c>Awake()</c> dispatch for
    /// <c>AddComponent</c> past this fixture's synchronous test methods, unlike the plain-order
    /// MonoBehaviours other fixtures add and read from immediately (e.g. <c>GameRules</c> in
    /// <see cref="Level5BasketballShotMarkerSessionTests"/>). Invoked explicitly here, the same way
    /// <c>Level5BasketBallMatchRulesTests.InvokeStart</c> drives <c>Start()</c> for the same reason.
    ///
    /// If a future Unity version stops deferring this, a real automatic <c>Awake()</c> would run
    /// before this explicit one - harmless today since every field it sets is an idempotent
    /// reassignment (worst case, <c>Players</c> is rebuilt as a second empty registry before any test
    /// reads or adopts it), but worth knowing about if <c>Awake()</c> ever gains a non-idempotent step.
    /// </summary>
    private static void InvokeAwake(LevelRuntimeContext context)
    {
        MethodInfo awake = typeof(LevelRuntimeContext).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(awake, "LevelRuntimeContext must declare Awake()");
        awake.Invoke(context, null);
    }

    private static void InvokeOnDestroy(LevelRuntimeContext context)
    {
        MethodInfo onDestroy = typeof(LevelRuntimeContext).GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(onDestroy, "LevelRuntimeContext must declare OnDestroy()");
        onDestroy.Invoke(context, null);
    }

    private LevelRuntimeContext SpawnContext(string name)
    {
        LevelRuntimeContext context = Spawn(name).AddComponent<LevelRuntimeContext>();
        InvokeAwake(context);
        return context;
    }

    private static MatchConfiguration BuildConfiguration()
    {
        GameModeDefinition mode = TestDefinitions.Mode(GameModeId.TotalPoints);
        LevelDefinition level = TestDefinitions.Level(1);
        PlayerRoster roster = TestDefinitions.SoloRoster();

        return new MatchConfiguration(
            mode,
            level,
            roster,
            MatchModifiers.Default,
            new ResolvedMatchRules(),
            CheerleaderSelection.None,
            "level runtime context test");
    }

    [Test]
    public void Awake_CapturesTheConfiguredRulesAndRoster()
    {
        MatchConfiguration configuration = BuildConfiguration();
        ActiveMatch.Begin(configuration);

        LevelRuntimeContext context = SpawnContext("context");

        Assert.That(context.HasValidatedConfiguration, Is.True);
        Assert.AreSame(configuration, context.Configuration);
        Assert.AreSame(configuration.Rules, context.Rules);
        Assert.AreSame(configuration.Roster, context.Roster);
    }

    [Test]
    public void Awake_CreatesAnEmptyRegistryBeforeAnyoneAdopts()
    {
        LevelRuntimeContext context = SpawnContext("context");

        Assert.IsNotNull(context.Players);
        Assert.That(context.Players.Participants, Is.Empty);
    }

    [Test]
    public void AdoptPlayerRegistry_ReplacesTheDefaultWithTheSuppliedInstance()
    {
        LevelRuntimeContext context = SpawnContext("context");
        PlayerRegistry preAdoption = context.Players;
        PlayerRegistry supplied = new PlayerRegistry();

        context.AdoptPlayerRegistry(supplied);

        Assert.AreNotSame(preAdoption, context.Players, "adoption must replace the pre-Awake default");
        Assert.AreSame(supplied, context.Players, "must expose the exact registry GameLevelManager filled, not a copy");
    }

    [Test]
    public void AdoptPlayerRegistry_NullLeavesTheActiveRegistryInPlace()
    {
        LevelRuntimeContext context = SpawnContext("context");
        PlayerRegistry active = context.Players;

        context.AdoptPlayerRegistry(null);

        Assert.AreSame(active, context.Players, "a null adoption must not clear or replace the active registry");
    }

    [Test]
    public void OnDestroy_ClearsTheStaticInstance()
    {
        LevelRuntimeContext context = SpawnContext("context");
        Assert.AreSame(context, LevelRuntimeContext.instance);

        InvokeOnDestroy(context);

        Assert.IsNull(LevelRuntimeContext.instance);
    }

    [Test]
    public void RequestMatchEnd_DelegatesToTheResolvedMatchController()
    {
        GameObject go = Spawn("context-with-controller");
        MatchController controller = go.AddComponent<MatchController>();
        LevelRuntimeContext context = go.AddComponent<LevelRuntimeContext>();
        InvokeAwake(context);
        controller.BeginPlay();

        Assert.AreSame(controller, context.MatchController);
        Assert.IsTrue(context.RequestMatchEnd(MatchEndReason.TimeExpired));
        Assert.IsTrue(controller.IsOver);
    }

    /// <summary>
    /// <c>GameLevelManager</c> never assigns the serialized <c>matchController</c> field before
    /// creating/finding its <see cref="LevelRuntimeContext"/>, so production resolution routes through
    /// <c>Awake</c>'s <c>GetComponent&lt;MatchController&gt;() ?? FindAnyObjectByType&lt;MatchController&gt;()</c>
    /// fallback whenever the two do not share a GameObject - unlike
    /// <see cref="RequestMatchEnd_DelegatesToTheResolvedMatchController"/>, which only exercises the
    /// same-GameObject <c>GetComponent</c> branch.
    /// </summary>
    [Test]
    public void Awake_ResolvesAMatchControllerElsewhereInTheSceneWhenNotOnItsOwnGameObject()
    {
        MatchController controller = Spawn("scene-match-controller").AddComponent<MatchController>();

        LevelRuntimeContext context = SpawnContext("context-without-its-own-controller");

        Assert.AreSame(controller, context.MatchController,
            "GetComponent finds nothing on the context's own GameObject, so Awake must fall back to FindAnyObjectByType");
    }

    [Test]
    public void RequestMatchEnd_WithNoMatchControllerReturnsFalse()
    {
        LevelRuntimeContext context = SpawnContext("context-without-controller");

        Assert.IsNull(context.MatchController);
        Assert.IsFalse(context.RequestMatchEnd(MatchEndReason.TimeExpired));
    }
}
