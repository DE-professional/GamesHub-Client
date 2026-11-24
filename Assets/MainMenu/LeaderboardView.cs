using System.Collections.Generic;
using Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Util;

/// <summary>
/// The "Leaderboard" view of the Scores screen: one game's global board, Today (the daily board for
/// today's UTC puzzle) or All time (each player's best day), paged from the server. A logged-in
/// player's own entry is pinned under the list with a Find me button that jumps the list to their
/// row, wherever they rank. Built in code by <see cref="Build"/>; <see cref="ScoreHistoryPanel"/> owns it.
/// </summary>
/// <remarks>
/// The list is a window onto the board: a run of consecutive entries starting at some offset, with
/// "Load earlier" above it when it doesn't start at the top and "Load more" below it while the board
/// goes on. Find me replaces the window with one around the player's rank, so a player ranked 4,000th
/// doesn't page through 80 pages to get there. Ranks, order and formula-version filtering all come
/// from the server and are never recomputed here.
/// </remarks>
public class LeaderboardView : MonoBehaviour
{
    // A loaded board: entries[0] is at offset `start`. Each player appears at most once per board,
    // so the tag identifies an entry; `tags` drops repeats when new scores shift entries between pages.
    class Board
    {
        public readonly List<LeaderboardEntry> entries = new();
        public readonly HashSet<string> tags = new();
        public int start;
        public int nextOffset;    // 0: no more pages (see LeaderboardPage.HasMorePages)
        public LeaderboardEntry me; // null: no entry (guest, or no score on this board)
        public bool loaded;
        public bool wrongVersion; // the server ranks a different formula version than this build submits

        public void Clear()
        {
            entries.Clear();
            tags.Clear();
            start = nextOffset = 0;
            me = null;
            loaded = wrongVersion = false;
        }

        public void Append(LeaderboardEntry[] page)
        {
            foreach (LeaderboardEntry e in page)
                if (tags.Add(e.Tag)) entries.Add(e);
        }

        public void Prepend(LeaderboardEntry[] page)
        {
            var earlier = new List<LeaderboardEntry>();
            foreach (LeaderboardEntry e in page)
                if (tags.Add(e.Tag)) earlier.Add(e);
            entries.InsertRange(0, earlier);
        }

        // The newest page's `me` wins: the player's rank moves as other scores arrive
        public void TakeMe(LeaderboardPage page) => me = page.HasMe ? page.me : null;
    }

    const int PageSize = LeaderboardService.DefaultPageSize;
    const float RowHeight = 50f;
    const float ListTop = 0.655f;    // same as My scores
    const float ListBottom = 0.285f; // leaves room for the pinned "You" bar
    const int FindContext = 5;       // rows shown above the player when jumping to them
    const int MaxFindPages = 10;     // a tie group can push the player past their rank; stop looking after this

    RetroTheme theme;
    GameId game;
    LeaderboardKind kind = LeaderboardKind.Daily;
    string today;
    readonly Board[] boards = { new Board(), new Board() };
    Board Current => boards[(int)kind];

    // A reset (new game, refresh, login/logout) makes running requests discard their results;
    // `busy` keeps one request at a time so pages can't interleave.
    int version;
    bool busy;

    Button todayButton, allTimeButton;
    ScrollRect scroll;
    RectTransform content;
    TextMeshProUGUI statusText;
    Image youBar;
    TextMeshProUGUI youText;
    Button findMeButton, loginButton;
    readonly Dictionary<string, RectTransform> rowsByTag = new();
    Button earlierButton, moreButton; // rows inside the list; null when not shown

    /// <summary>Builds the view, hidden.</summary>
    public static LeaderboardView Build(Transform parent, RetroTheme theme)
    {
        RectTransform root = RetroUI.Rect("LeaderboardView", parent);
        RetroUI.Stretch(root);
        root.gameObject.SetActive(false);

        LeaderboardView view = root.gameObject.AddComponent<LeaderboardView>();
        view.theme = theme;
        view.BuildLayout();
        return view;
    }

    /// <summary>
    /// Shows <paramref name="gameId"/>'s boards, freshly loaded: boards change with every submission,
    /// so nothing is kept from an earlier visit or another game.
    /// </summary>
    public void Show(GameId gameId)
    {
        game = gameId;
        gameObject.SetActive(true);
        ResetBoards();
        Open();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        version++; // drop anything still loading
        busy = false;
    }

    void OnEnable()
    {
        if (Services.Auth != null) Services.Auth.SessionChanged += OnSessionChanged;
    }

    void OnDisable()
    {
        if (Services.Auth != null) Services.Auth.SessionChanged -= OnSessionChanged;
    }

    // Login, logout, or a rejected token (ApiClient logged the player out): `me` changes on every
    // board, so load again.
    void OnSessionChanged(Session session)
    {
        ResetBoards();
        Open();
    }

    void ResetBoards()
    {
        version++;
        busy = false;
        today = DailyPuzzle.TodayUtc(); // the same UTC date the games submit with
        foreach (Board b in boards) b.Clear();
    }

    // Shows the current board, loading it on first open. Switching back to a loaded tab doesn't reload.
    void Open()
    {
        UpdateKindButtons();
        if (Current.loaded) Render();
        else LoadFrom(0);
    }

    // ---------- Layout ----------

    void BuildLayout()
    {
        // Controls above the list, where My scores has its summary and sort button
        todayButton = SmallButton("TodayButton", "Today", () => SelectKind(LeaderboardKind.Daily), -400, 190, theme.mustard);
        allTimeButton = SmallButton("AllTimeButton", "All time", () => SelectKind(LeaderboardKind.AllTime), -195, 190, theme.mustard);
        SmallButton("RefreshButton", "Refresh", Refresh, 400, 190, theme.orange);

        scroll = ScoreHistoryPanel.BuildScrollList("Board", transform, theme, ListBottom, ListTop);
        content = scroll.content;

        statusText = RetroUI.Text("Status", transform, theme, "", 30, theme.cream);
        statusText.rectTransform.anchorMin = new Vector2(0.5f, ListBottom);
        statusText.rectTransform.anchorMax = new Vector2(0.5f, ListTop);
        statusText.rectTransform.sizeDelta = new Vector2(1000, 0);
        statusText.textWrappingMode = TextWrappingModes.Normal;

        // Pinned under the list: the player's own entry (or why there isn't one), with Find me
        youBar = RetroUI.Pill("You", transform, theme, theme.panel, 16f);
        RetroUI.Place(youBar.rectTransform, new Vector2(0.5f, 0.24f), new Vector2(1000, 64));
        youBar.raycastTarget = false;

        youText = RetroUI.Text("Text", youBar.transform, theme, "", 28, theme.cream);
        RetroUI.Stretch(youText.rectTransform);
        youText.rectTransform.offsetMin = new Vector2(24, 0);
        youText.rectTransform.offsetMax = new Vector2(-250, 0);
        youText.alignment = TextAlignmentOptions.Left;
        youText.enableAutoSizing = true;
        youText.fontSizeMin = 16;
        youText.fontSizeMax = 28;

        findMeButton = BarButton("FindMeButton", "Find me", FindMe);
        loginButton = BarButton("LoginButton", "Log in", OpenLogin);
    }

    Button SmallButton(string name, string label, UnityEngine.Events.UnityAction onClick, float x, float width, Color face)
    {
        Button b = RetroUI.Button(name, transform, theme, label, onClick, 24, face);
        var rt = (RectTransform)b.transform;
        RetroUI.Place(rt, new Vector2(0.5f, 0.69f), new Vector2(width, 52));
        rt.anchoredPosition = new Vector2(x, 0);
        return b;
    }

    Button BarButton(string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        Button b = RetroUI.Button(name, youBar.transform, theme, label, onClick, 24, theme.coral);
        var rt = (RectTransform)b.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(200, 46);
        rt.anchoredPosition = new Vector2(-14, 3); // clear of the bar's edge, shadow included
        b.gameObject.SetActive(false);
        return b;
    }

    // ---------- Actions ----------

    void SelectKind(LeaderboardKind k)
    {
        if (k == kind) return;
        kind = k;
        version++; // the other tab's request, if any, is dropped; that tab loads on its next open
        busy = false;
        Open();
    }

    void Refresh()
    {
        ResetBoards();
        Open();
    }

    // Replaces the window with the board from `offset`. Used for the first page and for retries.
    async void LoadFrom(int offset)
    {
        if (busy) return;
        busy = true;
        int v = version;
        Board board = Current;
        LeaderboardKind k = kind;

        ShowStatus("LOADING");
        youBar.gameObject.SetActive(false); // RenderYou shows it again with this board's answer
        LeaderboardResult r = await Request(k, offset);
        if (this == null || v != version) return;
        busy = false;
        if (!r.Ok)
        {
            ShowError(r.Outcome);
            return;
        }

        board.Clear();
        board.start = offset;
        Accept(board, r.Page);
        Render();
    }

    async void LoadMore()
    {
        Board board = Current;
        if (busy || board.nextOffset == 0) return;
        busy = true;
        int v = version;
        SetListButtonLabel(moreButton, "LOADING");

        LeaderboardResult r = await Request(kind, board.nextOffset);
        if (this == null || v != version) return;
        busy = false;
        if (!r.Ok)
        {
            SetListButtonLabel(moreButton, FailedLabel(r.Outcome));
            return;
        }

        float scrollY = content.anchoredPosition.y;
        Accept(board, r.Page);
        Render();
        SetScroll(scrollY); // stay where the player was; the new rows are below
    }

    async void LoadEarlier()
    {
        Board board = Current;
        if (busy || board.start == 0) return;
        busy = true;
        int v = version;
        SetListButtonLabel(earlierButton, "LOADING");

        int from = Mathf.Max(0, board.start - PageSize);
        LeaderboardResult r = await Request(kind, from, board.start - from);
        if (this == null || v != version) return;
        busy = false;
        if (!r.Ok)
        {
            SetListButtonLabel(earlierButton, FailedLabel(r.Outcome));
            return;
        }

        // Keep the row that was first in view where it was, with the new rows above it
        string anchorTag = board.entries.Count > 0 ? board.entries[0].Tag : null;
        board.Prepend(r.Page.entries);
        board.start = from;
        board.TakeMe(r.Page);
        Render();
        if (anchorTag != null && rowsByTag.TryGetValue(anchorTag, out RectTransform row)) ScrollTo(row, top: true);
    }

    // Shows the player's own row: scrolls to it if it's loaded, otherwise loads a window starting just
    // above their rank. Within a tie the player can be listed below their rank, so it reads on until
    // it finds them.
    async void FindMe()
    {
        Board board = Current;
        if (busy || board.me == null) return;
        if (rowsByTag.TryGetValue(board.me.Tag, out RectTransform loadedRow))
        {
            ScrollTo(loadedRow, top: false);
            return;
        }

        busy = true;
        int v = version;
        LeaderboardKind k = kind;
        string myTag = board.me.Tag;
        int from = Mathf.Max(0, board.me.rank - 1 - FindContext);
        ShowStatus("FINDING YOU");

        var window = new Board { start = from };
        int offset = from;
        bool found = false;
        for (int page = 0; page < MaxFindPages; page++)
        {
            LeaderboardResult r = await Request(k, offset);
            if (this == null || v != version) return;
            if (!r.Ok)
            {
                busy = false;
                ShowError(r.Outcome);
                return;
            }

            Accept(window, r.Page);
            found = window.tags.Contains(myTag);
            if (found || window.me == null || window.nextOffset == 0) break;
            offset = window.nextOffset;
        }
        busy = false;

        board.Clear();
        board.start = window.start;
        board.entries.AddRange(window.entries);
        board.tags.UnionWith(window.tags);
        board.nextOffset = window.nextOffset;
        board.me = window.me;
        board.wrongVersion = window.wrongVersion;
        board.loaded = true;
        Render();

        if (board.me != null && rowsByTag.TryGetValue(board.me.Tag, out RectTransform row))
            ScrollTo(row, top: false);
        else if (board.me == null)
            ShowStatusOver("YOU'RE NOT ON THIS BOARD ANY MORE.");
        else
            Debug.LogWarning($"Leaderboard {game} {k}: {myTag} not found near rank {board.me.rank}.");
    }

    // The login dropdown stays on screen with the Scores panel; open it for the player
    void OpenLogin()
    {
        LoginDropdown login = FindAnyObjectByType<LoginDropdown>();
        if (login != null) login.SetOpen(true);
    }

    Awaitable<LeaderboardResult> Request(LeaderboardKind k, int offset, int limit = PageSize) =>
        Services.Leaderboards.GetPageAsync(game, k, today, offset, limit);

    // Appends a page to `board`, and takes its `me` and paging state
    void Accept(Board board, LeaderboardPage page)
    {
        board.Append(page.entries);
        board.nextOffset = page.HasMorePages ? page.nextOffset : 0;
        board.TakeMe(page);
        board.wrongVersion = page.formulaVersion != game.FormulaVersion;
        board.loaded = true;
    }

    // ---------- Rendering ----------

    void Render()
    {
        ClearRows();
        Board board = Current;
        RenderYou(board);

        if (board.wrongVersion)
        {
            ShowStatus("LEADERBOARD UNAVAILABLE FOR THIS VERSION OF THE GAME.");
            return;
        }
        if (board.entries.Count == 0 && board.start == 0)
        {
            ShowStatus(kind == LeaderboardKind.Daily ? "NO SCORES YET TODAY.\nBE THE FIRST!" : "NO SCORES YET.");
            return;
        }

        statusText.text = "";
        string myTag = board.me?.Tag;
        if (board.start > 0) earlierButton = AddListButton("LoadEarlier", "Load earlier", LoadEarlier);
        for (int i = 0; i < board.entries.Count; i++)
        {
            LeaderboardEntry e = board.entries[i];
            rowsByTag[e.Tag] = AddRow(e, i, e.Tag == myTag);
        }
        if (board.nextOffset > 0) moreButton = AddListButton("LoadMore", "Load more", LoadMore);
    }

    void RenderYou(Board board)
    {
        findMeButton.gameObject.SetActive(false);
        loginButton.gameObject.SetActive(false);
        youBar.gameObject.SetActive(true);

        if (board.me != null)
        {
            LeaderboardEntry me = board.me;
            string highlight = ColorUtility.ToHtmlStringRGB(theme.mustard);
            youText.text = $"<color=#{highlight}>YOU</color>   #{ScoreHistoryPanel.FormatScore(me.rank)}   " +
                           $"{me.Tag.ToUpperInvariant()}   {ScoreText(me.score)}";
            findMeButton.gameObject.SetActive(true);
        }
        else if (Services.Auth.IsLoggedIn)
        {
            youText.text = kind == LeaderboardKind.Daily ? "YOU HAVEN'T PLAYED TODAY" : "NO SCORE YET";
        }
        else
        {
            // Guests' scores stay on their device; only accounts are on the boards
            youText.text = "LOG IN TO SEE YOUR RANK";
            loginButton.gameObject.SetActive(FindAnyObjectByType<LoginDropdown>() != null);
        }
    }

    RectTransform AddRow(LeaderboardEntry e, int index, bool isMe)
    {
        Color background = isMe ? theme.mustard
            : index % 2 == 0 ? theme.panel : Color.Lerp(theme.panel, theme.burnt, 0.35f);
        Color ink = isMe ? theme.ink : theme.cream;

        Image row = RetroUI.Pill(isMe ? "MyRow" : "Row", content, theme, background, 14f);
        row.raycastTarget = false;
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = RowHeight;

        // Rank | tag | (all time: date of the best score) | score
        TextMeshProUGUI rank = RetroUI.Text("Rank", row.transform, theme, ScoreHistoryPanel.FormatScore(e.rank), 28,
            isMe ? theme.ink : theme.coral);
        Column(rank.rectTransform, left: true, 24, 110);
        rank.alignment = TextAlignmentOptions.Left;

        TextMeshProUGUI tag = RetroUI.Text("Player", row.transform, theme, e.Tag.ToUpperInvariant(), 28, ink);
        RetroUI.Stretch(tag.rectTransform);
        tag.rectTransform.offsetMin = new Vector2(150, 0);
        tag.rectTransform.offsetMax = new Vector2(kind == LeaderboardKind.AllTime ? -430 : -200, 0);
        tag.alignment = TextAlignmentOptions.Left;
        tag.enableAutoSizing = true; // long Name#1234 tags shrink instead of running into the score
        tag.fontSizeMin = 16;
        tag.fontSizeMax = 28;

        if (kind == LeaderboardKind.AllTime)
        {
            TextMeshProUGUI date = RetroUI.Text("Date", row.transform, theme, ScoreHistoryPanel.FormatDate(e.puzzleDate), 24,
                isMe ? theme.ink : Color.Lerp(theme.cream, theme.panel, 0.3f));
            Column(date.rectTransform, left: false, 200, 220);
            date.alignment = TextAlignmentOptions.Right;
        }

        bool loss = e.score == ScoreFormulas.Loss;
        TextMeshProUGUI score = RetroUI.Text("Score", row.transform, theme, ScoreText(e.score), 28,
            loss && !isMe ? theme.coral : ink);
        Column(score.rectTransform, left: false, 24, 160);
        score.alignment = TextAlignmentOptions.Right;

        return row.rectTransform;
    }

    // "Load earlier" / "Load more", as a row of the list. The button sits inside a plain row so its
    // shadow isn't clipped at the list's left edge.
    Button AddListButton(string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform holder = RetroUI.Rect(name, content);
        var layout = holder.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = RowHeight + 12;

        Button b = RetroUI.Button("Button", holder, theme, label, onClick, 24, theme.orange);
        RetroUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(340, 46));
        ((RectTransform)b.transform).anchoredPosition = new Vector2(3, 3);
        return b;
    }

    static void SetListButtonLabel(Button b, string label)
    {
        if (b == null) return;
        TextMeshProUGUI t = b.GetComponentInChildren<TextMeshProUGUI>();
        if (t != null) t.text = label;
    }

    static string FailedLabel(LeaderboardOutcome outcome) =>
        outcome == LeaderboardOutcome.NetworkError ? "OFFLINE. TRY AGAIN" : "FAILED. TRY AGAIN";

    void ShowError(LeaderboardOutcome outcome)
    {
        // NotLoggedIn: the player was just logged out, and OnSessionChanged is already reloading as a guest
        if (outcome == LeaderboardOutcome.NotLoggedIn) return;
        ClearRows();
        RenderYou(Current);
        ShowStatus(outcome == LeaderboardOutcome.NetworkError
            ? "CAN'T REACH THE SERVER.\nPRESS REFRESH TO TRY AGAIN."
            : "SOMETHING WENT WRONG.\nPRESS REFRESH TO TRY AGAIN.");
    }

    void ShowStatus(string text)
    {
        ClearRows();
        statusText.text = text;
    }

    // A message without clearing the list (for Find me results)
    void ShowStatusOver(string text) => youText.text = text;

    void ClearRows()
    {
        // Deactivate first: Destroy happens at the end of the frame, and the layout would
        // otherwise still count the old rows this frame.
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        rowsByTag.Clear();
        earlierButton = moreButton = null;
        content.anchoredPosition = Vector2.zero;
    }

    void UpdateKindButtons()
    {
        SetFace(todayButton, kind == LeaderboardKind.Daily ? theme.coral : theme.mustard);
        SetFace(allTimeButton, kind == LeaderboardKind.AllTime ? theme.coral : theme.mustard);
    }

    static void SetFace(Button button, Color face)
    {
        RetroButton retro = button.GetComponent<RetroButton>();
        retro.normalColor = face;
        retro.faceImage.color = face;
    }

    // ---------- Scrolling ----------

    // Scrolls so `row` is at the top of the view, or centred in it
    void ScrollTo(RectTransform row, bool top)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content); // rows get their positions now, not next frame
        float viewHeight = scroll.viewport.rect.height;
        float rowCentre = -row.anchoredPosition.y; // from the top of the content (layout children are top-anchored)
        float y = top ? rowCentre - row.rect.height * 0.5f - 6f : rowCentre - viewHeight * 0.5f;
        SetScroll(y);
    }

    void SetScroll(float y)
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float max = Mathf.Max(0f, content.rect.height - scroll.viewport.rect.height);
        scroll.StopMovement();
        content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, max));
    }

    static string ScoreText(int score) =>
        score == ScoreFormulas.Loss ? "LOSS" : ScoreHistoryPanel.FormatScore(score);

    static void Column(RectTransform rt, bool left, float inset, float width)
    {
        float edge = left ? 0f : 1f;
        rt.anchorMin = new Vector2(edge, 0f);
        rt.anchorMax = new Vector2(edge, 1f);
        rt.pivot = new Vector2(edge, 0.5f);
        rt.sizeDelta = new Vector2(width, 0f);
        rt.anchoredPosition = new Vector2(left ? inset : -inset, 0f);
    }
}
