using UnityEngine;
using System.Collections.Generic;

// 1. We group the Quest settings into their own class.
// This automatically makes them collapsible in the Inspector.
[System.Serializable]
public class NarrativeQuestSettings
{
    [Tooltip("If true, this enemy will count towards a kill quest variable.")]
    public bool incrementQuestVar = false;

    [Tooltip("The exact name of the Dialogue System variable to increment.")]
    public string variableName;

    [Tooltip("How much to add (usually 1).")]
    public int incrementAmount = 1;

    [Tooltip("Refresh the Quest Tracker HUD immediately on death?")]
    public bool updateQuestTracker = true;
}

[System.Serializable]
public class NarrativeSpawnData
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public Transform spawnPoint;

    // 2. This reference will now appear as a Foldout arrow named "Quest Settings"
    public NarrativeQuestSettings questSettings;
}

public class NarrativeCombatSpawner : MonoBehaviour
{
    [Header("Configuration")]
    public List<NarrativeSpawnData> enemiesToSpawn;

    [Tooltip("If true, tells the DomeBattleManager to show 'Victory' when these specific enemies die.")]
    public bool triggerDomeVictoryOnDefeat = true;

    [Tooltip("Timeline Signal calls this function.")]
    public bool spawnImmediately = false;

    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool battleStarted = false;

    void Start()
    {
        if (spawnImmediately)
        {
            SpawnEnemies();
        }
    }

    public void SpawnEnemies()
    {
        if (battleStarted) return;
        battleStarted = true;

        foreach (var data in enemiesToSpawn)
        {
            if (data.enemyPrefab == null || data.spawnPoint == null) continue;

            GameObject newEnemy = Instantiate(data.enemyPrefab, data.spawnPoint.position, data.spawnPoint.rotation);

            // --- QUEST INJECTION LOGIC ---
            // 3. We now access the fields via 'data.questSettings'
            if (data.questSettings != null && data.questSettings.incrementQuestVar && !string.IsNullOrEmpty(data.questSettings.variableName))
            {
                // Check if the enemy already has the script (to avoid duplicates), otherwise Add it.
                EnemyDeathIncrementer incrementer = newEnemy.GetComponent<EnemyDeathIncrementer>();
                if (incrementer == null)
                {
                    incrementer = newEnemy.AddComponent<EnemyDeathIncrementer>();
                }

                // Inject the data from the Inspector into the new enemy instance
                incrementer.variableName = data.questSettings.variableName;
                incrementer.incrementAmount = data.questSettings.incrementAmount;
                incrementer.updateQuestTracker = data.questSettings.updateQuestTracker;
            }
            // -----------------------------

            activeEnemies.Add(newEnemy);
        }

        StartCoroutine(MonitorBattleRoutine());
    }

    private System.Collections.IEnumerator MonitorBattleRoutine()
    {
        while (activeEnemies.Count > 0)
        {
            activeEnemies.RemoveAll(x => x == null || !x.activeInHierarchy);
            yield return new WaitForSeconds(1.0f);
        }

        OnBattleComplete();
    }

    private void OnBattleComplete()
    {
        Debug.Log("Narrative Battle Complete!");

        if (triggerDomeVictoryOnDefeat && DomeBattleManager.instance != null)
        {
            DomeBattleManager.instance.OnVictory();
        }
    }
}