using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class HealEffect : IAbilityEffect
{
    [Header("Activation")]
    [Range(0f, 100f)] public float chance = 100f;

    [Header("Heal Settings")]
    public int healAmount = 25;

    [Header("Stat Scaling")]
    [Tooltip("How the heal scales with the caster's primary stats.")]
    public List<StatScaling> scalingFactors;

    public void Apply(GameObject caster, GameObject target)
    {
        // 1. Chance Check
        if (chance < 100f && Random.Range(0f, 100f) > chance)
        {
            return;
        }

        Health targetHealth = target.GetComponentInChildren<Health>();
        if (targetHealth == null) return;

        float finalHeal = healAmount;
        bool isCrit = false;

        CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
        PlayerStats casterStats = (casterRoot != null) ? casterRoot.PlayerStats : null;

        if (casterStats != null)
        {
            finalHeal *= (1 + casterStats.secondaryStats.healingBonus / 100f);

            foreach (var scaling in scalingFactors)
            {
                switch (scaling.stat)
                {
                    case StatType.Strength: finalHeal += casterStats.finalStrength * scaling.ratio; break;
                    case StatType.Agility: finalHeal += casterStats.finalAgility * scaling.ratio; break;
                    case StatType.Intelligence: finalHeal += casterStats.finalIntelligence * scaling.ratio; break;
                    case StatType.Faith: finalHeal += casterStats.finalFaith * scaling.ratio; break;
                }
            }

            if (Random.value < (casterStats.secondaryStats.spellCritChance / 100f))
            {
                finalHeal *= 1.5f;
                isCrit = true;
            }
        }

        targetHealth.Heal(Mathf.FloorToInt(finalHeal), caster, isCrit);
    }

    public string GetEffectDescription()
    {
        string prefix = "";
        if (chance < 100f)
        {
            prefix = $"{chance}% Chance to ";
        }
        return $"{prefix}restore {healAmount} health.";
    }
}