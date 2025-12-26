using UnityEngine;
using System.Collections.Generic;

// This is just a helper class (Not a MonoBehaviour)
[System.Serializable]
public class NarrativeSpawnData
{
    public GameObject enemyPrefab;
    public Transform spawnPoint;
}

// This is the Main Script (Must match filename 'NarrativeCombatSpawner.cs')
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