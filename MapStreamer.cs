using UnityEngine;
using System.Collections; // Required for IEnumerator
using System.Collections.Generic;

[System.Serializable]
public class MapChunk
{
    [Tooltip("The grid coordinate (X, Y) of this chunk.")]
    public Vector2Int coordinates;
    [Tooltip("The parent GameObject containing all terrain/props for this chunk.")]
    public GameObject chunkObject;
}

public class MapStreamer : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The size of one chunk in world units (meters).")]
    public float chunkSize = 500f;
    [Tooltip("How many chunks radius to keep active around the player. 1 = 3x3 grid.")]
    public int viewRadius = 1;

    [Header("References")]
    [Tooltip("Leave this empty! The script will automatically find the Main Camera at runtime.")]
    public Transform viewer;

    public List<MapChunk> allChunks;

    private Dictionary<Vector2Int, GameObject> chunkMap = new Dictionary<Vector2Int, GameObject>();
    private Vector2Int currentGridCoord;

    void Start()
    {
        // 1. Build dictionary for fast O(1) lookups
        foreach (var chunk in allChunks)
        {
            if (!chunkMap.ContainsKey(chunk.coordinates))
            {
                chunkMap.Add(chunk.coordinates, chunk.chunkObject);
                chunk.chunkObject.SetActive(false); // Start everything hidden
            }
        }

        // Auto-Find Main Camera
        if (viewer == null)
        {
            if (Camera.main != null)
            {
                viewer = Camera.main.transform;
            }
            else
            {
                Debug.LogError("MapStreamer: No object tagged 'MainCamera' found in the scene! Streaming will not work.");
                var wagon = FindAnyObjectByType<WagonController>();
                if (wagon != null) viewer = wagon.transform;
            }
        }

        // 2. Initial load
        if (viewer != null)
        {
            currentGridCoord = GetCoordinateFromPosition(viewer.position);
            UpdateChunks();
        }

        // --- OPTIMIZATION: Start the slow check loop ---
        StartCoroutine(StreamerRoutine());
    }

    // --- OPTIMIZATION: Replaced Update() with Coroutine ---
    private IEnumerator StreamerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(0.5f); // Check 2 times per second

        while (true)
        {
            if (viewer != null)
            {
                Vector2Int playerCoord = GetCoordinateFromPosition(viewer.position);

                if (playerCoord != currentGridCoord)
                {
                    currentGridCoord = playerCoord;
                    UpdateChunks();
                }
            }
            yield return wait;
        }
    }
    // -------------------------------------------------------

    private Vector2Int GetCoordinateFromPosition(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x / chunkSize);
        int y = Mathf.FloorToInt(pos.z / chunkSize); // Unity Z is Grid Y
        return new Vector2Int(x, y);
    }

    private void UpdateChunks()
    {
        foreach (var kvp in chunkMap)
        {
            int distX = Mathf.Abs(kvp.Key.x - currentGridCoord.x);
            int distY = Mathf.Abs(kvp.Key.y - currentGridCoord.y);

            bool shouldBeVisible = (distX <= viewRadius && distY <= viewRadius);

            if (kvp.Value.activeSelf != shouldBeVisible)
            {
                kvp.Value.SetActive(shouldBeVisible);
            }
        }
    }
}