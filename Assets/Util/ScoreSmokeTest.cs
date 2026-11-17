using System;
using System.Linq;
using UnityEngine;
using Util;

/// <summary>
/// Temporary: exercises <see cref="ScoreService"/> against the local server. Add to an empty
/// scene, set the fields, press Play, and read the Console: every check logs PASS or FAIL.
/// </summary>
/// <remarks>
/// <para>
/// Submits a real score for today for whichever account is logged in, which uses up that
/// game's daily attempt. Use a test account (@example.com) and a game you aren't playing.
/// Re-running on the same day is fine: the first submission then reads AlreadySubmitted.
/// </para>
/// <para>Modes:</para>
/// <list type="bullet">
/// <item><b>Online</b>: server running. Formulas, submit, duplicate, closed date, played status, history.</item>
/// <item><b>QueueSubmit</b>: log in once in Online mode first, then stop the server and run this.
/// The score must come back Queued.</item>
/// <item><b>QueueFlush</b>: start the server again and run this. The restore sends the queued
/// score; history must then contain it.</item>
/// <item><b>Guest</b>: logs out (deletes the saved token), then checks the device-only store.
/// Log in again in Online mode afterwards.</item>
/// </list>
/// </remarks>
public class ScoreSmokeTest : MonoBehaviour
{
    enum Mode { Online, QueueSubmit, QueueFlush, Guest }

    [Tooltip("Online: server running.\n" +
             "QueueSubmit: server STOPPED; queues a score.\n" +
             "QueueFlush: server running; sends the score QueueSubmit queued, so run QueueSubmit first.\n" +
             "Guest: logs out, then tests device-only scores.")]
    [SerializeField] Mode mode = Mode.Online;

    // Local test account only (@example.com). The password comes from EditorPrefs:
    // GamesHub → Test Account Password…
    [SerializeField] string email = "test1@example.com";

    [Tooltip("Online and Guest modes submit for this game. Pick one you aren't playing today.")]
    [SerializeField] string onlineSlug = "runner";

    [Tooltip("QueueSubmit and QueueFlush use this game, so it must differ from onlineSlug.")]
    [SerializeField] string queueSlug = "blackjack";

    [SerializeField] int testScore = 1234;

    int passed, failed;

    async void Start()
    {
        try
        {
            switch (mode)
            {
                case Mode.Online:      await RunOnline(); break;
                case Mode.QueueSubmit: await RunQueueSubmit(); break;
                case Mode.QueueFlush:  await RunQueueFlush(); break;
                case Mode.Guest:       await RunGuest(); break;
            }
        }
        catch (Exception e)
        {
            failed++;
            Debug.LogException(e);
        }

        string summary = $"ScoreSmokeTest {mode}: {passed} passed, {failed} failed";
        if (failed == 0) Debug.Log(summary);
        else Debug.LogError(summary);
    }

    async Awaitable RunOnline()
    {
        CheckFormulas();

        if (!await EnsureLoggedIn()) return;
        GameId game = Find(onlineSlug);
        string today = DailyPuzzle.TodayUtc();

        PlayedStatus before = await Services.Scores.GetPlayedStatusAsync(game, today);
        Debug.Log($"Played status before submitting: {before} (NotPlayed on the first run of the day; " +
                  "Played if you already ran this or played the game on this device today)");
        Check("status before is known", before != PlayedStatus.Unknown);

        SubmitResult first = await Services.Scores.SubmitAsync(game, today, testScore);
        Check("first submit", first, SubmitResult.Accepted, SubmitResult.AlreadySubmitted);

        SubmitResult again = await Services.Scores.SubmitAsync(game, today, testScore + 1);
        Check("second submit, same day", again, SubmitResult.AlreadySubmitted);

        // Two days back is outside the grace period at any time of day.
        string closed = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        SubmitResult old = await Services.Scores.SubmitAsync(game, closed, testScore);
        Check("submit for two days ago", old, SubmitResult.DateClosed);

        PlayedStatus after = await Services.Scores.GetPlayedStatusAsync(game, today);
        Check("status after submit", after, PlayedStatus.Played);

        HistoryResult history = await Services.Scores.GetHistoryAsync(game);
        Check("history loads", history.Outcome, HistoryOutcome.Success);
        ScoreRecord todays = history.Scores.FirstOrDefault(s => s.puzzleDate == today);
        Check("history has today's score", todays != null);
        if (todays != null && first == SubmitResult.Accepted)
            Check("history keeps the first score, not the duplicate", todays.score, testScore);
        Check("history is newest first", IsNewestFirst(history));
        Check("history has only the current formula version",
              history.Scores.All(s => s.formulaVersion == game.FormulaVersion));
        Debug.Log($"History for {game}: {history.Scores.Count} score(s)");
    }

    async Awaitable RunQueueSubmit()
    {
        RestoreResult restore = await Services.Auth.RestoreAsync();
        Check("restore with the server stopped", restore, RestoreResult.NetworkError);
        Check("a saved login exists", PlayerPrefs.HasKey(AuthService.TokenKey));
        if (restore != RestoreResult.NetworkError)
        {
            // Submitting now would store a real score instead of queueing it, using up the day.
            Debug.LogError("The server answered, so it isn't stopped (stop the Node server, not the " +
                           "database). Nothing was submitted.");
            return;
        }

        GameId game = Find(queueSlug);
        string today = DailyPuzzle.TodayUtc();
        SubmitResult r = await Services.Scores.SubmitAsync(game, today, testScore);
        Check("submit while offline", r, SubmitResult.Queued);

        PlayedStatus status = await Services.Scores.GetPlayedStatusAsync(game, today);
        Check("queued score counts as played", status, PlayedStatus.Played);
        Debug.Log("Now start the server and run QueueFlush (today, within the grace period at the latest).");
    }

    async Awaitable RunQueueFlush()
    {
        GameId game = Find(queueSlug);
        string today = DailyPuzzle.TodayUtc();

        // Checked before logging in: the restore starts sending the queue straight away.
        bool queued = Services.Scores.HasPendingScore(game, today);
        Check($"a {game.Slug} score for today is queued", queued);
        if (!queued)
        {
            Debug.LogError("Nothing to send. Run QueueSubmit first (server stopped), then QueueFlush " +
                           "(server running). Reset Played State also empties the queue.");
            return;
        }

        // The restore raises SessionChanged, which starts sending the queue in the background.
        if (!await EnsureLoggedIn()) return;
        await Services.Scores.FlushPendingAsync(); // returns at once if that send is still running
        await Awaitable.WaitForSecondsAsync(2f);

        Check("queue is empty after sending", !Services.Scores.HasPendingScore(game, today));
        HistoryResult history = await Services.Scores.GetHistoryAsync(game);
        Check("history loads", history.Outcome, HistoryOutcome.Success);
        ScoreRecord todays = history.Scores.FirstOrDefault(s => s.puzzleDate == today);
        Check("queued score reached the server", todays != null);
        if (todays != null) Check("queued score value", todays.score, testScore);
    }

    async Awaitable RunGuest()
    {
        await Services.Auth.LogoutAsync();
        Services.Auth.Logout(); // also clears a saved token that was never restored
        Check("no saved login", !PlayerPrefs.HasKey(AuthService.TokenKey));

        GameId game = Find(onlineSlug);
        string today = DailyPuzzle.TodayUtc();

        SubmitResult first = await Services.Scores.SubmitAsync(game, today, testScore);
        Check("guest submit", first, SubmitResult.Accepted, SubmitResult.AlreadySubmitted);

        SubmitResult again = await Services.Scores.SubmitAsync(game, today, testScore + 1);
        Check("guest second submit, same day", again, SubmitResult.AlreadySubmitted);

        PlayedStatus status = await Services.Scores.GetPlayedStatusAsync(game, today);
        Check("guest status after submit", status, PlayedStatus.Played);

        HistoryResult history = await Services.Scores.GetHistoryAsync(game);
        Check("guest history loads", history.Outcome, HistoryOutcome.Success);
        Check("guest history has today's score", history.Scores.Any(s => s.puzzleDate == today));

        Services.Scores.DiscardGuestScores();
        HistoryResult cleared = await Services.Scores.GetHistoryAsync(game);
        Check("guest scores discarded", cleared.Scores.Count, 0);
        Debug.Log("Logged out. Run Online mode with a password to log in again.");
    }

    void CheckFormulas()
    {
        Check("Blackjack(500)", ScoreFormulas.Blackjack(500), 500);
        Check("Blackjack(0) is a loss", ScoreFormulas.Blackjack(0), 0);
        Check("Blackjack clamps to MaxScore", ScoreFormulas.Blackjack(5_000_000), ScoreFormulas.MaxScore);
        Check("PriceIsNice(3 of 3 tries, 0 s)", ScoreFormulas.PriceIsNice(3, 3, 0f), 10000);
        Check("PriceIsNice(1 of 3 tries, 30 s)", ScoreFormulas.PriceIsNice(1, 3, 30f), 3333);
        Check("PriceIsNice never 0 on a win", ScoreFormulas.PriceIsNice(1, 3, 1e9f), 1);
        Check("Minesweeper(0 s)", ScoreFormulas.Minesweeper(0f), 10000);
        Check("Minesweeper(120 s)", ScoreFormulas.Minesweeper(120f), 5000);
        Check("Minesweeper(360 s)", ScoreFormulas.Minesweeper(360f), 2500);
        Check("Sudoku(300 s, 0 mistakes)", ScoreFormulas.Sudoku(300f, 0), 5000);
        Check("Sudoku(270 s, 1 mistake)", ScoreFormulas.Sudoku(270f, 1), 5000);
        Check("Runner(0) still counts", ScoreFormulas.Runner(0), 1);
        Check("Runner(250)", ScoreFormulas.Runner(250), 250);
    }

    async Awaitable<bool> EnsureLoggedIn()
    {
        RestoreResult restore = await Services.Auth.RestoreAsync();
        Debug.Log($"Restore: {restore}");
        string password = TestCredentials.Password;
        if (!Services.Auth.IsLoggedIn && restore != RestoreResult.NetworkError)
        {
            if (string.IsNullOrEmpty(password))
                Debug.LogWarning("No test password set: GamesHub → Test Account Password…");
            else
            {
                LoginResult login = await Services.Auth.LoginAsync(email, password);
                Debug.Log($"Login: {login}");
            }
        }

        Check("logged in", Services.Auth.IsLoggedIn);
        if (Services.Auth.IsLoggedIn) Debug.Log($"Testing as {Services.Auth.Current.user.Tag}");
        return Services.Auth.IsLoggedIn;
    }

    static GameId Find(string slug)
    {
        foreach (GameId game in GameIds.All)
            if (game.Slug == slug) return game;
        throw new ArgumentException($"No game with slug \"{slug}\" in GameIds.All");
    }

    static bool IsNewestFirst(HistoryResult history)
    {
        for (int i = 1; i < history.Scores.Count; i++)
            if (string.CompareOrdinal(history.Scores[i - 1].puzzleDate, history.Scores[i].puzzleDate) < 0) return false;
        return true;
    }

    void Check(string label, bool ok) => Report(label, ok, ok ? "true" : "false");

    void Check<T>(string label, T actual, params T[] expected)
    {
        bool ok = expected.Contains(actual);
        Report(label, ok, ok ? $"{actual}" : $"{actual}, expected {string.Join(" or ", expected)}");
    }

    void Report(string label, bool ok, string detail)
    {
        if (ok) { passed++; Debug.Log($"PASS {label}: {detail}"); }
        else    { failed++; Debug.LogError($"FAIL {label}: {detail}"); }
    }
}
