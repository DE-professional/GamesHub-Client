using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Util;

public class RunnerManager : MonoBehaviour
{
    public static RunnerManager Instance { get; private set; }

    public float startSpeed = 6f;
    public float maxSpeed = 18f;
    public float speedIncreasePerSecond = 0.15f;

    public GameObject titlePanel;
    public TMP_Text scoreLabel;
    public GameObject gameOverPanel;
    public TMP_Text gameOverScoreLabel;
    public AudioSource sfxSource;
    public AudioClip dieSound;

    public float Speed { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsStarted { get; private set; }
    public bool IsRunning => IsStarted && !IsGameOver;
    // The click/key that starts the game shouldn't also make the player jump
    public bool StartedThisFrame => startFrame == Time.frameCount;

    private float distance;

    // The UTC puzzle date this run uses for its seed and its score. Set once, in Awake.
    public string PuzzleDate { get; private set; }
    private int startFrame = -1;

    // Restarting goes straight back into a run instead of the title screen
    private static bool skipTitle;

    // Statics survive between Play sessions when domain reload is disabled
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => skipTitle = false;

    void Awake()
    {
        Instance = this;
        PuzzleDate = DailyPuzzle.TodayUtc();
        Time.timeScale = 1f;
        Speed = startSpeed;
    }

    void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (skipTitle || titlePanel == null)
            StartGame();
        else
            titlePanel.SetActive(true);

        // Only skip once, so coming back from the main menu shows the title again
        skipTitle = false;
    }

    // Wired to the title screen's Start button
    public void StartGame()
    {
        if (IsStarted) return;
        IsStarted = true;
        startFrame = Time.frameCount;
        if (titlePanel != null) titlePanel.SetActive(false);
        Services.Scores.MarkAttemptStarted(GameIds.Runner, PuzzleDate);
    }

    void Update()
    {
        if (IsGameOver)
        {
            if (RestartPressed()) Restart();
            return;
        }

        if (!IsStarted)
        {
            if (StartKeyPressed()) StartGame();
            return;
        }

        Speed = Mathf.Min(maxSpeed, Speed + speedIncreasePerSecond * Time.deltaTime);
        distance += Speed * Time.deltaTime;

        if (scoreLabel != null) scoreLabel.text = Score.ToString();
    }

    public int Score => Mathf.FloorToInt(distance);

    public void GameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Time.timeScale = 0f;
        // AudioSources keep playing at timeScale 0
        if (sfxSource != null && dieSound != null) sfxSource.PlayOneShot(dieSound);

        int best = Mathf.Max(Score, PlayerPrefs.GetInt("RunnerBest", 0));
        PlayerPrefs.SetInt("RunnerBest", best);
        SubmitScore(ScoreFormulas.Runner(Score));

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (gameOverScoreLabel != null)
            gameOverScoreLabel.text = $"Score: {Score}\nBest: {best}\n\nPress Space to restart";
    }

    // Saves the day's score: on the server when logged in, on the device for a guest.
    async void SubmitScore(int score)
    {
        SubmitResult result = await Services.Scores.SubmitAsync(GameIds.Runner, PuzzleDate, score);
        Debug.Log($"Runner score {score} for {PuzzleDate}: {result}");
    }

    // Hook this up to a UI button too if you want one
    public void Restart()
    {
        Time.timeScale = 1f;
        skipTitle = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    bool RestartPressed()
    {
        if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.rKey.wasPressedThisFrame))
            return true;

        // Clicks on the game-over panel's buttons (e.g. Return to Menu) aren't restarts
        bool pointerPressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
        return pointerPressed && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
    }

    bool StartKeyPressed()
    {
        return Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
