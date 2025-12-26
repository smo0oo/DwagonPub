using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ObjectState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public bool IsActive;
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

    // --- THE REGISTRY ---
    // Holds references to all tracked objects in the current scene, active or inactive.
    private static HashSet<StatefulEntity> registeredEntities = new HashSet<StatefulEntity>();

    public static void RegisterEntity(StatefulEntity entity)
    {
        if (entity != null && !registeredEntities.Contains(entity))
        {
            registeredEntities.Add(entity);
        }
    }

    public static void UnregisterEntity(StatefulEntity entity)
    {
        if (registeredEntities.Contains(entity))
        {
            registeredEntities.Remove(entity);
        }
    }
    // --------------------

    private Dictionary<string, Dictionary<string, ObjectState>> sceneObjectStates = new Dictionary<string, Dictionary<string, ObjectState>>();
    private Dictionary<string, List<DroppedItemState>> sceneDroppedItemStates = new Dictionary<string, List<DroppedItemState>>();

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    public void CaptureSceneState(string sceneName, GameObject worldItemPrefab)
    {
        // --- Capture Stateful Entities ---
        // New Logic: Iterate over the registry instead of searching the scene.
        // This ensures we catch objects even if they were disabled script-wise.
        var entityStates = new Dictionary<string, ObjectState>();

        foreach (var entity in registeredEntities)
        {
            if (entity == null) continue;

            if (!string.IsNullOrEmpty(entity.uniqueId))
            {
                entityStates[entity.uniqueId] = new ObjectState
                {
                    Position = entity.transform.position,
                    Rotation = entity.transform.rotation,
                    IsActive = entity.gameObject.activeSelf // Save the disabled state correctly
                };
            }
        }
        sceneObjectStates[sceneName] = entityStates;

        // --- Capture Dropped WorldItems ---
        // WorldItems are dynamic, so we still find them, but we might want to register them too eventually.
        // For now, finding them is fine as they are usually active when dropped.
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
            Destroy(item.gameObject);
        }
        sceneDroppedItemStates[sceneName] = droppedItemsList;
    }

    public void RestoreSceneState(string sceneName, GameObject worldItemPrefab)
    {
        // --- Restore Stateful Entities ---
        if (sceneObjectStates.TryGetValue(sceneName, out var entityStates))
        {
            // Iterate over the registry. 
            // Since the scene just loaded, all default NPCs have Awoken and Registered themselves (Active).
            foreach (var entity in registeredEntities)
            {
                if (entity == null) continue;

                if (entityStates.TryGetValue(entity.uniqueId, out var savedState))
                {
                    // Restore position
                    entity.transform.position = savedState.Position;
                    entity.transform.rotation = savedState.Rotation;

                    // Restore Active State
                    // If 'savedState.IsActive' is false, this will immediately hide the NPC.
                    entity.gameObject.SetActive(savedState.IsActive);
                }
            }
        }

        // --- Restore Dropped WorldItems ---
        if (sceneDroppedItemStates.TryGetValue(sceneName, out var droppedItemsList))
        {
            foreach (var itemState in droppedItemsList)
            {
                GameObject itemGO = Instantiate(worldItemPrefab, itemState.Position, itemState.Rotation);
                WorldItem worldItem = itemGO.GetComponent<WorldItem>();
                if (worldItem != null)
                {
                    worldItem.itemData = GameManager.instance.allItemsDatabase.FirstOrDefault(i => i.id == itemState.ItemID);
                    worldItem.quantity = itemState.Quantity;
                }
            }
        }
    }
}