using UnityEngine;

[System.Serializable]
public class ManaEffect : IAbilityEffect
{
    public int manaAmount = 25;

    // --- MODIFIED METHOD ---
    public void Apply(GameObject caster, GameObject target)
    {
        CharacterRoot targetRoot = target.GetComponentInParent<CharacterRoot>();
        PlayerStats targetStats = (targetRoot != null) ? targetRoot.PlayerStats : null;

        if (targetStats == null)
        {
            Debug.LogWarning($"ManaEffect could not find a PlayerStats component on {target.name}.");
            return;
        }

        // Use the robust CharacterRoot pattern to find the caster's stats.
        CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
        PlayerStats casterStats = (casterRoot != null) ? casterRoot.PlayerStats : null;

        float finalManaAmount = manaAmount;

        if (casterStats != null)
        {
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
        return $"Restores {manaAmount} mana.";
    }
}