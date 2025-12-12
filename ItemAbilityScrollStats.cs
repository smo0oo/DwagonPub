using UnityEngine;
using System.Text;

[CreateAssetMenu(fileName = "New Ability Scroll Stats", menuName = "Inventory/Stats/Ability Scroll")]
public class ItemAbilityScrollStats : ItemStats
{
    [Header("Scroll Settings")]
    [Tooltip("The Ability asset that this scroll will teach to the Dome.")]
    public Ability abilityToTeach;

    public override string GetStatsDescription()
    {
        if (abilityToTeach == null)
        {
            return "Teaches an unknown ability.";
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"<color=#a0a0ff>Use: Teaches the Dome the following ability:</color>");
        sb.AppendLine();
        sb.AppendLine($"<b>{abilityToTeach.displayName}</b>");
        sb.AppendLine($"<color=#c0c0c0>{abilityToTeach.description}</color>");

        return sb.ToString();
    }
}