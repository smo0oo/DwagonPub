using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Helper classes to store the data
[System.Serializable]
public class ObjectState
{
    public Vector3 Position;
    public Quaternion Rotation;
    // You could add more data here, like current health for an NPC
}

[System.Serializable]
public class DroppedItemState
{
    public string ItemID;
    public int Quantity;
    public Vector3 Position;
    public Quaternion Rotation;
}

public class SceneStateManager : MonoBehaviour
{
    public static SceneStateManager instance;

    // A dictionary to hold the state of all objects for each scene
    // Key: Scene Name (string), Value: Dictionary<Object ID, Object State>
    private Dictionary<string, Dictionary<string, ObjectState>> sceneObjectStates = new Dictionary<string, Dictionary<string, ObjectState>>();

    // A separate dictionary for dropped items, which are created at runtime
    private Dictionary<string, List<DroppedItemState>> sceneDroppedItemStates = new Dictionary<string, List<DroppedItemState>>();

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    /// <summary>
    /// Called by the GameManager BEFORE a scene is unloaded.
    /// </summary>
    public void CaptureSceneState(string sceneName, GameObject worldItemPrefab)
    {
        // --- Capture Stateful Entities (like NPCs) ---
        // --- FIX: Replaced FindObjectsOfType with FindObjectsByType ---
        var entities = FindObjectsByType<StatefulEntity>(FindObjectsSortMode.None);
        var entityStates = new Dictionary<string, ObjectState>();
        foreach (var entity in entities)
        {
            entityStates[entity.uniqueId] = new ObjectState
            {
                Position = entity.transform.position,
                Rotation = entity.transform.rotation
            };
        }
        sceneObjectStates[sceneName] = entityStates;

        // --- Capture Dropped WorldItems ---
        // --- FIX: Replaced FindObjectsOfType with FindObjectsByType ---
        var worldItems = FindObjectsByType<WorldItem>(FindObjectsSortMode.None);
        var droppedItemsList = new List<DroppedItemState>();
        foreach (var item in worldItems)
        {
            droppedItemsList.Add(new DroppedItemState
            {
                ItemID = item.itemData.id,
                Quantity = item.quantity,
                Position = item.transform.position,
                Rotation = item.transform.rotation
            });
            // Destroy the runtime object so it doesn't cause issues
            Destroy(item.gameObject);
        }
        sceneDroppedItemStates[sceneName] = droppedItemsList;
    }

    /// <summary>
    /// Called by the GameManager AFTER a new scene has loaded.
    /// </summary>
    public void RestoreSceneState(string sceneName, GameObject worldItemPrefab)
    {
        // --- Restore Stateful Entities ---
        if (sceneObjectStates.TryGetValue(sceneName, out var entityStates))
        {
            // --- FIX: Replaced FindObjectsOfType with FindObjectsByType ---
            var entities = FindObjectsByType<StatefulEntity>(FindObjectsSortMode.None);
            foreach (var entity in entities)
            {
                if (entityStates.TryGetValue(entity.uniqueId, out var savedState))
                {
                    entity.transform.position = savedState.Position;
                    entity.transform.rotation = savedState.Rotation;
                }
            }
        }

        // --- Restore (Re-Spawn) Dropped WorldItems ---
        if (sceneDroppedItemStates.TryGetValue(sceneName, out var droppedItemsList))
        {
            foreach (var itemState in droppedItemsList)
            {
                GameObject itemGO = Instantiate(worldItemPrefab, itemState.Position, itemState.Rotation);
                WorldItem worldItem = itemGO.GetComponent<WorldItem>();
                if (worldItem != null)
                {
                    // Find the ItemData asset from its ID
                    worldItem.itemData = GameManager.instance.allItemsDatabase.FirstOrDefault(i => i.id == itemState.ItemID);
                    worldItem.quantity = itemState.Quantity;
                }
            }
        }
    }
}