using System.Collections;
using Menu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Visuals for one board cell: a raised tile that sinks flat into the felt when revealed
public class MineTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public RetroTheme theme;
    public RectTransform face;
    public Image faceImage;
    public Image shadow;
    public TextMeshProUGUI label;
    public Image icon;

    [Header("Icons")]
    public Sprite flagSprite;
    public Sprite mineSprite;

    [Header("Colors")]
    public Color hiddenA = new Color(0.96f, 0.71f, 0.18f);   // Mustard checkerboard
    public Color hiddenB = new Color(0.91f, 0.62f, 0.15f);
    public Color revealedA = new Color(0.27f, 0.16f, 0.09f);
    public Color revealedB = new Color(0.23f, 0.135f, 0.075f);
    public Color mineColor = new Color(0.95f, 1f, 0.47f);    // Cream
    public Color explodedColor = new Color(1f, 0.53f, 0.45f); // Coral
    [Tooltip("Number colors for 1..8 adjacent mines")]
    public Color[] numberColors =
    {
        new Color(0.95f, 1f, 0.47f),   // Cream
        new Color(0.96f, 0.71f, 0.18f), // Mustard
        new Color(0.91f, 0.45f, 0.13f), // Orange
        new Color(1f, 0.53f, 0.45f),    // Coral
        new Color(0.80f, 0.24f, 0.10f), // Burnt red
        new Color(1f, 0.85f, 0.65f),
        new Color(0.95f, 1f, 0.47f),
        new Color(0.96f, 0.71f, 0.18f),
    };

    public Vector2 depth = new Vector2(-3f, -3f);

    bool raised = true, hovered, pressed;
    Color baseColor;

    // Alternate tiles get a slightly different shade so the grid reads at a glance
    public void SetHidden(bool alternate)
    {
        raised = true;
        baseColor = alternate ? hiddenB : hiddenA;
        shadow.enabled = true;
        label.text = "";
        icon.enabled = false;
        Apply();
    }

    public void SetFlagged(bool flagged)
    {
        icon.sprite = flagSprite;
        icon.enabled = flagged;
    }

    public void SetRevealed(int adjacentMines, bool alternate)
    {
        raised = false;
        baseColor = alternate ? revealedB : revealedA;
        shadow.enabled = false;
        icon.enabled = false;
        label.text = adjacentMines > 0 ? adjacentMines.ToString() : "";
        if (adjacentMines > 0) label.color = numberColors[Mathf.Clamp(adjacentMines - 1, 0, numberColors.Length - 1)];
        Apply();
        if (isActiveAndEnabled) StartCoroutine(Pop());
    }

    public void SetMine(bool exploded)
    {
        raised = false;
        baseColor = exploded ? explodedColor : mineColor;
        shadow.enabled = false;
        label.text = "";
        icon.sprite = mineSprite;
        icon.enabled = true;
        Apply();
        if (isActiveAndEnabled) StartCoroutine(Pop());
    }

    void Apply()
    {
        bool lit = raised && hovered;
        faceImage.color = lit ? Color.Lerp(baseColor, theme ? theme.cream : Color.white, 0.55f) : baseColor;
        // Raised tiles sit above their shadow and dip toward it under the cursor
        float sink = !raised ? 1f : pressed ? 1f : hovered ? 0.4f : 0f;
        face.anchoredPosition = raised ? -depth * (1f - sink) : Vector2.zero;
    }

    IEnumerator Pop()
    {
        const float duration = 0.15f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            face.localScale = Vector3.one * (0.75f + 0.25f * (1f - (1f - t) * (1f - t)));
            yield return null;
        }
        face.localScale = Vector3.one;
    }

    public void OnPointerEnter(PointerEventData e) { hovered = true; Apply(); }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; Apply(); }
    public void OnPointerDown(PointerEventData e) { pressed = true; Apply(); }
    public void OnPointerUp(PointerEventData e) { pressed = false; Apply(); }
}
