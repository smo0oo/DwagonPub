using UnityEngine;
using System.Collections.Generic;

public enum SceneType
{
    Town,
    Dungeon,
    WorldMap,
    MainMenu,
    DomeBattle,
    Cinematic
}

public enum PlayerSceneState
{
    Active,
    Inactive,
    Hidden,
    SpawnAtMarker
}

[System.Serializable]
public class PlayerConfig
{
    public string note = "Player Name";
    public PlayerSceneState state = PlayerSceneState.Active;
}

public class SceneInfo : MonoBehaviour
{
    public static SceneInfo instance;

    [Tooltip("Set the type for this scene.")]
    public SceneType type;

    [Header("Player Configuration")]
    [Tooltip("Define the state for each of the 5 party slots.")]
    public List<PlayerConfig> playerConfigs = new List<PlayerConfig>();

    [Header("Tithe Settings (Towns Only)")]
    [Tooltip("If true, entering this town will grant resources to the wagon.")]
    public bool givesTithe = true;
    public int titheFuelAmount = 50;
    public int titheRationsAmount = 50;

    [Header("Enemy Level Scaling")]
    [Tooltip("If true, enemies in this scene will automatically scale to the Party's level.")]
    public bool scaleEnemiesToPlayer = true;

    [Tooltip("The lowest level variance. e.g., -2 means enemies can spawn 2 levels below the player.")]
    public int minLevelOffset = -1;

    [Tooltip("The highest level variance. e.g., +2 means enemies can spawn 2 levels above the player.")]
    public int maxLevelOffset = 1;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (scaleEnemiesToPlayer)
        {
            ScaleAllEnemiesInScene();
        }
    }

    public void ScaleAllEnemiesInScene()
    {
        if (PartyManager.instance == null) return;

        int partyLevel = PartyManager.instance.partyLevel;

        // --- AAA PERFORMANCE FIX: Using the new, much faster Unity API ---
        EnemyAI[] enemiesInScene = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (EnemyAI enemy in enemiesInScene)
        {
            ApplyScalingToEnemy(enemy, partyLevel);
        }
    }

    // Making this public allows dynamically spawned enemies (like from a spawner) 
    // to ask the SceneInfo for their level right after they spawn!
    public void ApplyScalingToEnemy(EnemyAI enemy, int baseLevel)
    {
        int randomLevel = baseLevel + UnityEngine.Random.Range(minLevelOffset, maxLevelOffset + 1);
        randomLevel = Mathf.Max(1, randomLevel); // Prevent level 0 or negative levels

        enemy.SetLevel(randomLevel);
    }

    [ContextMenu("Setup 5-Player Default")]
    void SetupDefaultParty()
    {
        playerConfigs.Clear();
        for (int i = 0; i < 5; i++)
        {
            playerConfigs.Add(new PlayerConfig { note = $"Player {i}", state = PlayerSceneState.Active });
        }
    }
}