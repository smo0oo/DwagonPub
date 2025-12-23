using UnityEngine;
using System.Collections.Generic;

// --- RESTORED ENUM ---
public enum SceneType
{
    Town,
    Dungeon,
    WorldMap,
    MainMenu,
    DomeBattle,
    Cinematic
}
// ---------------------

public enum PlayerSceneState
{
    Active,             // Player is Visible and Controllable (Standard Gameplay)
    Inactive,           // Player is Visible but ignores Input/AI (Town NPC behavior)
    Hidden,             // Player is Invisible and Disabled (Cutscenes/Ambush)
    SpawnAtMarker       // Player is Visible, Inactive, and forced to a 'TownCharacterSpawnPoint'
}

[System.Serializable]
public class PlayerConfig
{
    public string note = "Player Name"; // Helper text for the Inspector
    public PlayerSceneState state = PlayerSceneState.Active;
}

public class SceneInfo : MonoBehaviour
{
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

    // Context Menu to quickly reset the list to 5 default players
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