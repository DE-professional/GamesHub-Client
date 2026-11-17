using Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the shared 70s look (<see cref="RetroTheme"/> / <see cref="RetroUI"/>) to Price Is Nice at
/// load, like <c>RunnerRetroHud</c> does for the Runner. Existing objects and their wiring
/// (GameManager references, button onClicks, layout) are kept; only the look changes.
/// </summary>
/// <remarks>
/// Set up by <c>Tools → Price Is Nice → Set Up Retro Theme</c>, which adds this to the Canvas and
/// fills in every reference. Any reference left empty is skipped.
/// </remarks>
public class PriceIsNiceRetroUI : MonoBehaviour
{
    public RetroTheme theme;
    [Tooltip("Regular-weight font for longer text (descriptions, prompts). Empty: the theme's display font.")]
    public TMP_FontAsset bodyFont;

    [Header("Screen")]
    public Image background;
    public TMP_Text title;
    public TMP_Text loadingText;

    [Header("Start")]
    public Button startButton;
    public TMP_Text introHeading;
    public TMP_Text introSub;

    [Header("Screenshots")]
    public Image screenshotFrame;
    public Image[] carouselArrows;
    public TMP_Text gameName;
    public TMP_Text gameDescription;

    [Header("Guessing")]
    public Image guessPanel;
    public TMP_Text prompt;
    public TMP_Text shownPrice;
    public Button higherButton;
    public Button lowerButton;
    public TMP_Text guessesLeft;
    public TMP_Text timer;
    public Image divider;

    [Header("Result")]
    public Image resultPanel;
    public TMP_Text resultTitle;
    public TMP_Text resultGame;
    public TMP_Text resultScore;
    public Button returnButton;

    // Colors for the rich text that GameManager, Timer and GuessTracker build at runtime, as
    // "#RRGGBB" for <color> tags. Set from the theme in Awake, before any of them write text;
    // the defaults are the old look, in case this component is missing.

    /// <summary>Small caption labels ("GUESSES LEFT", "TIME", "SCORE").</summary>
    public static string LabelColor { get; private set; } = "#8B909C";

    /// <summary>The revealed price.</summary>
    public static string HighlightColor { get; private set; } = "#F2C14E";

    /// <summary>Numbers next to a label (the score).</summary>
    public static string ValueColor { get; private set; } = "#EDEEF0";

    // Statics survive between Play sessions when domain reload is disabled.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        LabelColor = "#8B909C";
        HighlightColor = "#F2C14E";
        ValueColor = "#EDEEF0";
    }

    void Awake()
    {
        if (theme == null)
        {
            Debug.LogWarning("PriceIsNiceRetroUI: no theme assigned, keeping the old look. Run Tools → Price Is Nice → Set Up Retro Theme.", this);
            return;
        }

        LabelColor = Hex(theme.coral);
        HighlightColor = Hex(theme.mustard);
        ValueColor = Hex(theme.cream);

        TMP_FontAsset display = theme.font;
        TMP_FontAsset body = bodyFont != null ? bodyFont : theme.font;

        // Screen: the menu's background color and bottom stripes, and the logo-style title
        if (background != null)
        {
            background.color = theme.background;
            RetroUI.Stripes("Stripes", background.transform, theme, 0.045f, 10f, 6f).SetAsFirstSibling();
        }
        ReplaceWithLayered(title, "PRICE IS NICE", 88, new Vector2(0.5f, 0.92f), new Vector2(1400, 130));
        Style(loadingText, display, theme.cream, upper: true, fit: true);

        // Start: the intro texts are children of the button; keep them from catching its clicks
        if (startButton != null) RetroUI.Restyle(startButton, theme, theme.mustard, theme.buttonRadius, 44);
        Style(introHeading, display, theme.cream, fit: true);
        Layered(introHeading);
        Style(introSub, body, theme.mustard, fit: true);
        if (introHeading != null) introHeading.raycastTarget = false;
        if (introSub != null) introSub.raycastTarget = false;

        // Screenshots
        if (screenshotFrame != null) screenshotFrame.color = theme.panel;
        if (carouselArrows != null)
            foreach (Image arrow in carouselArrows)
                if (arrow != null) arrow.color = theme.cream;
        Style(gameName, display, theme.cream, fit: true);
        Style(gameDescription, body, Color.Lerp(theme.cream, theme.background, 0.2f));

        // Guessing
        if (guessPanel != null) guessPanel.color = theme.panel;
        Style(prompt, body, theme.mustard, upper: true, fit: true);
        Style(shownPrice, display, theme.cream, fit: true);
        Layered(shownPrice);
        // Arrow characters aren't in Rubik, so the labels are words only
        RestyleButton(higherButton, "Higher", theme.mustard, 32);
        RestyleButton(lowerButton, "Lower", theme.coral, 32);
        Style(guessesLeft, display, theme.cream);
        Style(timer, display, theme.cream);
        if (divider != null) divider.color = theme.burnt;

        // Result
        if (resultPanel != null) resultPanel.color = theme.panel;
        Style(resultTitle, display, theme.mustard, upper: true, fit: true);
        Layered(resultTitle); // GameManager changes it to "Not this time." on a loss; the layers follow
        Style(resultGame, body, theme.cream);
        Style(resultScore, display, theme.coral);
        if (returnButton != null) RetroUI.Restyle(returnButton, theme, theme.coral, theme.buttonRadius, 30);
    }

    // Font and color only: sizes and layout stay as designed. Rubik is wider than the old mono
    // font, so one-line texts shrink to fit (never grow) instead of overflowing.
    static void Style(TMP_Text t, TMP_FontAsset font, Color color, bool upper = false, bool fit = false)
    {
        if (t == null) return;
        t.font = font;
        t.color = color;
        t.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        t.characterSpacing = 1f;
        if (fit)
        {
            t.enableAutoSizing = true;
            t.fontSizeMax = t.fontSize;
            t.fontSizeMin = t.fontSize * 0.5f;
        }
    }

    // The logo's stacked lettering on a text scripts still write to (see RetroLayeredText)
    void Layered(TMP_Text t)
    {
        if (t != null) RetroLayeredText.Apply(t, theme);
    }

    void RestyleButton(Button button, string label, Color face, float fontSize)
    {
        if (button == null) return;
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.text = label;
        RetroUI.Restyle(button, theme, face, theme.buttonRadius, fontSize);
    }

    // The logo's stacked coral/burnt/cream lettering, in the original label's place
    void ReplaceWithLayered(TMP_Text original, string text, float size, Vector2 anchor, Vector2 box)
    {
        if (original == null) return;
        RectTransform layered = RetroUI.LayeredText(original.name, original.transform.parent, theme, text, size, anchor, box);
        layered.SetSiblingIndex(original.transform.GetSiblingIndex());
        original.gameObject.SetActive(false);
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
}
