using TMPro;
using UnityEngine;

namespace Menu
{
    // Shared 70s look, pulled from the stacked GamesHub logo (cream on burnt orange on coral)
    [CreateAssetMenu(menuName = "GamesHub/Retro Theme", fileName = "RetroTheme")]
    public class RetroTheme : ScriptableObject
    {
        [Header("Assets")]
        public TMP_FontAsset font;
        [Tooltip("White 9-sliced pill; corner radius is set per Image via pixelsPerUnitMultiplier")]
        public Sprite pill;

        [Header("Logo colors")]
        public Color cream = new Color(0.95f, 1f, 0.47f);
        public Color burnt = new Color(0.65f, 0.34f, 0f);
        public Color coral = new Color(1f, 0.53f, 0.45f);

        [Header("Supporting colors")]
        public Color mustard = new Color(0.96f, 0.71f, 0.18f);
        public Color orange = new Color(0.91f, 0.45f, 0.13f);
        public Color background = new Color(0.17f, 0.10f, 0.07f);
        public Color panel = new Color(0.27f, 0.16f, 0.09f);
        public Color ink = new Color(0.20f, 0.10f, 0.04f);

        [Header("Shape")]
        [Tooltip("How far the face sits above its shadow, logo-style (down-left)")]
        public Vector2 depth = new Vector2(-6f, -6f);
        public float buttonRadius = 26f;
        public float panelRadius = 22f;

        // Order the stripe bands are drawn in, top to bottom
        public Color[] Stripes => new[] { cream, mustard, orange, coral, burnt };
    }
}
