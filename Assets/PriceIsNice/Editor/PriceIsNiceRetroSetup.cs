using System.Collections.Generic;
using Menu;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tools → Price Is Nice → Set Up Retro Theme: adds <see cref="PriceIsNiceRetroUI"/> to the Price
/// Is Nice Canvas and fills in the theme, fonts and every scene reference by path. Safe to run
/// again: it reuses the existing component and only overwrites the references it can find.
/// </summary>
static class PriceIsNiceRetroSetup
{
    const string ScenePath = "Assets/PriceIsNice/PriceIsNice.unity";
    const string ThemePath = "Assets/MainMenu/UI/RetroTheme.asset";
    const string BodyFontPath = "Assets/MainMenu/Font/Rubik/Rubik-VariableFont_wght SDF.asset";

    const string Panel = "Panel";
    const string Carousel = Panel + "/GameWrapper/ClaudeCarousel";
    const string Guess = Panel + "/PriceIsNiceWrapper";
    const string Result = Panel + "/WinWrapper";

    [MenuItem("Tools/Price Is Nice/Set Up Retro Theme")]
    static void SetUp()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject canvas = GameObject.Find("Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Price Is Nice", "No active object named 'Canvas' in the scene.", "OK");
            return;
        }

        var theme = AssetDatabase.LoadAssetAtPath<RetroTheme>(ThemePath);
        var bodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BodyFontPath);
        var missing = new List<string>();
        if (theme == null) missing.Add(ThemePath);
        if (bodyFont == null) missing.Add(BodyFontPath);

        Undo.SetCurrentGroupName("Set Up Price Is Nice Retro Theme");
        var ui = canvas.GetComponent<PriceIsNiceRetroUI>();
        if (ui == null) ui = Undo.AddComponent<PriceIsNiceRetroUI>(canvas);
        Undo.RecordObject(ui, "Set Up Price Is Nice Retro Theme");

        Transform root = canvas.transform;
        T Get<T>(string path) where T : Component
        {
            Transform t = root.Find(path); // finds inactive objects too
            T c = t != null ? t.GetComponent<T>() : null;
            if (c == null) missing.Add($"{path} ({typeof(T).Name})");
            return c;
        }

        if (theme != null) ui.theme = theme;
        if (bodyFont != null) ui.bodyFont = bodyFont;

        ui.background = Get<Image>(Panel);
        ui.title = Get<TMP_Text>(Panel + "/WelcomeText");
        ui.loadingText = Get<TMP_Text>(Panel + "/Loading Text");

        ui.startButton = Get<Button>(Panel + "/StartButton");
        ui.introHeading = Get<TMP_Text>(Panel + "/StartButton/IntroHeading");
        ui.introSub = Get<TMP_Text>(Panel + "/StartButton/IntroSub");

        ui.screenshotFrame = Get<Image>(Carousel + "/ScreenshotBG");
        ui.carouselArrows = new[] { Get<Image>(Carousel + "/Button/Image"), Get<Image>(Carousel + "/Button (1)/Image") };
        ui.gameName = Get<TMP_Text>(Carousel + "/GameName");
        ui.gameDescription = Get<TMP_Text>(Carousel + "/GameDesc");

        ui.guessPanel = Get<Image>(Guess);
        ui.prompt = Get<TMP_Text>(Guess + "/PromptLabel");
        ui.shownPrice = Get<TMP_Text>(Guess + "/ExplanationText");
        ui.higherButton = Get<Button>(Guess + "/ButtonHolder/Higher");
        ui.lowerButton = Get<Button>(Guess + "/ButtonHolder/Lower");
        ui.guessesLeft = Get<TMP_Text>(Guess + "/GuessesLeft");
        ui.timer = Get<TMP_Text>(Guess + "/Timer");
        ui.divider = Get<Image>(Guess + "/Divider");

        ui.resultPanel = Get<Image>(Result);
        ui.resultTitle = Get<TMP_Text>(Result + "/NiceJob");
        ui.resultGame = Get<TMP_Text>(Result + "/GameName");
        ui.resultScore = Get<TMP_Text>(Result + "/Score");
        ui.returnButton = Get<Button>(Result + "/ReturnToMenuButton");

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = ui;

        string message = missing.Count == 0
            ? "PriceIsNiceRetroUI is on the Canvas with every reference set, and the scene is saved. Press Play to see the new look."
            : "Set up and saved, but these weren't found (the look skips them):\n\n" + string.Join("\n", missing);
        EditorUtility.DisplayDialog("Price Is Nice", message, "OK");
    }
}
