using UnityEngine;

[RequireComponent(typeof(Inventory))]
public class MerchantStockController : MonoBehaviour
{
    [Header("Random Stock Generation")]
    [Tooltip("The Loot Table determining what random items this merchant CAN sell.")]
    public LootTable randomStockTable;

    [Tooltip("Minimum number of random items to generate.")]
    public int minRandomItems = 3;
    [Tooltip("Maximum number of random items to generate.")]
    public int maxRandomItems = 8;

    [Header("Settings")]
    [Tooltip("If true, keeps items defined in the Inspector. If false, wipes the inventory before generating.")]
    public bool keepManualItems = true;

    private Inventory merchantInventory;

    void Start()
    {
        merchantInventory = GetComponent<Inventory>();
        Restock();
    }

    public void Restock()
    {
        if (merchantInventory == null || randomStockTable == null) return;

        // 1. Handle Existing Items
        if (!keepManualItems)
        {
            // Clear everything if you want a purely random merchant
            // Note: A simple clear loop or re-initialization might be needed depending on Inventory implementation
            // For now, we assume we just append if keepManualItems is true.
            for (int i = 0; i < merchantInventory.items.Count; i++)
            {
                // Remove existing stock (sets to null/0)
                merchantInventory.RemoveItem(i, 9999);
            }
        }

        // 2. Generate New Random Stock
        int itemsToCreate = Random.Range(minRandomItems, maxRandomItems + 1);

        for (int i = 0; i < itemsToCreate; i++)
        {
            // Pick a random entry from the table
            LootDrop potentialDrop = GetRandomDropFromTable();

            if (potentialDrop != null)
            {
                ItemData itemToAdd = potentialDrop.itemData;
                int quantity = Random.Range(potentialDrop.minQuantity, potentialDrop.maxQuantity + 1);

                // A. If it's Equipment (Sword/Armor), Randomize it!
                if (!itemToAdd.isStackable && LootFactory.instance != null)
                {
                    itemToAdd = LootFactory.instance.GenerateLoot(itemToAdd);
                }
                // B. If it's Stackable (Potion), use as is.

                // Add to the merchant's inventory
                merchantInventory.AddItem(itemToAdd, quantity);
            }
        }
    }

    private LootDrop GetRandomDropFromTable()
    {
        if (randomStockTable.potentialDrops.Count == 0) return null;

        // Simple weighted roll could go here, or just pick random for now
        LootDrop drop = randomStockTable.potentialDrops[Random.Range(0, randomStockTable.potentialDrops.Count)];

        // Check drop chance
        if (Random.value <= drop.dropChance) return drop;
        return null;
    }
}