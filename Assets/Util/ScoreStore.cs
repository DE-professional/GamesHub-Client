using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One stored score. Same shape as the server's score object, so API responses parse
/// straight into it.
/// </summary>
[Serializable] public class ScoreRecord
{
    /// <summary>The game's slug.</summary>
    public string game;

    /// <summary>UTC date of the puzzle, <c>yyyy-MM-dd</c>. A calendar date: never convert it to local time.</summary>
    public string puzzleDate;

    public int score;
    public int formulaVersion;

    /// <summary>When the score was stored, as an ISO 8601 UTC timestamp.</summary>
    public string submittedAt;
}

/// <summary>Outcome of submitting a score.</summary>
public enum SubmitResult
{
    /// <summary>Stored: on the server for a player, on the device for a guest.</summary>
    Accepted,

    /// <summary>
    /// Couldn't reach the server, or the session ended. Saved on the device and sent
    /// automatically once the player is logged in again, if that day's puzzle is still open.
    /// Returned only by <see cref="ScoreService"/>.
    /// </summary>
    Queued,

    /// <summary>A score for this game and day already exists. The first one stands.</summary>
    AlreadySubmitted,

    /// <summary>The day's puzzle no longer accepts scores (past the 1-hour grace period).</summary>
    DateClosed,

    /// <summary>The server rejected the request as invalid. A client bug; logged as an error.</summary>
    Rejected,

    /// <summary>The server rejected the token. <see cref="ScoreService"/> turns this into <see cref="Queued"/>.</summary>
    NotLoggedIn,

    /// <summary>The server couldn't be reached. <see cref="ScoreService"/> turns this into <see cref="Queued"/>.</summary>
    NetworkError,

    /// <summary>A server failure (5xx, unreadable response). <see cref="ScoreService"/> turns this into <see cref="Queued"/>.</summary>
    ServerError
}

/// <summary>Whether a day's puzzle has been played.</summary>
public enum PlayedStatus
{
    NotPlayed,
    Played,

    /// <summary>Couldn't find out (server unreachable, or a saved login not yet restored).</summary>
    Unknown
}

/// <summary>Outcome of loading score history.</summary>
public enum HistoryOutcome
{
    Success,
    NotLoggedIn,
    NetworkError,
    ServerError
}

/// <summary>A player's score history for one game.</summary>
public readonly struct HistoryResult
{
    public readonly HistoryOutcome Outcome;

    /// <summary>
    /// Scores from the game's current formula version only, newest puzzle date first.
    /// Empty (never null) unless <see cref="Ok"/>.
    /// </summary>
    public readonly IReadOnlyList<ScoreRecord> Scores;

    public bool Ok => Outcome == HistoryOutcome.Success;

    public HistoryResult(HistoryOutcome outcome, IReadOnlyList<ScoreRecord> scores)
    {
        Outcome = outcome;
        Scores = scores ?? Array.Empty<ScoreRecord>();
    }
}

/// <summary>
/// Where scores are saved and read. <see cref="ApiScoreStore"/> uses the server for logged-in
/// players; <see cref="LocalScoreStore"/> keeps guest scores on the device.
/// <see cref="ScoreService"/> picks between them; game code should use <c>Services.Scores</c>.
/// </summary>
/// <remarks>Await each returned <see cref="Awaitable"/> exactly once.</remarks>
public interface IScoreStore
{
    /// <summary>Saves the score for one game's puzzle.</summary>
    /// <param name="puzzleDate">The date from <see cref="DailyPuzzle.TodayUtc"/> taken when the puzzle started.</param>
    /// <param name="score">From <see cref="ScoreFormulas"/>; <see cref="ScoreFormulas.Loss"/> for a loss.</param>
    Awaitable<SubmitResult> SubmitAsync(GameId game, string puzzleDate, int score);

    /// <summary>Whether the game's puzzle for <paramref name="puzzleDate"/> has already been played.</summary>
    Awaitable<PlayedStatus> GetPlayedStatusAsync(GameId game, string puzzleDate);

    /// <summary>All of the player's scores for the game's current formula version, newest first.</summary>
    Awaitable<HistoryResult> GetHistoryAsync(GameId game);
}
