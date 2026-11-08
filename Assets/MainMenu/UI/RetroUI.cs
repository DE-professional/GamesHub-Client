using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Menu
{
    // Builders for the 70s menu look. Everything takes a RetroTheme so colors/font live in one asset.
    public static class RetroUI
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, Vector2 offset = default)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = offset;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
        }

        // Rounded (pill) Image; radius is in canvas pixels
        public static Image Pill(string name, Transform parent, RetroTheme theme, Color color, float radius)
        {
            RectTransform rt = Rect(name, parent);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            SetPill(img, theme, radius);
            return img;
        }

        public static void SetPill(Image img, RetroTheme theme, float radius)
        {
            img.sprite = theme.pill;
            img.type = Image.Type.Sliced;
            // The sprite's corner is `border` px wide; scale it down to the requested radius
            float corner = theme.pill ? theme.pill.border.x : radius;
            img.pixelsPerUnitMultiplier = corner / Mathf.Max(1f, radius);
        }

        public static TextMeshProUGUI Text(string name, Transform parent, RetroTheme theme, string content,
            float size, Color color)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = theme.font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            return t;
        }

        // Same construction as the GamesHub logo: coral, then burnt orange, then cream, each nudged up-right
        public static RectTransform LayeredText(string name, Transform parent, RetroTheme theme, string content,
            float size, Vector2 anchor, Vector2 sizeDelta)
        {
            RectTransform root = Rect(name, parent);
            Place(root, anchor, sizeDelta);

            float step = Mathf.Max(2f, size * 0.035f);
            Color[] layers = { theme.coral, theme.burnt, theme.cream };
            Transform current = root;
            for (int i = 0; i < layers.Length; i++)
            {
                TextMeshProUGUI t = Text(i == 0 ? "Back" : i == 1 ? "Middle" : "Top", current, theme, content, size, layers[i]);
                Stretch(t.rectTransform, i == 0 ? Vector2.zero : new Vector2(step, step));
                current = t.transform;
            }
            return root;
        }

        // Pill button with a solid offset shadow; RetroButton handles the sink-on-press
        public static Button Button(string name, Transform parent, RetroTheme theme, string label,
            UnityAction onClick, float fontSize = 40f, Color? face = null)
        {
            RectTransform root = Rect(name, parent);

            Image shadow = Pill("Shadow", root, theme, theme.burnt, theme.buttonRadius);
            Stretch(shadow.rectTransform, theme.depth);
            shadow.raycastTarget = false;

            Image faceImg = Pill("Face", root, theme, face ?? theme.mustard, theme.buttonRadius);
            Stretch(faceImg.rectTransform);

            TextMeshProUGUI text = Text("Label", faceImg.transform, theme, label.ToUpperInvariant(), fontSize, theme.ink);
            Stretch(text.rectTransform);
            text.characterSpacing = 4f;

            // Root needs a graphic to catch clicks across the face *and* the sunk position
            Image hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            Button btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = hit;
            if (onClick != null) btn.onClick.AddListener(onClick);

            RetroButton retro = root.gameObject.AddComponent<RetroButton>();
            retro.face = faceImg.rectTransform;
            retro.faceImage = faceImg;
            retro.shadow = shadow.rectTransform;
            retro.normalColor = faceImg.color;
            retro.hoverColor = theme.cream;
            retro.depth = theme.depth;
            return btn;
        }

        // Sunset disc: lower half is sliced by background-colored bars that thicken toward the bottom
        public static RectTransform Sun(string name, Transform parent, RetroTheme theme, Color color,
            Vector2 anchor, float diameter, int cuts = 4)
        {
            Image disc = Pill(name, parent, theme, color, diameter * 0.5f);
            Place(disc.rectTransform, anchor, new Vector2(diameter, diameter));
            disc.raycastTarget = false;

            float y = 0f;
            for (int i = 0; i < cuts; i++)
            {
                float thickness = diameter * (0.015f + 0.012f * i);
                y -= diameter * 0.09f;
                RectTransform cut = Rect($"Cut{i + 1}", disc.transform);
                Place(cut, new Vector2(0.5f, 0.5f), new Vector2(diameter + 4f, thickness));
                cut.anchoredPosition = new Vector2(0f, y);
                Image img = cut.gameObject.AddComponent<Image>();
                img.color = theme.background;
                img.raycastTarget = false;
                y -= thickness;
            }
            return disc.rectTransform;
        }

        // Full-width run of horizontal stripes, centred on `anchorY` (0 = bottom, 1 = top of parent)
        public static RectTransform Stripes(string name, Transform parent, RetroTheme theme, float anchorY,
            float stripeHeight, float gap)
        {
            Color[] colors = theme.Stripes;
            RectTransform band = Rect(name, parent);
            band.anchorMin = new Vector2(0f, anchorY);
            band.anchorMax = new Vector2(1f, anchorY);
            band.sizeDelta = new Vector2(0f, colors.Length * stripeHeight + (colors.Length - 1) * gap);

            for (int i = 0; i < colors.Length; i++)
            {
                RectTransform s = Rect($"Stripe{i + 1}", band);
                s.anchorMin = new Vector2(0f, 1f);
                s.anchorMax = new Vector2(1f, 1f);
                s.pivot = new Vector2(0.5f, 1f);
                s.sizeDelta = new Vector2(0f, stripeHeight);
                s.anchoredPosition = new Vector2(0f, -i * (stripeHeight + gap));
                Image img = s.gameObject.AddComponent<Image>();
                img.color = colors[i];
                img.raycastTarget = false;
            }
            return band;
        }
    }
}
