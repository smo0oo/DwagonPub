using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DamageEffect : IAbilityEffect
{
    [Tooltip("The base damage of the effect.")]
    public int baseDamage = 5;

    public enum DamageType { Physical, Magical }
    public DamageType damageType = DamageType.Physical;

    // --- NEW SETTING ---
    [Tooltip("If true, adds the caster's global damage multiplier (e.g. Strength) to this attack. Uncheck this for small attacks like Punch.")]
    public bool useGlobalStatBonus = true;
    // -------------------

    [Header("Stat Scaling")]
    [Tooltip("How the damage scales with the caster's primary stats.")]
    public List<StatScaling> scalingFactors;

    [Header("Splash Effect Settings")]
    public bool isSplash = false;
    public float splashRadius = 3f;
    public float splashDamageMultiplier = 0.5f;

    // Static buffer for Non-Allocating Physics
    private static Collider[] _splashBuffer = new Collider[50];

    public void Apply(GameObject caster, GameObject target)
    {
        Health targetHealth = target.GetComponentInChildren<Health>();
        if (targetHealth == null) return;

        float finalDamage = baseDamage;
        bool isCrit = false;

        CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
        PlayerStats casterStats = (casterRoot != null) ? casterRoot.GetComponentInChildren<PlayerStats>() : null;

        if (casterStats != null)
        {
            if (damageType == DamageType.Physical)
            {
                float weaponDamage = baseDamage;
                PlayerEquipment equipment = caster.GetComponentInParent<PlayerEquipment>();
                if (equipment != null)
                {
                    equipment.equippedItems.TryGetValue(EquipmentType.RightHand, out ItemStack rightHandItem);
                    equipment.equippedItems.TryGetValue(EquipmentType.LeftHand, out ItemStack leftHandItem);

                    ItemWeaponStats weaponStats = null;
                    if (rightHandItem != null && rightHandItem.itemData.stats is ItemWeaponStats)
                        weaponStats = rightHandItem.itemData.stats as ItemWeaponStats;
                    else if (leftHandItem != null && leftHandItem.itemData.stats is ItemWeaponStats)
                        weaponStats = leftHandItem.itemData.stats as ItemWeaponStats;

                    if (weaponStats != null)
                        weaponDamage = Random.Range(weaponStats.DamageLow, weaponStats.DamageHigh + 1);
                }

                finalDamage = weaponDamage;

                // --- FIX: Only add global stats if the box is checked ---
                if (useGlobalStatBonus)
                {
                    finalDamage += casterStats.secondaryStats.damageMultiplier;
                }
                // --------------------------------------------------------

                foreach (var scaling in scalingFactors)
                {
                    switch (scaling.stat)
                    {
                        case StatType.Strength: finalDamage += casterStats.finalStrength * scaling.ratio; break;
                        case StatType.Agility: finalDamage += casterStats.finalAgility * scaling.ratio; break;
                        case StatType.Intelligence: finalDamage += casterStats.finalIntelligence * scaling.ratio; break;
                        case StatType.Faith: finalDamage += casterStats.finalFaith * scaling.ratio; break;
                    }
                }

                if (Random.value < (casterStats.secondaryStats.critChance / 100f))
                {
                    finalDamage *= 2;
                    isCrit = true;
                }
            }
            else // Magical Damage
            {
                // Magic usually uses percentage multipliers, so we keep this logic standard
                finalDamage *= (1 + casterStats.secondaryStats.magicAttackDamage / 100f);

                foreach (var scaling in scalingFactors)
                {
                    switch (scaling.stat)
                    {
                        case StatType.Strength: finalDamage += casterStats.finalStrength * scaling.ratio; break;
                        case StatType.Agility: finalDamage += casterStats.finalAgility * scaling.ratio; break;
                        case StatType.Intelligence: finalDamage += casterStats.finalIntelligence * scaling.ratio; break;
                        case StatType.Faith: finalDamage += casterStats.finalFaith * scaling.ratio; break;
                    }
                }
                if (Random.value < (casterStats.secondaryStats.spellCritChance / 100f))
                {
                    finalDamage *= 2;
                    isCrit = true;
                }
            }
        }

        int finalDamageInt = Mathf.FloorToInt(finalDamage);
        targetHealth.TakeDamage(finalDamageInt, damageType, isCrit, caster);

        if (isSplash)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(target.transform.position, splashRadius, _splashBuffer);
            int splashDamage = Mathf.FloorToInt(finalDamageInt * splashDamageMultiplier);

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _splashBuffer[i];
                if (hit.transform.root == target.transform.root || hit.transform.root == caster.transform.root) continue;

                CharacterRoot splashTargetRoot = hit.GetComponentInParent<CharacterRoot>();
                if (splashTargetRoot == null) continue;

                bool isHostile = (casterRoot == null) || (splashTargetRoot.gameObject.layer != casterRoot.gameObject.layer);

                if (isHostile)
                {
                    Health splashTargetHealth = splashTargetRoot.GetComponentInChildren<Health>();
                    if (splashTargetHealth != null)
                    {
                        splashTargetHealth.TakeDamage(splashDamage, damageType, false, caster);
                    }
                }
            }
        }
    }

    public string GetEffectDescription()
    {
        string description = $"Deals {baseDamage} {damageType} damage.";
        if (isSplash)
        {
            description += $" Also deals {splashDamageMultiplier * 100}% damage to nearby enemies.";
        }
        return description;
    }
}