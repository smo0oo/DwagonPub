using UnityEngine;
using System.Collections.Generic;

public enum ItemType
{
    Weapon,
    Armour,
    Trinket,
    Consumable,
    Resource,
    Quest,
    Junk,
    Currency
}

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    [Header("Item Information")]
    public string id = "Item";
    public string itemName = "New Item";
    public string displayName = "New Item";
    [TextArea(4, 10)]
    public string description = "Item Description";
    public Sprite icon;

    [Header("Visuals")]
    [Tooltip("The 3D prefab to instantiate when this item is equipped.")]
    public GameObject equippedPrefab;

    [Header("Item Properties")]
    public ItemType itemType = ItemType.Junk;
    public int itemValue = 0;
    public ItemStats stats;

    [Header("Randomization Settings")]
    [Tooltip("If true, this item will NEVER get random Affixes (Prefixes/Suffixes) or stat changes. Use this for Quest items or Unique Legendaries.")]
    public bool isUnique = false; // <--- NEW FIELD

    [Header("Stacking")]
    public bool isStackable = true;
    public int maxStackSize = 16;

    [Header("Requirements")]
    public int levelRequirement = 1;
    public List<PlayerClass> allowedClasses = new List<PlayerClass>();

    public int GetMaxStackSize()
    {
        return isStackable ? maxStackSize : 1;
    }

    private void OnValidate()
    {
        if (!isStackable)
        {
            maxStackSize = 1;
        }
    }
}