using UnityEngine;
using System.Text;

[CreateAssetMenu(fileName = "New Weapon Class Stats", menuName = "Inventory/Stats/Weapon Base")]
public class ItemWeaponStats : ItemStats
{
    // --- RENAMED for clarity ---
    [Header("Weapon Stats")]
    [Tooltip("The base time in seconds between default attacks with this weapon.")]
    public float baseAttackTime = 2.0f;
    public int DamageLow;
    public int DamageHigh;

    [Header("Primary Stat Modifiers")]
    public int strengthBonus = 0;
    public int agilityBonus = 0;
    public int intelligenceBonus = 0;
    public int faithBonus = 0;

    [Header("Secondary Stat Bonuses (Ratings)")]
    public int critRatingBonus = 0;
    public int hasteRatingBonus = 0;

    public enum WeaponCategory { Hands, Sword, Mace, Axe, Knife, PoleArm, Spear, Wand, Staff, Bow, CrossBow }
    public WeaponCategory weaponCategory;
    public enum Handed { OneHanded, TwoHanded }
    public Handed handed;

    public override string GetStatsDescription()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"Damage: {DamageLow} - {DamageHigh}");
        sb.AppendLine($"Speed: {baseAttackTime}");
        sb.AppendLine($"Type: {handed} {weaponCategory}");

        if (strengthBonus > 0) sb.AppendLine($"Strength: +{strengthBonus}");
        if (agilityBonus > 0) sb.AppendLine($"Agility: +{agilityBonus}");
        if (intelligenceBonus > 0) sb.AppendLine($"Intelligence: +{intelligenceBonus}");
        if (faithBonus > 0) sb.AppendLine($"Faith: +{faithBonus}");

        if (critRatingBonus > 0) sb.AppendLine($"Critical Strike Rating: +{critRatingBonus}");
        if (hasteRatingBonus > 0) sb.AppendLine($"Haste Rating: +{hasteRatingBonus}");

        return sb.ToString().Trim();
    }
}