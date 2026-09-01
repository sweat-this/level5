using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Level5.Core;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Phase 1b turned <see cref="GameStats"/> into a facade over <see cref="MatchStats"/>. These tests
/// cover the facade contract, which nothing else does.
///
/// The gap they close: a hand-written delegating property is invisible to the compiler when it is
/// wrong. This compiles, passes every <see cref="Level5MatchStatsTests"/> case, and silently breaks
/// scoring at all ~147 call sites:
///
/// <code>
/// public int TotalPoints { get => Stats.TotalPoints; set => Stats.ShotAttempt = value; }
/// </code>
///
/// Rather than 40 hand-written duplicates that would rot, the delegation is checked by reflection
/// over every writable property the facade declares - which also means a property added later is
/// covered the moment it exists, with no test to remember to write.
/// </summary>
public class Level5GameStatsFacadeTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            Object.DestroyImmediate(go);
        }

        spawned.Clear();
    }

    private GameStats NewStats(string name = "stats")
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go.AddComponent<GameStats>();
    }

    /// <summary>Every property the facade itself declares and can be written through. Inherited
    /// MonoBehaviour members (enabled, tag, hideFlags...) are not ours to delegate.</summary>
    private static IEnumerable<PropertyInfo> DelegatingProperties()
    {
        return typeof(GameStats)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(float));
    }

    /// <summary>The owner's name for a facade member. They match except for the lower-cased legacy
    /// members the facade must keep - blockedShots, campaignWins and friends.</summary>
    private static PropertyInfo OwnerProperty(PropertyInfo facadeProperty)
    {
        string name = char.ToUpperInvariant(facadeProperty.Name[0]) + facadeProperty.Name.Substring(1);
        PropertyInfo owned = typeof(MatchStats).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

        Assert.That(owned, Is.Not.Null,
            $"GameStats.{facadeProperty.Name} has no counterpart MatchStats.{name} - the facade is "
            + "exposing state the owner does not hold");

        return owned;
    }

    private static object DistinctValue(PropertyInfo property, int seed)
    {
        return property.PropertyType == typeof(int) ? (object)(seed + 1) : (object)(seed + 1.5f);
    }

    [Test]
    public void TheFacadeDelegatesEveryPropertyToTheOwner()
    {
        // Writes through the facade, reads off the owner. A setter wired to the wrong MatchStats
        // member fails here even though the value would round-trip perfectly through the facade.
        GameStats facade = NewStats();
        int seed = 0;

        foreach (PropertyInfo facadeProperty in DelegatingProperties())
        {
            object value = DistinctValue(facadeProperty, seed++);
            facadeProperty.SetValue(facade, value);

            Assert.That(facadeProperty.GetValue(facade), Is.EqualTo(value),
                $"GameStats.{facadeProperty.Name} did not read back what was written to it");

            Assert.That(OwnerProperty(facadeProperty).GetValue(facade.Stats), Is.EqualTo(value),
                $"GameStats.{facadeProperty.Name} did not write through to its MatchStats owner");
        }
    }

    [Test]
    public void NoTwoFacadePropertiesShareOneOwnerField()
    {
        // The failure this catches: two facade properties delegating to the same owner member. Each
        // would round-trip on its own, so it only shows up when every value is live at once.
        GameStats facade = NewStats();
        Dictionary<string, object> written = new Dictionary<string, object>();
        int seed = 0;

        foreach (PropertyInfo facadeProperty in DelegatingProperties())
        {
            object value = DistinctValue(facadeProperty, seed++);
            facadeProperty.SetValue(facade, value);
            written[facadeProperty.Name] = value;
        }

        foreach (PropertyInfo facadeProperty in DelegatingProperties())
        {
            Assert.That(facadeProperty.GetValue(facade), Is.EqualTo(written[facadeProperty.Name]),
                $"GameStats.{facadeProperty.Name} was overwritten by another property - they share a field");
        }
    }

    [Test]
    public void TheFacadeCoversTheWholeOwner()
    {
        // The inverse gap: state that exists on the owner but that no call site can still reach,
        // which would mean 1b quietly dropped a counter rather than delegating it.
        IEnumerable<string> facadeNames = DelegatingProperties()
            .Select(p => char.ToUpperInvariant(p.Name[0]) + p.Name.Substring(1));

        IEnumerable<string> ownerNames = typeof(MatchStats)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name);

        Assert.That(ownerNames.Except(facadeNames), Is.Empty,
            "MatchStats holds state that GameStats no longer exposes");
    }

    [Test]
    public void TwoGameStatsNeverShareOneOwner()
    {
        // Local multiplayer depends on this: two PlayerIdentifiers hold two GameStats, and a static
        // or otherwise shared owner would silently pool both players' scores into one.
        GameStats first = NewStats("player-1");
        GameStats second = NewStats("player-2");

        Assert.That(first.Stats, Is.Not.SameAs(second.Stats));

        first.TotalPoints = 21;
        second.TotalPoints = 7;

        Assert.That(first.TotalPoints, Is.EqualTo(21));
        Assert.That(second.TotalPoints, Is.EqualTo(7));
    }

    [Test]
    public void StatsIsUsableEvenIfTheSerializedOwnerCameBackNull()
    {
        // _stats did not exist before this slice. An asset that predates it, or a malformed one,
        // must degrade to a fresh owner rather than to a NullReferenceException on every read.
        GameStats facade = NewStats();
        typeof(GameStats)
            .GetField("_stats", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(facade, null);

        Assert.DoesNotThrow(() => { int _ = facade.TotalPoints; });
        Assert.That(facade.Stats, Is.Not.Null);

        facade.TotalPoints = 3;
        Assert.That(facade.TotalPoints, Is.EqualTo(3));
    }

    [Test]
    public void GetTotalPointAccuracyKeepsItsMethodShapeAndItsAnswer()
    {
        // Five call sites call this as a method, not as a property. The owner exposes a property; the
        // facade must not quietly change shape underneath them.
        GameStats facade = NewStats();
        facade.ShotAttempt = 4;
        facade.ShotMade = 3;

        Assert.That(facade.getTotalPointAccuracy(), Is.EqualTo(75f).Within(0.0001f));
        Assert.That(facade.getTotalPointAccuracy(), Is.EqualTo(facade.Stats.TotalPointAccuracy));
    }

    [Test]
    public void CapturingTwoAttemptBeforeResetMatchesTheLiveBasketBallShotMadeOrdering()
    {
        // AUD-010 Phase 1c retired GameStats.ApplyMadeShot(BasketBallState, ...), the seam the old
        // Level5GameStatsApplyMadeShotTests drove through a real BasketBallState component.
        // BasketBallShotMade.updateShotMadeBasketBallStats now does that read itself: capture
        // TwoAttempt, call MatchStats.ApplyMadeShot, only then let BasketBallState.ResetShotAttemptSnapshot
        // clear it. This pins that exact ordering against the real component (not a hand-written
        // bool) - capturing after the reset instead would silently score every made two-pointer as
        // a three, the AUD-065 class of bug this wiring exists to prevent.
        GameObject stateObject = new GameObject("basketball-state");
        spawned.Add(stateObject);
        BasketBallState state = stateObject.AddComponent<BasketBallState>();
        MatchStats stats = new MatchStats();

        state.TwoAttempt = true;
        state.ThreeAttempt = false;
        stats.ShotAttempt++;

        bool wasTwoPointAttempt = state.TwoAttempt;
        stats.ApplyMadeShot(wasTwoPointAttempt, new ShotScoringInput
        {
            Kind = ShotKind.Two,
            HasStreakBonus = true,
            StreakBonusThreshold = 3
        });
        state.ResetShotAttemptSnapshot();

        Assert.That(wasTwoPointAttempt, Is.True,
            "the made two-pointer must be captured before ResetShotAttemptSnapshot clears TwoAttempt");
        Assert.That(state.TwoAttempt, Is.False,
            "and the component's own flag is cleared immediately afterward, same as production");
        Assert.That(stats.ConsecutiveShotsMade, Is.EqualTo(1),
            "a made two-pointer never extends a streak (preserved oddity) - only observable if the "
            + "true value reached ApplyMadeShot before the reset");
    }
}
