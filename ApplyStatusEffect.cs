using UnityEngine;

[System.Serializable]
public class ApplyStatusEffect : IAbilityEffect
{
    [Header("Activation")]
    [Range(0f, 100f)] public float chance = 100f;

    [Header("Effect Settings")]
    [Tooltip("The StatusEffect asset to apply to the target.")]
    public StatusEffect statusEffectToApply;

    public void Apply(GameObject caster, GameObject target)
    {
        // 1. Chance Check
        if (chance < 100f && Random.Range(0f, 100f) > chance)
        {
            return;
        }

        if (statusEffectToApply == null)
        {
            Debug.LogWarning("ApplyStatusEffect has no StatusEffect asset assigned.");
            return;
        }

        StatusEffectHolder holder = target.GetComponentInChildren<StatusEffectHolder>();

        if (holder != null)
        {
            holder.AddStatusEffect(statusEffectToApply, caster);
        }
        else
        {
            // Debug.LogWarning($"Target {target.name} does not have a StatusEffectHolder.");
        }
    }

    public string GetEffectDescription()
    {
        if (statusEffectToApply == null) return "Apply unassigned effect.";

        string prefix = "";
        if (chance < 100f)
        {
            prefix = $"{chance}% Chance to ";
        }
        return $"{prefix}apply {statusEffectToApply.effectName}.";
    }
}