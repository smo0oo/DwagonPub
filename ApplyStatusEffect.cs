using UnityEngine;

[System.Serializable]
public class ApplyStatusEffect : IAbilityEffect
{
    [Tooltip("The StatusEffect asset to apply to the target.")]
    public StatusEffect statusEffectToApply;

    public void Apply(GameObject caster, GameObject target)
    {
        if (statusEffectToApply == null)
        {
            Debug.LogWarning("ApplyStatusEffect has no StatusEffect asset assigned.");
            return;
        }

        // --- MODIFIED LINE ---
        // Changed GetComponent to GetComponentInChildren to search the entire character hierarchy.
        StatusEffectHolder holder = target.GetComponentInChildren<StatusEffectHolder>();

        if (holder != null)
        {
            holder.AddStatusEffect(statusEffectToApply, caster);
        }
        else
        {
            Debug.LogWarning($"Target {target.name} does not have a StatusEffectHolder component. Cannot apply {statusEffectToApply.effectName}.");
        }
    }

    public string GetEffectDescription()
    {
        if (statusEffectToApply == null) return "Applies an unassigned status effect.";

        string description = $"Applies {statusEffectToApply.effectName}.";
        return description;
    }
}