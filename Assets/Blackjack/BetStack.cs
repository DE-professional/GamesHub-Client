using System.Collections;
using System.Collections.Generic;
using Menu;
using UnityEngine;
using UnityEngine.UI;

// Bet circle on the felt. Chips fly in from the rack buttons and pile up here.
public class BetStack : MonoBehaviour
{
    [Header("Look")]
    public RetroTheme theme;
    public RectTransform pileRoot;     // Where landed chips are parented
    public Transform flightLayer;      // Chips fly above everything else (usually the Canvas)

    [Header("Chips")]
    [Tooltip("Ascending chip values; each one flies from the matching rack button and takes its color")]
    public int[] denominations = { 5, 10, 20, 50, 100 };
    public RetroButton[] sources;
    public float chipSize = 72f;
    public float stackStep = 9f;       // Height of each chip in a pile
    public int chipsPerPile = 8;
    public float pileSpacing = 82f;

    [Header("Motion")]
    public float flightDuration = 0.4f;
    public float arcHeight = 120f;
    public float stagger = 0.06f;      // Delay between chips when several are bet at once
    public int maxChipsPerBet = 12;    // Big bets get merged into fewer, larger chips past this

    readonly List<RectTransform> landed = new List<RectTransform>();
    readonly List<RectTransform> inFlight = new List<RectTransform>();
    int launched; // Slots handed out so far, including chips still in the air

    public void Clear()
    {
        StopAllCoroutines();
        foreach (RectTransform chip in landed) if (chip) Destroy(chip.gameObject);
        foreach (RectTransform chip in inFlight) if (chip) Destroy(chip.gameObject);
        landed.Clear();
        inFlight.Clear();
        launched = 0;
    }

    // Breaks `amount` into chips and flies each one onto the pile.
    // `from` overrides the start point (e.g. doubling down while the rack is hidden).
    public void Add(int amount, Transform from = null)
    {
        if (amount <= 0) return;
        List<int> chips = Breakdown(amount);
        for (int i = 0; i < chips.Count; i++)
        {
            int d = chips[i];
            Transform start = from ? from : (sources != null && d < sources.Length && sources[d] ? sources[d].transform : transform);
            StartCoroutine(Fly(d, start.position, i * stagger));
        }
    }

    // Largest chips first; returns denomination indices. Any remainder below the smallest chip still shows one chip.
    List<int> Breakdown(int amount)
    {
        var result = new List<int>();
        for (int d = denominations.Length - 1; d >= 0 && result.Count < maxChipsPerBet; d--)
            while (amount >= denominations[d] && result.Count < maxChipsPerBet)
            {
                result.Add(d);
                amount -= denominations[d];
            }
        if (result.Count == 0) result.Add(0);
        return result;
    }

    IEnumerator Fly(int denomination, Vector3 startWorld, float delay)
    {
        int slot = launched++;
        if (delay > 0f) yield return new WaitForSeconds(delay);

        RectTransform chip = BuildChip(ChipColor(denomination));
        inFlight.Add(chip);
        chip.SetParent(flightLayer ? flightLayer : transform.root, false);
        chip.SetAsLastSibling();

        // Work in the flight layer's local space so the arc height is in canvas pixels
        RectTransform layer = (RectTransform)chip.parent;
        Vector3 start = layer.InverseTransformPoint(startWorld);
        Vector3 end = layer.InverseTransformPoint(pileRoot.TransformPoint(SlotPosition(slot)));

        float elapsed = 0f;
        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);
            float eased = 1f - (1f - t) * (1f - t); // Ease out so it settles onto the pile
            Vector3 p = Vector3.Lerp(start, end, eased);
            p.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
            chip.localPosition = p;
            chip.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.25f); // Lifts toward the camera mid-flight
            yield return null;
        }

        chip.SetParent(pileRoot, false);
        chip.anchoredPosition = SlotPosition(slot);
        chip.localScale = Vector3.one;
        inFlight.Remove(chip);
        landed.Add(chip);
        // Later slots must draw on top, but chips can land out of order
        landed.Sort((a, b) => a.anchoredPosition.y.CompareTo(b.anchoredPosition.y));
        for (int i = 0; i < landed.Count; i++) landed[i].SetSiblingIndex(i);

        yield return Bounce(chip);
    }

    IEnumerator Bounce(RectTransform chip)
    {
        const float duration = 0.12f;
        float elapsed = 0f;
        while (elapsed < duration && chip)
        {
            elapsed += Time.deltaTime;
            float s = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI) * 0.12f;
            chip.localScale = new Vector3(1f + s, 1f - s, 1f); // Squash on impact
            yield return null;
        }
        if (chip) chip.localScale = Vector3.one;
    }

    // First pile in the middle, then right, then left; past that the piles just keep growing
    Vector2 SlotPosition(int slot)
    {
        int pile = (slot / chipsPerPile) % 3;
        int height = slot % chipsPerPile + slot / (chipsPerPile * 3) * chipsPerPile;
        float x = pile == 0 ? 0f : pile == 1 ? pileSpacing : -pileSpacing;
        return new Vector2(x, height * stackStep);
    }

    Color ChipColor(int denomination)
    {
        if (sources != null && denomination < sources.Length && sources[denomination])
            return sources[denomination].normalColor;
        return theme.Stripes[denomination % theme.Stripes.Length];
    }

    // Same build as the rack chips, but the edge is a darker shade of the face so stacked chips stay distinct
    RectTransform BuildChip(Color face)
    {
        RectTransform root = RetroUI.Rect("Chip", transform);
        root.sizeDelta = new Vector2(chipSize, chipSize);

        float radius = chipSize * 0.5f;
        Image edge = RetroUI.Pill("Edge", root, theme, Color.Lerp(face, theme.ink, 0.45f), radius);
        RetroUI.Stretch(edge.rectTransform, new Vector2(0f, -stackStep * 0.6f));

        Image faceImg = RetroUI.Pill("Face", root, theme, face, radius);
        RetroUI.Stretch(faceImg.rectTransform);

        Image inlay = RetroUI.Pill("Inlay", faceImg.transform, theme, theme.cream, radius * 0.6f);
        RetroUI.Place(inlay.rectTransform, new Vector2(0.5f, 0.5f), Vector2.one * chipSize * 0.6f);
        // Cream on cream needs a rim to read
        if (face == theme.cream) inlay.gameObject.AddComponent<Outline>().effectColor = theme.mustard;

        foreach (Image img in root.GetComponentsInChildren<Image>()) img.raycastTarget = false;
        return root;
    }
}
