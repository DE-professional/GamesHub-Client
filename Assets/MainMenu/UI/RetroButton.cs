using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Menu
{
    // Sinks the button face onto its shadow on hover/press, instead of a color tint
    [RequireComponent(typeof(Button))]
    public class RetroButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        public RectTransform face;
        public Image faceImage;
        public RectTransform shadow;
        public Color normalColor = Color.white;
        public Color hoverColor = Color.white;
        public Vector2 depth = new Vector2(-6f, -6f);
        public float speed = 25f;

        Button button;
        bool hovered, pressed, selected;
        float sink; // 0 = raised, 1 = flush with the shadow

        void Awake() => button = GetComponent<Button>();

        void OnEnable()
        {
            hovered = pressed = selected = false;
            sink = 0f;
            Apply();
        }

        void Update()
        {
            bool active = button.interactable;
            float target = !active ? 1f : pressed ? 1f : (hovered || selected) ? 0.45f : 0f;
            if (Mathf.Approximately(sink, target)) return;
            sink = Mathf.MoveTowards(sink, target, Time.unscaledDeltaTime * speed);
            Apply();
        }

        void Apply()
        {
            // Shadow sits at `depth`; the face slides toward it
            if (shadow) shadow.anchoredPosition = depth;
            if (face) face.anchoredPosition = depth * sink;
            if (!faceImage) return;
            Color c = Color.Lerp(normalColor, hoverColor, Mathf.Clamp01(sink * 2f));
            if (button && !button.interactable) c.a *= 0.5f;
            faceImage.color = c;
        }

        public void OnPointerEnter(PointerEventData e) => hovered = true;
        public void OnPointerExit(PointerEventData e)
        {
            hovered = pressed = false;
            // A mouse click selects the button; drop that so it doesn't stay half-sunk
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnPointerDown(PointerEventData e) => pressed = true;
        public void OnPointerUp(PointerEventData e) => pressed = false;
        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;
    }
}
