using UnityEngine;

[System.Serializable]
public class ManaEffect : IAbilityEffect
{
    [Header("Activation")]
    [Range(0f, 100f)] public float chance = 100f;

    [Header("Mana Settings")]
    public int manaAmount = 25;

    public void Apply(GameObject caster, GameObject target)
    {
        // 1. Chance Check
        if (chance < 100f && Random.Range(0f, 100f) > chance)
        {
            return;
        }

        CharacterRoot targetRoot = target.GetComponentInParent<CharacterRoot>();
        PlayerStats targetStats = (targetRoot != null) ? targetRoot.PlayerStats : null;

        if (targetStats == null)
        {
            // Fail silently in production or log warning
            // Debug.LogWarning($"ManaEffect could not find a PlayerStats component on {target.name}.");
            return;
        }

        // Find caster stats for scaling (if applicable)
        CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
        PlayerStats casterStats = (casterRoot != null) ? casterRoot.PlayerStats : null;

        float finalManaAmount = manaAmount;

        if (casterStats != null)
        {
            // Example: Scaling mana restore with Healing Bonus or Intelligence could go here
            finalManaAmount *= (1 + casterStats.secondaryStats.healingBonus / 100f);
        }

        float actualManaRestored = targetStats.RestoreMana(finalManaAmount);

        if (FloatingTextManager.instance != null && actualManaRestored > 0)
        {
            Vector3 textPosition = target.transform.position + Vector3.up * 4.0f;
            FloatingTextManager.instance.ShowMana(Mathf.FloorToInt(actualManaRestored), textPosition);
        }
    }

    public string GetEffectDescription()
    {
        string prefix = "";
        if (chance < 100f)
        {
            prefix = $"{chance}% Chance to ";
        }
        return $"{prefix}restore {manaAmount} mana.";
    }
}