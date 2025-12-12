using UnityEngine;
using UnityEditor;

public class StatusEffectEditor
{
    [MenuItem("CONTEXT/StatusEffect/Add Tick Effect/Damage Effect")]
    private static void AddTickDamageEffect(MenuCommand command)
    {
        StatusEffect effect = (StatusEffect)command.context;
        effect.tickEffects.Add(new DamageEffect());
        EditorUtility.SetDirty(effect);
    }

    [MenuItem("CONTEXT/StatusEffect/Add Tick Effect/Heal Effect")]
    private static void AddTickHealEffect(MenuCommand command)
    {
        StatusEffect effect = (StatusEffect)command.context;
        effect.tickEffects.Add(new HealEffect());
        EditorUtility.SetDirty(effect);
    }

    [MenuItem("CONTEXT/StatusEffect/Add Tick Effect/Mana Effect")]
    private static void AddTickManaEffect(MenuCommand command)
    {
        StatusEffect effect = (StatusEffect)command.context;
        effect.tickEffects.Add(new ManaEffect());
        EditorUtility.SetDirty(effect);
    }

    // This allows a status effect to apply another status effect on its tick
    [MenuItem("CONTEXT/StatusEffect/Add Tick Effect/Apply Status Effect")]
    private static void AddTickStatusEffect(MenuCommand command)
    {
        StatusEffect effect = (StatusEffect)command.context;
        effect.tickEffects.Add(new ApplyStatusEffect());
        EditorUtility.SetDirty(effect);
    }
}