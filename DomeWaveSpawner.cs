using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class Wave
{
    public string waveName = "Wave 1";
    public List<GameObject> enemyPrefabs;
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

    [Header("Siege Settings")]
    public float siegeDetectionRadius = 500f;
    public float siegeLeashRadius = 500f;

    private int currentWaveIndex = 0;
    private int enemiesAlive = 0;
    public bool IsSpawning { get; private set; } = false;

    public void StartSpawning()
    {
        if (waves.Count == 0 || IsSpawning) return;
        IsSpawning = true;
        currentWaveIndex = 0;
        StartCoroutine(WaveRoutine());
    }

    public void StopSpawning()
    {
        StopAllCoroutines();
        IsSpawning = false;
        enemiesAlive = 0;
    }

    private IEnumerator WaveRoutine()
    {
        while (currentWaveIndex < waves.Count)
        {
            Wave currentWave = waves[currentWaveIndex];
            Debug.Log($"Starting {currentWave.waveName}");

            for (int i = 0; i < currentWave.count; i++)
            {
                if (currentWave.enemyPrefabs.Count > 0)
                {
                    SpawnEnemy(currentWave.enemyPrefabs[Random.Range(0, currentWave.enemyPrefabs.Count)]);
                }
                yield return new WaitForSeconds(currentWave.spawnInterval);
            }

            yield return new WaitUntil(() => enemiesAlive <= 0);
            Debug.Log($"{currentWave.waveName} Cleared!");
            currentWaveIndex++;

            if (currentWaveIndex < waves.Count) yield return new WaitForSeconds(timeBetweenWaves);
        }

        IsSpawning = false;
        if (DomeBattleManager.instance != null) DomeBattleManager.instance.OnVictory();
    }

    private void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null || spawnPoints.Count == 0) return;

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
        GameObject enemy = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);

        // 1. Configure AI
        EnemyAI ai = enemy.GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.ActivateAI();
            ai.chaseLeashRadius = siegeLeashRadius;
        }

        // 2. Configure Targeting (The Critical Fix)
        AITargeting targeting = enemy.GetComponent<AITargeting>();
        if (targeting != null)
        {
            targeting.detectionRadius = siegeDetectionRadius;
            targeting.checkLineOfSight = false; // X-Ray vision for dome

            // Enable Siege Priority Logic
            targeting.prioritizeDomeMarkers = true;

            // --- CRITICAL FIX: Patch Layer Mask ---
            // If the enemy's mask doesn't include "DomeMarker", they can't see it physically.
            // We force add Layer 16 ("DomeMarker") to their mask.
            int domeLayer = LayerMask.NameToLayer("DomeMarker");
            if (domeLayer != -1)
            {
                targeting.playerLayer |= (1 << domeLayer);
            }
            // --------------------------------------
        }

        EnemyHealthTracker tracker = enemy.AddComponent<EnemyHealthTracker>();
        tracker.spawner = this;

        enemiesAlive++;
    }

    public void OnEnemyKilled() { enemiesAlive--; }
}

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
            health.OnHealthChanged -= CheckDeath;
            Destroy(this);
        }
    }

    void OnDestroy()
    {
        if (health != null) health.OnHealthChanged -= CheckDeath;
    }
}