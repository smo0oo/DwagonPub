using UnityEngine;
using System.Collections.Generic;

public enum TileType
{
    None,
    Wall,
    Floor,
    Room,       // Macro-Tile
    Hallway, Corner, TJunction, CrossRoad,
    Doorway, HallwayDoor
}

public enum PrefabFacing { Forward_Z_Positive = 0, Right_X_Positive = 90, Back_Z_Negative = 180, Left_X_Negative = 270 }

[System.Serializable]
public struct WallOrientationSettings
{
    [Range(0, 270)] public float faceNorthOffset;
    [Range(0, 270)] public float faceEastOffset;
    [Range(0, 270)] public float faceSouthOffset;
    [Range(0, 270)] public float faceWestOffset;
}

[System.Serializable]
public struct InternalCornerSettings
{
    [Range(0, 270)] public float topRightOffset;
    [Range(0, 270)] public float bottomRightOffset;
    [Range(0, 270)] public float bottomLeftOffset;
    [Range(0, 270)] public float topLeftOffset;
}

[System.Serializable]
public class ColorToPrefabMapping
{
    public string description;
    public Color colorKey;
    public TileType type;

    [Header("Macro Room Settings")]
    [Tooltip("The expected pixel dimensions of this room (e.g., 2x2 for 20x20m).")]
    public Vector2Int size = Vector2Int.one;
    [Tooltip("Random selection of full-room prefabs.")]
    public List<GameObject> roomVariants = new List<GameObject>();

    [Header("Micro Tile Settings")]
    public GameObject prefab;
    public PrefabFacing meshFacing = PrefabFacing.Forward_Z_Positive;
    public GameObject roomCornerPrefab;
    public GameObject externalCornerPrefab;
    public WallOrientationSettings wallCorrections;
    public InternalCornerSettings cornerCorrections;

    [Header("Global Adjustments")]
    public float manualYRotation = 0;
}