using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class HealEffect : IAbilityEffect
{
    public int healAmount = 25;

    [Header("Stat Scaling")]
    [Tooltip("How the heal scales with the caster's primary stats.")]
    public List<StatScaling> scalingFactors;

    public void Apply(GameObject caster, GameObject target)
    {
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
        return $"Heals for {healAmount} health.";
    }
}