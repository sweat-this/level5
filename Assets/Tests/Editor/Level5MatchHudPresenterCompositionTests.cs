using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AUD-012 Phase 2b Slice 62: <see cref="MatchHudPresenter"/> no longer reads
/// <c>GameLevelManager.instance</c>/<c>Timer.instance</c> itself - <see cref="MatchHudPresenter.updatePlayerScore"/>
/// and the <c>VersusCpu</c>/<c>BeatThaComputahs</c> branch of the private <c>GetDisplayText</c> only
/// consult whatever delegates <see cref="MatchHudPresenter.BindGameLevelManagerContext"/> was called
/// with. Mirrors the shape of <c>Level5TimerMatchEndContextTests</c>: composition-forwarding tests
/// driving the real methods directly (both are public; <c>GetDisplayText</c> via reflection), then
/// separate production-adapter tests against live <see cref="GameLevelManager"/>/<see cref="GameRules"/>
/// singletons.
///
/// Deliberately does not exercise <see cref="MatchHudPresenter.SetScoreDisplayText"/>'s many game-mode
/// branches - every one of them is gated behind a live <c>PlayerData.instance</c>, the persistence-layer
/// blocker this slice explicitly does not touch (see docs/systems-restructure-plan.md, Slice 62).
/// Building that rig here would smuggle persistence-layer scope into a game-manager-cycle slice.
/// </summary>
public class Level5MatchHudPresenterCompositionTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private GameLevelManager savedGameLevelManagerInstance;
    private GameRules savedGameRulesInstance;

    [SetUp]
    public void SetUp()
    {
        savedGameLevelManagerInstance = GameLevelManager.instance;
        savedGameRulesInstance = GameRules.instance;
    }

    [TearDown]
    public void TearDown()
    {
        GameLevelManager.instance = savedGameLevelManagerInstance;
        GameRules.instance = savedGameRulesInstance;

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

    private GameLevelManager SpawnManagerWithoutAwake(string name)
    {
        GameObject go = new GameObject(name);
        go.SetActive(false);
        spawned.Add(go);
        return go.AddComponent<GameLevelManager>();
    }

    private Text SpawnText(string name)
    {
        return Spawn(name).AddComponent<Text>();
    }

    private MatchHudPresenter MakePresenter()
    {
        return Spawn("hud").AddComponent<MatchHudPresenter>();
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

    private static string InvokeGetDisplayText(MatchHudPresenter hud, int modeId)
    {
        MethodInfo method = typeof(MatchHudPresenter).GetMethod("GetDisplayText", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "MatchHudPresenter must declare GetDisplayText(int)");
        return (string)method.Invoke(hud, new object[] { modeId });
    }

    private PlayerIdentifier MakePlayer(string name, int pid, string displayName)
    {
        GameObject go = Spawn(name);
        PlayerIdentifier player = go.AddComponent<PlayerIdentifier>();
        player.pid = pid;
        player.isCpu = false;
        player.characterProfile = go.AddComponent<CharacterProfile>();
        player.characterProfile.PlayerDisplayName = displayName;
        player.gameStats = go.AddComponent<GameStats>();
        player.gameStats.Stats.TotalPoints = 7;
        return player;
    }

    // ==================== updatePlayerScore() forwarding ====================

    [Test]
    public void UpdatePlayerScore_ForwardsThroughSortedGameStatsListReaderAndScoreClockTextReader()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "displayP1ScoreText", SpawnText("p1"));
        SetPrivateField(hud, "displayP2ScoreText", SpawnText("p2"));
        SetPrivateField(hud, "displayP3ScoreText", SpawnText("p3"));
        SetPrivateField(hud, "displayP4ScoreText", SpawnText("p4"));

        PlayerIdentifier player = MakePlayer("player1", 0, "Blood");
        Text scoreClock = SpawnText("score-clock");

        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier> { player },
            primaryPlayerReader: () => player,
            firstRegisteredPlayerReader: () => player,
            scoreClockTextReader: () => scoreClock);

        hud.updatePlayerScore();

        Assert.That(scoreClock.text, Is.EqualTo("7"),
            "the score clock must show the exact TotalPoints of the sortedGameStatsListReader's first entry.");
        Assert.That((Text)GetPrivateField(hud, "displayP1ScoreText"), Has.Property("text").Contains("Blood"),
            "player 1's display must include the character's display name from the forwarded PlayerIdentifier.");
    }

    // ==================== GetDisplayText's VersusCpu branch forwarding ====================

    [Test]
    public void GetDisplayText_VersusCpuMode_NoPlayers_ReportsGameOverWithoutThrowing()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.VersusCpu);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => new List<PlayerIdentifier>(),
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => null);

        string result = InvokeGetDisplayText(hud, Modes.VersusCpu);

        Assert.That(result, Does.StartWith("Game over"),
            "an empty sortedGameStatsListReader result must report 'Game over' rather than throwing - unchanged from the former GameLevelManager.instance.getSortedGameStatsList() empty-list behavior.");
    }

    [Test]
    public void GetDisplayText_VersusCpuMode_ForwardsThroughSortedGameStatsListReader()
    {
        MatchHudPresenter hud = MakePresenter();
        SetPrivateField(hud, "gameModeId", Modes.VersusCpu);
        SetPrivateField(hud, "gameStats1", Spawn("stats-holder").AddComponent<GameStats>());

        PlayerIdentifier winner = MakePlayer("winner", 0, "Champ");
        bool readerCalled = false;
        hud.BindGameLevelManagerContext(
            sortedGameStatsListReader: () => { readerCalled = true; return new List<PlayerIdentifier> { winner }; },
            primaryPlayerReader: () => null,
            firstRegisteredPlayerReader: () => null,
            scoreClockTextReader: () => null);

        string result = InvokeGetDisplayText(hud, Modes.VersusCpu);

        Assert.IsTrue(readerCalled, "GetDisplayText's VersusCpu branch must consult the bound sortedGameStatsListReader.");
        Assert.That(result, Does.Contain("Champ wins!"));
    }

    // ================ production adapters: GameRules' MatchHudPresenter composition ================

    private static MethodInfo GameRulesAdapterMethod(string name, BindingFlags extra = BindingFlags.Static)
    {
        MethodInfo method = typeof(GameRules).GetMethod(name, BindingFlags.NonPublic | extra);
        Assert.IsNotNull(method, $"GameRules.{name} must exist as a production MatchHudPresenter adapter");
        return method;
    }

    [Test]
    public void ReadSortedGameStatsListForHud_LiveGameLevelManager_ForwardsResult()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = MakePlayer("registered", 0, "Solo");
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = GameRulesAdapterMethod("ReadSortedGameStatsListForHud").Invoke(null, null);

        Assert.That(result, Is.EqualTo(manager.getSortedGameStatsList()));
    }

    [Test]
    public void ReadPrimaryPlayerForHud_LiveGameLevelManager_ForwardsPlayer1()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = MakePlayer("registered", 0, "Solo");
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = GameRulesAdapterMethod("ReadPrimaryPlayerForHud").Invoke(null, null);

        Assert.AreSame(manager.Player1, result);
    }

    [Test]
    public void ReadFirstRegisteredPlayerForHud_LiveGameLevelManager_ForwardsFirstRegisteredEntry()
    {
        GameLevelManager manager = SpawnManagerWithoutAwake("game-level-manager");
        GameLevelManager.instance = manager;
        PlayerIdentifier player = MakePlayer("registered", 0, "Solo");
        ((PlayerRegistry)GetPrivateField(manager, "registry")).Add(player);

        object result = GameRulesAdapterMethod("ReadFirstRegisteredPlayerForHud").Invoke(null, null);

        Assert.AreSame(manager.players[0], result);
    }

    [Test]
    public void ReadScoreClockTextForHud_ReadsThisInstancesOwnTimerFieldNotTheStatic()
    {
        GameRules rules = Spawn("game-rules").AddComponent<GameRules>();
        Text ownTimerScoreClock = SpawnText("own-timer-score-clock");
        Timer ownTimer = Spawn("own-timer").AddComponent<Timer>();
        ownTimer.ScoreClockText = ownTimerScoreClock;
        SetPrivateField(rules, "timer", ownTimer);

        object result = GameRulesAdapterMethod("ReadScoreClockTextForHud", BindingFlags.Instance).Invoke(rules, null);

        Assert.AreSame(ownTimerScoreClock, result,
            "the adapter must read this GameRules instance's own resolved timer field, not the Timer.instance static.");
    }
}
