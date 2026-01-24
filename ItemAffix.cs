using UnityEngine;
using System.Collections.Generic; // <--- THIS WAS MISSING

public enum AffixType { Prefix, Suffix }

[CreateAssetMenu(menuName = "RPG/Item Affix")]
public class ItemAffix : ScriptableObject
{
    public string affixName; // e.g., "Sharp", "of the Bear"
    public AffixType type;

    [Header("Restrictions")]
    // Only allow this affix on specific item types (e.g., can't have +Damage on Chest armor)
    public List<ItemType> allowedItemTypes;

    [Header("Stat Modifiers")]
    // Ranges allow for variance (e.g. +5 to +10 Strength)
    public int strengthBonusMin;
    public int strengthBonusMax;

    public int agilityBonusMin;
    public int agilityBonusMax;

    public int damageBonusMin;
    public int damageBonusMax;

    [Tooltip("Percentage reduction in attack time (0.1 = 10% faster)")]
    public float attackSpeedMultiplier = 0f;

    // Helper to get a random value within range
    public int GetStrength() => Random.Range(strengthBonusMin, strengthBonusMax + 1);
    public int GetAgility() => Random.Range(agilityBonusMin, agilityBonusMax + 1);
    public int GetDamage() => Random.Range(damageBonusMin, damageBonusMax + 1);
}