using UnityEngine;
using UnityEditor;

public class PlaceableTrapEditor
{
    // --- FRIENDLY EFFECTS ---

    [MenuItem("CONTEXT/PlaceableTrap/Add Friendly Effect/Heal Effect")]
    private static void AddFriendlyHealEffect(MenuCommand command)
    {
        PlaceableTrap trap = (PlaceableTrap)command.context;
        trap.friendlyEffects.Add(new HealEffect());
        EditorUtility.SetDirty(trap);
    }

    [MenuItem("CONTEXT/PlaceableTrap/Add Friendly Effect/Damage Effect")]
    private static void AddFriendlyDamageEffect(MenuCommand command)
    {
        PlaceableTrap trap = (PlaceableTrap)command.context;
        trap.friendlyEffects.Add(new DamageEffect());
        EditorUtility.SetDirty(trap);
    }

    [MenuItem("CONTEXT/PlaceableTrap/Add Friendly Effect/Mana Effect")] // --- NEW ---
    private static void AddFriendlyManaEffect(MenuCommand command)
    {
        PlaceableTrap trap = (PlaceableTrap)command.context;
        trap.friendlyEffects.Add(new ManaEffect());
        EditorUtility.SetDirty(trap);
    }

    // --- HOSTILE EFFECTS ---

    [MenuItem("CONTEXT/PlaceableTrap/Add Hostile Effect/Damage Effect")]
    private static void AddHostileDamageEffect(MenuCommand command)
    {
        PlaceableTrap trap = (PlaceableTrap)command.context;
        trap.hostileEffects.Add(new DamageEffect());
        EditorUtility.SetDirty(trap);
    }

    [MenuItem("CONTEXT/PlaceableTrap/Add Hostile Effect/Heal Effect")]
    private static void AddHostileHealEffect(MenuCommand command)
    {
        PlaceableTrap trap = (PlaceableTrap)command.context;
        trap.hostileEffects.Add(new HealEffect());
        EditorUtility.SetDirty(trap);
    }

    [MenuItem("CONTEXT/PlaceableTrap/Add Hostile Effect/Mana Effect")] // --- NEW ---
    private static void AddHostileManaEffect(MenuCommand command)
    {
        PlaceableTrap trap = (PlaceableTrap)command.context;
        trap.hostileEffects.Add(new ManaEffect());
        EditorUtility.SetDirty(trap);
    }
}
