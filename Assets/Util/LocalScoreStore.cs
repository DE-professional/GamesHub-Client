using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Keeps guest scores on the device, in PlayerPrefs (IndexedDB on WebGL). Follows the same
/// one-score-per-game-per-day rule as the server.
/// </summary>
/// <remarks>
/// Guest scores are never uploaded. They're discarded when the guest registers
/// (<see cref="DiscardAll"/>); the registration screen should warn about this.
/// </remarks>
public class LocalScoreStore : IScoreStore
{
    [Serializable] class ScoreList
    {
        public List<ScoreRecord> items = new();
    }

    const string KeyPrefix = "GamesHub.GuestScores.";

    /// <summary>Oldest scores are dropped beyond this, to keep PlayerPrefs small (WebGL allows 1 MB in total).</summary>
    const int MaxScoresPerGame = 366;

    // The interface is async for the API store; this one finishes immediately.
#pragma warning disable CS1998
    public async Awaitable<SubmitResult> SubmitAsync(GameId game, string puzzleDate, int score)
    {
        ScoreList list = Load(game.Slug);
        if (list.items.Exists(s => s.puzzleDate == puzzleDate)) return SubmitResult.AlreadySubmitted;

        list.items.Add(new ScoreRecord
        {
            game = game.Slug,
            puzzleDate = puzzleDate,
            score = score,
            formulaVersion = game.FormulaVersion,
            submittedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
        });
        SortNewestFirst(list.items);
        if (list.items.Count > MaxScoresPerGame)
            list.items.RemoveRange(MaxScoresPerGame, list.items.Count - MaxScoresPerGame);

        Save(game.Slug, list);
        return SubmitResult.Accepted;
    }

    public async Awaitable<PlayedStatus> GetPlayedStatusAsync(GameId game, string puzzleDate) =>
        Load(game.Slug).items.Exists(s => s.puzzleDate == puzzleDate) ? PlayedStatus.Played : PlayedStatus.NotPlayed;

    public async Awaitable<HistoryResult> GetHistoryAsync(GameId game)
    {
        List<ScoreRecord> scores = Load(game.Slug).items.FindAll(s => s.formulaVersion == game.FormulaVersion);
        SortNewestFirst(scores);
        return new HistoryResult(HistoryOutcome.Success, scores);
    }
#pragma warning restore CS1998

    /// <summary>Deletes every guest score on the device. Call when a guest registers.</summary>
    public void DiscardAll() => DeleteAll();

    /// <summary>Deletes every guest score on the device. Static for development tools that run outside Play mode.</summary>
    public static void DeleteAll()
    {
        foreach (GameId game in GameIds.All) PlayerPrefs.DeleteKey(KeyPrefix + game.Slug);
        PlayerPrefs.Save();
    }

    static ScoreList Load(string slug)
    {
        string json = PlayerPrefs.GetString(KeyPrefix + slug, "");
        if (string.IsNullOrEmpty(json)) return new ScoreList();
        try
        {
            return JsonUtility.FromJson<ScoreList>(json) ?? new ScoreList();
        }
        catch (ArgumentException e)
        {
            Debug.LogWarning($"Guest scores for {slug} are unreadable and were reset: {e.Message}");
            return new ScoreList();
        }
    }

    static void Save(string slug, ScoreList list)
    {
        PlayerPrefs.SetString(KeyPrefix + slug, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }

    // yyyy-MM-dd strings sort chronologically with an ordinal comparison.
    static void SortNewestFirst(List<ScoreRecord> scores) =>
        scores.Sort((a, b) => string.CompareOrdinal(b.puzzleDate, a.puzzleDate));
}
