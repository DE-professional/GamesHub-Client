using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CellView : MonoBehaviour
{
    public int row;
    public int col;
    public TMP_Text label;
    public TMP_Text[] noteLabel;
    public Button button;
    public Image background;

    // Set by SudokuRetroUI when the cell is created; the defaults are the plain look
    [HideInInspector] public Color normalColor = Color.white;
    [HideInInspector] public Color similarColor = new Color(0.75f, 0.9f, 1f);
    [HideInInspector] public Color selectedColor = new Color(0.6f, 0.75f, 1f);
    [HideInInspector] public Color givenTextColor = Color.blue;
    [HideInInspector] public Color enteredTextColor = Color.blue;
    [HideInInspector] public Color selectedTextColor = Color.blue;
    [HideInInspector] public Color noteColor = Color.black;
    [HideInInspector] public Color selectedNoteColor = Color.black;

    private System.Action<int, int> onClickCallback;
    private bool isGiven;
    private bool isSelected;
    private bool isSimilar;

    public void Setup(int row, int col, int value, bool isGiven, System.Action<int, int> onClick)
    {
        this.row = row;
        this.col = col;
        this.isGiven = isGiven;
        onClickCallback = onClick;

        label.text = value == 0 ? "" : value.ToString();
        Refresh();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClickCallback?.Invoke(row, col));
    }

    public void SetValue(int value)
    {
        label.text = value == 0 ? "" : value.ToString();
    }

    public void SetHighlight(bool isSelected, bool isSimiliar)
    {
        this.isSelected = isSelected;
        isSimilar = isSimiliar;
        Refresh();
    }

    /// <summary>Repaints the background and digit for the current state and colors.</summary>
    public void Refresh()
    {
        background.color = isSelected ? selectedColor : isSimilar ? similarColor : normalColor;
        label.color = isSelected ? selectedTextColor : isGiven ? givenTextColor : enteredTextColor;
        foreach (TMP_Text note in noteLabel)
            if (note != null) note.color = isSelected ? selectedNoteColor : noteColor;
    }

    public void SetNotes(HashSet<int> notes)
    {
        for (int i = 0; i < 9; i++)
        {
            int number = i + 1;
            noteLabel[i].text = (notes != null && notes.Contains(number)) ? number.ToString() : "";
        }
    }
}
