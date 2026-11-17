using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// The game-wide score interface (<c>Util.Services.Scores</c>). Sends a logged-in player's
/// scores to the server and keeps a guest's on the device, so game code never has to check
/// which one applies.
/// </summary>
/// <remarks>
/// <para>
/// <b>Who counts as a guest:</b> nobody is logged in and no login token is saved. A saved token
/// that hasn't been restored yet (e.g. offline at launch) means an account: its scores are
/// queued for the server, never saved as guest scores.
/// </para>
/// <para>
/// <b>Queue:</b> a score that can't reach the server (offline, server error, expired session)
/// is saved on the device and returns <see cref="SubmitResult.Queued"/>. Queued scores are sent
/// when a session starts (login or restore) and before each new submission, as long as the
/// server still accepts their date: today, or yesterday during the first hour after midnight UTC.
/// A queued score is only sent for the account that earned it.
/// </para>
/// <para>
/// <b>One attempt per day</b> is the client's job. Call <see cref="MarkAttemptStarted"/> when
/// the player starts a puzzle and check <see cref="GetPlayedStatusAsync"/> before letting them.
/// </para>
/// Await each returned <see cref="Awaitable"/> exactly once.
/// </remarks>
public class ScoreService : IScoreStore
{
    [Serializable] class PendingScore
    {
        public string slug;
        public int formulaVersion;
        public string puzzleDate;
        public int score;

        /// <summary>The account's tag, or empty when the score was earned before a saved login was restored.</summary>
        public string userTag;
    }

    [Serializable] class PendingList
    {
        public List<PendingScore> items = new();
    }

    const string PendingKey = "GamesHub.PendingScores";
    const string AttemptKeyPrefix = "GamesHub.Attempt.";

    readonly AuthService auth;
    readonly IScoreStore remote;
    readonly LocalScoreStore local;
    bool flushing;

    /// <param name="auth">Decides which store is used, and starts sending queued scores on login.</param>
    /// <param name="remote">The server store, built on the same <see cref="ApiClient"/> as <paramref name="auth"/>.</param>
    /// <param name="local">The guest store.</param>
    public ScoreService(AuthService auth, IScoreStore remote, LocalScoreStore local)
    {
        this.auth = auth;
        this.remote = remote;
        this.local = local;
        auth.SessionChanged += session => { if (session != null) FlushInBackground(); };
    }

    /// <summary>
    /// Saves a finished game's score. Returns <see cref="SubmitResult.Accepted"/>,
    /// <see cref="SubmitResult.Queued"/>, <see cref="SubmitResult.AlreadySubmitted"/>,
    /// <see cref="SubmitResult.DateClosed"/> or <see cref="SubmitResult.Rejected"/>.
    /// </summary>
    public async Awaitable<SubmitResult> SubmitAsync(GameId game, string puzzleDate, int score)
    {
        if (!auth.IsLoggedIn)
        {
            if (!HasSavedLogin) return await local.SubmitAsync(game, puzzleDate, score);
            Enqueue(game, puzzleDate, score, "");
            return SubmitResult.Queued;
        }

        string tag = auth.Current.user.Tag;
        SubmitResult r = await remote.SubmitAsync(game, puzzleDate, score);
        switch (r)
        {
            case SubmitResult.NetworkError:
            case SubmitResult.ServerError:
            case SubmitResult.NotLoggedIn:
                Enqueue(game, puzzleDate, score, tag);
                return SubmitResult.Queued;
        }

        // The server is reachable: a good moment to send anything left over.
        FlushInBackground();
        return r;
    }

    /// <summary>
    /// Whether the game's puzzle for <paramref name="puzzleDate"/> was already played: started on
    /// this device (<see cref="MarkAttemptStarted"/>), waiting in the queue, or stored on the server
    /// or in the guest scores. <see cref="PlayedStatus.Unknown"/> when the server can't be asked.
    /// </summary>
    public async Awaitable<PlayedStatus> GetPlayedStatusAsync(GameId game, string puzzleDate)
    {
        if (PlayerPrefs.GetString(AttemptKeyPrefix + game.Slug, "") == puzzleDate) return PlayedStatus.Played;
        if (LoadPending().items.Exists(p => p.slug == game.Slug && p.puzzleDate == puzzleDate)) return PlayedStatus.Played;

        if (auth.IsLoggedIn) return await remote.GetPlayedStatusAsync(game, puzzleDate);
        if (HasSavedLogin) return PlayedStatus.Unknown;
        return await local.GetPlayedStatusAsync(game, puzzleDate);
    }

    /// <summary>
    /// The player's scores for the game's current formula version, newest first: from the server
    /// when logged in, from the device for a guest. <see cref="HistoryOutcome.NotLoggedIn"/> while a
    /// saved login hasn't been restored. Queued scores are not included.
    /// </summary>
    public async Awaitable<HistoryResult> GetHistoryAsync(GameId game)
    {
        if (auth.IsLoggedIn) return await remote.GetHistoryAsync(game);
        if (HasSavedLogin) return new HistoryResult(HistoryOutcome.NotLoggedIn, null);
        return await local.GetHistoryAsync(game);
    }

    /// <summary>
    /// Records on the device that the player started this puzzle, so quitting and restarting
    /// doesn't give a second attempt. Call when play actually begins, not when the scene loads.
    /// </summary>
    public void MarkAttemptStarted(GameId game, string puzzleDate)
    {
        PlayerPrefs.SetString(AttemptKeyPrefix + game.Slug, puzzleDate);
        PlayerPrefs.Save();
    }

    /// <summary>Deletes every guest score on the device. Call when a guest registers.</summary>
    public void DiscardGuestScores() => local.DiscardAll();

    /// <summary>
    /// Development only: forgets everything on this device that marks a puzzle as played, for every
    /// game: attempt marks, queued scores (which are lost, not sent) and guest scores. The saved login
    /// is kept. Scores already on the server still count; delete those in the database.
    /// Used by the <c>GamesHub → Reset Played State</c> Editor menu.
    /// </summary>
    public static void ResetLocalPlayedState()
    {
        foreach (GameId game in GameIds.All) PlayerPrefs.DeleteKey(AttemptKeyPrefix + game.Slug);
        PlayerPrefs.DeleteKey(PendingKey);
        LocalScoreStore.DeleteAll(); // also saves PlayerPrefs
    }

    /// <summary>
    /// Sends queued scores for the logged-in account. Runs automatically on login and after each
    /// submission; call it yourself only to retry sooner. Does nothing when logged out or
    /// already running.
    /// </summary>
    public async Awaitable FlushPendingAsync()
    {
        if (flushing || !auth.IsLoggedIn) return;
        flushing = true;
        try
        {
            string tag = auth.Current.user.Tag;
            DropClosedPending();

            foreach (PendingScore p in LoadPending().items)
            {
                if (p.userTag != "" && p.userTag != tag) continue;

                SubmitResult r = await remote.SubmitAsync(new GameId(p.slug, p.formulaVersion), p.puzzleDate, p.score);
                if (r == SubmitResult.NetworkError || r == SubmitResult.ServerError || r == SubmitResult.NotLoggedIn)
                    return; // Keep it and the rest for the next attempt.

                RemovePending(p);
                if (r == SubmitResult.DateClosed || r == SubmitResult.Rejected)
                    Debug.LogWarning($"Queued score for {p.slug} on {p.puzzleDate} was dropped: {r}");

                // A logout or another login happened while the request was in flight.
                if (!auth.IsLoggedIn || auth.Current.user.Tag != tag) return;
            }
        }
        finally
        {
            flushing = false;
        }
    }

    /// <summary>A login token is saved, whether or not it has been restored yet.</summary>
    static bool HasSavedLogin => PlayerPrefs.HasKey(AuthService.TokenKey);

    async void FlushInBackground()
    {
        try
        {
            await FlushPendingAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    void Enqueue(GameId game, string puzzleDate, int score, string userTag)
    {
        PendingList list = LoadPending();
        // One score per game per day: the first one stands, as on the server.
        if (list.items.Exists(p => p.slug == game.Slug && p.puzzleDate == puzzleDate && p.userTag == userTag)) return;

        list.items.Add(new PendingScore
        {
            slug = game.Slug,
            formulaVersion = game.FormulaVersion,
            puzzleDate = puzzleDate,
            score = score,
            userTag = userTag
        });
        SavePending(list);
    }

    // Reloads before removing: Enqueue may have added scores while a request was in flight.
    void RemovePending(PendingScore sent)
    {
        PendingList list = LoadPending();
        list.items.RemoveAll(p => p.slug == sent.slug && p.puzzleDate == sent.puzzleDate && p.userTag == sent.userTag);
        SavePending(list);
    }

    /// <summary>Drops queued scores whose day the server no longer accepts.</summary>
    void DropClosedPending()
    {
        // The server accepts the UTC dates of now and of now − 1 hour.
        string earliestOpen = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        PendingList list = LoadPending();
        int removed = list.items.RemoveAll(p => string.CompareOrdinal(p.puzzleDate, earliestOpen) < 0);
        if (removed == 0) return;

        Debug.LogWarning($"Dropped {removed} queued score(s) whose day has closed.");
        SavePending(list);
    }

    static PendingList LoadPending()
    {
        string json = PlayerPrefs.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return new PendingList();
        try
        {
            PendingList list = JsonUtility.FromJson<PendingList>(json) ?? new PendingList();
            foreach (PendingScore p in list.items) p.userTag ??= "";
            return list;
        }
        catch (ArgumentException e)
        {
            Debug.LogWarning($"Queued scores are unreadable and were reset: {e.Message}");
            return new PendingList();
        }
    }

    static void SavePending(PendingList list)
    {
        if (list.items.Count == 0) PlayerPrefs.DeleteKey(PendingKey);
        else PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }
}
