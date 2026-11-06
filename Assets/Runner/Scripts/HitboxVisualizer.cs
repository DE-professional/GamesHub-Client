using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class HitboxVisualizer : MonoBehaviour
{
    [SerializeField] bool showOnStart = true;
    [SerializeField] float lineWidth = 0.03f;          // world units
    [SerializeField] Color solidColor = Color.green;
    [SerializeField] Color triggerColor = Color.yellow;
    [SerializeField] Color disabledColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] float rescanInterval = 0.5f;      // seconds between searches for new colliders
    [SerializeField] int sortingOrder = 32767;         // draw on top of everything

    readonly Dictionary<BoxCollider2D, LineRenderer> lines = new();
    readonly List<BoxCollider2D> toRemove = new();
    readonly Vector3[] corners = new Vector3[4];
    Material lineMaterial;
    bool visible;
    float nextScan;

    void Awake()
    {
        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        visible = showOnStart;
    }

    void Update()
    {
        if (TogglePressed())
        {
            visible = !visible;
            foreach (var lr in lines.Values) lr.enabled = visible;
        }

        if (visible && Time.unscaledTime >= nextScan)
        {
            Rescan();
            nextScan = Time.unscaledTime + rescanInterval;
        }
    }

    // LateUpdate so the boxes match positions after movement/animation this frame
    void LateUpdate()
    {
        if (!visible) return;

        toRemove.Clear();
        foreach (var pair in lines)
        {
            BoxCollider2D col = pair.Key;
            LineRenderer lr = pair.Value;

            if (col == null)                 // collider was destroyed
            {
                toRemove.Add(col);
                if (lr != null) Destroy(lr.gameObject);
                continue;
            }

            bool active = col.gameObject.activeInHierarchy;   // handles pooled objects
            lr.enabled = active;
            if (!active) continue;

            Vector2 half = col.size * 0.5f;
            Vector2 o = col.offset;
            Transform t = col.transform;
            corners[0] = t.TransformPoint(o + new Vector2(-half.x, -half.y));
            corners[1] = t.TransformPoint(o + new Vector2(-half.x,  half.y));
            corners[2] = t.TransformPoint(o + new Vector2( half.x,  half.y));
            corners[3] = t.TransformPoint(o + new Vector2( half.x, -half.y));
            lr.SetPositions(corners);

            Color c = !col.enabled ? disabledColor : col.isTrigger ? triggerColor : solidColor;
            lr.startColor = lr.endColor = c;
        }
        foreach (var dead in toRemove) lines.Remove(dead);
    }

    void Rescan()
    {
        // Unity 2022.2+ / 2021.3.18+. On older versions use FindObjectsOfType<BoxCollider2D>().
        var found = FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var col in found)
        {
            if (lines.ContainsKey(col)) continue;

            var go = new GameObject($"Hitbox_{col.name}");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = 4;
            lr.widthMultiplier = lineWidth;
            lr.material = lineMaterial;
            lr.sortingOrder = sortingOrder;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lines.Add(col, lr);
        }
    }

    bool TogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F1);
#endif
    }

    void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }
}