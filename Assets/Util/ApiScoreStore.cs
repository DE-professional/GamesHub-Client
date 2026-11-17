using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saves and reads the logged-in player's scores on the server
/// (<c>POST /games/:slug/scores</c>, <c>GET /games/:slug/scores/me</c>).
/// </summary>
/// <remarks>
/// Every request needs the token on the shared <see cref="ApiClient"/>. A rejected token
/// raises <see cref="ApiClient.Unauthorized"/>, which logs the player out.
/// </remarks>
public class ApiScoreStore : IScoreStore
{
    [Serializable] struct SubmitBody
    {
        public string puzzleDate;
        public int score;
        public int formulaVersion;
    }

    [Serializable] class SubmitResponse
    {
        public ScoreRecord score;
    }

    [Serializable] class HistoryPage
    {
        public ScoreRecord[] scores;
        public string nextBefore;
    }

    /// <summary>The server's maximum page size.</summary>
    const string PageSize = "100";

    /// <summary>Guards against a server bug looping forever: 50 pages is over 13 years of daily scores.</summary>
    const int MaxPages = 50;

    readonly ApiClient api;

    public ApiScoreStore(ApiClient api) => this.api = api;

    public async Awaitable<SubmitResult> SubmitAsync(GameId game, string puzzleDate, int score)
    {
        var body = new SubmitBody { puzzleDate = puzzleDate, score = score, formulaVersion = game.FormulaVersion };
        var r = await api.Post<SubmitResponse>($"/games/{game.Slug}/scores", body);

        switch (r.Outcome)
        {
            case ApiOutcome.Success:      return SubmitResult.Accepted;
            case ApiOutcome.NetworkError: return SubmitResult.NetworkError;
            // A 2xx means the score was stored; a retry will get already_submitted and be dropped.
            case ApiOutcome.ParseError:   return SubmitResult.ServerError;
        }

        switch (r.ErrorCode)
        {
            case "already_submitted":  return SubmitResult.AlreadySubmitted;
            case "puzzle_date_closed": return SubmitResult.DateClosed;
        }

        if (r.Status == 401) return SubmitResult.NotLoggedIn;
        if (r.Status >= 400 && r.Status < 500)
        {
            Debug.LogError($"Score for {game} on {puzzleDate} rejected: {r.Status} {r.ErrorCode}");
            return SubmitResult.Rejected;
        }
        return SubmitResult.ServerError;
    }

    public async Awaitable<PlayedStatus> GetPlayedStatusAsync(GameId game, string puzzleDate)
    {
        // The newest score is enough: puzzle dates can't be later than today.
        var r = await api.Get<HistoryPage>($"/games/{game.Slug}/scores/me?limit=1");
        if (!r.Ok || r.Data == null) return PlayedStatus.Unknown;

        ScoreRecord[] scores = r.Data.scores;
        return scores != null && scores.Length > 0 && scores[0].puzzleDate == puzzleDate
            ? PlayedStatus.Played
            : PlayedStatus.NotPlayed;
    }

    public async Awaitable<HistoryResult> GetHistoryAsync(GameId game)
    {
        var all = new List<ScoreRecord>();
        string before = null;

        for (int page = 0; page < MaxPages; page++)
        {
            string path = $"/games/{game.Slug}/scores/me?limit={PageSize}";
            if (before != null) path += "&before=" + before;

            var r = await api.Get<HistoryPage>(path);
            if (!r.Ok || r.Data == null) return new HistoryResult(ToHistoryOutcome(r, game), null);

            if (r.Data.scores != null)
                foreach (ScoreRecord s in r.Data.scores)
                    if (s.formulaVersion == game.FormulaVersion) all.Add(s);

            // JsonUtility can read a JSON null string as "" rather than null.
            string next = string.IsNullOrEmpty(r.Data.nextBefore) ? null : r.Data.nextBefore;
            if (next == null) return new HistoryResult(HistoryOutcome.Success, all);
            if (next == before)
            {
                Debug.LogWarning($"Score history for {game}: nextBefore did not advance ({next}); stopping.");
                break;
            }
            before = next;
        }

        return new HistoryResult(HistoryOutcome.Success, all);
    }

    static HistoryOutcome ToHistoryOutcome<T>(ApiResult<T> r, GameId game)
    {
        if (r.Outcome == ApiOutcome.NetworkError) return HistoryOutcome.NetworkError;
        if (r.Outcome == ApiOutcome.HttpError && r.Status == 401) return HistoryOutcome.NotLoggedIn;
        if (r.Outcome == ApiOutcome.HttpError && r.Status >= 400 && r.Status < 500)
            Debug.LogError($"Score history for {game} rejected: {r.Status} {r.ErrorCode}");
        return HistoryOutcome.ServerError;
    }
}
