using UnityEngine;

public interface IAbilityEffect
{
    /// <summary>
    /// Applies the effect's logic to the target.
    /// </summary>
    void Apply(GameObject caster, GameObject target);

    /// <summary>
    /// Returns a string describing what the effect does for UI tooltips.
    /// </summary>
    string GetEffectDescription();
}