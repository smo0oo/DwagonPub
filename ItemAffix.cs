using UnityEngine;
using System.Collections.Generic;

public enum AffixType { Prefix, Suffix }

public enum SpecialMechanic
{
    None,
    LifeSteal,
    CooldownReduction,
    MovementSpeed,
    ExplodeOnKill
}

// AAA Addition: A scalable data structure to hold multiple mechanics per affix with roll variance.
[System.Serializable]
public struct AffixMechanicBonus
{
    public SpecialMechanic mechanic;
    [Tooltip("The minimum possible roll for this mechanic (e.g., 0.05 for 5%).")]
    public float minValue;
    [Tooltip("The maximum possible roll for this mechanic (e.g., 0.10 for 10%).")]
    public float maxValue;

    public float GetRandomValue() => Random.Range(minValue, maxValue);
}

[CreateAssetMenu(menuName = "RPG/Item Affix")]
public class ItemAffix : ScriptableObject
{
    public string affixName; // e.g., "Sharp", "of the Bear"
    public AffixType type;

    [Header("Restrictions")]
    // Only allow this affix on specific item types (e.g., can't have +Damage on Chest armor)
    public List<ItemType> allowedItemTypes;

    [Header("Economy")]
    [Tooltip("Multiply the item's base value. 1.5 = +50% price.")]
    public float priceMultiplier = 1.0f;

    [Header("Core Stat Modifiers")]
    public int strengthBonusMin;
    public int strengthBonusMax;

    public int agilityBonusMin;
    public int agilityBonusMax;

    public int intelligenceBonusMin;
    public int intelligenceBonusMax;

    public int damageBonusMin;
    public int damageBonusMax;

    [Header("Combat Feel & Scaling")]
    [Tooltip("Percentage reduction in attack time (0.1 = 10% faster)")]
    public float attackSpeedMultiplier = 0f;

    [Tooltip("Increases the baseline critical strike chance by this percentage.")]
    public float critChanceBonus = 0f;

    [Tooltip("Increases the damage multiplier when a critical strike lands.")]
    public float critDamageMultiplierBonus = 0f;

    [Header("Build-Defining Mechanics")]
    [Tooltip("A list of special mechanical hooks that radically alter how a build plays. You can stack as many as you want.")]
    public List<AffixMechanicBonus> specialMechanics = new List<AffixMechanicBonus>();

    // Helpers to get a random value within range for item generation
    public int GetStrength() => Random.Range(strengthBonusMin, strengthBonusMax + 1);
    public int GetAgility() => Random.Range(agilityBonusMin, agilityBonusMax + 1);
    public int GetIntelligence() => Random.Range(intelligenceBonusMin, intelligenceBonusMax + 1);
    public int GetDamage() => Random.Range(damageBonusMin, damageBonusMax + 1);
}