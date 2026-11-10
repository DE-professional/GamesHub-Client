using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Picks each game's daily puzzle on the client. The same slug and date give the same seed
/// on every device, so every player gets the same puzzle without asking the server.
/// </summary>
/// <remarks>
/// Call <see cref="TodayUtc"/> once when a puzzle starts, and use that one string both for
/// <see cref="Seed"/> and as <c>puzzleDate</c> when submitting the score.
/// </remarks>
public static class DailyPuzzle
{
    /// <summary>Today's UTC date as <c>yyyy-MM-dd</c>: the <c>puzzleDate</c> the server expects.</summary>
    /// <remarks>
    /// InvariantCulture is required: some device cultures use non-Gregorian calendars
    /// (e.g. th-TH formats this year as 2569).
    /// </remarks>
    public static string TodayUtc() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The puzzle seed for one game on one date. Always non-negative.</summary>
    /// <param name="slug">The game's slug, exactly as the server uses it (e.g. <c>sudoku</c>).</param>
    /// <param name="puzzleDate">A date from <see cref="TodayUtc"/>.</param>
    /// <remarks>
    /// Known answers (check these on every platform you build for):
    /// <c>Seed("sudoku", "2026-11-10")</c> = 192043695,
    /// <c>Seed("sudoku", "2026-11-11")</c> = 1363850,
    /// <c>Seed("minesweeper", "2026-11-10")</c> = 1020838125.
    /// </remarks>
    public static int Seed(string slug, string puzzleDate)
    {
        unchecked
        {
            // FNV-1a (32-bit) over the UTF-8 bytes. Never string.GetHashCode(): its result
            // isn't guaranteed to be the same across runtimes, platforms, or even runs.
            uint h = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(slug + ":" + puzzleDate))
            {
                h ^= b;
                h *= 16777619;
            }

            // MurmurHash3 finalizer. Consecutive dates differ in one character, which FNV-1a
            // alone leaves mostly in the low bits; this spreads the change across all of them.
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;

            return (int)(h & 0x7FFFFFFF);
        }
    }
}
