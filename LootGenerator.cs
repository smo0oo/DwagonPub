using UnityEngine;

public class LootGenerator : MonoBehaviour
{
    [Header("Loot Configuration")]
    [Tooltip("The Loot Table asset that defines the possible drops.")]
    public LootTable lootTable;

    [Header("Experience Reward")]
    [Tooltip("The amount of experience points awarded when this is triggered.")]
    public int experienceValue = 10;

    [Header("Required Prefab")]
    [Tooltip("The generic WorldItem prefab to be spawned for each drop.")]
    public GameObject worldItemPrefab;

    public void DropLoot()
    {
        if (lootTable != null)
        {
            foreach (LootDrop drop in lootTable.potentialDrops)
            {
                if (Random.value <= drop.dropChance)
                {
                    int quantity = Random.Range(drop.minQuantity, drop.maxQuantity + 1);
                    if (worldItemPrefab != null)
                    {
                        Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                        GameObject droppedItemGO = Instantiate(worldItemPrefab, transform.position, randomRotation);

                        // --- MODIFIED: Changed GetComponent to GetComponentInChildren ---
                        WorldItem worldItem = droppedItemGO.GetComponentInChildren<WorldItem>();

                        if (worldItem != null)
                        {
                            worldItem.itemData = drop.itemData;
                            worldItem.quantity = quantity;
                        }
                        else
                        {
                            Debug.LogError("Instantiated worldItemPrefab but could not find the 'WorldItem' script on it or its children!", droppedItemGO);
                        }
                    }
                }
            }
        }

        if (experienceValue > 0 && PartyManager.instance != null)
        {
            PartyManager.instance.AddExperience(experienceValue);
        }
    }
}