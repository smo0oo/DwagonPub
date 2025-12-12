using UnityEngine;

[System.Serializable]
public class SecondaryStats
{
    // --- Ratings (from stats and gear) ---
    public int blockRating;
    public int parryRating;
    public int damageRating;
    public int critRating;
    public int dodgeRating;
    public int hasteRating;
    public int spellCritRating;
    public int cooldownReductionRating;
    public int healingBonusRating;
    public int magicResistanceRating;
    public int magicAttackRating;
    public int physicalResistanceRating;

    // --- Final Percentages (calculated from ratings and level) ---
    public float blockChance;
    public float parryChance;
    public float damageMultiplier;
    public float critChance;
    public float dodgeChance;
    public float attackSpeed;
    public float spellCritChance;
    public float cooldownReduction;
    public float healingBonus;
    public float magicResistance;
    public float magicAttackDamage;
    public float physicalResistance;

    // --- Tooltip Strings ---
    public string healthTooltip; // <-- ADD THIS
    public string manaTooltip;   // <-- ADD THIS
    public string critChanceTooltip;
    public string spellCritChanceTooltip;
    public string attackSpeedTooltip;
    public string cooldownReductionTooltip;
    public string dodgeChanceTooltip;
    public string parryChanceTooltip;
    public string blockChanceTooltip;
    public string magicResistTooltip;
    public string healingBonusTooltip;
}