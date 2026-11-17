using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    [Header("UI Image Components")]
    public Image backgroundImage;
    public Image rankImage;
    public Image suitImage;

    [Header("Suit Colors")]
    public Color redSuitColor = new Color(0.80f, 0.24f, 0.10f);   // 70s burnt red-orange
    public Color blackSuitColor = new Color(0.20f, 0.10f, 0.04f); // Dark brown ink

    /// <summary>
    /// Configures the card visual components using the provided data and atlas.
    /// </summary>
    
    public void DisplayCard(Card card, CardAtlas atlas, bool isFaceDown = false)
    {
        if (isFaceDown)
        {
            backgroundImage.sprite = atlas.cardBack;
            rankImage.gameObject.SetActive(false);
            suitImage.gameObject.SetActive(false);
        }
        else
        {
            backgroundImage.sprite = atlas.cardBackground;
            rankImage.sprite = atlas.GetRankSprite(card.Rank);
            suitImage.sprite = atlas.GetSuitSprite(card.Suit);
            rankImage.gameObject.SetActive(true);
            suitImage.gameObject.SetActive(true);
            Color suitColor = GetSuitColor(card.Suit);
            rankImage.color = suitColor;
            suitImage.color = suitColor;
        }
    }

    private Color GetSuitColor(Suit cardSuit)
    {
        if (cardSuit == Suit.Hearts || cardSuit == Suit.Diamonds)
        {
            return redSuitColor;
        }
        else
        {
            return blackSuitColor;
        }
    }

    public IEnumerator AnimateMoveFrom(Vector3 startPosition, float duration = 0.3f)
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 1. Instantly hide card so no flash occurs during layout calculation
        canvasGroup.alpha = 0f;

        // 2. Wait until end of frame so Unity finishes calculating Layout Group positioning
        yield return new WaitForEndOfFrame();

        // 3. Teleport to start position and reveal
        Vector3 targetPosition = transform.position;
        transform.position = startPosition;
        canvasGroup.alpha = 1f;

        // 4. Smooth slide animation
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
    }
    public IEnumerator AnimateFlip(Card card, CardAtlas atlas, float duration = 0.4f)
    {
        Quaternion startRotation = transform.localRotation;
        Quaternion midRotation = Quaternion.Euler(0, 90, 0);
        Quaternion endRotation = Quaternion.Euler(0, 0, 0);

        float halfDuration = duration / 2f;

        // Phase 1: Rotate 0 to 90 degrees (Card back hides)
        float elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(startRotation, midRotation, elapsed / halfDuration);
            yield return null;
        }

        // Midpoint: Swap visuals from Face Down to Face Up
        DisplayCard(card, atlas, isFaceDown: false);

        // Phase 2: Rotate 90 to 0 degrees (Card face reveals)
        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(midRotation, endRotation, elapsed / halfDuration);
            yield return null;
        }

        transform.localRotation = endRotation;
    }
}