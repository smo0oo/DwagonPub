using UnityEngine;
using System.Text;

[CreateAssetMenu(fileName = "New Armour Class Stats", menuName = "Inventory/Stats/Armour Base")]
public class ItemArmourStats : ItemStats
{
    [Header("Armour Stats")]
    public int armourPhysRes = 10;
    public int armourMagRes = 10;

    [Header("Primary Stat Modifiers")]
    public int strengthBonus = 0;
    public int agilityBonus = 0;
    public int intelligenceBonus = 0;
    public int faithBonus = 0;

    [Header("Secondary Stat Bonuses (Ratings)")]
    public int critRatingBonus = 0;
    public int dodgeRatingBonus = 0;
    public int blockRatingBonus = 0;
    public int parryRatingBonus = 0;

    public enum ArmourCategory { Cloth, Leather, Mail, Plate }
    public ArmourCategory armourCategory;

    // --- UPDATED ENUM ---
    public enum ArmourSlot
    {
        Head,
        Chest,
        Hands,
        Belt,
        Legs,
        Feet
    }
    public ArmourSlot armourSlot;

    public override string GetStatsDescription()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Physical Resist: {armourPhysRes}");
        sb.AppendLine($"Magical Resist: {armourMagRes}");
        sb.AppendLine($"Slot: {armourSlot}");

        if (strengthBonus > 0) sb.AppendLine($"Strength: +{strengthBonus}");
        if (agilityBonus > 0) sb.AppendLine($"Agility: +{agilityBonus}");
        if (intelligenceBonus > 0) sb.AppendLine($"Intelligence: +{intelligenceBonus}");
        if (faithBonus > 0) sb.AppendLine($"Faith: +{faithBonus}");

        if (critRatingBonus > 0) sb.AppendLine($"Critical Strike Rating: +{critRatingBonus}");
        if (dodgeRatingBonus > 0) sb.AppendLine($"Dodge Rating: +{dodgeRatingBonus}");
        if (blockRatingBonus > 0) sb.AppendLine($"Block Rating: +{blockRatingBonus}");
        if (parryRatingBonus > 0) sb.AppendLine($"Parry Rating: +{parryRatingBonus}");

        return sb.ToString().Trim();
    }
}