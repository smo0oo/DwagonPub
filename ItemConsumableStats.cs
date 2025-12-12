using UnityEngine;
using System.Text; // Required for StringBuilder
using System.Linq;  // Required for Any() and Concat()

[CreateAssetMenu(fileName = "New Consumable Class Stats", menuName = "Inventory/Stats/Consumable Base")]
public class ItemConsumableStats : ItemStats
{
    [Header("Ability Link")]
    [Tooltip("The ability that is triggered when this consumable is used.")]
    public Ability usageAbility;

    public override string GetStatsDescription()
    {
        if (usageAbility == null) return "No effect.";

        // --- REFACTORED LOGIC ---
        // Combine both friendly and hostile effects into a single list for generating the description.
        var allEffects = usageAbility.friendlyEffects.Concat(usageAbility.hostileEffects).ToList();

        if (allEffects.Count == 0)
        {
            return "No effect.";
        }

        // Use a StringBuilder for efficient string concatenation.
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < allEffects.Count; i++)
        {
            sb.Append(allEffects[i].GetEffectDescription());
            // Add a new line if this is not the last effect in the list.
            if (i < allEffects.Count - 1)
            {
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }
}
