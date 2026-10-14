using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Level5.BackendV2;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>Real screen actions with controlled request ordering and isolated session files.</summary>
public class Level5BackendV2CorrespondenceOrderingPlayModeTests
{
    private readonly Dictionary<string, byte[]> savedFiles = new Dictionary<string, byte[]>();
    private BackendV2Session previousSession;
    private DelayedTransport transport;
    private MonoBehaviour screen;

    [SetUp]
    public void SetUp()
    {
        previousSession = BackendV2SessionStore.Current;
        string path = Path.Combine(Application.persistentDataPath, "backendv2_session.json");
        foreach (string suffix in new[] { "", ".bak", ".tmp" })
            savedFiles[path + suffix] = File.Exists(path + suffix) ? File.ReadAllBytes(path + suffix) : null;
        BackendV2SessionPersistenceStore.Clear();
        BackendV2SessionPersistenceBootstrap.EnsureInitialized();
        Guid player = Guid.NewGuid();
        BackendV2SessionStore.Set(new BackendV2Session("fake", DateTimeOffset.UtcNow.AddHours(1),
            player, "fake", DateTimeOffset.UtcNow.AddDays(1)));
        transport = new DelayedTransport(player);
        BackendV2Runtime.Override(transport);
    }

    [TearDown]
    public void TearDown()
    {
        GameObject host = GameObject.Find("CorrespondenceScreen");
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
        BackendV2SessionStore.Clear();
        BackendV2Runtime.Reset();
        if (previousSession != null) BackendV2SessionStore.Set(previousSession);
        foreach (var file in savedFiles)
        {
            if (file.Value == null)
            {
                if (File.Exists(file.Key)) File.Delete(file.Key);
            }
            else File.WriteAllBytes(file.Key, file.Value);
        }
        savedFiles.Clear();
    }

    private static Button ButtonNamed(string label) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
        .First(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<Text>()?.text == label);

    private static Button ViewButton(Guid seriesId) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
        .First(button => button.gameObject.activeInHierarchy && button.GetComponentInChildren<Text>()?.text == "View"
            && button.transform.parent.GetComponentsInChildren<Text>()
                .Any(text => text.text.Contains(seriesId.ToString().Substring(0, 8))));

    private static string VisibleText() => string.Join("\n", UnityEngine.Object.FindObjectsByType<Text>(FindObjectsSortMode.None)
        .Where(text => text.gameObject.activeInHierarchy).Select(text => text.text));

    // The controller belongs to Assembly-CSharp, which this test assembly cannot reference.
    // Reflection observes only when its existing asynchronous refresh has settled.
    private bool IsRefreshing => (bool)screen.GetType().GetField("refreshing", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);

    private static IEnumerator WaitFor(Func<bool> ready)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(ready(), Is.True, "screen did not settle: " + VisibleText());
        yield return null;
    }

    private IEnumerator Open()
    {
        yield return SceneManager.LoadSceneAsync("level_00_multiplayer");
        screen = GameObject.Find("CorrespondenceScreen").GetComponents<MonoBehaviour>()
            .First(component => component.GetType().Name == "CorrespondenceScreenController");
        yield return WaitFor(() => VisibleText().Contains("History: 2 loaded") && !IsRefreshing);
    }

    [UnityTest]
    public IEnumerator LastViewedSeriesWinsWhenResponsesCompleteOutOfOrder()
    {
        yield return Open();
        ViewButton(transport.FirstSeriesId).onClick.Invoke();
        ViewButton(transport.SecondSeriesId).onClick.Invoke();
        yield return WaitFor(() => transport.PendingDetails.Count == 2);
        transport.Complete(transport.SecondSeriesId);
        string latest = "Selected completed series " + transport.SecondSeriesId.ToString().Substring(0, 8);
        yield return WaitFor(() => VisibleText().Contains(latest));
        transport.Complete(transport.FirstSeriesId);
        yield return WaitFor(() => transport.PendingDetails.Count == 0);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.That(VisibleText(), Does.Contain(latest), "older responses must not replace the latest selection");
        Assert.That(VisibleText(), Does.Not.Contain("Selected completed series " + transport.FirstSeriesId.ToString().Substring(0, 8)));
    }

    [UnityTest]
    public IEnumerator TabSelectedDuringAnotherRefreshEventuallyRefreshesItsOwnSource()
    {
        yield return Open();
        int before = transport.Count("incoming");
        transport.NewIncoming = true;
        transport.HoldActive = true;
        ButtonNamed("Active").onClick.Invoke();
        yield return WaitFor(() => transport.ActiveWaiting);
        ButtonNamed("Incoming").onClick.Invoke();
        transport.HoldActive = false;
        yield return WaitFor(() => transport.Count("incoming") == before + 1 && !IsRefreshing);
        Assert.That(VisibleText(), Does.Contain("Incoming Challenges (1 loaded)"));
    }

    [UnityTest]
    public IEnumerator TabsSelectedInOneFrameSerializeTheirRefreshes()
    {
        yield return Open();
        int incomingBefore = transport.Count("incoming");
        transport.HoldActive = true;
        ButtonNamed("Active").onClick.Invoke();
        ButtonNamed("Incoming").onClick.Invoke();
        yield return WaitFor(() => transport.ActiveWaiting);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.That(transport.Count("incoming"), Is.EqualTo(incomingBefore), "the next tab must wait for the running refresh");
        transport.HoldActive = false;
        yield return WaitFor(() => transport.Count("incoming") == incomingBefore + 1 && !IsRefreshing);
    }

    [UnityTest]
    public IEnumerator OnlyTheLatestQueuedTabRefreshesAfterTheRunningRequest()
    {
        yield return Open();
        int incomingBefore = transport.Count("incoming");
        int outgoingBefore = transport.Count("outgoing");
        transport.HoldActive = true;
        ButtonNamed("Active").onClick.Invoke();
        yield return WaitFor(() => transport.ActiveWaiting);
        ButtonNamed("Outgoing").onClick.Invoke();
        ButtonNamed("Incoming").onClick.Invoke();
        transport.HoldActive = false;
        yield return WaitFor(() => transport.Count("incoming") == incomingBefore + 1 && !IsRefreshing);
        Assert.That(transport.Count("outgoing"), Is.EqualTo(outgoingBefore), "superseded queued tabs must not refetch");
    }

    [UnityTest]
    public IEnumerator SelectingInboxCancelsTheQueuedDetailedTabRefresh()
    {
        yield return Open();
        int incomingBefore = transport.Count("incoming");
        transport.HoldActive = true;
        ButtonNamed("Active").onClick.Invoke();
        yield return WaitFor(() => transport.ActiveWaiting);
        ButtonNamed("Incoming").onClick.Invoke();
        ButtonNamed("Inbox").onClick.Invoke();
        transport.HoldActive = false;
        yield return WaitFor(() => !IsRefreshing);
        Assert.That(transport.Count("incoming"), Is.EqualTo(incomingBefore));
        Assert.That(VisibleText(), Does.Contain("Correspondence inbox"));
    }

    [UnityTest]
    public IEnumerator RefreshInvalidatesAPendingCompletedDetailSelection()
    {
        yield return Open();
        ViewButton(transport.FirstSeriesId).onClick.Invoke();
        yield return WaitFor(() => transport.PendingDetails.Count == 1);
        int historyBefore = transport.Count("history");
        ButtonNamed("Refresh").onClick.Invoke();
        yield return WaitFor(() => transport.Count("history") == historyBefore + 1 && !IsRefreshing);
        transport.Complete(transport.FirstSeriesId);
        yield return WaitFor(() => transport.PendingDetails.Count == 0);
        for (int frame = 0; frame < 10; frame++) yield return null;
        Assert.That(VisibleText(), Does.Not.Contain("Selected completed series"), "refresh must not resurrect an invalidated selection");
    }

    private sealed class DelayedTransport : IApiTransport
    {
        public readonly List<ApiRequest> Requests = new List<ApiRequest>();
        public readonly Dictionary<Guid, Action<RawApiResponse>> PendingDetails = new Dictionary<Guid, Action<RawApiResponse>>();
        public Guid FirstSeriesId { get; } = Guid.NewGuid();
        public Guid SecondSeriesId { get; } = Guid.NewGuid();
        public bool HoldActive;
        public bool ActiveWaiting;
        public bool NewIncoming;
        private readonly Guid player;
        private readonly Guid opponent = Guid.NewGuid();

        public DelayedTransport(Guid player) { this.player = player; }

        public int Count(string source) => Requests.Count(request => request.RelativePath == "api/v2/series/" + source);

        public IEnumerator Send(ApiRequest request, Action<RawApiResponse> completed)
        {
            Requests.Add(request);
            const string prefix = "api/v2/series/";
            if (!request.RelativePath.StartsWith(prefix))
            {
                completed(RawApiResponse.Completed(200, "[]"));
                yield break;
            }
            string source = request.RelativePath.Substring(prefix.Length);
            if (Guid.TryParse(source, out Guid id))
            {
                bool settled = false;
                PendingDetails[id] = response =>
                {
                    completed(response);
                    PendingDetails.Remove(id);
                    settled = true;
                };
                while (!settled) yield return null;
                yield break;
            }
            if (source == "active" && HoldActive)
            {
                ActiveWaiting = true;
                while (HoldActive) yield return null;
            }
            var items = new List<SeriesSummaryDto>();
            if (source == "completed" || source == "history")
            {
                items.Add(Summary(FirstSeriesId, "Completed"));
                items.Add(Summary(SecondSeriesId, "Completed"));
            }
            else if (source == "incoming" && NewIncoming)
                items.Add(Summary(Guid.NewGuid(), "PendingAcceptance"));
            completed(RawApiResponse.Completed(200, BackendV2Json.Serialize(new SeriesSummaryPageDto { Items = items, Limit = 20 })));
        }

        private SeriesSummaryDto Summary(Guid id, string status) => new SeriesSummaryDto
        {
            Id = id, ChallengerId = opponent, OpponentId = player, Status = status,
            Revision = 4, TotalGames = 3, CurrentGameNumber = 1
        };

        public void Complete(Guid id)
        {
            PendingDetails[id](RawApiResponse.Completed(200, BackendV2Json.Serialize(new SeriesResponseDto
            {
                Id = id, ChallengerId = opponent, OpponentId = player, Status = "Completed", TotalGames = 3,
                Revision = 4, WinnerId = player, CompletedAt = DateTimeOffset.UtcNow,
                Rules = new FrozenRulesDto { RulesetId = "most-points", RulesetVersion = 1 }
            })));
        }
    }
}
