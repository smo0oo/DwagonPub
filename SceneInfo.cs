using UnityEngine;

// The enum is now correctly defined here as the authoritative source.
public enum SceneType { Town, Dungeon, WorldMap, MainMenu, DomeBattle }

public class SceneInfo : MonoBehaviour
{
    [Tooltip("Set the type for this scene.")]
    public SceneType type;
}