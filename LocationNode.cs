using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Splines;

// --- UPDATED: Added DualModeLocation ---
public enum NodeType { Scene, Event, Waypoint, DualModeLocation }
// ---------------------------------------

[System.Serializable]
public class RoadConnection
{
    public LocationNode destinationNode;
    public int travelTimeHours;
    public SplineContainer roadSpline;
    public string destinationSpawnPointID;
    public bool reverseSpline = false;
    [Range(-180f, 180f)] public float manualYRotation = 0f;
    public string combatSceneName = "DomeBattle";
    [Range(0, 10)] public int ambushChance = 2;
    public LootTable forageLootTable;
    public float forageSuccessChance = 0.5f;
}

public class LocationNode : MonoBehaviour
{
    [Header("Location Info")]
    public string locationName;
    public NodeType nodeType = NodeType.Scene;

    // --- UPDATED: Added Dungeon Scene Entry ---
    [Header("Dual Mode Settings")]
    [Tooltip("The name of the Dungeon Scene to load when starting an operation here.")]
    public string dualModeDungeonScene;
    // ------------------------------------------

    [Header("Paper Map Mapping")]
    [Tooltip("If true, the icon position is calculated from the node's World Position relative to the World Map Bounds.")]
    public bool useAutoPosition = true;

    [Tooltip("If Auto Position is FALSE, use these coordinates (0-100 scale).")]
    public Vector2 manualMapCoords;

    [Header("Visual Feedback")]
    public GameObject gpsHighlight;

    [Header("Scene Settings")]
    public string sceneToLoad;
    public bool isStartingNode = false;

    [Header("Connections")]
    public List<RoadConnection> connections = new List<RoadConnection>();

    void Awake()
    {
        SetRoadsVisibility(false);
        if (gpsHighlight != null) gpsHighlight.SetActive(false);
    }

    void Start()
    {
        if (isStartingNode && WorldMapManager.instance != null)
        {
            WorldMapManager.instance.SetCurrentLocation(this, true);
        }
    }

    public void SetRoadsVisibility(bool isVisible)
    {
        foreach (var connection in connections)
        {
            if (connection.roadSpline != null)
            {
                MeshRenderer roadRenderer = connection.roadSpline.GetComponent<MeshRenderer>();
                if (roadRenderer != null) roadRenderer.enabled = isVisible;
            }
        }
    }

    public void SetRoadsVisibilityExcept(SplineContainer activeSpline, bool showOthers)
    {
        foreach (var connection in connections)
        {
            if (connection.roadSpline != null)
            {
                MeshRenderer roadRenderer = connection.roadSpline.GetComponent<MeshRenderer>();
                if (roadRenderer != null)
                {
                    if (connection.roadSpline == activeSpline) roadRenderer.enabled = true;
                    else roadRenderer.enabled = showOthers;
                }
            }
        }
    }
}