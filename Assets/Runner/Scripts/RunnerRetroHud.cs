using Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Applies the shared 70s look (RetroTheme/RetroUI) to the Runner HUD at load, like the main menu does.
// Existing objects and their wiring (RunnerManager labels, button onClicks) are kept; only the look changes.
public class RunnerRetroHud : MonoBehaviour
{
    public RetroTheme theme;
    [Tooltip("Rubik Black with the coral offset underlay, used for the live score")]
    public Material shadowMaterial;

    [Header("Title screen")]
    public Image titlePanel;
    public TMP_Text title;
    public TMP_Text controls;
    public Button startButton;

    [Header("In game")]
    public TMP_Text score;

    [Header("Game over")]
    public Image gameOverPanel;
    public TMP_Text gameOverTitle;
    public TMP_Text gameOverScore;
    public Button returnButton;

    void Awake()
    {
        if (theme == null) return;

        // Panels: theme background, mostly opaque so the scene still reads behind them
        Color dim = theme.background;
        dim.a = 0.88f;

        if (titlePanel != null)
        {
            titlePanel.color = dim;
            ReplaceWithLayered(title, "RUNNER", 190, new Vector2(0.5f, 0.66f), new Vector2(1200, 240));
            RetroUI.Stripes("Stripes", titlePanel.transform, theme, 0.5f, 10f, 6f).SetSiblingIndex(0);
        }
        if (startButton != null)
        {
            RetroUI.Restyle(startButton, theme, theme.mustard, theme.buttonRadius, 48);
            Place(startButton.transform, new Vector2(0.5f, 0.36f), new Vector2(420, 110));
        }
        if (controls != null)
        {
            Body(controls, 34, theme.cream);
            controls.text = controls.text.ToUpperInvariant();
            controls.lineSpacing = 20f;
            Place(controls.transform, new Vector2(0.5f, 0.17f), new Vector2(1400, 140));
        }

        if (score != null)
        {
            Body(score, 96, theme.cream);
            if (shadowMaterial != null) score.fontSharedMaterial = shadowMaterial;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.color = dim;
            ReplaceWithLayered(gameOverTitle, "GAME OVER", 150, new Vector2(0.5f, 0.68f), new Vector2(1400, 200));
        }
        if (gameOverScore != null)
        {
            Body(gameOverScore, 48, theme.cream);
            Place(gameOverScore.transform, new Vector2(0.5f, 0.44f), new Vector2(1400, 300));
        }
        if (returnButton != null)
        {
            RetroUI.Restyle(returnButton, theme, theme.coral, theme.buttonRadius, 44);
            Place(returnButton.transform, new Vector2(0.5f, 0.17f), new Vector2(560, 110));
        }
    }

    // The logo's stacked coral/burnt/cream lettering, in the original label's place
    void ReplaceWithLayered(TMP_Text original, string text, float size, Vector2 anchor, Vector2 box)
    {
        if (original == null) return;
        RectTransform layered = RetroUI.LayeredText(original.name, original.transform.parent, theme, text, size, anchor, box);
        layered.SetSiblingIndex(original.transform.GetSiblingIndex());
        original.gameObject.SetActive(false);
    }

    void Body(TMP_Text t, float size, Color color)
    {
        t.font = theme.font;
        t.fontSize = size;
        t.fontStyle = FontStyles.Normal;
        t.color = color;
        t.characterSpacing = 2f;
    }

    static void Place(Transform t, Vector2 anchor, Vector2 size)
    {
        var rt = (RectTransform)t;
        rt.pivot = new Vector2(0.5f, 0.5f);
        RetroUI.Place(rt, anchor, size);
        rt.anchoredPosition = Vector2.zero;
    }
}
