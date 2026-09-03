using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 1c: <see cref="BasketBallShotMarker"/>'s last direct <c>GameRules</c> dependency
/// (<c>MarkersRemaining</c>, <c>IsGameOver()</c>, <c>RequestGameOver()</c>) is replaced by a live
/// <see cref="IShotMarkerSession"/> bound once to each active, scene-authored marker by
/// <c>GameRules</c>' own composition step (<c>GameRules.BindShotMarkerSessionToMarkers</c>, called from
/// <c>GameRules.Awake()</c>) - the same bind/rebind/null-guard shape
/// <see cref="Level5BasketballMoneyBallStateTests"/> already establishes for <c>IMoneyBallState</c>.
/// This file covers <c>GameRules</c>' composition-time wiring; the marker's own
/// <c>BindShotMarkerSession</c>/completion behavior is covered by
/// <see cref="Level5BasketballMarkerOwnershipTests"/>.
/// </summary>
public class Level5BasketballShotMarkerSessionTests
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
    }

    private GameObject Spawn(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name} must declare a field named '{fieldName}'");
        return field.GetValue(target);
    }

    /// <summary>
    /// A bare GameRules instance, assigned directly to the public static <c>instance</c> field rather
    /// than through Awake() - the GameObject is left inactive so Unity defers Awake(), matching
    /// <see cref="Level5BasketballMoneyBallStateTests"/>' own MakeGameRules helper: this exercises
    /// <c>BindShotMarkerSessionToMarkers</c> in isolation, without pulling in MatchController/
    /// MatchHudPresenter/ProgressionService/MatchSession.
    /// </summary>
    private GameRules MakeGameRules()
    {
        GameObject go = Spawn("game-rules");
        go.SetActive(false);
        GameRules gameRules = go.AddComponent<GameRules>();
        GameRules.instance = gameRules;
        return gameRules;
    }

    private void InvokeBindShotMarkerSessionToMarkers(GameRules gameRules)
    {
        MethodInfo method = typeof(GameRules).GetMethod("BindShotMarkerSessionToMarkers", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "GameRules.BindShotMarkerSessionToMarkers must exist");
        method.Invoke(gameRules, null);
    }

    private BasketBallShotMarker MakeActiveTaggedMarker(string name)
    {
        GameObject go = Spawn(name);
        go.tag = "shot_marker";
        return go.AddComponent<BasketBallShotMarker>();
    }

    [Test]
    public void BindShotMarkerSessionToMarkers_ActiveTaggedMarker_ReceivesTheSurvivingGameRulesSession()
    {
        GameRules gameRules = MakeGameRules();
        BasketBallShotMarker marker = MakeActiveTaggedMarker("marker");

        InvokeBindShotMarkerSessionToMarkers(gameRules);

        Assert.AreSame(gameRules, GetPrivateField(marker, "markerSession"));
    }

    [Test]
    public void BindShotMarkerSessionToMarkers_MultipleActiveTaggedMarkers_AllReceiveTheSameSession()
    {
        GameRules gameRules = MakeGameRules();
        BasketBallShotMarker markerA = MakeActiveTaggedMarker("marker-a");
        BasketBallShotMarker markerB = MakeActiveTaggedMarker("marker-b");

        InvokeBindShotMarkerSessionToMarkers(gameRules);

        Assert.AreSame(gameRules, GetPrivateField(markerA, "markerSession"));
        Assert.AreSame(gameRules, GetPrivateField(markerB, "markerSession"));
    }

    [Test]
    public void BindShotMarkerSessionToMarkers_TaggedObjectWithoutMarkerComponent_LogsActionableErrorAndDoesNotThrow()
    {
        GameRules gameRules = MakeGameRules();
        GameObject untyped = Spawn("untyped-shot-marker");
        untyped.tag = "shot_marker";

        LogAssert.Expect(LogType.Error, new Regex("tagged 'shot_marker' but has no BasketBallShotMarker component"));
        Assert.DoesNotThrow(() => InvokeBindShotMarkerSessionToMarkers(gameRules));
    }

    [Test]
    public void BindShotMarkerSessionToMarkers_UnrelatedObject_IsUnaffected()
    {
        GameRules gameRules = MakeGameRules();
        BasketBallShotMarker marker = MakeActiveTaggedMarker("marker");
        GameObject unrelated = Spawn("unrelated");
        // No LogAssert.Expect: this object is untagged and irrelevant to the scan, so nothing about
        // it should be touched or logged.

        InvokeBindShotMarkerSessionToMarkers(gameRules);

        Assert.AreSame(gameRules, GetPrivateField(marker, "markerSession"));
        Assert.IsNull(unrelated.GetComponent<BasketBallShotMarker>());
    }

    [Test]
    public void BindShotMarkerSessionToMarkers_NoTaggedMarkers_DoesNotThrow()
    {
        GameRules gameRules = MakeGameRules();

        Assert.DoesNotThrow(() => InvokeBindShotMarkerSessionToMarkers(gameRules));
    }

    /// <summary>
    /// AUD-010 Phase 1c: drives the real, pre-existing duplicate-instance guard at the top of
    /// <c>GameRules.Awake()</c> - unchanged by this slice, but <c>BindShotMarkerSessionToMarkers()</c>
    /// now sits right after it, so a duplicate must never reach it either. Unlike
    /// <see cref="MakeGameRules"/>'s surviving instance (assigned to <c>GameRules.instance</c> directly,
    /// bypassing Awake() to avoid MatchController/MatchHudPresenter/ProgressionService/MatchSession),
    /// the <em>duplicate</em>'s real <c>Awake()</c> is safe to invoke here: with <c>GameRules.instance</c>
    /// already pointing at a different object, the guard clause returns immediately - <c>Destroy(gameObject)</c>
    /// then <c>return</c> - before any of those heavier composition steps ever run. Unity does not
    /// actually free the object outside Play Mode (hence <c>LogAssert.ignoreFailingMessages</c>, not a
    /// specific message match - the exact wording is Unity's own, not this codebase's), so the
    /// assertions below check the effect this slice actually owns (the session never reaches the
    /// duplicate's markers, <c>instance</c> never moves) rather than whether the GameObject was freed.
    /// </summary>
    [Test]
    public void Awake_DuplicateInstance_NeverBindsMarkersAndNeverReplacesTheSurvivingInstance()
    {
        GameRules survivor = MakeGameRules();
        BasketBallShotMarker marker = MakeActiveTaggedMarker("marker");
        InvokeBindShotMarkerSessionToMarkers(survivor);
        Assert.AreSame(survivor, GetPrivateField(marker, "markerSession"), "precondition: the surviving instance must already own this marker's session");

        LogAssert.ignoreFailingMessages = true; // Destroy() outside Play Mode logs Unity's own diagnostic; not what this test is about.
        GameObject duplicateGo = Spawn("duplicate-game-rules");
        duplicateGo.AddComponent<GameRules>(); // active GameObject -> Awake() runs synchronously and for real

        Assert.AreSame(survivor, GameRules.instance, "the duplicate's Awake() must never replace the surviving instance");
        Assert.AreSame(survivor, GetPrivateField(marker, "markerSession"), "the duplicate must never reach BindShotMarkerSessionToMarkers - the marker's session must still be the survivor's");
    }
}
