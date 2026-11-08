using System.Collections.Generic;
using Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [Header("Text")]
    public string gameTitle = "GamesHub";

    [Header("Game slot button labels (what the player sees)")]
    public List<string> gameDisplayNames = new List<string>();

    [Header("Game slot scenes (scene names must be added to Build Settings)")]
    public List<string> gameSceneNames = new List<string>();

    [Header("Look")]
    public RetroTheme theme;

    [Tooltip("Where the menu is built, under the scene's logo. Left empty, a standalone canvas is created.")]
    public RectTransform menuRoot;

    private GameObject mainPanel;
    private GameObject gamesPanel;

    private void Awake()
    {
        if (theme == null) theme = ScriptableObject.CreateInstance<RetroTheme>(); // default colors, no font/sprite
        EnsureEventSystem();
        Transform root = menuRoot != null ? menuRoot : CreateCanvas().transform;

        mainPanel = BuildMainPanel(root);
        gamesPanel = BuildGamesPanel(root);
        ShowMain();
    }

    // ---------- Button actions ----------

    private void ShowMain()
    {
        mainPanel.SetActive(true);
        gamesPanel.SetActive(false);
    }

    private void ShowGames()
    {
        mainPanel.SetActive(false);
        gamesPanel.SetActive(true);
    }

    private void OnScores()
    {
        // nothing yet lol
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnSelectGame(int index)
    {
        string sceneName = gameSceneNames[index];
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"Slot {index + 1}: scene '{sceneName}' is not in Build Settings.");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }

    // ---------- Panels ----------

    private GameObject BuildMainPanel(Transform parent)
    {
        GameObject panel = CreatePanel("MainPanel", parent);

        // The scene already shows the logo; only draw a title when running standalone
        if (menuRoot == null)
            RetroUI.LayeredText("Title", panel.transform, theme, gameTitle, 110,
                new Vector2(0.5f, 0.8f), new Vector2(900, 160));

        GameObject column = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
        column.transform.SetParent(panel.transform, false);
        RectTransform rt = column.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.4f);
        rt.sizeDelta = new Vector2(440, 330);

        VerticalLayoutGroup v = column.GetComponent<VerticalLayoutGroup>();
        v.spacing = 32;
        v.childAlignment = TextAnchor.MiddleCenter;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = v.childForceExpandHeight = true;

        CreateButton("PlayButton", column.transform, "Play", ShowGames);
        CreateButton("ScoresButton", column.transform, "Scores", OnScores);
        CreateButton("QuitButton", column.transform, "Quit", OnQuit);

        return panel;
    }

    private GameObject BuildGamesPanel(Transform parent)
    {
        GameObject panel = CreatePanel("GamesPanel", parent);

        RetroUI.LayeredText("Header", panel.transform, theme, "Select a Game", 64,
            new Vector2(0.5f, 0.69f), new Vector2(900, 90));

        // Size the grid to the number of games, shrinking cell height when the
        // rows wouldn't otherwise fit between the header and the Back button
        const int maxColumns = 3;
        const float maxGridHeight = 380f;
        Vector2 spacing = new Vector2(40, 40);
        int count = gameSceneNames.Count;
        int columns = Mathf.Clamp(count, 1, maxColumns);
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)maxColumns));
        float cellHeight = Mathf.Min(170f, (maxGridHeight - spacing.y * (rows - 1)) / rows);
        Vector2 cellSize = new Vector2(360, cellHeight);

        GameObject grid = new GameObject("Slots", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(panel.transform, false);
        RectTransform rt = grid.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.405f); // midway between header and Back button
        rt.sizeDelta = new Vector2(columns * cellSize.x + (columns - 1) * spacing.x,
                                   rows * cellSize.y + (rows - 1) * spacing.y);

        GridLayoutGroup g = grid.GetComponent<GridLayoutGroup>();
        g.cellSize = cellSize;
        g.spacing = spacing;
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = columns;
        g.childAlignment = TextAnchor.MiddleCenter;

        // One slot per scene; display names are optional
        for (int i = 0; i < gameSceneNames.Count; i++)
        {
            int index = i; // capture for closure
            string label = (gameDisplayNames != null && index < gameDisplayNames.Count
                            && !string.IsNullOrEmpty(gameDisplayNames[index]))
                ? gameDisplayNames[index]
                : $"Game {index + 1}";
            // Cycle the logo's palette across the slots
            Color[] faces = { theme.mustard, theme.coral, theme.orange };
            RetroUI.Button($"Slot{i + 1}", grid.transform, theme, label, () => OnSelectGame(index), 36,
                faces[i % faces.Length]);
        }

        Button back = RetroUI.Button("BackButton", panel.transform, theme, "Back", ShowMain, 32, theme.cream);
        RetroUI.Place((RectTransform)back.transform, new Vector2(0.5f, 0.145f), new Vector2(260, 72));

        return panel;
    }

    // ---------- UI helpers ----------

    private Canvas CreateCanvas()
    {
        GameObject go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Image bg = go.AddComponent<Image>();
        bg.color = theme.background;
        return canvas;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        // NOTE: if your project uses ONLY the new Input System package, swap
        // StandaloneInputModule for InputSystemUIInputModule.
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private GameObject CreatePanel(string name, Transform parent)
    {
        // Transparent: the scene's backdrop and logo show through
        RectTransform rt = RetroUI.Rect(name, parent);
        RetroUI.Stretch(rt);
        return rt.gameObject;
    }

    private Button CreateButton(string name, Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        return RetroUI.Button(name, parent, theme, label, onClick, 44);
    }
}
