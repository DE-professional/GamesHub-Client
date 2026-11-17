using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Util;

/// <summary>One day's Price Is Nice puzzle: the Steam game, its price and its screenshots.</summary>
public sealed class PriceIsNicePuzzle
{
    /// <summary>The UTC puzzle date this game was picked for.</summary>
    public readonly string PuzzleDate;
    public readonly string Name;
    public readonly string ShortDescription;

    /// <summary>The US price in dollars, e.g. 9.99.</summary>
    public readonly double ActualPrice;

    /// <summary>The price as Steam formats it, e.g. <c>$9.99</c>.</summary>
    public readonly string PriceFormatted;

    /// <summary>Downloaded screenshots, in Steam's order. May be fewer than the game has, or none.</summary>
    public readonly IReadOnlyList<Sprite> Screenshots;

    public PriceIsNicePuzzle(PriceIsNicePuzzleData data, IReadOnlyList<Sprite> screenshots)
    {
        PuzzleDate = data.puzzleDate;
        Name = data.name;
        ShortDescription = data.shortDescription ?? "";
        ActualPrice = data.price.cents / 100.0;
        PriceFormatted = data.price.formatted;
        Screenshots = screenshots;
    }
}

/// <summary>
/// Gets the day's Price Is Nice game from the server and downloads its screenshots, and keeps the
/// result across scene loads. The main menu calls <see cref="Preload"/> so the game scene usually
/// opens with everything ready; the game calls <see cref="WaitAsync"/>, which also starts the load
/// if nobody did.
/// </summary>
/// <remarks>
/// The server picks one game per date and serves it to everyone (<c>GET /games/price-is-nice/puzzle</c>),
/// so every player gets the same game. Screenshots come straight from Steam's CDN.
/// </remarks>
public static class PriceIsNiceLoader
{
    public enum LoadState { Idle, Loading, Ready, Failed }

    const int MaxScreenshots = 8;       // full-size screenshots are ~8 MB each in memory
    const int PuzzleAttempts = 3;       // the first request of a day can take a while: the server asks Steam
    const float RetryDelaySeconds = 3f;
    const int ScreenshotTimeoutSeconds = 20;

    static string loadDate;       // date of the current or last load
    static int generation;        // bumped per load; a load whose number is stale discards its result
    static PriceIsNicePuzzle current;
    static bool removedOldAppList;

    /// <summary>State of the load for the date last passed to <see cref="Preload"/>.</summary>
    public static LoadState State { get; private set; }

    // Statics survive between Play sessions when domain reload is disabled, but the sprites don't.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loadDate = null;
        generation++;
        current = null;
        State = LoadState.Idle;
    }

    /// <summary>
    /// Starts loading the puzzle for <paramref name="puzzleDate"/> in the background. Does nothing
    /// if it is already loading or loaded; retries after a failure. A different date replaces (and
    /// frees) the previous puzzle.
    /// </summary>
    public static void Preload(string puzzleDate)
    {
        if (loadDate == puzzleDate && (State == LoadState.Loading || State == LoadState.Ready)) return;

        RemoveOldAppList();
        Release();
        loadDate = puzzleDate;
        State = LoadState.Loading;
        Load(puzzleDate, ++generation);
    }

    /// <summary>
    /// The puzzle for <paramref name="puzzleDate"/>, starting the load if needed. Null if it
    /// couldn't be loaded (offline, or the server couldn't provide it). Await exactly once.
    /// </summary>
    public static async Awaitable<PriceIsNicePuzzle> WaitAsync(string puzzleDate)
    {
        Preload(puzzleDate);
        while (State == LoadState.Loading && loadDate == puzzleDate)
            await Awaitable.NextFrameAsync();

        return State == LoadState.Ready && current != null && current.PuzzleDate == puzzleDate ? current : null;
    }

    static async void Load(string puzzleDate, int loadGeneration)
    {
        PriceIsNicePuzzle result = null;
        try
        {
            result = await LoadPuzzle(puzzleDate, loadGeneration);
        }
        catch (OperationCanceledException)
        {
            return; // Play mode ended; ResetStatics starts over next time
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (loadGeneration != generation)
        {
            DestroySprites(result?.Screenshots); // superseded by a newer load
            return;
        }

        current = result;
        State = result != null ? LoadState.Ready : LoadState.Failed;
    }

    static async Awaitable<PriceIsNicePuzzle> LoadPuzzle(string puzzleDate, int loadGeneration)
    {
        PuzzleResult r = default;
        for (int attempt = 1; attempt <= PuzzleAttempts; attempt++)
        {
            r = await Services.Puzzles.GetPriceIsNiceAsync(puzzleDate);
            if (loadGeneration != generation) return null;
            if (r.Outcome != PuzzleOutcome.Unavailable) break;
            if (attempt < PuzzleAttempts) await Awaitable.WaitForSecondsAsync(RetryDelaySeconds);
        }

        if (!r.Ok)
        {
            Debug.LogWarning($"Price Is Nice: no puzzle for {puzzleDate} ({r.Outcome}).");
            return null;
        }

        List<Sprite> screenshots = await DownloadScreenshots(r.Data.screenshots);
        if (loadGeneration != generation)
        {
            DestroySprites(screenshots);
            return null;
        }
        return new PriceIsNicePuzzle(r.Data, screenshots);
    }

    // ---------- Screenshots ----------

    /// <summary>Downloads up to <see cref="MaxScreenshots"/> screenshots in parallel. Failed ones are skipped.</summary>
    static async Awaitable<List<Sprite>> DownloadScreenshots(string[] urls)
    {
        var requests = new List<UnityWebRequest>();
        var sprites = new List<Sprite>();
        try
        {
            if (urls != null)
            {
                foreach (string url in urls)
                {
                    if (requests.Count == MaxScreenshots) break;
                    if (string.IsNullOrEmpty(url)) continue;
                    // nonReadable: the texture lives on the GPU only, halving its memory
                    UnityWebRequest req = UnityWebRequestTexture.GetTexture(url, true);
                    req.timeout = ScreenshotTimeoutSeconds;
                    requests.Add(req);
                    req.SendWebRequest();
                }
            }

            foreach (UnityWebRequest req in requests)
            {
                while (!req.isDone) await Awaitable.NextFrameAsync();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Price Is Nice: screenshot failed to download ({req.responseCode} {req.error})");
                    continue;
                }
                Texture2D texture = DownloadHandlerTexture.GetContent(req);
                sprites.Add(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f)));
            }
        }
        finally
        {
            foreach (UnityWebRequest req in requests) req.Dispose();
        }
        return sprites;
    }

    /// <summary>Frees the current puzzle's textures, e.g. when the date changes.</summary>
    static void Release()
    {
        if (current == null) return;
        DestroySprites(current.Screenshots);
        current = null;
    }

    static void DestroySprites(IReadOnlyList<Sprite> sprites)
    {
        if (sprites == null) return;
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;
            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
        }
    }

    // Older builds cached Steam's whole app list (several MB) here; the server picks the game now.
    static void RemoveOldAppList()
    {
        if (removedOldAppList) return;
        removedOldAppList = true;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "applist.json");
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Price Is Nice: couldn't remove the old app list cache ({e.Message})");
        }
    }
}
