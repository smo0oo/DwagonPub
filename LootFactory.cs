using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootFactory : MonoBehaviour
{
    public static LootFactory instance;

    [Header("Databases")]
    public List<ItemAffix> allPrefixes;
    public List<ItemAffix> allSuffixes;

    void Awake()
    {
        instance = this;
    }

    public ItemData GenerateLoot(ItemData template)
    {
        // 1. Always clone the base data so we don't modify the project file
        ItemData newItem = Instantiate(template);
        newItem.name = template.name;

        // 2. Clone the stats container if it exists
        if (template.stats != null)
        {
            newItem.stats = Instantiate(template.stats);
            newItem.stats.name = template.stats.name;
        }

        // --- NEW: CHECK FOR UNIQUE STATUS ---
        // If this is a Unique item (e.g. "Excalibur" or "Key to Dungeon"), 
        // return it immediately without adding random names or stats.
        if (template.isUnique)
        {
            return newItem;
        }
        // ------------------------------------

        // 3. Roll for Rarity
        float roll = Random.value;
        if (roll < 0.05f) MakeRare(newItem);
        else if (roll < 0.25f) MakeMagic(newItem);

        return newItem;
    }

    // ... (Rest of the script: MakeMagic, MakeRare, AddRandomAffix, ApplyAffix remain unchanged) ...

    private void MakeMagic(ItemData item)
    {
        item.stats.rarity = ItemStats.Rarity.Uncommon;
        if (Random.value > 0.5f) AddRandomAffix(item, allPrefixes);
        else AddRandomAffix(item, allSuffixes);
    }

    private void MakeRare(ItemData item)
    {
        item.stats.rarity = ItemStats.Rarity.Rare;
        AddRandomAffix(item, allPrefixes);
        AddRandomAffix(item, allSuffixes);
    }

    private void AddRandomAffix(ItemData item, List<ItemAffix> pool)
    {
        var validAffixes = pool.Where(x => x.allowedItemTypes.Contains(item.itemType)).ToList();
        if (validAffixes.Count == 0) return;
        ApplyAffix(item, validAffixes[Random.Range(0, validAffixes.Count)]);
    }

    private void ApplyAffix(ItemData item, ItemAffix affix)
    {
        if (affix.type == AffixType.Prefix) item.displayName = $"{affix.affixName} {item.displayName}";
        else item.displayName = $"{item.displayName} {affix.affixName}";

        if (item.stats is ItemWeaponStats weaponStats)
        {
            weaponStats.strengthBonus += affix.GetStrength();
            weaponStats.agilityBonus += affix.GetAgility();
            int dmg = affix.GetDamage();
            weaponStats.DamageLow += dmg;
            weaponStats.DamageHigh += dmg;
            if (affix.attackSpeedMultiplier > 0) weaponStats.baseAttackTime *= (1.0f - affix.attackSpeedMultiplier);
        }
    }
}