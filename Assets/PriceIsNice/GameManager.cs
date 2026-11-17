using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Util;

/// <summary>
/// Price Is Nice: guess whether today's Steam game costs more or less than the price shown.
/// The game is picked by the server (the same for every player); <see cref="PriceIsNiceLoader"/>
/// fetches it and its screenshots, and the main menu starts that in advance.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static event Action<double> OnApproxUpdated;

    // Broadcasts (remainingGuesses)
    public static event Action<int> OnGuessesChanged;

    // Game over notifications
    public static event Action<bool> OnGameOver;

    const int MaxTries = 3;

    [SerializeField] private Button StartButton;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private ImageCarousel _imageCarousel;
    [SerializeField] private TextMeshProUGUI gameDescription;
    [SerializeField] private TextMeshProUGUI gameName;
    [Tooltip("How far off the shown price may be, per guess: index 0 is the last guess")]
    [SerializeField] List<Double> difficultyList;
    [SerializeField] private GameObject priceIsNiceWrapper;
    public Timer timer;
    [Header("State Data")]
    [SerializeField] private double actualPrice;
    [SerializeField] private double approxPrice;
    [SerializeField] private int triesRemaining;
    private bool isGameOver;

    [SerializeField] private TextMeshProUGUI promptText;
    private const string PromptText = "Is it higher or lower than";
    [SerializeField] private GameObject winWrapper;

    private PriceIsNicePuzzle puzzle;
    private string date;

    private void Awake()
    {
        StartButton.onClick.AddListener(StartGame);
    }

    private void OnDestroy()
    {
        OnGameOver -= GameOver;
        StartButton.onClick.RemoveAllListeners();
    }

    private async void Start()
    {
        date = DailyPuzzle.TodayUtc();
        triesRemaining = MaxTries;
        loadingText.gameObject.SetActive(true);

        // Usually already loaded by the main menu; otherwise this starts the load and waits.
        PriceIsNicePuzzle loaded = await PriceIsNiceLoader.WaitAsync(date);
        if (this == null) return; // left the scene while loading

        if (loaded == null)
        {
            loadingText.text = "Couldn't load today's game.\nCheck your connection, then go back and try again.";
            return;
        }

        puzzle = loaded;
        actualPrice = puzzle.ActualPrice;
        approxPrice = Math.Round(GetApproxPrice(actualPrice), 2);

        _imageCarousel.SetSprites(puzzle.Screenshots);
        gameDescription.text = puzzle.ShortDescription;
        gameName.text = puzzle.Name;
        promptText.SetText(PromptText);
        loadingText.gameObject.SetActive(false);
        StartButton.gameObject.SetActive(true);
    }

    private void StartGame()
    {
        isGameOver = false;
        StartButton.gameObject.SetActive(false);
        _imageCarousel.gameObject.SetActive(true);
        priceIsNiceWrapper.SetActive(true);
        OnGuessesChanged?.Invoke(triesRemaining);
        OnApproxUpdated?.Invoke(approxPrice);
        OnGameOver += GameOver;
        timer.StartTimer();
        Services.Scores.MarkAttemptStarted(GameIds.PriceIsNice, date);
    }

    private void GameOver(bool isWin)
    {
        var congratsText = winWrapper.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        var gameInfo = winWrapper.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
        var scoreInfo = winWrapper.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        float timeTaken = timer.StopTimer();

        gameInfo.text = $"{puzzle.Name} sells for\n<font=\"IBMPlexMono-SemiBold SDF\"><size=300%><color=#F2C14E>{puzzle.PriceFormatted}</color></size></font>";
        _imageCarousel.gameObject.SetActive(false);
        priceIsNiceWrapper.SetActive(false);

        if (isWin)
        {
            int score = ScoreFormulas.PriceIsNice(triesRemaining, MaxTries, timeTaken);
            scoreInfo.text = $"SCORE  <color=#EDEEF0>{score}</color>";
            SubmitScore(score);
        }
        else
        {
            scoreInfo.SetText("");
            congratsText.SetText("Not this time.");
            SubmitScore(ScoreFormulas.Loss);
        }
        winWrapper.SetActive(true);
    }

    // Saves the day's score: on the server when logged in, on the device for a guest.
    async void SubmitScore(int score)
    {
        SubmitResult result = await Services.Scores.SubmitAsync(GameIds.PriceIsNice, date, score);
        Debug.Log($"PriceIsNice score {score} for {date}: {result}");
    }

    public void GuessHigher()
    {
        EvaluateGuess(actualPrice > approxPrice);
    }

    public void GuessLower()
    {
        EvaluateGuess(actualPrice < approxPrice);
    }

    public void EvaluateGuess(bool isCorrect)
    {
        if (isGameOver) return;

        if (isCorrect)
        {
            isGameOver = true;
            OnGameOver?.Invoke(true); // Player won!
        }
        else
        {
            triesRemaining--;
            OnGuessesChanged?.Invoke(triesRemaining);
            if (triesRemaining <= 0)
            {
                isGameOver = true; // Reveal real value
                OnGameOver?.Invoke(false); // Player lost!
            }
            else
            {
                approxPrice = Math.Round(GetApproxPrice(actualPrice), 2);
                OnApproxUpdated?.Invoke(approxPrice);
            }
        }
    }

    // A random wrong price, closer to the real one with each guess used. Deliberately not seeded:
    // only the game itself has to be the same for everyone.
    private double GetApproxPrice(double actualPrice)
    {
        var rand = new System.Random();
        // e.g. actual price 4.99, factor 2.5: shown price between 1.996 and 12.475,
        // and at least 5% (0.2495) away from the real price
        var maxPrice = actualPrice * difficultyList[triesRemaining - 1];
        var minPrice = actualPrice / difficultyList[triesRemaining - 1];
        var minDifference = actualPrice * 0.05;
        double appx;
        do
        {
            appx = rand.NextDouble() * (maxPrice - minPrice) + minPrice;
        } while (Math.Abs(appx - actualPrice) < minDifference);

        return appx;
    }
}
