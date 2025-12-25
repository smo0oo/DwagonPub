using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class ObjectState
{
    public Vector3 Position;
    public Quaternion Rotation;
    public bool IsActive; // NEW: Store whether it is on or off
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
        // FIX: Added 'FindObjectsInactive.Include' to find the NPC you just disabled
        var entities = FindObjectsByType<StatefulEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var entityStates = new Dictionary<string, ObjectState>();
        foreach (var entity in entities)
        {
            entityStates[entity.uniqueId] = new ObjectState
            {
                Position = entity.transform.position,
                Rotation = entity.transform.rotation,
                IsActive = entity.gameObject.activeSelf // NEW: Save the active state
            };
        }
        sceneObjectStates[sceneName] = entityStates;

        // --- Capture Dropped WorldItems ---
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
            // FIX: Search inactive objects too so we can find them and ensure they stay disabled
            var entities = FindObjectsByType<StatefulEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var entity in entities)
            {
                if (entityStates.TryGetValue(entity.uniqueId, out var savedState))
                {
                    entity.transform.position = savedState.Position;
                    entity.transform.rotation = savedState.Rotation;

                    // NEW: Apply the saved active state
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