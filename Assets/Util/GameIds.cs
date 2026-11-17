/// <summary>
/// One game as the server knows it: its permanent slug and the version of its score formula.
/// </summary>
/// <remarks>
/// Scores from different formula versions are never compared, so raise
/// <see cref="FormulaVersion"/> whenever a game's formula in <see cref="ScoreFormulas"/> changes.
/// </remarks>
public readonly struct GameId
{
    /// <summary>The server's slug for the game. Permanent: never change one in use.</summary>
    public readonly string Slug;

    /// <summary>Version of the game's score formula, starting at 1.</summary>
    public readonly int FormulaVersion;

    public GameId(string slug, int formulaVersion)
    {
        Slug = slug;
        FormulaVersion = formulaVersion;
    }

    public override string ToString() => $"{Slug} v{FormulaVersion}";
}

/// <summary>
/// Every game's <see cref="GameId"/>. Use these for <see cref="DailyPuzzle.Seed"/> and for
/// score submission, so the two can never use different slugs.
/// </summary>
public static class GameIds
{
    public static readonly GameId Minesweeper = new("minesweeper", 1);
    public static readonly GameId Sudoku      = new("sudoku", 1);
    public static readonly GameId PriceIsNice = new("price-is-nice", 1);
    public static readonly GameId Blackjack   = new("blackjack", 1);
    public static readonly GameId Runner      = new("runner", 1);

    public static readonly GameId[] All = { Minesweeper, Sudoku, PriceIsNice, Blackjack, Runner };

    /// <summary>Finds a game by its slug. False for null, empty, or unknown slugs.</summary>
    public static bool TryFind(string slug, out GameId game)
    {
        foreach (GameId g in All)
        {
            if (g.Slug == slug)
            {
                game = g;
                return true;
            }
        }
        game = default;
        return false;
    }
}
