using UnityEngine;

[CreateAssetMenu(fileName = "New Trinket Class Stats", menuName = "Inventory/Stats/Trinket Base")]
public class ItemTrinketStats : ItemStats
{
    [Header("Stat Modifiers")]
    public int strengthBonus = 0;
    public int agilityBonus = 0;
    public int intelligenceBonus = 0;
    public int faithBonus = 0; // Renamed from spiritBonus

    public enum TrinketSlot { Neck, Ring }
    public TrinketSlot trinketSlot;
    public override string GetStatsDescription()
    {
        string stats = $"Slot: {trinketSlot}";
        if (strengthBonus > 0) stats += $"\nStrength: +{strengthBonus}";
        if (agilityBonus > 0) stats += $"\nAgility: +{agilityBonus}";
        if (intelligenceBonus > 0) stats += $"\nIntelligence: +{intelligenceBonus}";
        if (faithBonus > 0) stats += $"\nFaith: +{faithBonus}"; // Renamed from Spirit
        return stats;
    }
}
