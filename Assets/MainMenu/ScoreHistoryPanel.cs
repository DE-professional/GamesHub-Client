using System;
using System.Collections.Generic;
using System.Globalization;
using Menu;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Util;

/// <summary>
/// The Scores screen: one tab per game, and the player's history for the selected game,
/// sortable by date or score. Logged-in players see their server history; guests see the
/// scores saved on this device. Built in code by <see cref="Build"/>, like the rest of the menu.
/// </summary>
public class ScoreHistoryPanel : MonoBehaviour
{
    enum SortMode { Newest, Oldest, Highest, Lowest }

    const float RowHeight = 50f;
    const float ListTop = 0.655f;    // just under the summary row
    const float ListBottom = 0.215f; // just above the Back button

    RetroTheme theme;
    GameId[] games;
    UnityEngine.UI.Button[] tabs;
    RectTransform content;
    TextMeshProUGUI summaryText;
    TextMeshProUGUI statusText;
    TextMeshProUGUI sortLabel;

    int selected;
    SortMode sort = SortMode.Newest;
    List<ScoreRecord> scores = new List<ScoreRecord>();
    int loadVersion; // a newer load makes older, still-running ones discard their results
    bool loaded;     // scores holds a finished load (false while loading or after an error)

    /// <summary>Builds the panel, hidden. Call <see cref="Show"/> to open it.</summary>
    /// <param name="labels">Tab label for each game, in the same order as <paramref name="games"/>.</param>
    /// <param name="onBack">Called by the Back button.</param>
    public static ScoreHistoryPanel Build(Transform parent, RetroTheme theme,
        IReadOnlyList<string> labels, IReadOnlyList<GameId> games, UnityAction onBack)
    {
        RectTransform root = RetroUI.Rect("ScoresPanel", parent);
        RetroUI.Stretch(root);
        root.gameObject.SetActive(false);

        ScoreHistoryPanel panel = root.gameObject.AddComponent<ScoreHistoryPanel>();
        panel.theme = theme;
        panel.games = new GameId[games.Count];
        for (int i = 0; i < games.Count; i++) panel.games[i] = games[i];
        panel.BuildLayout(labels, onBack);
        return panel;
    }

    /// <summary>Opens the panel and loads the selected game's history.</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        Load();
    }

    public void Hide() => gameObject.SetActive(false);

    void OnEnable()
    {
        if (Services.Auth != null) Services.Auth.SessionChanged += OnSessionChanged;
    }

    void OnDisable()
    {
        if (Services.Auth != null) Services.Auth.SessionChanged -= OnSessionChanged;
    }

    // Login or logout while the panel is open: the history now comes from somewhere else.
    void OnSessionChanged(Session session) => Load();

    // ---------- Layout ----------

    void BuildLayout(IReadOnlyList<string> labels, UnityAction onBack)
    {
        // The menu hides the logo and the "Logged in as" line on this screen, so the header takes
        // the logo's place and the list gets the space below it.
        RetroUI.LayeredText("Header", transform, theme, "Scores", 96,
            new Vector2(0.5f, 0.86f), new Vector2(900, 130));

        // Game tabs
        RectTransform tabRow = RetroUI.Rect("Tabs", transform);
        RetroUI.Place(tabRow, new Vector2(0.5f, 0.76f), new Vector2(1100, 64));
        var h = tabRow.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        h.spacing = 16;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;

        tabs = new UnityEngine.UI.Button[games.Length];
        for (int i = 0; i < games.Length; i++)
        {
            int index = i; // capture for closure
            tabs[i] = RetroUI.Button($"Tab{i + 1}", tabRow, theme, labels[i], () => SelectGame(index), 24);
            TextMeshProUGUI tabLabel = tabs[i].GetComponentInChildren<TextMeshProUGUI>();
            tabLabel.enableAutoSizing = true; // "PRICE IS NICE" is wider than a fifth of the row
            tabLabel.fontSizeMin = 14;
            tabLabel.fontSizeMax = 24;
        }

        // Summary on the left, sort button on the right, both above the list
        summaryText = RetroUI.Text("Summary", transform, theme, "", 26, theme.cream);
        RetroUI.Place(summaryText.rectTransform, new Vector2(0.5f, 0.69f), new Vector2(700, 52));
        summaryText.rectTransform.anchoredPosition = new Vector2(-150, 0);
        summaryText.alignment = TextAlignmentOptions.Left;
        summaryText.enableAutoSizing = true; // a long Name#1234 tag shrinks the line instead of overflowing
        summaryText.fontSizeMin = 16;
        summaryText.fontSizeMax = 26;

        UnityEngine.UI.Button sortButton = RetroUI.Button("SortButton", transform, theme, "", CycleSort, 24, theme.orange);
        RetroUI.Place((RectTransform)sortButton.transform, new Vector2(0.5f, 0.69f), new Vector2(300, 52));
        ((RectTransform)sortButton.transform).anchoredPosition = new Vector2(350, 0);
        sortLabel = sortButton.GetComponentInChildren<TextMeshProUGUI>();

        BuildList();

        statusText = RetroUI.Text("Status", transform, theme, "", 30, theme.cream);
        statusText.rectTransform.anchorMin = new Vector2(0.5f, ListBottom);
        statusText.rectTransform.anchorMax = new Vector2(0.5f, ListTop);
        statusText.rectTransform.sizeDelta = new Vector2(1000, 0);
        statusText.textWrappingMode = TextWrappingModes.Normal;

        UnityEngine.UI.Button back = RetroUI.Button("BackButton", transform, theme, "Back", onBack, 32, theme.cream);
        RetroUI.Place((RectTransform)back.transform, new Vector2(0.5f, 0.145f), new Vector2(260, 72));

        UpdateTabs();
        UpdateSortLabel();
    }

    // ScrollView: ScrollRect → Viewport (clips, catches drags) → Content (sized by its rows)
    void BuildList()
    {
        RectTransform scroll = RetroUI.Rect("ScoreList", transform);
        scroll.anchorMin = new Vector2(0.5f, ListBottom);
        scroll.anchorMax = new Vector2(0.5f, ListTop);
        scroll.sizeDelta = new Vector2(1000, 0);

        RectTransform viewport = RetroUI.Rect("Viewport", scroll);
        RetroUI.Stretch(viewport);
        viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
        // Invisible, but a raycast target, so dragging anywhere in the list scrolls it
        var hit = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
        hit.color = Color.clear;

        content = RetroUI.Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var v = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        v.spacing = 6;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        var sr = scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        sr.viewport = viewport;
        sr.content = content;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        // Shown only when the rows overflow; the viewport then shrinks to make room for it.
        sr.verticalScrollbar = BuildScrollbar(scroll);
        sr.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        sr.verticalScrollbarSpacing = 12f;
    }

    // Scrollbar (track) → SlidingArea → Handle, pinned to the list's right edge
    UnityEngine.UI.Scrollbar BuildScrollbar(RectTransform scroll)
    {
        const float width = 16f;

        UnityEngine.UI.Image track = RetroUI.Pill("Scrollbar", scroll, theme, theme.panel, width * 0.5f);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(1f, 0f);
        trackRect.anchorMax = new Vector2(1f, 1f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.sizeDelta = new Vector2(width, 0f);
        trackRect.anchoredPosition = Vector2.zero;

        RectTransform slidingArea = RetroUI.Rect("SlidingArea", trackRect);
        RetroUI.Stretch(slidingArea);

        UnityEngine.UI.Image handle = RetroUI.Pill("Handle", slidingArea, theme, theme.mustard, width * 0.5f);
        RetroUI.Stretch(handle.rectTransform); // the Scrollbar sets the handle's anchors from here on

        var bar = track.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
        bar.handleRect = handle.rectTransform;
        bar.targetGraphic = handle;
        bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
        return bar;
    }

    // ---------- Actions ----------

    void SelectGame(int index)
    {
        if (index == selected) return;
        selected = index;
        UpdateTabs();
        Load();
    }

    void CycleSort()
    {
        sort = (SortMode)(((int)sort + 1) % 4);
        UpdateSortLabel();
        if (loaded) Render(); // re-sort what's loaded: no request needed
    }

    async void Load()
    {
        int version = ++loadVersion;
        loaded = false;
        scores.Clear();
        ClearRows();
        summaryText.text = "";
        statusText.text = "LOADING";
        if (games.Length == 0)
        {
            statusText.text = "NO GAMES";
            return;
        }

        HistoryResult result = await Services.Scores.GetHistoryAsync(games[selected]);
        if (this == null || version != loadVersion) return;

        if (!result.Ok)
        {
            statusText.text = result.Outcome switch
            {
                HistoryOutcome.NetworkError => "CAN'T REACH THE SERVER.\nTRY AGAIN LATER.",
                // A saved login that couldn't be restored, usually because the server is down
                HistoryOutcome.NotLoggedIn  => "CAN'T REACH THE SERVER.\nYOUR SCORES WILL SHOW ONCE YOU'RE BACK ONLINE.",
                _                           => "SOMETHING WENT WRONG.\nTRY AGAIN LATER."
            };
            return;
        }

        scores = new List<ScoreRecord>(result.Scores);
        loaded = true;
        Render();
    }

    // ---------- Rendering ----------

    void Render()
    {
        ClearRows();
        bool guest = !Services.Auth.IsLoggedIn;
        // Stands in for the menu's "Logged in as" line, which is hidden on this screen
        string who = guest ? "GUEST" : Services.Auth.Current.user.Tag.ToUpperInvariant();

        if (scores.Count == 0)
        {
            summaryText.text = guest ? "GUEST   SCORES STAY ON THIS DEVICE" : who;
            statusText.text = "NO SCORES YET.\nPLAY TODAY'S PUZZLE!";
            return;
        }

        statusText.text = "";
        int best = 0;
        foreach (ScoreRecord s in scores) best = Math.Max(best, s.score);
        summaryText.text = $"{who}   {scores.Count} PLAYED   BEST {FormatScore(best)}";

        var sorted = new List<ScoreRecord>(scores);
        sorted.Sort(Compare);

        string today = DailyPuzzle.TodayUtc();
        for (int i = 0; i < sorted.Count; i++) AddRow(sorted[i], i, sorted[i].puzzleDate == today);
    }

    // Score sorts break ties by date, newest first. One score per day, so dates never tie.
    int Compare(ScoreRecord a, ScoreRecord b)
    {
        int byDate = string.CompareOrdinal(b.puzzleDate, a.puzzleDate); // newest first
        switch (sort)
        {
            case SortMode.Oldest:  return -byDate;
            case SortMode.Highest: return b.score != a.score ? b.score.CompareTo(a.score) : byDate;
            case SortMode.Lowest:  return a.score != b.score ? a.score.CompareTo(b.score) : byDate;
            default:               return byDate;
        }
    }

    void AddRow(ScoreRecord record, int index, bool isToday)
    {
        Color background = index % 2 == 0 ? theme.panel : Color.Lerp(theme.panel, theme.burnt, 0.35f);
        UnityEngine.UI.Image row = RetroUI.Pill("Row", content, theme, background, 14f);
        row.raycastTarget = false;
        var layout = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.minHeight = layout.preferredHeight = RowHeight;

        string date = FormatDate(record.puzzleDate);
        if (isToday) date += $"   <color=#{ColorUtility.ToHtmlStringRGB(theme.mustard)}>TODAY</color>";
        TextMeshProUGUI dateText = RetroUI.Text("Date", row.transform, theme, date, 28, theme.cream);
        Inset(dateText.rectTransform);
        dateText.alignment = TextAlignmentOptions.Left;

        bool loss = record.score == ScoreFormulas.Loss;
        TextMeshProUGUI scoreText = RetroUI.Text("Score", row.transform, theme,
            loss ? "LOSS" : FormatScore(record.score), 28, loss ? theme.coral : theme.cream);
        Inset(scoreText.rectTransform);
        scoreText.alignment = TextAlignmentOptions.Right;
    }

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
        content.anchoredPosition = Vector2.zero; // back to the top
    }

    void UpdateTabs()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            Color face = i == selected ? theme.coral : theme.mustard;
            RetroButton retro = tabs[i].GetComponent<RetroButton>();
            retro.normalColor = face;
            retro.faceImage.color = face;
        }
    }

    void UpdateSortLabel()
    {
        sortLabel.text = sort switch
        {
            SortMode.Oldest  => "OLDEST FIRST",
            SortMode.Highest => "HIGHEST FIRST",
            SortMode.Lowest  => "LOWEST FIRST",
            _                => "NEWEST FIRST"
        };
    }

    static void Inset(RectTransform rt)
    {
        RetroUI.Stretch(rt);
        rt.offsetMin = new Vector2(24, 0);
        rt.offsetMax = new Vector2(-24, 0);
    }

    // Puzzle dates are calendar dates: format them as they are, never convert to local time.
    static string FormatDate(string puzzleDate) =>
        DateTime.TryParseExact(puzzleDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
            ? d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture).ToUpperInvariant()
            : puzzleDate;

    static string FormatScore(int score) => score.ToString("N0", CultureInfo.InvariantCulture);
}
