using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

public class SudokuUI : MonoBehaviour
{
    public SudokuBoard board;
    public GameObject cellPrefab;
    public Transform boardContainer;
    public GameObject winPopup;
    public TMP_Text mistakesLabel;
    public TMP_Text timerLabel;
    public TMP_Text notesButtonLabel;
    public TMP_Text winPopupText;

    private CellView[,] cellViews = new CellView[9, 9];
    private CellView selectedCell;
    private int selectedRow = -1;
    private int selectedCol = -1;
    private int mistakes = 0;
    private float timer = 0;
    private bool notesMode = false;
    public static string date = DailyPuzzle.TodayUtc();
    public SudokuGenerator sudokuGenerator;
    
    void Start()
    {
        sudokuGenerator = new SudokuGenerator();
        int[,] answer = sudokuGenerator.sudokuAnswerGenerator();
        int[,] starting = sudokuGenerator.sudokuPuzzleGenerator(answer, 40);

        board.LoadPuzzle(answer, starting);
        BuildGrid();
    }

    void BuildGrid()
    {
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                GameObject obj = Instantiate(cellPrefab, boardContainer);
                CellView view = obj.GetComponent<CellView>();

                SudokuCell data = board.cells[r, c];
                view.Setup(r, c, data.value, data.isSolved, OnCellClicked);

                cellViews[r, c] = view;
            }
        }
    }

    void OnCellClicked(int row, int col)
    {
        selectedRow = row;
        selectedCol = col;

        selectedCell = cellViews[selectedRow, selectedCol];
        int selectedValue = board.GetSelectedValue(selectedRow, selectedCol);

        HighlightSimilarCells(selectedValue);
    }

    void Update()
    {
        timer += Time.deltaTime;
        int timeInSeconds = Mathf.FloorToInt(timer);
        timerLabel.text = timeInSeconds.ToString();

        if (selectedRow < 0 || selectedCol < 0) return;
        if (Keyboard.current == null) return;

        for (int i = 1; i <= 9; i++)
        {
            Key numKey = Key.Digit1 + (i - 1);
            Key padKey = Key.Numpad1 + (i - 1);

            if (Keyboard.current[numKey].wasPressedThisFrame || Keyboard.current[padKey].wasPressedThisFrame)
            {
                EnterNumber(i);
            }
        }
    }

    void EnterNumber(int number)
    {
        if (board.GetSelectedValue(selectedRow, selectedCol) != 0)
            return;

        SudokuCell cell = board.cells[selectedRow, selectedCol];

        if(notesMode)
        {
            if (number == 0) 
            {
                cell.ClearNotes();
            }
            else
            {
                cell.ToggleNote(number);
            }

            cellViews[selectedRow, selectedCol].SetNotes(cell.notes);
        }
        else
        {
            bool success = board.TrySetValue(selectedRow, selectedCol, number);
            if (success)
            {
                cellViews[selectedRow, selectedCol].SetValue(number);
                cellViews[selectedRow, selectedCol].SetNotes(null);
                HighlightSimilarCells(number);

                if (board.IsBoardComplete())
                {
                    ShowWinPopup();
                }
            }
            else
            {
                mistakesLabel.text = (mistakes += 1).ToString();
            }
        }
    }

    void HighlightSimilarCells(int selectedValue)
    {
        for (int loopRow = 0; loopRow < 9; loopRow++)
        {
            for (int loopCol = 0; loopCol < 9; loopCol++)
            {
                if (board.GetSelectedValue(loopRow, loopCol) == selectedValue && selectedValue != 0)
                    cellViews[loopRow,loopCol].SetHighlight(false, true);
                else
                    cellViews[loopRow,loopCol].SetHighlight(false, false);
            }
        }

        selectedCell.SetHighlight(true,true);
    }

    public void ToggleNotesMode()
    {
        notesMode = !notesMode;
        notesButtonLabel.text = notesMode ? "Notes: On" : "Notes: Off";
    }

    void ShowWinPopup()
    {   
        int timeInSeconds = Mathf.FloorToInt(timer);
        winPopupText.text = $"winner\nScore: {timeInSeconds + mistakes*30}";
        winPopup.SetActive(true);
    }
}