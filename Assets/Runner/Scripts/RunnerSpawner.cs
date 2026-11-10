using UnityEngine;

public class RunnerSpawner : MonoBehaviour
{
    public RunnerObstacle[] obstaclePrefabs;
    public float minInterval = 0.9f;
    public float maxInterval = 2.0f;

    public RunnerPlayer player;
    // Random-height obstacles never exceed this fraction of the player's jump apex.
    // Below 1 so there's still airtime to clear the obstacle's width.
    [Range(0.3f, 0.95f)] public float maxJumpFraction = 0.75f;

    private double timer;
    private float jumpApex;
    private string date;
    private System.Random random;

    void Start()
    {
        date = DailyPuzzle.TodayUtc();
        random = new System.Random(DailyPuzzle.Seed("runner",date));
        timer = maxInterval;
        if (player == null) player = FindFirstObjectByType<RunnerPlayer>();
        if (player != null) jumpApex = player.JumpApexHeight;
    }

    void Update()
    {
        if (obstaclePrefabs == null || obstaclePrefabs.Length == 0) return;
        if (RunnerManager.Instance == null || !RunnerManager.Instance.IsRunning) return;

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        // The prefab's own root position is an offset from the spawner, so
        // overhead obstacles can sit higher than ground ones
        var prefab = obstaclePrefabs[random.Next(0, obstaclePrefabs.Length)];
        var obstacle = Instantiate(prefab, transform.position + prefab.transform.position, Quaternion.identity);

        if (obstacle.randomHeight)
        {
            float max = obstacle.maxHeight;
            if (jumpApex > 0f) max = Mathf.Min(max, jumpApex * maxJumpFraction);
            obstacle.SetHeight(Random.Range(Mathf.Min(obstacle.minHeight, max), max));
        }

        // Keep gaps roughly constant in distance as speed ramps up
        var m = RunnerManager.Instance;
        float speedFactor = m.startSpeed / m.Speed;
        timer = ((random.NextDouble() * maxInterval) + minInterval) * Mathf.Max(0.5f, speedFactor);
    }
}
