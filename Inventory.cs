using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

[System.Serializable]
public class ItemStack
{
    public ItemData itemData;
    public int quantity;

    public ItemStack(ItemData data, int amount)
    {
        itemData = data;
        quantity = amount;
    }
}

public class Inventory : MonoBehaviour
{
    public event Action OnInventoryChanged;

    [Tooltip("The list of items this inventory is holding.")]
    public List<ItemStack> items = new List<ItemStack>();

    [Tooltip("The total number of slots this inventory has.")]
    public int inventorySize = 24;

    public enum SortType { Default, Rarity, Value, Type }
    private List<ItemStack> originalOrder;

    // --- THIS METHOD HAS BEEN UPDATED WITH SMARTER LOGIC ---
    void Awake()
    {
        foreach (var item in items)
        {
            if (item == null) continue;

            // If an item asset is assigned in the editor...
            if (item.itemData != null)
            {
                // For non-stackable items, the quantity should ALWAYS be 1.
                // This corrects items added in the editor with a default quantity of 0.
                if (!item.itemData.isStackable)
                {
                    item.quantity = 1;
                }
                // If a stackable item has 0 quantity, it's invalid "ghost" data. Clear it.
                else if (item.quantity <= 0)
                {
                    item.itemData = null;
                    item.quantity = 0;
                }
            }
            // If no item data is assigned, ensure quantity is also 0.
            else
            {
                item.quantity = 0;
            }
        }

        // Pad the list with empty slots
        while (items.Count < inventorySize)
        {
            items.Add(new ItemStack(null, 0));
        }
        originalOrder = new List<ItemStack>(items);
    }

    public bool AddItem(ItemData itemToAdd, int amount = 1)
    {
        bool itemAdded = false;

        // --- This logic uses isStackable from ItemData now ---
        int amountToAdd = itemToAdd.isStackable ? amount : 1;

        if (itemToAdd.isStackable)
        {
            foreach (ItemStack stack in items)
            {
                if (stack.itemData == itemToAdd && stack.quantity < itemToAdd.GetMaxStackSize())
                {
                    int spaceLeft = itemToAdd.GetMaxStackSize() - stack.quantity;
                    int amountToStack = Mathf.Min(amountToAdd, spaceLeft);

                    stack.quantity += amountToStack;
                    amountToAdd -= amountToStack;
                    itemAdded = true;

                    if (amountToAdd <= 0) break;
                }
            }
        }

        if (amountToAdd > 0)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].itemData == null || items[i].quantity <= 0)
                {
                    items[i].itemData = itemToAdd;
                    items[i].quantity = amountToAdd;
                    itemAdded = true;
                    amountToAdd = 0;

                    if (i < originalOrder.Count)
                    {
                        originalOrder[i] = items[i];
                    }
                    break;
                }
            }
        }

        if (itemAdded)
        {
            OnInventoryChanged?.Invoke();
            return true;
        }

        Debug.LogWarning($"{this.gameObject.name}'s inventory is full. Could not add {itemToAdd.displayName}.");
        return false;
    }

    #region Unchanged Code
    public void SortItems(SortType sortType)
    {
        if (sortType == SortType.Default)
        {
            items = new List<ItemStack>(originalOrder);
            while (items.Count < inventorySize)
            {
                items.Add(new ItemStack(null, 0));
            }
        }
        else
        {
            List<ItemStack> filledSlots = items.Where(s => s.itemData != null && s.quantity > 0).ToList();
            List<ItemStack> emptySlots = items.Where(s => s.itemData == null || s.quantity <= 0).ToList();

            switch (sortType)
            {
                case SortType.Rarity:
                    filledSlots = filledSlots.OrderByDescending(s => s.itemData.stats.rarity)
                                             .ThenBy(s => s.itemData.displayName).ToList();
                    break;
                case SortType.Value:
                    filledSlots = filledSlots.OrderByDescending(s => s.itemData.itemValue * s.quantity)
                                             .ThenBy(s => s.itemData.displayName).ToList();
                    break;
                case SortType.Type:
                    filledSlots = filledSlots.OrderBy(s => GetItemTypeSortOrder(s.itemData.itemType))
                                             .ThenBy(s => s.itemData.displayName).ToList();
                    break;
            }
            items = filledSlots.Concat(emptySlots).ToList();
        }

        OnInventoryChanged?.Invoke();
    }
    private int GetItemTypeSortOrder(ItemType type)
    {
        switch (type)
        {
            case ItemType.Armour: return 0;
            case ItemType.Weapon: return 1;
            case ItemType.Consumable: return 2;
            case ItemType.Resource: return 3;
            case ItemType.Quest: return 4;
            case ItemType.Junk: return 5;
            case ItemType.Currency: return 6;
            default: return 7;
        }
    }
    public bool AddItemToNewStack(ItemData itemToAdd, int amount)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].itemData == null || items[i].quantity <= 0)
            {
                items[i].itemData = itemToAdd;
                items[i].quantity = amount;
                if (i < originalOrder.Count) originalOrder[i] = new ItemStack(itemToAdd, amount);
                OnInventoryChanged?.Invoke();
                return true;
            }
        }
        return false;
    }
    public void SwapItems(int indexA, int indexB)
    {
        if (indexA < 0 || indexA >= items.Count || indexB < 0 || indexB >= items.Count) return;

        (items[indexA], items[indexB]) = (items[indexB], items[indexA]);
        (originalOrder[indexA], originalOrder[indexB]) = (originalOrder[indexB], originalOrder[indexA]);
        OnInventoryChanged?.Invoke();
    }
    public void SetItemStack(int index, ItemStack stack)
    {
        if (index < 0 || index >= items.Count) return;
        items[index] = stack;
        originalOrder[index] = stack;
        OnInventoryChanged?.Invoke();
    }
    public void RemoveItem(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= items.Count) return;
        ItemStack stack = items[slotIndex];
        if (stack.itemData != null)
        {
            stack.quantity -= amount;
            if (stack.quantity <= 0)
            {
                stack.itemData = null;
                stack.quantity = 0;
            }
            originalOrder[slotIndex] = new ItemStack(stack.itemData, stack.quantity);
            OnInventoryChanged?.Invoke();
        }
    }
    #endregion
}