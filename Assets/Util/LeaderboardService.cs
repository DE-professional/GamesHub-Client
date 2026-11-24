using System;
using UnityEngine;

/// <summary>One player's entry on a leaderboard. The same shape on both boards and in <c>me</c>.</summary>
[Serializable] public class LeaderboardEntry
{
    /// <summary>From the server, 1-based. Ties share a rank (1, 2, 2, 4): show it as sent, never count rows.</summary>
    public int rank;
    public LeaderboardPlayer player;
    public int score;

    /// <summary>UTC date of the score, <c>yyyy-MM-dd</c>. A calendar date: never convert it to local time.</summary>
    public string puzzleDate;

    /// <summary>When the score was stored, as an ISO 8601 UTC timestamp.</summary>
    public string submittedAt;

    /// <summary>The full tag, e.g. <c>Name#4821</c>. Unique per board: each player appears at most once.</summary>
    public string Tag => player == null ? "" : $"{player.displayName}#{player.discriminator}";
}

[Serializable] public class LeaderboardPlayer
{
    public string displayName;
    public int discriminator;
}

/// <summary>One page of a board, as the server sends it.</summary>
/// <remarks>
/// Parsed with <see cref="JsonUtility"/>, which has no nullable types: a JSON <c>null</c> for
/// <see cref="nextOffset"/> reads as 0, and for <see cref="me"/> as an entry with rank 0. The server
/// never sends either value (offsets after a page are ≥ 1, ranks start at 1), so read them only
/// through <see cref="HasMorePages"/> and <see cref="HasMe"/>.
/// </remarks>
[Serializable] public class LeaderboardPage
{
    public string game;

    /// <summary>The formula version this board ranks. Scores from other versions are left out.</summary>
    public int formulaVersion;

    /// <summary>The board's date on the daily board; empty on the all-time board.</summary>
    public string puzzleDate;

    /// <summary>In display order. Never null after <see cref="LeaderboardService"/> returns it.</summary>
    public LeaderboardEntry[] entries;

    /// <summary>The offset of the next page. Use <see cref="HasMorePages"/>: 0 here means none.</summary>
    public int nextOffset;

    /// <summary>The logged-in player's own entry, wherever it is on the board. Use <see cref="HasMe"/>.</summary>
    public LeaderboardEntry me;

    /// <summary>Whether a page follows this one (<c>nextOffset</c> wasn't null).</summary>
    public bool HasMorePages => nextOffset > 0;

    /// <summary>Whether <see cref="me"/> is a real entry (it wasn't null: logged in, with a score on this board).</summary>
    public bool HasMe => me != null && me.rank > 0;
}

/// <summary>Which board to read.</summary>
public enum LeaderboardKind
{
    /// <summary>Every player's score for one UTC day.</summary>
    Daily,

    /// <summary>Every player's best single day.</summary>
    AllTime
}

/// <summary>Outcome of a <see cref="LeaderboardService"/> request.</summary>
public enum LeaderboardOutcome
{
    Success,

    /// <summary>The server couldn't be reached. Offer a retry.</summary>
    NetworkError,

    /// <summary>
    /// The token was rejected. <see cref="ApiClient.Unauthorized"/> has already logged the player
    /// out; the board can be read again as a guest.
    /// </summary>
    NotLoggedIn,

    /// <summary>A server failure (5xx, unreadable response) or a client bug (other 4xx, logged). Offer a retry.</summary>
    Error
}

/// <summary>Result of a <see cref="LeaderboardService"/> request. <see cref="Page"/> is set only on success.</summary>
public readonly struct LeaderboardResult
{
    public readonly LeaderboardOutcome Outcome;
    public readonly LeaderboardPage Page;

    public bool Ok => Outcome == LeaderboardOutcome.Success;

    public LeaderboardResult(LeaderboardOutcome outcome, LeaderboardPage page)
    {
        Outcome = outcome;
        Page = page;
    }
}

/// <summary>
/// Reads the daily and all-time leaderboards (<c>GET /games/:slug/leaderboard/...</c>).
/// Access it through <c>Util.Services.Leaderboards</c>.
/// </summary>
/// <remarks>
/// <para>
/// Boards are readable without login. The token is sent whenever there is one, so a logged-in
/// player's response includes <see cref="LeaderboardPage.me"/>; a rejected token logs them out
/// through <see cref="ApiClient.Unauthorized"/>.
/// </para>
/// <para>
/// Responses are never cached here: they change with every submission. The server has already
/// ranked, ordered and filtered by formula version; don't redo any of it.
/// </para>
/// </remarks>
public class LeaderboardService
{
    /// <summary>The server's maximum page size.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The server's default page size.</summary>
    public const int DefaultPageSize = 50;

    readonly ApiClient api;

    public LeaderboardService(ApiClient api) => this.api = api;

    /// <summary>One page of a board. Await exactly once.</summary>
    /// <param name="puzzleDate">Daily board only: a UTC date from <see cref="DailyPuzzle.TodayUtc"/> or earlier. Ignored for all-time.</param>
    /// <param name="offset">0 for the first page, or a previous page's <c>nextOffset</c>.</param>
    /// <param name="limit">Entries per page, 1 to <see cref="MaxPageSize"/>.</param>
    public async Awaitable<LeaderboardResult> GetPageAsync(GameId game, LeaderboardKind kind, string puzzleDate,
        int offset, int limit = DefaultPageSize)
    {
        offset = Math.Max(0, offset);
        limit = Mathf.Clamp(limit, 1, MaxPageSize);

        string path = kind == LeaderboardKind.Daily
            ? $"/games/{game.Slug}/leaderboard/daily?puzzleDate={Uri.EscapeDataString(puzzleDate)}&limit={limit}&offset={offset}"
            : $"/games/{game.Slug}/leaderboard/all-time?limit={limit}&offset={offset}";

        var r = await api.Get<LeaderboardPage>(path);

        if (r.Ok && r.Data != null)
        {
            r.Data.entries ??= Array.Empty<LeaderboardEntry>();
            return new LeaderboardResult(LeaderboardOutcome.Success, r.Data);
        }

        if (r.Outcome == ApiOutcome.NetworkError) return new LeaderboardResult(LeaderboardOutcome.NetworkError, null);
        if (r.Outcome == ApiOutcome.HttpError && r.Status == 401) return new LeaderboardResult(LeaderboardOutcome.NotLoggedIn, null);

        // Bad slug, date, limit or offset: a client bug. puzzle_date_in_future usually means a local date was sent.
        if (r.Outcome == ApiOutcome.HttpError && r.Status >= 400 && r.Status < 500)
            Debug.LogError($"Leaderboard {game} {kind} rejected: {r.Status} {r.ErrorCode}");
        else
            Debug.LogWarning($"Leaderboard {game} {kind} failed: {r.Outcome} {r.Status} {r.ErrorCode}");
        return new LeaderboardResult(LeaderboardOutcome.Error, null);
    }
}
