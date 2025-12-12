using UnityEngine;
using UnityEditor;

public class AbilityEditor
{
    // --- FRIENDLY EFFECTS ---

    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Heal Effect")]
    private static void AddFriendlyHealEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.friendlyEffects.Add(new HealEffect());
        EditorUtility.SetDirty(ability);
    }

    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Damage Effect")]
    private static void AddFriendlyDamageEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.friendlyEffects.Add(new DamageEffect());
        EditorUtility.SetDirty(ability);
    }

    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Mana Effect")]
    private static void AddFriendlyManaEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.friendlyEffects.Add(new ManaEffect());
        EditorUtility.SetDirty(ability);
    }

    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Apply Status Effect")]
    private static void AddFriendlyStatusEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.friendlyEffects.Add(new ApplyStatusEffect());
        EditorUtility.SetDirty(ability);
    }

    // --- NEW FRIENDLY EFFECT ---
    [MenuItem("CONTEXT/Ability/Add Friendly Effect/Sequence Effect")]
    private static void AddFriendlySequenceEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.friendlyEffects.Add(new SequenceEffect());
        EditorUtility.SetDirty(ability);
    }


    // --- HOSTILE EFFECTS ---

    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Damage Effect")]
    private static void AddHostileDamageEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.hostileEffects.Add(new DamageEffect());
        EditorUtility.SetDirty(ability);
    }

    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Heal Effect")]
    private static void AddHostileHealEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.hostileEffects.Add(new HealEffect());
        EditorUtility.SetDirty(ability);
    }

    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Mana Effect")]
    private static void AddHostileManaEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.hostileEffects.Add(new ManaEffect());
        EditorUtility.SetDirty(ability);
    }

    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Apply Status Effect")]
    private static void AddHostileStatusEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.hostileEffects.Add(new ApplyStatusEffect());
        EditorUtility.SetDirty(ability);
    }

    // --- NEW HOSTILE EFFECT ---
    [MenuItem("CONTEXT/Ability/Add Hostile Effect/Sequence Effect")]
    private static void AddHostileSequenceEffect(MenuCommand command)
    {
        Ability ability = (Ability)command.context;
        ability.hostileEffects.Add(new SequenceEffect());
        EditorUtility.SetDirty(ability);
    }
}