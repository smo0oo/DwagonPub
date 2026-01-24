using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootFactory : MonoBehaviour
{
    public static LootFactory instance;

    [Header("Databases")]
    public List<ItemAffix> allPrefixes;
    public List<ItemAffix> allSuffixes;

    [Header("Rarity Settings")]
    [Tooltip("Chance for an item to be Rare (checked first). 0.05 = 5%")]
    [Range(0f, 1f)] public float rareChance = 0.05f;

    [Tooltip("Chance for an item to be Magic (checked if not Rare). 0.25 = 25%")]
    [Range(0f, 1f)] public float magicChance = 0.25f;

    void Awake()
    {
        instance = this;
    }

    public ItemData GenerateLoot(ItemData template)
    {
        // 1. Create Clone
        ItemData newItem = Instantiate(template);
        newItem.name = template.name; // Keep Unity internal name consistent

        // 2. Clone Stats (Crucial: otherwise all items share the same stats object)
        if (template.stats != null)
        {
            newItem.stats = Instantiate(template.stats);
            newItem.stats.name = template.stats.name;
        }

        // --- Unique Check ---
        // If this is a Unique item (e.g. "Excalibur"), return it immediately 
        // without adding random names or stats.
        if (template.isUnique)
        {
            return newItem;
        }

        // 3. Roll for Rarity using Inspector variables
        float roll = Random.value;

        if (roll < rareChance)
        {
            MakeRare(newItem);
        }
        else if (roll < magicChance)
        {
            MakeMagic(newItem);
        }
        // Else: It remains Common (Base stats, Base name)

        return newItem;
    }

    private void MakeMagic(ItemData item)
    {
        if (item.stats != null) item.stats.rarity = ItemStats.Rarity.Uncommon;

        // 50/50 chance for Prefix vs Suffix
        if (Random.value > 0.5f) AddRandomAffix(item, allPrefixes);
        else AddRandomAffix(item, allSuffixes);
    }

    private void MakeRare(ItemData item)
    {
        if (item.stats != null) item.stats.rarity = ItemStats.Rarity.Rare;

        // Rare gets BOTH a Prefix and a Suffix
        AddRandomAffix(item, allPrefixes);
        AddRandomAffix(item, allSuffixes);
    }

    private void AddRandomAffix(ItemData item, List<ItemAffix> pool)
    {
        // Filter the pool to only find affixes allowed for this item type (e.g. No "+Damage" on Chest Armor)
        var validAffixes = pool.Where(x => x.allowedItemTypes.Contains(item.itemType)).ToList();

        if (validAffixes.Count == 0) return;

        ItemAffix chosen = validAffixes[Random.Range(0, validAffixes.Count)];
        ApplyAffix(item, chosen);
    }

    private void ApplyAffix(ItemData item, ItemAffix affix)
    {
        // 1. Rename
        if (affix.type == AffixType.Prefix)
            item.displayName = $"{affix.affixName} {item.displayName}";
        else
            item.displayName = $"{item.displayName} {affix.affixName}";

        // 2. Adjust Price (Economy Update)
        // We cast to int because itemValue is an integer
        if (affix.priceMultiplier > 0)
        {
            item.itemValue = Mathf.CeilToInt(item.itemValue * affix.priceMultiplier);
        }

        // 3. Apply Stats
        if (item.stats is ItemWeaponStats weaponStats)
        {
            weaponStats.strengthBonus += affix.GetStrength();
            weaponStats.agilityBonus += affix.GetAgility();

            int dmg = affix.GetDamage();
            weaponStats.DamageLow += dmg;
            weaponStats.DamageHigh += dmg;

            if (affix.attackSpeedMultiplier > 0)
            {
                weaponStats.baseAttackTime *= (1.0f - affix.attackSpeedMultiplier);
            }
        }
        else if (item.stats is ItemArmourStats armourStats)
        {
            armourStats.strengthBonus += affix.GetStrength();
            armourStats.agilityBonus += affix.GetAgility();
        }
    }
}