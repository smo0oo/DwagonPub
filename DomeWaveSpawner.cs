using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class Wave
{
    public string waveName = "Wave 1";
    public List<GameObject> enemyPrefabs; // List of possible enemies
    public int count = 5;
    public float spawnInterval = 1.5f;
}

public class DomeWaveSpawner : MonoBehaviour
{
    [Header("Wave Configuration")]
    public List<Wave> waves;
    public float timeBetweenWaves = 10f;

    [Header("Spawn Locations")]
    public List<Transform> spawnPoints;

    private int currentWaveIndex = 0;
    private int enemiesAlive = 0;

    // Tracks if the routine is currently running
    private bool isSpawning = false;

    public void StartSpawning()
    {
        if (waves.Count == 0) return;

        // --- FIX: Prevent double-start ---
        if (isSpawning) return;
        isSpawning = true;

        currentWaveIndex = 0;
        StartCoroutine(WaveRoutine());
    }

    public void StopSpawning()
    {
        StopAllCoroutines();
        isSpawning = false;
        enemiesAlive = 0; // Reset count just in case
    }

    private IEnumerator WaveRoutine()
    {
        while (currentWaveIndex < waves.Count)
        {
            Wave currentWave = waves[currentWaveIndex];
            Debug.Log($"Starting {currentWave.waveName}");

            // 1. Spawn Enemies
            for (int i = 0; i < currentWave.count; i++)
            {
                SpawnEnemy(currentWave.enemyPrefabs[Random.Range(0, currentWave.enemyPrefabs.Count)]);
                yield return new WaitForSeconds(currentWave.spawnInterval);
            }

            // 2. Wait for clear
            // We wait until all enemies are dead.
            yield return new WaitUntil(() => enemiesAlive <= 0);

            Debug.Log($"{currentWave.waveName} Cleared!");
            currentWaveIndex++;

            // 3. Delay before next wave
            if (currentWaveIndex < waves.Count)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }

        // 4. All waves complete
        isSpawning = false;
        if (DomeBattleManager.instance != null)
        {
            DomeBattleManager.instance.OnVictory();
        }
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null || spawnPoints.Count == 0) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];

        // You can swap Instantiate for ObjectPooler.instance.Get(...) if you have pooling setup for enemies
        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // Add tracker
        EnemyHealthTracker tracker = enemy.AddComponent<EnemyHealthTracker>();
        tracker.spawner = this;

        enemiesAlive++;
    }

    public void OnEnemyKilled()
    {
        enemiesAlive--;
    }
}

// Helper component added dynamically to spawned enemies
public class EnemyHealthTracker : MonoBehaviour
{
    public DomeWaveSpawner spawner;
    private Health health;

    void Start()
    {
        health = GetComponent<Health>();
        if (health != null) health.OnHealthChanged += CheckDeath;
    }

    void CheckDeath()
    {
        if (health.currentHealth <= 0)
        {
            if (spawner != null) spawner.OnEnemyKilled();

            // Clean up listener
            health.OnHealthChanged -= CheckDeath;

            // Destroy this tracker so it doesn't fire twice
            Destroy(this);
        }
    }

    void OnDestroy()
    {
        // Failsafe in case enemy is destroyed without health hitting 0 (e.g. Despawned)
        if (health != null) health.OnHealthChanged -= CheckDeath;
    }
}