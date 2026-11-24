using Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the shared 70s look (<see cref="RetroTheme"/> / <see cref="RetroUI"/>) to Sudoku at load,
/// like <c>RunnerRetroHud</c> and <c>PriceIsNiceRetroUI</c> do. Existing objects and their wiring
/// (SudokuUI references, button onClicks) are kept; only the look and layout change.
/// </summary>
/// <remarks>
/// Layout is in 1920×1080 canvas units: the board in the middle, title and buttons on the left,
/// a stats card on the right. Cells are colored as SudokuUI creates them (<see cref="SudokuUI.CellCreated"/>).
/// Any reference left empty is skipped.
/// </remarks>
public class SudokuRetroUI : MonoBehaviour
{
    public RetroTheme theme;
    [Tooltip("Regular-weight font for captions and notes. Empty: the theme's display font.")]
    public TMP_FontAsset bodyFont;
    public SudokuUI game;

    [Header("Left column")]
    public Button notesButton;
    public TMP_Text notesLabel;
    public Button returnButton;

    [Header("Board")]
    public GridLayoutGroup board;

    [Header("Stats")]
    public TMP_Text mistakesCaption;
    public TMP_Text mistakes;
    public TMP_Text timerCaption;
    public TMP_Text timer;

    [Header("Win popup")]
    public Image winPopup;
    public TMP_Text winText;

    const float SideX = 715f;     // centre of the left/right columns
    const float BoardPad = 10f;   // frame showing around the cells
    const float BoxLine = 6f;     // the lines between 3×3 boxes

    TMP_FontAsset display, body;
    RetroButton notesRetro;

    void Awake()
    {
        if (theme == null)
        {
            Debug.LogWarning("SudokuRetroUI: no theme assigned, keeping the old look.", this);
            return;
        }

        display = theme.font;
        body = bodyFont != null ? bodyFont : theme.font;

        if (game != null)
        {
            game.CellCreated += StyleCell;
            game.NotesModeChanged += SetNotesMode;
        }

        // Screen: the menu's background color and bottom stripes
        RectTransform bg = RetroUI.Rect("Background", transform);
        RetroUI.Stretch(bg);
        Image background = bg.gameObject.AddComponent<Image>();
        background.color = theme.background;
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();
        RetroUI.Stripes("Stripes", background.transform, theme, 0.045f, 10f, 6f);

        // Left column: logo-style title, then the buttons
        RectTransform title = RetroUI.LayeredText("Title", transform, theme, "SUDOKU", 104, Center, new Vector2(470, 150));
        title.anchoredPosition = new Vector2(-SideX, 330);

        if (notesButton != null)
        {
            notesRetro = RetroUI.Restyle(notesButton, theme, theme.mustard, theme.buttonRadius, 34);
            Place((RectTransform)notesButton.transform, new Vector2(-SideX, 20), new Vector2(360, 96));
        }
        if (notesLabel != null)
        {
            notesLabel.fontStyle = FontStyles.UpperCase; // SudokuUI writes "Notes: On" / "Notes: Off"
            FitButtonLabel(notesLabel);
        }
        if (returnButton != null)
        {
            RetroUI.Restyle(returnButton, theme, theme.coral, theme.buttonRadius, 34);
            Place((RectTransform)returnButton.transform, new Vector2(-SideX, -330), new Vector2(360, 96));
            FitButtonLabel(returnButton.GetComponentInChildren<TMP_Text>(true));
        }

        if (board != null) FrameBoard();
        StyleStats();
        if (winPopup != null) StyleWinPopup();
    }

    void OnDestroy()
    {
        if (game == null) return;
        game.CellCreated -= StyleCell;
        game.NotesModeChanged -= SetNotesMode;
    }

    // Dark cells on a burnt grid: the 2px gaps between cells show the frame through as thin lines,
    // and thicker lines on top split the 3×3 boxes.
    void FrameBoard()
    {
        var cells = (RectTransform)board.transform;
        Vector2 framed = cells.sizeDelta + Vector2.one * (BoardPad * 2f);
        int index = cells.GetSiblingIndex();

        Image shadow = RetroUI.Pill("BoardShadow", transform, theme, theme.coral, theme.panelRadius);
        Match(shadow.rectTransform, cells, framed, theme.depth * 1.5f);
        Image frame = RetroUI.Pill("BoardFrame", transform, theme, theme.burnt, theme.panelRadius);
        Match(frame.rectTransform, cells, framed, Vector2.zero);
        shadow.raycastTarget = frame.raycastTarget = false;
        shadow.transform.SetSiblingIndex(index);
        frame.transform.SetSiblingIndex(index + 1);

        // A sibling rather than a child, so the GridLayoutGroup doesn't lay the lines out as cells
        RectTransform lines = RetroUI.Rect("BoxLines", transform);
        Match(lines, cells, cells.sizeDelta, Vector2.zero);
        lines.SetSiblingIndex(cells.GetSiblingIndex() + 1);

        RectOffset pad = board.padding;
        Vector2 cell = board.cellSize, gap = board.spacing;
        for (int i = 3; i < 9; i += 3)
        {
            // Centred on the gap after the i-th cell
            float x = pad.left + i * cell.x + (i - 0.5f) * gap.x;
            float y = pad.top + i * cell.y + (i - 0.5f) * gap.y;

            RectTransform v = Line($"Column{i}", lines);
            v.anchorMin = new Vector2(0f, 0f);
            v.anchorMax = new Vector2(0f, 1f);
            v.sizeDelta = new Vector2(BoxLine, 0f);
            v.anchoredPosition = new Vector2(x, 0f);

            RectTransform h = Line($"Row{i}", lines);
            h.anchorMin = new Vector2(0f, 1f);
            h.anchorMax = new Vector2(1f, 1f);
            h.sizeDelta = new Vector2(0f, BoxLine);
            h.anchoredPosition = new Vector2(0f, -y);
        }
    }

    RectTransform Line(string name, Transform parent)
    {
        RectTransform rt = RetroUI.Rect(name, parent);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = theme.burnt;
        img.raycastTarget = false;
        return rt;
    }

    void StyleCell(CellView view)
    {
        if (view.background != null)
        {
            view.background.sprite = null; // flat squares, not the default bevelled UI sprite
            view.background.type = Image.Type.Simple;
        }

        view.normalColor = theme.panel;
        view.similarColor = Color.Lerp(theme.panel, theme.coral, 0.3f);
        view.selectedColor = theme.mustard;
        view.givenTextColor = theme.cream;
        view.enteredTextColor = theme.mustard;
        view.selectedTextColor = theme.ink;

        if (view.label != null)
        {
            view.label.font = display;
            view.label.fontStyle = FontStyles.Normal;
        }
        view.noteColor = theme.cream;
        view.selectedNoteColor = theme.ink;
        if (view.noteLabel != null && view.noteLabel.Length > 0 && view.noteLabel[0] != null)
            StyleNotes(view);
        view.Refresh();
    }

    // The prefab's 3×3 notes grid is 120×96 inside a 100×100 cell, so its right column spilled out
    // under the next cell. Fit it to the cell, inset clear of the box lines, with each note centred
    // in its slot, in the heavy display font (the regular weight is too thin this small).
    void StyleNotes(CellView view)
    {
        const float inset = 6f;
        if (view.noteLabel[0].transform.parent.TryGetComponent(out GridLayoutGroup notes) && board != null)
        {
            Vector2 slot = (board.cellSize - Vector2.one * (inset * 2f)) / 3f;
            notes.padding = new RectOffset((int)inset, (int)inset, (int)inset, (int)inset);
            notes.cellSize = slot;
            notes.spacing = Vector2.zero;
            notes.childAlignment = TextAnchor.MiddleCenter;
        }

        foreach (TMP_Text n in view.noteLabel)
        {
            if (n == null) continue;
            n.font = display;
            n.fontStyle = FontStyles.Normal;
            n.fontSize = 22;
            n.enableAutoSizing = false;
            n.alignment = TextAlignmentOptions.Center;
            n.textWrappingMode = TextWrappingModes.NoWrap;
            n.overflowMode = TextOverflowModes.Overflow;
            n.margin = Vector4.zero;
            n.characterSpacing = 0f;
        }
    }

    void SetNotesMode(bool on)
    {
        // The face picks the new color up as the click's press animation settles
        if (notesRetro != null) notesRetro.normalColor = on ? theme.orange : theme.mustard;
    }

    // A card on the right: coral captions over big stacked numbers
    void StyleStats()
    {
        Image cardShadow = RetroUI.Pill("StatsShadow", transform, theme, theme.burnt, theme.panelRadius);
        Place(cardShadow.rectTransform, new Vector2(SideX, 0) + theme.depth, new Vector2(380, 480));
        Image card = RetroUI.Pill("StatsCard", transform, theme, theme.panel, theme.panelRadius);
        Place(card.rectTransform, new Vector2(SideX, 0), new Vector2(380, 480));
        cardShadow.raycastTarget = card.raycastTarget = false;

        // Behind the stat texts, which come after it in the hierarchy
        int first = int.MaxValue;
        foreach (TMP_Text t in new[] { mistakesCaption, mistakes, timerCaption, timer })
            if (t != null) first = Mathf.Min(first, t.transform.GetSiblingIndex());
        if (first != int.MaxValue)
        {
            cardShadow.transform.SetSiblingIndex(first);
            card.transform.SetSiblingIndex(first + 1);
        }

        Image divider = RetroUI.Pill("StatsDivider", card.transform, theme, theme.burnt, 3f);
        Place(divider.rectTransform, Vector2.zero, new Vector2(300, 6));
        divider.raycastTarget = false;

        Caption(mistakesCaption, new Vector2(SideX, 165));
        Number(mistakes, new Vector2(SideX, 80));
        Caption(timerCaption, new Vector2(SideX, -75));
        Number(timer, new Vector2(SideX, -160));
    }

    void Caption(TMP_Text t, Vector2 pos)
    {
        if (t == null) return;
        Style(t, body, theme.coral, 30);
        t.fontStyle = FontStyles.UpperCase;
        t.characterSpacing = 8f;
        Place(t.rectTransform, pos, new Vector2(320, 50));
    }

    void Number(TMP_Text t, Vector2 pos)
    {
        if (t == null) return;
        Style(t, display, theme.cream, 92);
        Place(t.rectTransform, pos, new Vector2(320, 110));
        RetroLayeredText.Apply(t, theme); // SudokuUI keeps writing to it; the layers follow
    }

    void StyleWinPopup()
    {
        Transform popup = winPopup.transform;
        Color dim = theme.background;
        dim.a = 0.9f;
        winPopup.sprite = null;
        winPopup.color = dim; // still catches clicks, so the board can't be played underneath

        RetroUI.Stripes("Stripes", popup, theme, 0.5f, 10f, 6f).SetAsFirstSibling();

        Vector2 size = new Vector2(780, 560);
        Image shadow = RetroUI.Pill("PanelShadow", popup, theme, theme.burnt, theme.panelRadius);
        Place(shadow.rectTransform, theme.depth * 1.5f, size);
        Image panel = RetroUI.Pill("Panel", popup, theme, theme.panel, theme.panelRadius);
        Place(panel.rectTransform, Vector2.zero, size);

        RectTransform title = RetroUI.LayeredText("Title", panel.transform, theme, "SOLVED!", 120, Center, new Vector2(700, 160));
        title.anchoredPosition = new Vector2(0, 140);

        if (winText != null)
        {
            winText.transform.SetParent(panel.transform, false);
            Style(winText, display, theme.cream, 56);
            winText.fontStyle = FontStyles.UpperCase;
            winText.characterSpacing = 4f;
            Place(winText.rectTransform, new Vector2(0, -5), new Vector2(680, 100));
        }

        // The popup covers the scene's return button, so it gets its own copy
        if (returnButton != null)
        {
            GameObject copy = Instantiate(returnButton.gameObject, panel.transform);
            copy.name = returnButton.name;
            Place((RectTransform)copy.transform, new Vector2(0, -175), new Vector2(420, 100));
        }
    }

    void Style(TMP_Text t, TMP_FontAsset font, Color color, float size)
    {
        t.font = font;
        t.color = color;
        t.fontSize = size;
        t.fontStyle = FontStyles.Normal;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.characterSpacing = 2f;
    }

    // Rubik is wide; long labels shrink to fit their button instead of overflowing
    static void FitButtonLabel(TMP_Text t)
    {
        if (t == null) return;
        t.enableAutoSizing = true;
        t.fontSizeMax = t.fontSize;
        t.fontSizeMin = t.fontSize * 0.5f;
        t.margin = new Vector4(16, 0, 16, 0);
    }

    static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    // Centre-anchored at `pos`
    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.pivot = Center;
        RetroUI.Place(rt, Center, size);
        rt.anchoredPosition = pos;
    }

    // Centred on `target` (same anchors), with its own size, nudged by `offset`
    static void Match(RectTransform rt, RectTransform target, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = target.anchorMin;
        rt.anchorMax = target.anchorMax;
        rt.pivot = Center;
        rt.sizeDelta = size;
        Vector2 centre = target.anchoredPosition + Vector2.Scale(Center - target.pivot, target.sizeDelta);
        rt.anchoredPosition = centre + offset;
    }
}
