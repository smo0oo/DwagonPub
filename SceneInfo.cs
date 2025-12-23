using UnityEngine;

// Added "Cinematic" to the enum
public enum SceneType { Town, Dungeon, WorldMap, MainMenu, DomeBattle, Cinematic }

public class SceneInfo : MonoBehaviour
{
    [Tooltip("Set the type for this scene.")]
    public SceneType type;

    [Header("Tithe Settings (Towns Only)")]
    [Tooltip("If true, entering this town will grant resources to the wagon.")]
    public bool givesTithe = true;
    public int titheFuelAmount = 50;
    public int titheRationsAmount = 50;
}