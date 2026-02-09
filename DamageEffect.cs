using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DamageEffect : IAbilityEffect
{
    [Header("Activation")]
    [Range(0f, 100f)] public float chance = 100f;

    [Header("Damage Settings")]
    [Tooltip("The base damage of the Ability itself.")]
    public int baseDamage = 5;

    public enum DamageType { Physical, Magical }
    public DamageType damageType = DamageType.Physical;

    [Tooltip("If true, adds the caster's global damage multiplier to this attack.")]
    public bool useGlobalStatBonus = true;

    [Header("Stat Scaling")]
    public List<StatScaling> scalingFactors;

    [Header("Splash Effect Settings")]
    public bool isSplash = false;
    public float splashRadius = 3f;
    public float splashDamageMultiplier = 0.5f;

    // Static buffer for Non-Allocating Physics
    private static Collider[] _splashBuffer = new Collider[50];

    public void Apply(GameObject caster, GameObject target)
    {
        if (chance < 100f && Random.Range(0f, 100f) > chance) return;

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
                float weaponDamage = 0f;
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

                finalDamage += weaponDamage;
                if (useGlobalStatBonus) finalDamage += casterStats.secondaryStats.damageMultiplier;

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

                if (Random.value < (casterStats.secondaryStats.critChance / 100f)) { finalDamage *= 2; isCrit = true; }
            }
            else // Magical
            {
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
                if (Random.value < (casterStats.secondaryStats.spellCritChance / 100f)) { finalDamage *= 2; isCrit = true; }
            }
        }

        int finalDamageInt = Mathf.FloorToInt(finalDamage);
        targetHealth.TakeDamage(finalDamageInt, damageType, isCrit, caster);

        if (isSplash) HandleSplash(target, caster, finalDamageInt, damageType, casterRoot);
    }

    private void HandleSplash(GameObject mainTarget, GameObject caster, int mainDamage, DamageType type, CharacterRoot casterRoot)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(mainTarget.transform.position, splashRadius, _splashBuffer);
        int splashDamage = Mathf.FloorToInt(mainDamage * splashDamageMultiplier);

        // Cache the Main Target's Root to ensure we don't hit them twice
        CharacterRoot mainTargetRoot = mainTarget.GetComponentInParent<CharacterRoot>();

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _splashBuffer[i];

            // 1. Ignore Aggro/Activation Triggers
            if (hit.gameObject.layer == 21) continue;

            // 2. Identify the root of what we hit
            CharacterRoot hitRoot = hit.GetComponentInParent<CharacterRoot>();

            // 3. [FIX] Compare CharacterRoot references, NOT transform.root
            // Check against Main Target
            if (hitRoot != null && mainTargetRoot != null && hitRoot == mainTargetRoot) continue;

            // Check against Caster (Self-Damage prevention)
            if (hitRoot != null && casterRoot != null && hitRoot == casterRoot) continue;

            // If we hit a non-character object (like a barrel), hitRoot is null.
            // We usually want to splash damage props, so we allow null hitRoot unless it's the caster's child prop.
            if (hitRoot == null && hit.transform.IsChildOf(caster.transform)) continue;

            // 4. Resolve Target for Damage
            GameObject targetObj = (hitRoot != null) ? hitRoot.gameObject : hit.gameObject;

            // Check hostility
            bool isHostile = (casterRoot == null) || (targetObj.layer != casterRoot.gameObject.layer);

            if (isHostile)
            {
                Health splashTargetHealth = targetObj.GetComponentInChildren<Health>();
                if (splashTargetHealth != null)
                {
                    splashTargetHealth.TakeDamage(splashDamage, type, false, caster);
                }
            }
        }
    }

    public string GetEffectDescription()
    {
        string prefix = (chance < 100f) ? $"{chance}% Chance to " : "";
        string description = $"{prefix}deal {baseDamage} {damageType} damage";
        if (isSplash) description += $" (AoE: {splashDamageMultiplier * 100}% damage in {splashRadius}m)";
        return description + ".";
    }
}