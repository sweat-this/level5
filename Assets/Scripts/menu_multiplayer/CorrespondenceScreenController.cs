using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Utility;
using Level5.BackendV2;
using Level5.Core.Versus;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The #159 Multiplayer / Correspondence screen: Friends, Incoming Challenges, Outgoing/Waiting,
/// Your Turn, Active Series and Completed, each backed by the Backend V2 typed clients through the
/// plain-C# coordinators in <c>Level5.BackendV2</c> (<see cref="FriendsCoordinator"/>,
/// <see cref="SeriesListCoordinator"/>, <see cref="ChallengeCoordinator"/>,
/// <see cref="ActiveSeriesTurnClassifier"/>). This script owns only view state - which tab is
/// selected, loading/error flags (via those coordinators), which row has a command in flight - never
/// a decision Backend V2 owns.
///
/// Built entirely at runtime from code rather than authored as a scene/prefab: this repository's
/// menu screens are otherwise all hand-authored in the Editor (uGUI, scene-per-screen, see
/// <c>StartManager</c>), which is the convention a production pass should move this to once Editor
/// authoring access is available. Building it in code was the safe choice for this slice - it cannot
/// corrupt a shared prefab or hand-typed scene YAML - and keeps every visual decision in ordinary,
/// reviewable C#. Uses legacy <c>UnityEngine.UI.Text</c>/<c>InputField</c> rather than TextMeshPro
/// (the project's now-dominant text component) because runtime-constructing a correct
/// <c>TMP_InputField</c> hierarchy needs Editor-authored sub-objects this script does not have.
/// Visual polish is explicitly out of scope for this issue.
/// </summary>
public sealed class CorrespondenceScreenController : MonoBehaviour
{
    private enum Tab
    {
        Friends,
        Incoming,
        Outgoing,
        YourTurn,
        Active,
        Completed,
        History,
    }

    private const int DefaultLevelId = 1;

    private readonly FriendsCoordinator friends = new FriendsCoordinator();
    private readonly ChallengeCoordinator challenges = new ChallengeCoordinator();
    private readonly ChallengeFormState challengeForm = new ChallengeFormState();
    private readonly ActiveSeriesTurnClassifier turnClassifier = new ActiveSeriesTurnClassifier();

    private SeriesListCoordinator incoming;
    private SeriesListCoordinator outgoing;
    private SeriesListCoordinator active;
    private SeriesListCoordinator completed;
    private SeriesListCoordinator history;

    private Tab currentTab = Tab.Friends;
    private Guid? challengingFriendId;
    private Font uiFont;

    private RectTransform contentRoot;
    private Text statusBanner;
    private Text loginError;
    private InputField loginUsername;
    private InputField loginPassword;
    private GameObject loginPanel;
    private GameObject screenRoot;

    private void Awake()
    {
        BackendV2SessionPersistenceBootstrap.EnsureInitialized();
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        incoming = new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListIncoming);
        outgoing = new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListOutgoing);
        active = new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListActive);
        completed = new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListCompleted);
        history = new SeriesListCoordinator(BackendV2Runtime.Correspondence.ListHistory);

        BuildUi();
    }

    private void Start()
    {
        StartCoroutine(Resume());
    }

    /// <summary>Screen open/resume (#159 §10): confirm the session, refresh every list from Backend
    /// V2, and surface any pending remote result submission. Never trusts stale UI state.</summary>
    private IEnumerator Resume()
    {
        if (!BackendV2SessionStore.IsAuthenticated)
        {
            ShowLoginPanel(null);
            yield break;
        }

        HideLoginPanel();
        yield return RefreshAll();
    }

    private IEnumerator RefreshAll()
    {
        yield return friends.RefreshAll();
        yield return incoming.Refresh();
        yield return outgoing.Refresh();
        yield return active.Refresh();
        yield return completed.Refresh();
        yield return history.Refresh();
        turnClassifier.Reset();
        yield return turnClassifier.ClassifyAll(active.State.Items, RenderCurrentTab);
        RenderCurrentTab();
    }

    // ------------------------------------------------------------------ login

    private void ShowLoginPanel(string error)
    {
        screenRoot.SetActive(false);
        loginPanel.SetActive(true);
        loginError.text = error ?? string.Empty;
    }

    private void HideLoginPanel()
    {
        loginPanel.SetActive(false);
        screenRoot.SetActive(true);
    }

    private void OnLoginClicked()
    {
        StartCoroutine(DoLogin());
    }

    private IEnumerator DoLogin()
    {
        loginError.text = "signing in...";
        ApiResponse<BackendV2Session> result = null;
        yield return BackendV2Runtime.Session.Login(loginUsername.text, loginPassword.text, r => result = r);

        if (result != null && result.Success)
        {
            HideLoginPanel();
            yield return RefreshAll();
        }
        else
        {
            loginError.text = "sign-in failed: " + BackendV2ErrorMessages.Describe(result);
        }
    }

    // ------------------------------------------------------------------ tabs

    private void SelectTab(Tab tab)
    {
        currentTab = tab;
        StartCoroutine(RefreshTabThenRender(tab));
    }

    private IEnumerator RefreshTabThenRender(Tab tab)
    {
        switch (tab)
        {
            case Tab.Friends:
                yield return friends.RefreshAll();
                break;
            case Tab.Incoming:
                yield return incoming.Refresh();
                break;
            case Tab.Outgoing:
                yield return outgoing.Refresh();
                break;
            case Tab.YourTurn:
            case Tab.Active:
                yield return active.Refresh();
                turnClassifier.Reset();
                RenderCurrentTab();
                // Classification is a request per row and can take several seconds at real network
                // latency (see the doc comment on ClassifyAll) - render each row's turn as soon as
                // it is known instead of blocking this whole tab on the slowest one.
                yield return turnClassifier.ClassifyAll(active.State.Items, RenderCurrentTab);
                break;
            case Tab.Completed:
                yield return completed.Refresh();
                break;
            case Tab.History:
                yield return history.Refresh();
                break;
        }

        RenderCurrentTab();
    }

    private void RenderCurrentTab()
    {
        // Detach before destroying: Destroy() is deferred to end-of-frame, so an old child left
        // parented under contentRoot while this method's new rows are added underneath it too would
        // have both sets counted by the VerticalLayoutGroup/ContentSizeFitter for this frame's
        // layout pass - a one-frame doubled-height flash on every tab switch/refresh. SetParent
        // takes effect immediately, so detaching first removes them from layout consideration right
        // away while still destroying them safely.
        while (contentRoot.childCount > 0)
        {
            Transform child = contentRoot.GetChild(0);
            child.SetParent(null);
            Destroy(child.gameObject);
        }

        RenderPendingResultBanner();

        switch (currentTab)
        {
            case Tab.Friends:
                RenderFriendsTab();
                break;
            case Tab.Incoming:
                RenderChallengeList(incoming, "Incoming Challenges", isIncoming: true);
                break;
            case Tab.Outgoing:
                RenderChallengeList(outgoing, "Outgoing / Waiting", isIncoming: false);
                break;
            case Tab.YourTurn:
                RenderYourTurnTab();
                break;
            case Tab.Active:
                RenderActiveTab();
                break;
            case Tab.Completed:
                RenderCompletedTab();
                break;
            case Tab.History:
                RenderHistoryTab();
                break;
        }
    }

    private void RenderPendingResultBanner()
    {
        if (!PendingRemoteAttemptResult.HasPending)
        {
            return;
        }

        GameObject row = CreateRow(
            "A remote attempt result could not be submitted and is still pending.",
            ("Resend result", () =>
            {
                bool started = RemoteAttemptResultSubmitter.TryRetryPending();
                SetStatus(started ? "resending pending result..." : "a resend is already in progress");
            }));
        row.transform.SetAsFirstSibling();
    }

    // ------------------------------------------------------------------ friends tab

    private void RenderFriendsTab()
    {
        CreateHeader("Add a friend");
        GameObject form = CreateRow(string.Empty);
        InputField tagField = CreateInputField(form.transform, "friend tag, e.g. ADA#1234");
        CreateButton(form.transform, "Send Request", () =>
        {
            string tag = tagField.text;
            StartCoroutine(SendFriendRequest(tag));
        });

        if (challengingFriendId.HasValue)
        {
            RenderChallengeForm();
        }

        CreateHeader($"Incoming Friend Requests ({friends.Incoming.Items.Count})");
        RenderListStatus(friends.Incoming);
        foreach (FriendRequestResponseDto request in friends.Incoming.Items)
        {
            Guid requestId = request.Id;
            CreateRow(
                $"from {request.FromPlayerId}",
                ("Accept", () => StartCoroutine(RunFriendCommand(friends.Accept(requestId, _ => { })))),
                ("Decline", () => StartCoroutine(RunFriendCommand(friends.Decline(requestId, _ => { })))));
        }

        CreateHeader($"Outgoing Friend Requests ({friends.Outgoing.Items.Count})");
        RenderListStatus(friends.Outgoing);
        foreach (FriendRequestResponseDto request in friends.Outgoing.Items)
        {
            Guid requestId = request.Id;
            CreateRow(
                $"to {request.ToPlayerId}",
                ("Cancel", () => StartCoroutine(RunFriendCommand(friends.Cancel(requestId, _ => { })))));
        }

        CreateHeader($"Friends ({friends.Friends.Items.Count})");
        RenderListStatus(friends.Friends);
        foreach (FriendSummaryDto friend in friends.Friends.Items)
        {
            Guid playerId = friend.PlayerId;
            CreateRow(
                $"{friend.DisplayName} ({friend.Tag})",
                ("Challenge", () =>
                {
                    challengingFriendId = playerId;
                    challengeForm.OpponentId = playerId;
                    RenderCurrentTab();
                }),
                ("Remove", () => StartCoroutine(RunFriendCommand(friends.Remove(playerId, _ => { })))));
        }
    }

    private void RenderChallengeForm()
    {
        CreateHeader("New challenge");
        FriendSummaryDto opponent = friends.Friends.Items.FirstOrDefault(f => f.PlayerId == challengingFriendId);
        string opponentLabel = opponent != null ? $"{opponent.DisplayName} ({opponent.Tag})" : challengingFriendId.ToString();
        GameObject row = CreateRow($"opponent: {opponentLabel}, best of {challengeForm.TotalGames}");
        CreateButton(row.transform, "Bo1", () => { challengeForm.TotalGames = 1; RenderCurrentTab(); });
        CreateButton(row.transform, "Bo3", () => { challengeForm.TotalGames = 3; RenderCurrentTab(); });
        CreateButton(row.transform, "Bo5", () => { challengeForm.TotalGames = 5; RenderCurrentTab(); });
        CreateButton(row.transform, "Bo7", () => { challengeForm.TotalGames = 7; RenderCurrentTab(); });

        CompetitiveRuleset ruleset = VersusCatalogs.Rulesets.Supporting(VersusCapability.Asynchronous).FirstOrDefault();
        challengeForm.RulesetId = ruleset?.Id.Value;

        CreateRow(
            ruleset != null ? $"ruleset: {ruleset.DisplayName}" : "no asynchronous ruleset is available on this build",
            ("Send Challenge", () => StartCoroutine(SendChallenge())),
            ("Cancel", () =>
            {
                challengingFriendId = null;
                RenderCurrentTab();
            }));
    }

    private IEnumerator SendChallenge()
    {
        ApiResponse<SeriesResponseDto> result = null;
        yield return challenges.Create(challengeForm, r => result = r);

        if (result != null && result.Success)
        {
            challengeForm.CompleteSubmission();
            challengingFriendId = null;
            SetStatus("challenge sent");
            yield return outgoing.Refresh();
            RenderCurrentTab();
            yield break;
        }

        // A network/server failure is retryable - leave the form's clientRequestId in place so a
        // second "Send Challenge" tap reuses it. Validation is definitive.
        if (result != null && result.ErrorKind == ApiErrorKind.Validation)
        {
            challengeForm.CompleteSubmission();
        }

        SetStatus("could not send the challenge: " + BackendV2ErrorMessages.Describe(result));
    }

    private IEnumerator SendFriendRequest(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            SetStatus("enter a player tag first");
            yield break;
        }

        string error = null;
        yield return friends.SendRequestByTag(tag, e => error = e);
        SetStatus(error == null ? "friend request sent" : "could not send the friend request: " + error);
        RenderCurrentTab();
    }

    private IEnumerator RunFriendCommand(IEnumerator command)
    {
        yield return command;
        RenderCurrentTab();
    }

    // ------------------------------------------------------------------ challenge tabs

    private void RenderChallengeList(SeriesListCoordinator coordinator, string title, bool isIncoming)
    {
        CreateHeader($"{title} ({coordinator.State.Items.Count})");
        RenderListStatus(coordinator.State);
        foreach (SeriesSummaryDto series in coordinator.State.Items)
        {
            Guid seriesId = series.Id;
            List<(string, Action)> actions = new List<(string, Action)>();
            if (isIncoming)
            {
                actions.Add(("Accept", () => StartCoroutine(RunChallengeCommand(challenges.Accept(seriesId, incoming, _ => { })))));
                actions.Add(("Decline", () => StartCoroutine(RunChallengeCommand(challenges.Decline(seriesId, incoming, _ => { })))));
            }
            else
            {
                actions.Add(("Cancel", () => StartCoroutine(RunChallengeCommand(challenges.Cancel(seriesId, outgoing, _ => { })))));
            }

            CreateRow($"{DescribeSeries(series)}", actions.ToArray());
        }

        RenderLoadMore(coordinator);
    }

    private IEnumerator RunChallengeCommand(IEnumerator command)
    {
        // The command itself refreshes its owner list on success (ChallengeCoordinator.RunCommand) -
        // this only needs to wait for that and re-render.
        yield return command;
        RenderCurrentTab();
    }

    private void RenderYourTurnTab()
    {
        List<SeriesSummaryDto> yourTurnItems = turnClassifier.YourTurnItems(active.State.Items).ToList();
        CreateHeader($"Your Turn ({yourTurnItems.Count})");
        if (turnClassifier.IsLoading)
        {
            CreateRow("checking whose turn it is...");
        }
        else if (turnClassifier.ErrorMessage != null)
        {
            CreateRow(turnClassifier.ErrorMessage);
        }

        RenderListStatus(active.State);
        foreach (SeriesSummaryDto series in yourTurnItems)
        {
            Guid seriesId = series.Id;
            int gameNumber = series.CurrentGameNumber;
            CreateRow(
                DescribeSeries(series),
                ("Play", () => StartCoroutine(PlayTurn(seriesId, gameNumber))));
        }
    }

    private void RenderActiveTab()
    {
        CreateHeader($"Active Series ({active.State.Items.Count})");
        RenderListStatus(active.State);
        foreach (SeriesSummaryDto series in active.State.Items)
        {
            ActiveSeriesTurn? turn = turnClassifier.TurnFor(series.Id);
            string badge = turn == ActiveSeriesTurn.YourTurn ? "your turn"
                : turn == ActiveSeriesTurn.OpponentTurn ? "opponent's turn"
                : "checking...";
            Guid seriesId = series.Id;
            int gameNumber = series.CurrentGameNumber;

            if (turn == ActiveSeriesTurn.YourTurn)
            {
                CreateRow($"{DescribeSeries(series)} - {badge}", ("Play", () => StartCoroutine(PlayTurn(seriesId, gameNumber))));
            }
            else
            {
                CreateRow($"{DescribeSeries(series)} - {badge}");
            }
        }

        RenderLoadMore(active);
    }

    private void RenderCompletedTab()
    {
        CreateHeader($"Completed ({completed.State.Items.Count})");
        RenderListStatus(completed.State);
        foreach (SeriesSummaryDto series in completed.State.Items)
        {
            CreateRow(DescribeSeries(series));
        }

        RenderLoadMore(completed);
    }

    /// <summary>Every terminal series (Completed, Declined, Cancelled, Expired) - the durable
    /// history view, distinct from <see cref="RenderCompletedTab"/>'s "actually finished play"
    /// scope. Shows the status explicitly since this tab mixes all four.</summary>
    private void RenderHistoryTab()
    {
        CreateHeader($"History ({history.State.Items.Count})");
        RenderListStatus(history.State);
        foreach (SeriesSummaryDto series in history.State.Items)
        {
            CreateRow($"{DescribeSeries(series)} - {series.Status}");
        }

        RenderLoadMore(history);
    }

    private void RenderLoadMore(SeriesListCoordinator coordinator)
    {
        if (!coordinator.State.HasMore)
        {
            return;
        }

        CreateRow(
            string.Empty,
            ("Load more", () => StartCoroutine(RunLoadMore(coordinator))));
    }

    private IEnumerator RunLoadMore(SeriesListCoordinator coordinator)
    {
        yield return coordinator.LoadMore();
        RenderCurrentTab();
    }

    /// <summary>#159 §7 / #179: local character resolution -&gt; StartAttempt -&gt;
    /// RemoteAttemptDescriptorMapper -&gt; ordinary gameplay launch. The character comes from the
    /// existing player-select authority (<see cref="RemoteCharacterSelectionResolver"/>) rather than
    /// <c>CharacterSelection.None</c> - a resolution/lock failure is shown inline and never reaches
    /// <c>StartAttempt</c>. No level picker exists yet for remote attempts (none exists for local
    /// versus play either - VersusLauncher.Launch has no production caller today); this still uses a
    /// fixed default level, a known MVP limitation, not a silent gap.</summary>
    private IEnumerator PlayTurn(Guid seriesId, int gameNumber)
    {
        RemoteCharacterSelectionResult characterResult = RemoteCharacterSelectionResolver.ResolveCurrentPrimary();
        if (!characterResult.Succeeded)
        {
            SetStatus("could not resolve a player character: " + characterResult.Error);
            yield break;
        }

        SetStatus("starting attempt...");
        RemoteAttemptLaunch result = default;

        // Runs on this screen's own coroutine rather than via RemoteAttemptLauncher.Launch (which
        // hands off to BackendV2CoroutineHost for callers with no owning MonoBehaviour, e.g. the
        // match-end result submitter): this screen is exactly such an owner, and driving Run()
        // directly means a success's scene load and this coroutine settle in the same step, with no
        // separate cross-coroutine polling needed.
        yield return RemoteAttemptLauncher.Run(
            seriesId, gameNumber, DefaultLevelId, characterResult.Character, null,
            launch => result = launch);

        if (!result.Succeeded)
        {
            SetStatus("could not start the attempt: " + result.Error);
        }
    }

    // ------------------------------------------------------------------ small UI helpers

    private void SetStatus(string message)
    {
        statusBanner.text = message ?? string.Empty;
    }

    private void RenderListStatus<T>(ListViewState<T> state)
    {
        if (state.IsLoading)
        {
            CreateRow("loading...");
        }
        else if (state.ErrorMessage != null)
        {
            CreateRow("error: " + state.ErrorMessage);
        }
        else if (state.IsEmpty)
        {
            CreateRow("nothing here yet");
        }
    }

    private static string DescribeSeries(SeriesSummaryDto series)
    {
        return $"series {series.Id.ToString().Substring(0, 8)} - game {series.CurrentGameNumber}/{series.TotalGames}";
    }

    private void CreateHeader(string text)
    {
        GameObject go = new GameObject("Header", typeof(RectTransform));
        go.transform.SetParent(contentRoot, false);
        Text label = go.AddComponent<Text>();
        label.font = uiFont;
        label.text = text;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = 28;
    }

    private GameObject CreateRow(string text, params (string label, Action onClick)[] buttons)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(contentRoot, false);
        HorizontalLayoutGroup layoutGroup = row.AddComponent<HorizontalLayoutGroup>();
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.spacing = 8;
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 26;

        if (!string.IsNullOrEmpty(text))
        {
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(row.transform, false);
            Text label = labelGo.AddComponent<Text>();
            label.font = uiFont;
            label.text = text;
            label.color = Color.white;
            LayoutElement labelLayout = labelGo.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 420;
            labelLayout.flexibleWidth = 1;
        }

        foreach ((string label, Action onClick) button in buttons)
        {
            CreateButton(row.transform, button.label, button.onClick);
        }

        return row;
    }

    private Button CreateButton(Transform parent, string label, Action onClick)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.25f, 0.25f, 0.25f);
        Button button = go.AddComponent<Button>();
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 110;
        layout.preferredHeight = 24;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = uiFont;
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        RowCommandGuard guard = new RowCommandGuard(button);
        button.onClick.AddListener(() => guard.Run(onClick));
        return button;
    }

    private InputField CreateInputField(Transform parent, string placeholder)
    {
        GameObject go = new GameObject("Input", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f);
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredWidth = 220;
        layout.preferredHeight = 24;

        GameObject textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        Text text = textGo.AddComponent<Text>();
        text.font = uiFont;
        text.color = Color.white;
        text.supportRichText = false;
        StretchToParent(textGo.GetComponent<RectTransform>());

        GameObject placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
        placeholderGo.transform.SetParent(go.transform, false);
        Text placeholderText = placeholderGo.AddComponent<Text>();
        placeholderText.font = uiFont;
        placeholderText.text = placeholder;
        placeholderText.color = new Color(1f, 1f, 1f, 0.4f);
        placeholderText.fontStyle = FontStyle.Italic;
        StretchToParent(placeholderGo.GetComponent<RectTransform>());

        InputField field = go.AddComponent<InputField>();
        field.textComponent = text;
        field.placeholder = placeholderText;
        return field;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6, 2);
        rect.offsetMax = new Vector2(-6, -2);
    }

    /// <summary>
    /// A synchronous re-entrancy guard: refuses to invoke <paramref name="onClick"/> a second time
    /// while a prior invocation is still on the call stack (guards against an event system firing a
    /// listener twice for what looks like one click).
    ///
    /// This does NOT, by itself, cover issue #159 §11's "action buttons disabled or guarded while a
    /// row command is in flight" for the network commands most of these buttons start (they return
    /// immediately after calling <c>StartCoroutine</c>, so this guard's own window closes long before
    /// the request settles). That guarantee - no second concurrent command for the same row while its
    /// network call is outstanding - is enforced where it actually matters, at
    /// <see cref="Level5.BackendV2.RowCommandState"/> inside <see cref="Level5.BackendV2.FriendsCoordinator"/>/
    /// <see cref="Level5.BackendV2.ChallengeCoordinator"/> (covered by
    /// Level5BackendV2FriendsCoordinatorTests/Level5BackendV2ChallengeCoordinatorTests), which refuses
    /// the command outright rather than merely disabling a button that a player could still tap.
    /// Visually greying out a button for the true duration of its async command is not implemented in
    /// this slice - a reasonable follow-up, not a correctness gap.
    /// </summary>
    private sealed class RowCommandGuard
    {
        private readonly Button button;
        private bool running;

        public RowCommandGuard(Button button)
        {
            this.button = button;
        }

        public void Run(Action action)
        {
            if (running)
            {
                return;
            }

            running = true;
            try
            {
                action?.Invoke();
            }
            finally
            {
                running = false;
            }
        }
    }

    // ------------------------------------------------------------------ scene construction

    private void BuildUi()
    {
        Canvas canvas = FindOrCreateCanvas();
        EnsureEventSystem();

        GameObject root = new GameObject("CorrespondenceRoot", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        screenRoot = root;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        VerticalLayoutGroup rootLayout = root.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 6;
        rootLayout.padding = new RectOffset(12, 12, 12, 12);

        GameObject topBar = new GameObject("TopBar", typeof(RectTransform));
        topBar.transform.SetParent(root.transform, false);
        HorizontalLayoutGroup topBarLayout = topBar.AddComponent<HorizontalLayoutGroup>();
        topBarLayout.spacing = 6;
        LayoutElement topBarSize = topBar.AddComponent<LayoutElement>();
        topBarSize.preferredHeight = 30;

        CreateButton(topBar.transform, "Back", () => SceneTransition.LoadScene(Constants.SCENE_NAME_level_00_start));
        CreateButton(topBar.transform, "Friends", () => SelectTab(Tab.Friends));
        CreateButton(topBar.transform, "Incoming", () => SelectTab(Tab.Incoming));
        CreateButton(topBar.transform, "Outgoing", () => SelectTab(Tab.Outgoing));
        CreateButton(topBar.transform, "Your Turn", () => SelectTab(Tab.YourTurn));
        CreateButton(topBar.transform, "Active", () => SelectTab(Tab.Active));
        CreateButton(topBar.transform, "Completed", () => SelectTab(Tab.Completed));
        CreateButton(topBar.transform, "History", () => SelectTab(Tab.History));

        GameObject statusGo = new GameObject("Status", typeof(RectTransform));
        statusGo.transform.SetParent(root.transform, false);
        statusBanner = statusGo.AddComponent<Text>();
        statusBanner.font = uiFont;
        statusBanner.color = Color.yellow;
        LayoutElement statusLayout = statusGo.AddComponent<LayoutElement>();
        statusLayout.preferredHeight = 20;

        GameObject scrollGo = new GameObject("ContentScroll", typeof(RectTransform));
        scrollGo.transform.SetParent(root.transform, false);
        LayoutElement scrollLayout = scrollGo.AddComponent<LayoutElement>();
        scrollLayout.flexibleHeight = 1;
        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollGo.AddComponent<RectMask2D>();

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        StretchToParent(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        contentRoot = content.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0, 1);
        contentRoot.anchorMax = new Vector2(1, 1);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 4;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRoot;
        scrollRect.horizontal = false;

        // A sibling of screenRoot, not a child: ShowLoginPanel disables screenRoot entirely, which
        // would take a child login panel down with it regardless of its own SetActive(true).
        BuildLoginPanel(canvas.transform);
    }

    private void BuildLoginPanel(Transform parent)
    {
        GameObject panel = new GameObject("LoginPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        StretchToParent(panel.GetComponent<RectTransform>());
        panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.6f);
        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.childForceExpandWidth = false;
        layout.childAlignment = TextAnchor.UpperLeft;
        loginPanel = panel;

        GameObject title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(panel.transform, false);
        Text titleText = title.AddComponent<Text>();
        titleText.font = uiFont;
        titleText.text = "Sign in to Backend V2 to use Multiplayer / Correspondence";
        titleText.color = Color.white;

        loginUsername = CreateInputField(panel.transform, "username");
        loginPassword = CreateInputField(panel.transform, "password");
        loginPassword.contentType = InputField.ContentType.Password;

        CreateButton(panel.transform, "Sign In", OnLoginClicked);

        GameObject errorGo = new GameObject("Error", typeof(RectTransform));
        errorGo.transform.SetParent(panel.transform, false);
        loginError = errorGo.AddComponent<Text>();
        loginError.font = uiFont;
        loginError.color = Color.yellow;

        loginPanel.SetActive(false);
    }

    /// <summary>Reuses a Canvas already in the scene if one exists (left wherever it already is -
    /// not this screen's to move); otherwise creates one as a child of this controller's own
    /// GameObject, so the whole screen is one self-contained hierarchy rather than scattering loose
    /// root-level objects into the scene.</summary>
    private Canvas FindOrCreateCanvas()
    {
        Canvas existing = FindFirstObjectByType<Canvas>();
        if (existing != null)
        {
            return existing;
        }

        GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Matches every other menu scene's Canvas (e.g. level_00_start): ScaleWithScreenSize against
        // a 1920x1080 reference, balanced width/height match. Left at CanvasScaler's own default
        // (ConstantPixelSize) this screen would render at the wrong physical size on any resolution
        // other than the reference one, unlike every other screen in the project.
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }
}
