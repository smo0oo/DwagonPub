using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using PixelCrushers.DialogueSystem; // Required for Lua checks

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
    // Populated by StatefulEntity.Awake()
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
        // Iterate over the registry instead of searching the scene.
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
                    IsActive = entity.gameObject.activeSelf // Save the current active/inactive state
                };
            }
        }
        sceneObjectStates[sceneName] = entityStates;

        // --- Capture Dropped WorldItems ---
        // WorldItems are dynamic, so we find them in the scene.
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
            // We destroy them here because they will be respawned by RestoreSceneState next time
            Destroy(item.gameObject);
        }
        sceneDroppedItemStates[sceneName] = droppedItemsList;
    }

    public void RestoreSceneState(string sceneName, GameObject worldItemPrefab)
    {
        // 1. Get the save data for this scene (it might be null if we haven't visited it yet)
        sceneObjectStates.TryGetValue(sceneName, out var entityStates);

        // 2. Iterate over ALL entities currently in the scene
        // Since the scene just loaded, all entities (active or inactive) have run Awake() and registered themselves.
        foreach (var entity in registeredEntities)
        {
            if (entity == null) continue;

            // --- LOGIC GATE (New) ---
            // If the entity has an 'NPCSpawnCondition' script, we check that FIRST.
            // This allows the Dialogue System (Variables) to veto the existence of an object.
            if (entity.TryGetComponent<NPCSpawnCondition>(out var condition))
            {
                if (!condition.ShouldSpawn())
                {
                    // The Lua condition failed (e.g. "Quest_Complete" is false).
                    // Force the object to be disabled and SKIP restoring saved data.
                    entity.gameObject.SetActive(false);
                    continue;
                }
            }

            // --- RESTORE SAVED STATE ---
            // If we passed the logic gate, we check if we have saved data (Position/Rotation/Active).
            if (entityStates != null && entityStates.TryGetValue(entity.uniqueId, out var savedState))
            {
                entity.transform.position = savedState.Position;
                entity.transform.rotation = savedState.Rotation;

                // Restore the state from the save file.
                // Note: If savedState.IsActive is false (e.g. you killed them), they stay hidden.
                entity.gameObject.SetActive(savedState.IsActive);
            }
            // If no save data exists, the object remains in its default Scene state (Position/Active).
            // Since the logic gate passed (or didn't exist), it is allowed to exist.
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