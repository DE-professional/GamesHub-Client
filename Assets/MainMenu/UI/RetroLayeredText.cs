using TMPro;
using UnityEngine;

namespace Menu
{
    /// <summary>
    /// Gives an existing text the logo's stacked coral / burnt / cream lettering, in place. Unlike
    /// <see cref="RetroUI.LayeredText"/>, which builds new static text, this keeps the original text
    /// object, so scripts can keep changing its text and layout groups keep sizing it.
    /// </summary>
    /// <remarks>
    /// The original becomes the back (coral) layer; two copies are added as children, each nudged
    /// up-right, like the logo. The copies follow the original's text and style every frame it
    /// changes. Use <see cref="Apply"/> after setting the text's font and size.
    /// </remarks>
    [DisallowMultipleComponent]
    public class RetroLayeredText : MonoBehaviour
    {
        TMP_Text source;
        TMP_Text[] layers;
        string syncedText;

        /// <summary>Layers <paramref name="text"/>. Applying twice does nothing.</summary>
        public static RetroLayeredText Apply(TMP_Text text, RetroTheme theme)
        {
            if (text.TryGetComponent(out RetroLayeredText existing)) return existing;
            var layered = text.gameObject.AddComponent<RetroLayeredText>();
            layered.Build(text, theme);
            return layered;
        }

        void Build(TMP_Text text, RetroTheme theme)
        {
            source = text;
            text.color = theme.coral;

            // Same nudge as RetroUI.LayeredText, so it matches the logo and titles
            float step = Mathf.Max(2f, text.fontSize * 0.035f);
            Color[] colors = { theme.burnt, theme.cream };
            layers = new TMP_Text[colors.Length];

            Transform parent = text.transform;
            for (int i = 0; i < colors.Length; i++)
            {
                RectTransform rt = RetroUI.Rect(i == 0 ? "Middle" : "Top", parent);
                RetroUI.Stretch(rt, new Vector2(step, step));
                var layer = rt.gameObject.AddComponent<TextMeshProUGUI>();
                layer.color = colors[i];
                layer.raycastTarget = false;
                layers[i] = layer;
                parent = rt;
            }

            Sync();
        }

        void LateUpdate()
        {
            if (source != null && source.text != syncedText) Sync();
        }

        // Copies everything that affects the glyphs, so the layers line up exactly (auto-size
        // included: each layer fits the same text into the same rect).
        void Sync()
        {
            syncedText = source.text;
            foreach (TMP_Text layer in layers)
            {
                layer.font = source.font;
                layer.fontSharedMaterial = source.fontSharedMaterial;
                layer.fontStyle = source.fontStyle;
                layer.enableAutoSizing = source.enableAutoSizing;
                layer.fontSizeMin = source.fontSizeMin;
                layer.fontSizeMax = source.fontSizeMax;
                layer.fontSize = source.fontSize;
                layer.alignment = source.alignment;
                layer.textWrappingMode = source.textWrappingMode;
                layer.overflowMode = source.overflowMode;
                layer.characterSpacing = source.characterSpacing;
                layer.wordSpacing = source.wordSpacing;
                layer.lineSpacing = source.lineSpacing;
                layer.margin = source.margin;
                layer.richText = source.richText;
                layer.text = source.text;
            }
        }
    }
}
