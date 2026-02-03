using UnityEngine;
using System.Text;

[CreateAssetMenu(fileName = "New Weapon Class Stats", menuName = "Inventory/Stats/Weapon Base")]
public class ItemWeaponStats : ItemStats
{
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

        // Base Info
        sb.AppendLine($"Damage: <color=#FFFFFF>{DamageLow} - {DamageHigh}</color>");
        sb.AppendLine($"Speed: <color=#FFFFFF>{baseAttackTime}</color>");
        sb.AppendLine($"Type: <color=#DDDDDD>{handed} {weaponCategory}</color>");

        sb.AppendLine(); // Spacer

        // Primary Stats - Green for positive
        if (strengthBonus > 0) sb.AppendLine($"Strength: <color=#00FF00>+{strengthBonus}</color>");
        if (agilityBonus > 0) sb.AppendLine($"Agility: <color=#00FF00>+{agilityBonus}</color>");
        if (intelligenceBonus > 0) sb.AppendLine($"Intelligence: <color=#00FF00>+{intelligenceBonus}</color>");
        if (faithBonus > 0) sb.AppendLine($"Faith: <color=#00FF00>+{faithBonus}</color>");

        // Secondary Stats - Cyan/Yellow
        if (critRatingBonus > 0) sb.AppendLine($"Crit Rating: <color=#00FFFF>+{critRatingBonus}</color>");
        if (hasteRatingBonus > 0) sb.AppendLine($"Haste Rating: <color=#FFFF00>+{hasteRatingBonus}</color>");

        return sb.ToString().Trim();
    }
}