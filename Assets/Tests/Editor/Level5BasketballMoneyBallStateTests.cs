using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Level5.Core.Match;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// AUD-010 Phase 1c: <see cref="BasketballShotPipeline"/>'s last direct <c>GameRules</c> dependency,
/// <c>GameRules.instance.MoneyBallEnabled</c>, is replaced by a live <see cref="IMoneyBallState"/>
/// bound once to each spawned basketball by <c>GameRules</c>' own composition step
/// (<c>GameRules.BindMoneyBallStateToBasketballs</c>) - mirroring the bind/rebind/null-guard shape
/// <see cref="Level5BasketballGroundHeightProviderTests"/> already establishes for
/// <c>IGroundHeightProvider</c>. The pipeline-level "provider stays live, not a copied bool" behavior
/// is covered by <see cref="Level5BasketballShotPipelineTests"/>; this file covers the two balls'
/// own <c>BindMoneyBallState</c> methods and <c>GameRules</c>' composition-time wiring to them.
/// </summary>
public class Level5BasketballMoneyBallStateTests
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

    private sealed class FakeMoneyBallState : IMoneyBallState
    {
        public bool MoneyBallEnabled { get; set; }
    }

    // ==================== BasketBall.BindMoneyBallState ====================

    [Test]
    public void BasketBall_BindMoneyBallState_ValidProvider_Binds()
    {
        BasketBall ball = Spawn("ball").AddComponent<BasketBall>();
        FakeMoneyBallState provider = new FakeMoneyBallState { MoneyBallEnabled = true };

        ball.BindMoneyBallState(provider);

        Assert.AreSame(provider, GetPrivateField(ball, "moneyBallState"));
    }

    [Test]
    public void BasketBall_BindMoneyBallState_NullProvider_LogsAndLeavesUnbound()
    {
        BasketBall ball = Spawn("ball").AddComponent<BasketBall>();

        LogAssert.Expect(LogType.Error, new Regex("null money-ball state provider"));
        ball.BindMoneyBallState(null);

        Assert.IsNull(GetPrivateField(ball, "moneyBallState"));
    }

    [Test]
    public void BasketBall_BindMoneyBallState_SecondCall_IsRejectedAndOriginalProviderRetained()
    {
        BasketBall ball = Spawn("ball").AddComponent<BasketBall>();
        FakeMoneyBallState first = new FakeMoneyBallState { MoneyBallEnabled = false };
        FakeMoneyBallState second = new FakeMoneyBallState { MoneyBallEnabled = true };
        ball.BindMoneyBallState(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound money-ball state provider"));
        ball.BindMoneyBallState(second);

        Assert.AreSame(first, GetPrivateField(ball, "moneyBallState"),
            "a second BindMoneyBallState call must not overwrite the original binding");
    }

    // ==================== BasketBallAuto.BindMoneyBallState ====================

    [Test]
    public void BasketBallAuto_BindMoneyBallState_ValidProvider_Binds()
    {
        BasketBallAuto ball = Spawn("cpu-ball").AddComponent<BasketBallAuto>();
        FakeMoneyBallState provider = new FakeMoneyBallState { MoneyBallEnabled = true };

        ball.BindMoneyBallState(provider);

        Assert.AreSame(provider, GetPrivateField(ball, "moneyBallState"));
    }

    [Test]
    public void BasketBallAuto_BindMoneyBallState_NullProvider_LogsAndLeavesUnbound()
    {
        BasketBallAuto ball = Spawn("cpu-ball").AddComponent<BasketBallAuto>();

        LogAssert.Expect(LogType.Error, new Regex("null money-ball state provider"));
        ball.BindMoneyBallState(null);

        Assert.IsNull(GetPrivateField(ball, "moneyBallState"));
    }

    [Test]
    public void BasketBallAuto_BindMoneyBallState_SecondCall_IsRejectedAndOriginalProviderRetained()
    {
        BasketBallAuto ball = Spawn("cpu-ball").AddComponent<BasketBallAuto>();
        FakeMoneyBallState first = new FakeMoneyBallState { MoneyBallEnabled = false };
        FakeMoneyBallState second = new FakeMoneyBallState { MoneyBallEnabled = true };
        ball.BindMoneyBallState(first);

        LogAssert.Expect(LogType.Error, new Regex("already has a bound money-ball state provider"));
        ball.BindMoneyBallState(second);

        Assert.AreSame(first, GetPrivateField(ball, "moneyBallState"),
            "a second BindMoneyBallState call must not overwrite the original binding");
    }

    // ==================== GameRules.BindMoneyBallStateToBasketballs composition ====================

    /// <summary>
    /// A bare GameRules instance, assigned directly to the public static <c>instance</c> field rather
    /// than through Awake() - the GameObject is left inactive so Unity defers Awake() (it only runs
    /// once first activated), matching <see cref="Level5BasketballShotPipelineTests"/>' own
    /// MakeGameRules helper and its header comment on why that is safe here: GameRules.Awake() pulls
    /// in MatchController/MatchHudPresenter/ProgressionService/MatchSession, none of which
    /// BindMoneyBallStateToBasketballs needs.
    /// </summary>
    private GameRules MakeGameRules()
    {
        GameObject go = Spawn("game-rules");
        go.SetActive(false);
        GameRules gameRules = go.AddComponent<GameRules>();
        GameRules.instance = gameRules;
        return gameRules;
    }

    private void InvokeBindMoneyBallStateToBasketballs(GameRules gameRules, PlayerRegistry registry)
    {
        MethodInfo method = typeof(GameRules).GetMethod("BindMoneyBallStateToBasketballs", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "GameRules.BindMoneyBallStateToBasketballs must exist");
        method.Invoke(gameRules, new object[] { registry });
    }

    private PlayerIdentifier RegisterHumanWithBall(int pid, PlayerRegistry registry)
    {
        GameObject actorGo = Spawn($"human-actor-{pid}");
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, false);

        GameObject ballGo = Spawn($"human-ball-{pid}");
        ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<BasketBall>();
        identifier.setBasketball(ballGo);

        registry.Add(identifier);
        return identifier;
    }

    private PlayerIdentifier RegisterCpuWithBall(int pid, PlayerRegistry registry)
    {
        GameObject actorGo = Spawn($"cpu-actor-{pid}");
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, true);

        GameObject ballGo = Spawn($"cpu-ball-{pid}");
        ballGo.AddComponent<BasketBallState>();
        ballGo.AddComponent<BasketBallAuto>();
        identifier.setAutoBasketball(ballGo);

        registry.Add(identifier);
        return identifier;
    }

    private PlayerIdentifier RegisterParticipantWithNoBall(int pid, PlayerRegistry registry)
    {
        GameObject actorGo = Spawn($"no-ball-actor-{pid}");
        PlayerIdentifier identifier = actorGo.AddComponent<PlayerIdentifier>();
        identifier.setIds(pid, true);
        registry.Add(identifier);
        return identifier;
    }

    [Test]
    public void BindMoneyBallStateToBasketballs_HumanAndCpuParticipants_BothBallsReceiveTheSameLiveProvider()
    {
        GameRules gameRules = MakeGameRules();
        PlayerRegistry registry = new PlayerRegistry();
        PlayerIdentifier human = RegisterHumanWithBall(0, registry);
        PlayerIdentifier cpu = RegisterCpuWithBall(1, registry);

        InvokeBindMoneyBallStateToBasketballs(gameRules, registry);

        BasketBall humanBall = human.basketball.GetComponent<BasketBall>();
        BasketBallAuto cpuBall = cpu.autoBasketball.GetComponent<BasketBallAuto>();
        Assert.AreSame(gameRules, GetPrivateField(humanBall, "moneyBallState"));
        Assert.AreSame(gameRules, GetPrivateField(cpuBall, "moneyBallState"));
    }

    [Test]
    public void BindMoneyBallStateToBasketballs_SecondaryHumanParticipant_AlsoReceivesTheProvider()
    {
        GameRules gameRules = MakeGameRules();
        PlayerRegistry registry = new PlayerRegistry();
        RegisterHumanWithBall(0, registry);
        PlayerIdentifier secondaryHuman = RegisterHumanWithBall(1, registry);

        InvokeBindMoneyBallStateToBasketballs(gameRules, registry);

        BasketBall secondaryBall = secondaryHuman.basketball.GetComponent<BasketBall>();
        Assert.AreSame(gameRules, GetPrivateField(secondaryBall, "moneyBallState"));
    }

    [Test]
    public void BindMoneyBallStateToBasketballs_ParticipantWithNoBasketball_IsSkippedNotAnError()
    {
        GameRules gameRules = MakeGameRules();
        PlayerRegistry registry = new PlayerRegistry();
        RegisterHumanWithBall(0, registry);
        RegisterParticipantWithNoBall(1, registry);

        // No LogAssert.Expect: an unexpected Debug.LogError fails an EditMode test by default, so a
        // regression that starts requiring a ball for every participant fails here.
        Assert.DoesNotThrow(() => InvokeBindMoneyBallStateToBasketballs(gameRules, registry));
    }

    [Test]
    public void BindMoneyBallStateToBasketballs_NullRegistry_LogsAndDoesNotThrow()
    {
        GameRules gameRules = MakeGameRules();

        LogAssert.Expect(LogType.Error, new Regex("no participant registry"));
        Assert.DoesNotThrow(() => InvokeBindMoneyBallStateToBasketballs(gameRules, null));
    }
}
