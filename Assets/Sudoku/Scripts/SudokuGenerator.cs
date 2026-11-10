using System;
using System.Collections.Generic;

public class SudokuGenerator
{
    private string date;
    private System.Random rand;

    public SudokuGenerator()
    {
        date = DailyPuzzle.TodayUtc();
        rand = new Random(DailyPuzzle.Seed("sudoku", date));
    }
    public bool unUsedInBox(int[,] grid, int rowStart, int colStart, int num)
    {
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                if (grid[rowStart + i, colStart + j] == num)
                    return false;
                    
        return true;
    }

    public void fillBox(int[,] grid, int row, int col)
    {
        int num;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                do 
                {
                    num = rand.Next(1, 10);
                } while (!unUsedInBox(grid, row, col, num));
                grid[row + i, col + j] = num;
            }
    }
    
    public bool unUsedInRow(int[,] grid, int i, int num)
    {
        for (int j = 0; j < 9; j++)
            if (grid[i, j] == num)
                return false;
                
        return true;
    }

    public bool unUsedInCol(int[,] grid, int j, int num)
    {
        for (int i = 0; i < 9; i++)
            if (grid[i, j] == num)
                return false;
                
        return true;
    }

    public bool checkIfSafe(int[,] grid, int i, int j, int num)
    {
        return (unUsedInRow(grid, i, num) && unUsedInCol(grid, j, num) &&
                unUsedInBox(grid, i - i % 3, j - j % 3, num));
    }

    public void fillDiagonal(int[,] grid)
    {
        for (int i = 0; i < 9; i += 3)
            fillBox(grid, i, i);
    }

    public  bool fillRemaining(int[,] grid, int i, int j)
    {
        if (i == 9)
            return true;

        if (j == 9)
            return fillRemaining(grid, i + 1, 0);
        
        if (grid[i, j] != 0)
            return fillRemaining(grid, i, j + 1);

        for (int num = 1; num <= 9; num++)
            if (checkIfSafe(grid, i, j, num))
            {
                grid[i, j] = num;
                if (fillRemaining(grid, i, j + 1))
                    return true;

                grid[i, j] = 0;
            }

        return false;
    }

    //public  void removeKDigits(int[,] grid, int k)
    //{
    //    Random rand = new Random();
    //    while (k > 0)
    //    {
    //        int cellId = rand.Next(81);
    //        int i = cellId / 9;
    //        int j = cellId % 9;
    //        
    //        if (grid[i, j] != 0)
    //        {
    //            grid[i, j] = 0;
    //            k--;
    //        }
    //    }
    //}

    public  int[,] sudokuAnswerGenerator()
    {
        int[,] grid = new int[9, 9];
        fillDiagonal(grid);
        fillRemaining(grid, 0, 0);
        return grid;
    }
    
    //public  int[,] sudokuPuzzleGenerator(int[,] answerKey, int k)
    //{
    //    removeKDigits(answerKey, k);
    //    return answerKey;
    //}

    public  int[,] sudokuPuzzleGenerator(int[,] answerKey, int cellsToRemove)
    {
        int[,] puzzle = (int[,])answerKey.Clone();

        List<(int row, int col)> positions = new List<(int, int)>();
        for (int r = 0; r < 9; r++)
            for (int c = 0; c < 9; c++)
                positions.Add((r, c));

        Shuffle(positions);

        int removed = 0;
        foreach (var pos in positions)
        {
            if (removed >= cellsToRemove) break;

            int backup = puzzle[pos.row, pos.col];
            puzzle[pos.row, pos.col] = 0;

            int solutions = SudokuSolver.CountSolutions(puzzle, limit: 2);

            if (solutions == 1)
            {
                removed++;
            }
            else
            {
                puzzle[pos.row, pos.col] = backup;
            }
        }

        return puzzle;
    }

    private void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rand.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}