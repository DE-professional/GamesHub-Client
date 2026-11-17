using System;

/// <summary>
/// Every game's score formula, in one place. Higher is always better; a loss is
/// <see cref="Loss"/> (0) and any completed game scores at least 1, so the server can tell
/// them apart.
/// </summary>
/// <remarks>
/// Changing a formula changes what scores mean: raise that game's
/// <see cref="GameId.FormulaVersion"/> in <see cref="GameIds"/> at the same time.
/// </remarks>
public static class ScoreFormulas
{
    /// <summary>The server's upper limit for a score.</summary>
    public const int MaxScore = 1_000_000;

    /// <summary>The score for a lost game.</summary>
    public const int Loss = 0;

    /// <summary>Chips left after the day's hands. Running out of chips scores 0, a loss.</summary>
    public static int Blackjack(int chipsLeft) => Math.Clamp(chipsLeft, 0, MaxScore);

    /// <summary>
    /// Price Is Nice win: 5000 × (1 + triesLeft / maxTries) × 30 / (30 + seconds).
    /// A first-try guess in no time scores 10000; slower or later guesses score less.
    /// </summary>
    public static int PriceIsNice(int triesRemaining, int maxTries, float seconds) =>
        Win(5000.0 * (1 + (double)triesRemaining / maxTries) * (30.0 / (30.0 + seconds)));

    /// <summary>
    /// Minesweeper win: 10000 × 120 / (120 + seconds). Two minutes scores 5000, six minutes 2500.
    /// </summary>
    public static int Minesweeper(float seconds) => Win(10000.0 * 120.0 / (120.0 + seconds));

    /// <summary>
    /// Sudoku win: 10000 × 300 / (300 + seconds + 30 × mistakes). Each mistake costs as much as
    /// 30 seconds; five minutes without mistakes scores 5000.
    /// </summary>
    public static int Sudoku(float seconds, int mistakes) =>
        Win(10000.0 * 300.0 / (300.0 + seconds + 30.0 * mistakes));

    /// <summary>
    /// Runner: the distance shown on screen. Every run counts as completed, so the minimum is 1.
    /// </summary>
    public static int Runner(int distance) => Math.Clamp(distance, 1, MaxScore);

    /// <summary>Rounds a win's raw score and keeps it within 1..<see cref="MaxScore"/>.</summary>
    static int Win(double raw) =>
        (int)Math.Clamp(Math.Round(raw, MidpointRounding.AwayFromZero), 1, MaxScore);
}
