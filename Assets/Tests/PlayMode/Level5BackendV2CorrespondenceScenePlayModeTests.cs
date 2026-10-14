using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>Real runtime-built screen, fake typed transport, and isolated presentation/session files.</summary>
public class Level5BackendV2CorrespondenceScenePlayModeTests
{
    private BackendV2Session previousSession;
    private readonly Dictionary<string, byte[]> savedFiles = new Dictionary<string, byte[]>();
    private readonly List<Guid> testPlayers = new List<Guid>();
    private Guid player;
    private Guid opponent;
    private ScreenTransport transport;

    [SetUp]
    public void SetUp()
    {
        previousSession = BackendV2SessionStore.Current;
        string path = Path.Combine(Application.persistentDataPath, "backendv2_session.json");
        foreach (string suffix in new[] { "", ".bak", ".tmp" })
            savedFiles[path + suffix] = File.Exists(path + suffix) ? File.ReadAllBytes(path + suffix) : null;
        BackendV2SessionStore.Clear();
        BackendV2SessionPersistenceStore.Clear();
        player = Guid.NewGuid(); opponent = Guid.NewGuid(); testPlayers.Add(player);
        transport = new ScreenTransport(player, opponent);
        BackendV2Runtime.Override(transport);
    }

    [TearDown]
    public void TearDown()
    {
        GameObject screen = GameObject.Find("CorrespondenceScreen");
        if (screen != null) UnityEngine.Object.DestroyImmediate(screen);
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
        if (previousSession != null) BackendV2SessionStore.Set(previousSession);
        foreach (var file in savedFiles)
        {
            if (file.Value == null) { if (File.Exists(file.Key)) File.Delete(file.Key); }
            else File.WriteAllBytes(file.Key, file.Value);
        }
        savedFiles.Clear();
        foreach (Guid id in testPlayers)
            AtomicFile.TryDeleteFamily(Path.Combine(Application.persistentDataPath, "backendv2-correspondence-read-" + id.ToString("N") + ".json"));
        testPlayers.Clear();
    }

    private static BackendV2Session Session(Guid id) => new BackendV2Session(
        "fake-access", DateTimeOffset.UtcNow.AddHours(1), id, "fake-refresh", DateTimeOffset.UtcNow.AddDays(1));
    private IEnumerator OpenAuthenticated()
    {
        BackendV2SessionPersistenceBootstrap.EnsureInitialized();
        BackendV2SessionStore.Set(Session(player));
        yield return SceneManager.LoadSceneAsync("level_00_multiplayer");
        yield return WaitFor(() => VisibleText().Contains("Your turn (1 loaded)"));
    }
    private static IEnumerator WaitFor(Func<bool> ready)
    {
        float until = Time.realtimeSinceStartup + 10f;
        while (!ready() && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(ready(), Is.True, "screen did not settle: " + VisibleText());
        yield return null;
    }
    private static Button ButtonNamed(string label) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
        .First(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text == label);
    private static string VisibleText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
        .Where(t => t.gameObject.activeInHierarchy).Select(t => t.text));

    [UnityTest]
    public IEnumerator TheCorrespondenceSceneLoadsAndBuildsItsUiWithoutErrors()
    {
        yield return SceneManager.LoadSceneAsync("level_00_multiplayer");
        yield return null; yield return null; yield return null;
        GameObject screen = GameObject.Find("CorrespondenceScreen");
        Assert.That(screen, Is.Not.Null);
        Assert.That(screen.GetComponentInChildren<Canvas>(), Is.Not.Null);
        Assert.That(GameObject.Find("LoginPanel"), Is.Not.Null);
        Assert.That(transport.Requests, Is.Empty);
    }

    [UnityTest]
    public IEnumerator InboxReusesPagesAcknowledgesExplicitlyAndIsolatesAccountsAcrossSceneReload()
    {
        yield return OpenAuthenticated();
        Assert.That(VisibleText(), Does.Contain("Grace (GRACE#5678)"));
        Assert.That(VisibleText(), Does.Contain("Unconfirmed (0 loaded)"));
        int calls = transport.Requests.Count;
        ButtonNamed("Inbox").onClick.Invoke(); yield return null;
        Assert.That(transport.Requests.Count, Is.EqualTo(calls), "Inbox is a cache projection, not a duplicate query");
        string key = "challenge:" + transport.Incoming.Id.ToString("N");
        Assert.That(new CorrespondenceReadState(player).IsUnread(key), Is.True);
        UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.gameObject.activeInHierarchy
            && b.GetComponentInChildren<Text>()?.text == "Mark read"
            && b.transform.parent.GetComponentsInChildren<Text>().Any(t => t.text.Contains(transport.Incoming.Id.ToString().Substring(0, 8))))
            .onClick.Invoke(); yield return null;
        Assert.That(new CorrespondenceReadState(player).IsUnread(key), Is.False);
        yield return SceneManager.LoadSceneAsync("level_00_multiplayer");
        yield return WaitFor(() => VisibleText().Contains("Your turn (1 loaded)"));
        Assert.That(new CorrespondenceReadState(player).IsUnread(key), Is.False);
        Guid other = Guid.NewGuid(); testPlayers.Add(other);
        int beforeSwitch = transport.Requests.Count;
        BackendV2SessionStore.Clear(); BackendV2SessionStore.Set(Session(other));
        yield return WaitFor(() => transport.Requests.Count >= beforeSwitch + 9);
        Assert.That(VisibleText(), Does.Not.Contain("Grace (GRACE#5678)"), "foreign account rows must not survive switching");
        Assert.That(new CorrespondenceReadState(other).IsUnread(key), Is.True);
        BackendV2SessionStore.Set(Session(player));
        yield return WaitFor(() => VisibleText().Contains("Your turn (1 loaded)"));
        Assert.That(new CorrespondenceReadState(player).IsUnread(key), Is.False);
    }

    [UnityTest]
    public IEnumerator ActivePaginationPreservesCursorAndClassifiesOnlyTheNewRow()
    {
        yield return OpenAuthenticated();
        ButtonNamed("More Active").onClick.Invoke();
        yield return WaitFor(() => VisibleText().Contains("Your turn (2 loaded)"));
        Assert.That(VisibleText(), Does.Contain("Your turn (2 loaded)"));
        var pages = transport.Requests.Where(r => r.RelativePath == "api/v2/series/active").ToList();
        Assert.That(pages.Count, Is.EqualTo(2)); Assert.That(pages[1].Query["cursor"], Is.EqualTo("opaque /+="));
        Assert.That(transport.Requests.Count(r => r.RelativePath == "api/v2/series/" + transport.Active.Id), Is.EqualTo(1));
        Assert.That(transport.Requests.Count(r => r.RelativePath == "api/v2/series/" + transport.NextActive.Id), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator RematchReviewUsesNormalCreateAndPreservesRetryIdentity()
    {
        yield return OpenAuthenticated();
        ButtonNamed("Rematch").onClick.Invoke();
        yield return WaitFor(() => VisibleText().Contains("Rematch draft"));
        Assert.That(VisibleText(), Does.Contain("Rematch draft"));
        Assert.That(transport.Requests.All(r => r.Method == ApiHttpMethod.Get), Is.True, "prepare never sends a challenge");
        transport.FailFirstCreate = true;
        Button send = ButtonNamed("Send Challenge"); send.onClick.Invoke(); send.onClick.Invoke();
        yield return WaitFor(() => VisibleText().Contains("retry sends the same draft"));
        Assert.That(VisibleText(), Does.Contain("retry sends the same draft"));
        Assert.That(transport.Creates.Count, Is.EqualTo(1));
        ButtonNamed("Send Challenge").onClick.Invoke();
        yield return WaitFor(() => transport.Creates.Count == 2 && VisibleText().Contains("challenge sent"));
        Assert.That(transport.Creates.Count, Is.EqualTo(2));
        Assert.That(transport.Creates[1].ClientRequestId, Is.EqualTo(transport.Creates[0].ClientRequestId));
        Assert.That(transport.Creates[0].OpponentId, Is.EqualTo(opponent)); Assert.That(transport.Creates[0].RulesetVersion, Is.EqualTo(1));
        Assert.That(transport.Completed.Status, Is.EqualTo("Completed")); Assert.That(transport.Completed.Revision, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator AcceptConvergesInboxAndDetailedSourcesWithoutDuplicateCommand()
    {
        yield return OpenAuthenticated();
        Button accept = ButtonNamed("Accept"); accept.onClick.Invoke(); accept.onClick.Invoke();
        yield return WaitFor(() => VisibleText().Contains("Incoming challenges (0 loaded)") && VisibleText().Contains("Your turn (2 loaded)"));
        Assert.That(transport.Requests.Count(r => r.RelativePath.EndsWith("/accept")), Is.EqualTo(1));
        Assert.That(VisibleText(), Does.Contain("Incoming challenges (0 loaded)"));
        Assert.That(VisibleText(), Does.Contain("Your turn (2 loaded)"));
        foreach (string source in new[] { "outgoing", "active", "completed", "history" })
            Assert.That(transport.Requests.Count(r => r.RelativePath == "api/v2/series/" + source), Is.GreaterThanOrEqualTo(2));
    }

    [UnityTest]
    public IEnumerator DetailedTabsAndInboxAreReachableThroughAutomaticNavigation()
    {
        yield return OpenAuthenticated();
        Canvas.ForceUpdateCanvases();
        var reached = new HashSet<Selectable>(); var pending = new Queue<Selectable>();
        pending.Enqueue(ButtonNamed("Inbox"));
        while (pending.Count > 0)
        {
            var current = pending.Dequeue(); if (current == null || !reached.Add(current)) continue;
            foreach (var next in new[] { current.FindSelectableOnUp(), current.FindSelectableOnDown(), current.FindSelectableOnLeft(), current.FindSelectableOnRight() })
                if (next != null && !reached.Contains(next)) pending.Enqueue(next);
        }
        foreach (string label in new[] { "Back", "Inbox", "Refresh", "Friends", "Incoming", "Outgoing", "Your Turn", "Active", "Completed", "History" })
            Assert.That(reached, Does.Contain(ButtonNamed(label)), label + " must remain reachable");
        EventSystem.current.SetSelectedGameObject(ButtonNamed("Inbox").gameObject);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(ButtonNamed("Inbox").gameObject));
    }

    private sealed class ScreenTransport : IApiTransport
    {
        public readonly List<ApiRequest> Requests = new List<ApiRequest>();
        public readonly List<CreateChallengeDto> Creates = new List<CreateChallengeDto>();
        public readonly SeriesSummaryDto Incoming;
        public readonly SeriesSummaryDto Active;
        public readonly SeriesSummaryDto NextActive;
        public readonly SeriesSummaryDto Completed;
        public bool FailFirstCreate;
        private bool accepted;
        public ScreenTransport(Guid player, Guid opponent)
        {
            SeriesSummaryDto Summary(string status) => new SeriesSummaryDto { Id = Guid.NewGuid(), ChallengerId = opponent, OpponentId = player,
                Challenger = new PlayerProfileResponseDto { PlayerId = opponent, DisplayName = "Grace", Tag = "GRACE#5678" },
                Status = status, Revision = 1, TotalGames = 3, CurrentGameNumber = 1, CreatedAt = DateTimeOffset.UtcNow };
            Incoming = Summary("PendingAcceptance"); Active = Summary("Active"); NextActive = Summary("Active"); Completed = Summary("Completed");
        }
        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);
            if (request.Method == ApiHttpMethod.Post)
            {
                for (int i = 0; i < 3; i++) yield return null;
                if (request.RelativePath.EndsWith("/accept"))
                { accepted = true; Incoming.Status = "Active"; Incoming.Revision++; completed(RawApiResponse.Completed(200, BackendV2Json.Serialize(Detail(Incoming)))); yield break; }
                Creates.Add((CreateChallengeDto)request.Body);
                completed(FailFirstCreate && Creates.Count == 1 ? RawApiResponse.NetworkError()
                    : RawApiResponse.Completed(200, BackendV2Json.Serialize(Detail(Incoming)))); yield break;
            }
            if (!request.RelativePath.StartsWith("api/v2/series"))
            {
                string body = request.RelativePath == "api/v2/friends" ? BackendV2Json.Serialize(new[] {
                    new FriendSummaryDto { PlayerId = Incoming.ChallengerId, DisplayName = "Grace", Tag = "GRACE#5678" } }) : "[]";
                completed(RawApiResponse.Completed(200, body)); yield break;
            }
            string source = request.RelativePath.Substring("api/v2/series/".Length);
            if (Guid.TryParse(source, out Guid id))
            { var item = new[] { Incoming, Active, NextActive, Completed }.First(s => s.Id == id); completed(RawApiResponse.Completed(200, BackendV2Json.Serialize(Detail(item)))); yield break; }
            List<SeriesSummaryDto> items = source == "incoming" && !accepted ? new List<SeriesSummaryDto> { Incoming }
                : source == "active" ? request.Query.ContainsKey("cursor") ? new List<SeriesSummaryDto> { NextActive }
                    : accepted ? new List<SeriesSummaryDto> { Active, Incoming } : new List<SeriesSummaryDto> { Active }
                : source == "completed" || source == "history" ? new List<SeriesSummaryDto> { Completed } : new List<SeriesSummaryDto>();
            completed(RawApiResponse.Completed(200, BackendV2Json.Serialize(new SeriesSummaryPageDto { Items = items, Limit = 20,
                NextCursor = source == "active" && !request.Query.ContainsKey("cursor") ? "opaque /+=" : null })));
        }
        private static SeriesResponseDto Detail(SeriesSummaryDto item) => new SeriesResponseDto { Id = item.Id, ChallengerId = item.ChallengerId,
            OpponentId = item.OpponentId, Challenger = item.Challenger, Status = item.Status, Revision = item.Revision,
            CurrentGameNumber = item.CurrentGameNumber, TotalGames = item.TotalGames,
            Rules = new FrozenRulesDto { CompetitionProtocolVersion = 1, RulesetId = "most-points", RulesetVersion = 1, InformationPolicy = "SealedAttempt" },
            Games = new List<GameRoundViewDto> { new GameRoundViewDto { GameNumber = 1 } } };
    }
}
