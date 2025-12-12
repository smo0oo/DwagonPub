using UnityEngine;

/// <summary>
/// An abstract base class for all item stat ScriptableObjects.
/// You cannot create an instance of this directly, only its children (e.g., WeaponStats).
/// </summary>
public abstract class ItemStats : ScriptableObject
{
    [Header("Generaic Stats")]
    public int itemDingus = 10;
    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
    public Rarity rarity;


    /// <summary>
    /// Returns a formatted string of the stats specific to this type.
    /// This MUST be implemented by any script that inherits from ItemStats.
    /// </summary>
    public abstract string GetStatsDescription();
}