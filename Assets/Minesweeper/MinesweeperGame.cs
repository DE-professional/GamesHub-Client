using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.SceneManagement;
using System; // Required for DateTime

public class MinesweeperGame : MonoBehaviour
{
    [Header("UI References")]
    public GameObject buttonPrefab;
    public Transform boardPanel;
    public TextMeshProUGUI flagCounterText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI highScoreText;
    public GameObject titlePanel;
    public GameObject returnToMenuButton; // Shown once the game is won or lost

    [Header("Game Settings")]
    private int width = 20;
    private int height = 20;
    private int mineCount;
    [SerializeField] int minMines;
    [SerializeField] int maxMines;
    
    

    private Cell[,] grid;
    private bool gameOver = false;
    private bool gameStarted = false;
    private float timer = 0f;
    private int flagsRemaining;

    // Struct to hold individual cell data
    private class Cell
    {
        public int x, y;
        public bool isMine;
        public bool isRevealed;
        public bool isFlagged;
        public int adjacentMines;
        public GameObject uiButton;
        public MineTile tile;
        public bool Alternate => (x + y) % 2 == 1;
    }
    
    
    void Start()
    {
        if (titlePanel != null)
        {
            titlePanel.SetActive(true);
        }
        else
        {
            StartGame();
        }
        if (returnToMenuButton != null) returnToMenuButton.SetActive(false);
        mineCount = UnityEngine.Random.Range (minMines, maxMines);
        flagsRemaining = mineCount;
        
        UpdateFlagText();
        if (timerText != null) timerText.text = "Time: 0";
        LoadHighScore(); // This also checks if a new day has started
        GenerateBoard();
    }

    public void StartGame()
    {
        
    }

    void Update()
    {
        if (gameStarted && !gameOver)
        {
            timer += Time.deltaTime;
            timerText.text = "Time: " + Mathf.FloorToInt(timer).ToString();
        }
    }

    void GenerateBoard()
    {
        // Clear any existing children first to prevent stray/extra buttons
        foreach (Transform child in boardPanel)
        {
            Destroy(child.gameObject);
        }

        grid = new Cell[width, height];
        boardPanel.GetComponent<GridLayoutGroup>().constraintCount = width;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject obj = Instantiate(buttonPrefab, boardPanel);

                Cell cell = new Cell { x = x, y = y, uiButton = obj, tile = obj.GetComponent<MineTile>() };
                grid[x, y] = cell;
                cell.tile.SetHidden(cell.Alternate);

                // Setup click listener supporting left and right clicks
                int rx = x;
                int ry = y;
                
                EventTrigger trigger = obj.AddComponent<EventTrigger>();
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerClick;
                entry.callback.AddListener((data) => { OnCellClick(rx, ry, (PointerEventData)data); });
                trigger.triggers.Add(entry);
            }
        }
    }

    void OnCellClick(int x, int y, PointerEventData data)
    {
        if (gameOver) return;

        Cell cell = grid[x, y];

        // Right Click: Place/Remove Flag
        if (data.button == PointerEventData.InputButton.Right)
        {
            if (cell.isRevealed) return;

            cell.isFlagged = !cell.isFlagged;
            cell.tile.SetFlagged(cell.isFlagged);
            flagsRemaining += cell.isFlagged ? -1 : 1;
            UpdateFlagText();
            return;
        }

        // Left Click: Reveal Cell
        if (data.button == PointerEventData.InputButton.Left)
        {
            if (cell.isFlagged || cell.isRevealed) return;

            // First click safety: Generate mines after first click so player never hits a mine first
            if (!gameStarted)
            {
                gameStarted = true;
                PlaceMines(x, y);
                CalculateAdjacentMines();
                RevealSafePatch(x, y);
            }
            else if (cell.isMine)
            {
                BlowUp(cell);
                return;
            }
            else
            {
                RevealCell(x, y);
            }

            CheckWinCondition();
        }
    }

    void PlaceMines(int firstX, int firstY)
    {
        int placed = 0;
        while (placed < mineCount)
        {
            int rx = UnityEngine.Random.Range(0, width);
            int ry = UnityEngine.Random.Range(0, height);

            // Don't place mine on or immediately around the first click
            if (Mathf.Abs(rx - firstX) <= 1 && Mathf.Abs(ry - firstY) <= 1) continue;
            if (grid[rx, ry].isMine) continue;

            grid[rx, ry].isMine = true;
            placed++;
        }
    }

    void CalculateAdjacentMines()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y].isMine) continue;

                int count = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            if (grid[nx, ny].isMine) count++;
                        }
                    }
                }
                grid[x, y].adjacentMines = count;
            }
        }
    }

    void RevealSafePatch(int startX, int startY)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = startX + dx;
                int ny = startY + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    RevealCell(nx, ny);
                }
            }
        }
    }

    void RevealCell(int x, int y)
    {
        Cell cell = grid[x, y];
        if (cell.isRevealed || cell.isFlagged) return;

        cell.isRevealed = true;
        cell.tile.SetRevealed(cell.adjacentMines, cell.Alternate);

        if (cell.adjacentMines == 0)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    {
                        if (!grid[nx, ny].isRevealed) RevealCell(nx, ny);
                    }
                }
            }
        }
    }

    void BlowUp(Cell clickedMine)
    {
        gameOver = true;
        Debug.Log("Game Over!");
        if (returnToMenuButton != null) returnToMenuButton.SetActive(true);
        
        foreach (var cell in grid)
        {
            if (cell.isMine) cell.tile.SetMine(cell == clickedMine);
        }
    }

    void CheckWinCondition()
    {
        bool allSafeRevealed = true;
        foreach (var cell in grid)
        {
            if (!cell.isMine && !cell.isRevealed)
            {
                allSafeRevealed = false;
                break;
            }
        }

        if (allSafeRevealed)
        {
            gameOver = true;
            Debug.Log("You Win!");
            if (returnToMenuButton != null) returnToMenuButton.SetActive(true);
            SaveHighScore();
        }
    }

    void UpdateFlagText()
    {
        if (flagCounterText != null)
            flagCounterText.text = "Flags: " + flagsRemaining;
    }

    void LoadHighScore()
    {
        string today = DateTime.Now.ToString("yyyyMMdd");
        string savedDate = PlayerPrefs.GetString("HighScoreDate", "");

        // If the saved date is not today, wipe the old high score!
        if (savedDate != today)
        {
            PlayerPrefs.DeleteKey("HighScore");
            PlayerPrefs.SetString("HighScoreDate", today);
            PlayerPrefs.Save();
        }

        float bestTime = PlayerPrefs.GetFloat("HighScore", float.MaxValue);
        if (highScoreText != null)
        {
            if (bestTime == float.MaxValue)
                highScoreText.text = "Daily Best: None";
            else
                highScoreText.text = "Daily Best: " + Mathf.FloorToInt(bestTime).ToString() + "s";
        }
    }

    void SaveHighScore()
    {
        string today = DateTime.Now.ToString("yyyyMMdd");
        PlayerPrefs.SetString("HighScoreDate", today);

        float bestTime = PlayerPrefs.GetFloat("HighScore", float.MaxValue);
        if (timer < bestTime)
        {
            PlayerPrefs.SetFloat("HighScore", timer);
            PlayerPrefs.Save();
            if (highScoreText != null)
                highScoreText.text = "Daily Best: " + Mathf.FloorToInt(timer).ToString() + "s";
        }
    }
}