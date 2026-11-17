using System;
using UnityEngine;

/// <summary>The Price Is Nice puzzle as the server sends it (<c>GET /games/price-is-nice/puzzle</c>).</summary>
[Serializable] public class PriceIsNicePuzzleData
{
    /// <summary>UTC puzzle date, <c>yyyy-MM-dd</c>.</summary>
    public string puzzleDate;
    public int steamAppId;
    public string name;
    public string shortDescription;
    public PriceData price;

    /// <summary>Screenshot URLs on Steam's CDN, in Steam's order. The CDN allows cross-origin requests, so WebGL can load them.</summary>
    public string[] screenshots;

    [Serializable] public class PriceData
    {
        /// <summary>US price in cents, after any discount at the time the game was picked.</summary>
        public int cents;

        /// <summary>The price as Steam formats it, e.g. <c>$9.99</c>.</summary>
        public string formatted;
    }
}

/// <summary>Outcome of <see cref="PuzzleService.GetPriceIsNiceAsync"/>.</summary>
public enum PuzzleOutcome
{
    Success,

    /// <summary>
    /// Try again shortly: the server couldn't pick the game yet (<c>503 puzzle_unavailable</c>),
    /// or the server couldn't be reached.
    /// </summary>
    Unavailable,

    /// <summary>The date is not open (<c>puzzle_date_closed</c>): it's no longer, or not yet, that day.</summary>
    DateClosed,

    /// <summary>Any other failure: a client bug or a server error. Logged.</summary>
    Error
}

/// <summary>Result of <see cref="PuzzleService.GetPriceIsNiceAsync"/>. <see cref="Data"/> is set only on success.</summary>
public readonly struct PuzzleResult
{
    public readonly PuzzleOutcome Outcome;
    public readonly PriceIsNicePuzzleData Data;

    public bool Ok => Outcome == PuzzleOutcome.Success;

    public PuzzleResult(PuzzleOutcome outcome, PriceIsNicePuzzleData data)
    {
        Outcome = outcome;
        Data = data;
    }
}

/// <summary>
/// Fetches daily puzzles the server picks (today only Price Is Nice; every other game builds its
/// puzzle from <see cref="DailyPuzzle.Seed"/>). Access it through <c>Util.Services.Puzzles</c>.
/// </summary>
/// <remarks>Requests are sent without the login token: guests play too.</remarks>
public class PuzzleService
{
    [Serializable] class PuzzleResponse
    {
        public PriceIsNicePuzzleData puzzle;
    }

    readonly ApiClient api;

    public PuzzleService(ApiClient api) => this.api = api;

    /// <summary>The Price Is Nice game for <paramref name="puzzleDate"/>. Await exactly once.</summary>
    /// <param name="puzzleDate">A date from <see cref="DailyPuzzle.TodayUtc"/>.</param>
    public async Awaitable<PuzzleResult> GetPriceIsNiceAsync(string puzzleDate)
    {
        var r = await api.Get<PuzzleResponse>(
            $"/games/{GameIds.PriceIsNice.Slug}/puzzle?puzzleDate={Uri.EscapeDataString(puzzleDate)}",
            authenticate: false);

        if (r.Ok)
        {
            PriceIsNicePuzzleData data = r.Data?.puzzle;
            if (data != null && !string.IsNullOrEmpty(data.name) && data.price != null && data.price.cents > 0)
                return new PuzzleResult(PuzzleOutcome.Success, data);

            Debug.LogWarning("Price Is Nice puzzle response is missing the game or its price.");
            return new PuzzleResult(PuzzleOutcome.Error, null);
        }

        if (r.Outcome == ApiOutcome.NetworkError || r.ErrorCode == "puzzle_unavailable")
            return new PuzzleResult(PuzzleOutcome.Unavailable, null);
        if (r.ErrorCode == "puzzle_date_closed")
            return new PuzzleResult(PuzzleOutcome.DateClosed, null);

        Debug.LogError($"Price Is Nice puzzle request failed: {r.Outcome} {r.Status} {r.ErrorCode}");
        return new PuzzleResult(PuzzleOutcome.Error, null);
    }
}
