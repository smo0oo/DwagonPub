using UnityEngine;
using System.Collections.Generic; // This using statement is for Lists

// By putting this class in its own file, both runtime and editor scripts can use it.
[System.Serializable]
public class ColorToPrefabMapping
{
    public string description;
    public Color colorKey;
    public GameObject prefab;
    [Tooltip("Manual Y-axis rotation (0, 90, 180, 270). This will be ADDED to the automatic rotation.")]
    public float manualYRotation = 0;
}