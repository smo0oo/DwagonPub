using UnityEngine;

/// <summary>
/// A helper class to define a single potential item drop in a loot table.
/// This is a plain C# class, not a MonoBehaviour or ScriptableObject.
/// </summary>
[System.Serializable]
public class LootDrop
{
    [Tooltip("The item that could drop.")]
    public ItemData itemData;

    [Tooltip("The minimum quantity of this item to drop.")]
    [Range(1, 100)]
    public int minQuantity = 1;

    [Tooltip("The maximum quantity of this item to drop.")]
    [Range(1, 100)]
    public int maxQuantity = 1;

    [Tooltip("The chance of this item dropping (0.0 = 0%, 1.0 = 100%).")]
    [Range(0f, 1f)]
    public float dropChance = 0.5f;
}