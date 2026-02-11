using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Splines;

// --- Node Types ---
public enum NodeType { Scene, Event, Waypoint, DualModeLocation }

[System.Serializable]
public class RoadConnection
{
    public LocationNode destinationNode;

    [Header("Travel Requirements")]
    [Tooltip("Tags required to travel this road (e.g. 'Rocky', 'Snow'). Leave empty for standard roads.")]
    public List<string> requiredTags;
    [Tooltip("Color of the road line (Applied to the material color).")]
    public Color roadColor = Color.white;

    [Header("Travel Settings")]
    public int travelTimeHours;
    public SplineContainer roadSpline;
    public string destinationSpawnPointID;
    public bool reverseSpline = false;
    [Range(-180f, 180f)] public float manualYRotation = 0f;

    [Header("Combat & Events")]
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
    [TextArea] public string description;

    [Header("Dual Mode Settings")]
    public string dualModeDungeonScene;

    [Header("Paper Map Mapping")]
    public bool useAutoPosition = true;
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

    // --- LOGIC: Can we travel? ---
    public bool CanTravelTo(LocationNode target, out string missingTags)
    {
        missingTags = "";
        foreach (var conn in connections)
        {
            if (conn.destinationNode == target)
            {
                if (conn.requiredTags == null || conn.requiredTags.Count == 0) return true;

                if (WagonManager.instance != null)
                {
                    bool allowed = WagonManager.instance.HasTraversalTags(conn.requiredTags);
                    if (!allowed) missingTags = string.Join(", ", conn.requiredTags);
                    return allowed;
                }
                return true;
            }
        }
        return false;
    }

    public RoadConnection GetConnectionTo(LocationNode target)
    {
        foreach (var conn in connections)
        {
            if (conn.destinationNode == target) return conn;
        }
        return null;
    }

    // --- VISUALS: Render Logic ---
    public void SetRoadsVisibility(bool isVisible)
    {
        foreach (var connection in connections)
        {
            if (connection.roadSpline != null)
            {
                // FIX: Search in Children because Spline generators often put meshes on child objects
                MeshRenderer roadRenderer = connection.roadSpline.GetComponentInChildren<MeshRenderer>();

                if (roadRenderer != null)
                {
                    roadRenderer.enabled = isVisible;

                    // Apply Color Warning
                    if (isVisible)
                    {
                        // Note: Ensure your road material uses a shader with a main color property (e.g. _BaseColor or _Color)
                        if (roadRenderer.material.HasProperty("_BaseColor")) // URP/HDRP
                            roadRenderer.material.SetColor("_BaseColor", connection.roadColor);
                        else if (roadRenderer.material.HasProperty("_Color")) // Standard
                            roadRenderer.material.color = connection.roadColor;
                    }
                }
            }
        }
    }

    public void SetRoadsVisibilityExcept(SplineContainer activeSpline, bool showOthers)
    {
        foreach (var connection in connections)
        {
            if (connection.roadSpline != null)
            {
                MeshRenderer roadRenderer = connection.roadSpline.GetComponentInChildren<MeshRenderer>();
                if (roadRenderer != null)
                {
                    if (connection.roadSpline == activeSpline) roadRenderer.enabled = true;
                    else roadRenderer.enabled = showOthers;
                }
            }
        }
    }
}