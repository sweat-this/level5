#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.Linq;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 2b0: closes the same coverage gap
/// <see cref="Level5MoneyBallStateCompositionPlayModeTests"/> and
/// <see cref="Level5ShotMarkerSessionCompositionPlayModeTests"/> close for their own migrations, for
/// <c>BasketBallShotMade.BindMatchContext</c>. Every EditMode test for it
/// (<c>Level5BasketBallShotMadeTests</c>) calls it directly against a hand-built component - proving
/// the binding logic is correct in isolation, but never proving the real <c>GameLevelManager.Awake()</c>
/// -&gt; <c>FindAnyObjectByType&lt;BasketBallShotMade&gt;()</c> lookup actually reaches a live scene's
/// real, scene-authored hoop before any made shot can be scored. That depends on the scene actually
/// containing a <c>BasketBallShotMade</c> and on Unity's own object discovery finding it - neither of
/// which an EditMode test (which builds components directly, never lets a real scene load happen) can
/// observe.
///
/// This drives the real production flow - the same technique the two tests above already use - through
/// the start menu into a real gameplay scene, so the hoop under test is the actual scene-authored
/// <c>basketball_goal</c> object and bound by the actual <c>GameLevelManager.Awake()</c>, not a test
/// double standing in for either.
///
/// AUD-012 Phase 2c Slice 44: bootstrap now goes through <see cref="GameplayScenePlayModeHarness"/>
/// instead of this fixture's own <c>StartManager</c>/<c>Pause</c> handling, so it compiles into
/// <c>Level5.PlayModeTests</c> with no <c>Assembly-CSharp</c> reference. The expected match context is
/// read from the typed <see cref="ActiveMatch"/> the real launch produced (<c>GameLevelManager</c> and
/// <c>MatchRuntime</c> both live in <c>Assembly-CSharp</c> and are unreachable from this assembly, but
/// <c>MatchRuntime.Rules</c>/<c>MatchRuntime.ModeId</c> answer with <c>ActiveMatch.Configuration</c>'s
/// own <c>Rules</c> reference/<c>ModeId</c> exactly whenever a match is active, so asserting against
/// <see cref="ActiveMatch"/> directly is equivalent), not by re-deriving it from the hoop's own binding -
/// so the rules-identity assertion below stays non-circular. The compiler-backed exact-type
/// <c>BasketBallShotMade</c> binding proof stays in EditMode's <c>Level5BasketBallShotMadeTests</c>.
/// </summary>
public class Level5BasketBallShotMadeCompositionPlayModeTests
{
    private LocalProfileTestDatabase database;
    private System.Reflection.MethodInfo rememberPrimary;
    private object previousPrimary;
    [SetUp]
    public void IgnoreSceneLogNoise()
    {
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        try
        {
            yield return RealScenePlayModeTestSupport.UnloadAllLoadedScenes("shot-made-composition-cleanup");
        }
        finally
        {
            database?.Dispose();
            database = null;
            rememberPrimary?.Invoke(null, new[] { previousPrimary });
            rememberPrimary = null;
        }
    }

    [UnityTest]
    public IEnumerator TheRealSceneHoopIsBoundToTheRealMatchRulesAndModeBeforeGameplay()
    {
        RealScenePlayModeTestSupport.IgnoreSceneLogNoise();
        database = new LocalProfileTestDatabase();
        yield return database.Open();
        // Choose a valid shooter rather than relying on the remembered/default menu character.
        // MenuStart is outside this assembly; use its existing session seam without a new dependency.
        System.Type session = System.AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("PlayerSelectionSession")).First(type => type != null);
        previousPrimary = session.GetProperty("PrimaryCharacterId").GetValue(null);
        rememberPrimary = session.GetMethod("RememberPrimary");
        CharacterProfile shooter = Resources.LoadAll<GameObject>("Prefabs/menu_start/default_shooter_profiles")
            .Select(prefab => prefab.GetComponent<CharacterProfile>()).First(profile => profile != null && profile.IsShooter);
        rememberPrimary.Invoke(null, new object[] { shooter.PlayerId });
        yield return GameplayScenePlayModeHarness.EnterPlayableGameplayScene(_ => { });

        // The harness only guarantees a live PlayerController, which proves the player spawned but not
        // that GameLevelManager.Awake()'s own BindMatchContext composition step has finished - kept as
        // the fixture's original bounded settle rather than trusting the harness's readiness signal for
        // this.
        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }

        Assert.That(ActiveMatch.IsActive, Is.True, "the real menu launch must leave ActiveMatch active");

        MatchConfiguration configuration = ActiveMatch.Configuration;
        Assert.That(configuration, Is.Not.Null, "ActiveMatch.Configuration must be the configuration the real launch produced");

        BasketBallShotMade shotMade = Object.FindAnyObjectByType<BasketBallShotMade>(FindObjectsInactive.Include);
        Assert.That(shotMade, Is.Not.Null, "no BasketBallShotMade was found in the real scene - nothing for this test to verify");

        Assert.That(
            RealScenePlayModeTestSupport.GetField(shotMade, "hasBoundMatchContext"),
            Is.EqualTo(true),
            $"BasketBallShotMade '{shotMade.gameObject.name}' reached play with no bound match context - GameLevelManager.Awake()'s composition step did not reach it");

        ResolvedMatchRules boundRules = RealScenePlayModeTestSupport.GetField<ResolvedMatchRules>(shotMade, "matchRules");
        Assert.That(boundRules, Is.Not.Null);
        Assert.That(
            ReferenceEquals(boundRules, configuration.Rules),
            Is.True,
            $"BasketBallShotMade '{shotMade.gameObject.name}' is bound to a ResolvedMatchRules reference other than the launched match's own configuration.Rules");

        object boundModeValue = RealScenePlayModeTestSupport.GetField(shotMade, "gameModeId");
        Assert.That(
            boundModeValue,
            Is.Not.Null,
            $"BasketBallShotMade '{shotMade.gameObject.name}' reached play with no bound game mode identity - GameLevelManager.Awake()'s composition step did not reach it");
        Assert.That(
            (GameModeId)boundModeValue,
            Is.EqualTo(configuration.ModeId),
            $"BasketBallShotMade '{shotMade.gameObject.name}' is bound to a mode identity other than the launched match's own configuration.ModeId");
    }
}
#endif
