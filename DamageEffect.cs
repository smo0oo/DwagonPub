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

    [Header("Impact & Force")]
    public int poiseDamage = 10;
    public float knockbackForce = 0f;

    [Header("Stat Scaling")]
    public List<StatScaling> scalingFactors;

    [Header("Splash Effect Settings")]
    public bool isSplash = false;
    public float splashRadius = 3f;
    public float splashDamageMultiplier = 0.5f;

    private static Collider[] _splashBuffer = new Collider[50];

    public void Apply(GameObject caster, GameObject target)
    {
        if (chance < 100f && Random.Range(0f, 100f) > chance) return;

        Health targetHealth = target.GetComponentInChildren<Health>();

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
            else
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
        // --- AAA FIX: ENEMY LEVEL SCALING MULTIPLIER ---
        else if (caster != null)
        {
            EnemyAI enemyCaster = caster.GetComponent<EnemyAI>() ?? caster.GetComponentInParent<EnemyAI>();
            if (enemyCaster != null)
            {
                finalDamage *= enemyCaster.CurrentDamageMultiplier;
            }
        }
        // -----------------------------------------------

        int finalDamageInt = Mathf.FloorToInt(finalDamage);

        if (targetHealth != null)
        {
            targetHealth.TakeDamage(finalDamageInt, damageType, isCrit, caster, knockbackForce, poiseDamage);
        }

        if (isSplash)
        {
            HandleSplash(target, caster, finalDamageInt, damageType, casterRoot);
        }
    }

    private void HandleSplash(GameObject mainTarget, GameObject caster, int mainDamage, DamageType type, CharacterRoot casterRoot)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(mainTarget.transform.position, splashRadius, _splashBuffer);
        int splashDamage = Mathf.FloorToInt(mainDamage * splashDamageMultiplier);

        int splashPoise = Mathf.FloorToInt(poiseDamage * splashDamageMultiplier);
        float splashKnockback = knockbackForce * splashDamageMultiplier;

        CharacterRoot mainTargetRoot = mainTarget.GetComponentInParent<CharacterRoot>();

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _splashBuffer[i];
            if (hit.gameObject.layer == 21) continue;

            CharacterRoot hitRoot = hit.GetComponentInParent<CharacterRoot>();
            if (hitRoot != null && mainTargetRoot != null && hitRoot == mainTargetRoot) continue;
            if (hitRoot != null && casterRoot != null && hitRoot == casterRoot) continue;
            if (hitRoot == null && hit.transform.IsChildOf(caster.transform)) continue;

            GameObject targetObj = (hitRoot != null) ? hitRoot.gameObject : hit.gameObject;

            bool isProp = targetObj.GetComponent<DestructibleProp>() != null || targetObj.GetComponentInChildren<DestructibleProp>() != null;
            bool isHostile = isProp || (casterRoot == null) || (targetObj.layer != casterRoot.gameObject.layer);

            if (isHostile)
            {
                Health splashTargetHealth = targetObj.GetComponentInChildren<Health>();
                if (splashTargetHealth != null)
                {
                    splashTargetHealth.TakeDamage(splashDamage, type, false, caster, splashKnockback, splashPoise);
                }
            }
        }
    }

    // --- AAA FIX: LIVE TOOLTIP SCALING ---
    public string GetEffectDescription(GameObject caster = null)
    {
        float previewDamageLow = baseDamage;
        float previewDamageHigh = baseDamage;

        if (caster != null)
        {
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
            PlayerStats casterStats = (casterRoot != null) ? casterRoot.GetComponentInChildren<PlayerStats>() : null;

            if (casterStats != null)
            {
                if (damageType == DamageType.Physical)
                {
                    float weaponDamageLow = 0f;
                    float weaponDamageHigh = 0f;
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
                        {
                            weaponDamageLow = weaponStats.DamageLow;
                            weaponDamageHigh = weaponStats.DamageHigh;
                        }
                    }

                    previewDamageLow += weaponDamageLow;
                    previewDamageHigh += weaponDamageHigh;

                    if (useGlobalStatBonus)
                    {
                        previewDamageLow += casterStats.secondaryStats.damageMultiplier;
                        previewDamageHigh += casterStats.secondaryStats.damageMultiplier;
                    }

                    foreach (var scaling in scalingFactors)
                    {
                        float statBonus = 0f;
                        switch (scaling.stat)
                        {
                            case StatType.Strength: statBonus = casterStats.finalStrength * scaling.ratio; break;
                            case StatType.Agility: statBonus = casterStats.finalAgility * scaling.ratio; break;
                            case StatType.Intelligence: statBonus = casterStats.finalIntelligence * scaling.ratio; break;
                            case StatType.Faith: statBonus = casterStats.finalFaith * scaling.ratio; break;
                        }
                        previewDamageLow += statBonus;
                        previewDamageHigh += statBonus;
                    }
                }
                else // Magical
                {
                    float magicMult = (1 + casterStats.secondaryStats.magicAttackDamage / 100f);
                    previewDamageLow *= magicMult;
                    previewDamageHigh *= magicMult;

                    foreach (var scaling in scalingFactors)
                    {
                        float statBonus = 0f;
                        switch (scaling.stat)
                        {
                            case StatType.Strength: statBonus = casterStats.finalStrength * scaling.ratio; break;
                            case StatType.Agility: statBonus = casterStats.finalAgility * scaling.ratio; break;
                            case StatType.Intelligence: statBonus = casterStats.finalIntelligence * scaling.ratio; break;
                            case StatType.Faith: statBonus = casterStats.finalFaith * scaling.ratio; break;
                        }
                        previewDamageLow += statBonus;
                        previewDamageHigh += statBonus;
                    }
                }
            }
            // --- AAA FIX: ENEMY TOOLTIP SCALING ---
            else
            {
                EnemyAI enemyCaster = caster.GetComponent<EnemyAI>() ?? caster.GetComponentInParent<EnemyAI>();
                if (enemyCaster != null)
                {
                    previewDamageLow *= enemyCaster.CurrentDamageMultiplier;
                    previewDamageHigh *= enemyCaster.CurrentDamageMultiplier;
                }
            }
            // --------------------------------------
        }

        int finalLow = Mathf.FloorToInt(previewDamageLow);
        int finalHigh = Mathf.FloorToInt(previewDamageHigh);

        string damageText = finalLow == finalHigh ? $"{finalLow}" : $"{finalLow}-{finalHigh}";

        string prefix = (chance < 100f) ? $"{chance}% Chance to " : "";
        string description = $"{prefix}deal {damageText} {damageType} damage";

        if (isSplash) description += $" (AoE: {splashDamageMultiplier * 100}% damage in {splashRadius}m)";

        return description + ".";
    }
}