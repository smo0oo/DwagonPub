using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AIAbilitySelector : MonoBehaviour
{
    [Header("Abilities")]
    public List<Ability> abilities;
    public List<Ability> initialAbilities;
    public List<Ability> pursuingAbilities;

    [Header("Ability Ranges")]
    public Vector2 initialAbilityRange = new Vector2(0, 25);
    public Vector2 pursuingAbilityRange = new Vector2(10, 30);

    [Header("Conditional Logic")]
    [Tooltip("Health percentage below which the AI will prioritize using a DefensiveBuff.")]
    [Range(0f, 1f)]
    public float defensiveBuffHealthThreshold = 0.4f;
    [Tooltip("Target health percentage below which the AI will prioritize using a Finisher.")]
    [Range(0f, 1f)]
    public float finisherHealthThreshold = 0.2f;
    [Tooltip("The number of enemies that must be grouped together to trigger an AOE ability.")]
    public int aoeEnemyThreshold = 3;

    private EnemyAbilityHolder abilityHolder;
    private Health selfHealth;

    // --- NEW: Buffer for Non-Allocating Physics ---
    private Collider[] _aoeBuffer = new Collider[50];

    void Awake()
    {
        abilityHolder = GetComponent<EnemyAbilityHolder>();
        selfHealth = GetComponent<Health>();
    }

    public Ability SelectBestAbility(Transform currentTarget, bool hasUsedInitialAbilities)
    {
        if (currentTarget == null || abilityHolder == null) return null;

        Health targetHealth = currentTarget.GetComponent<Health>();

        // 1. SURVIVAL
        if (selfHealth != null && selfHealth.currentHealth / (float)selfHealth.maxHealth < defensiveBuffHealthThreshold)
        {
            Ability defensiveAbility = FindAbilityByType(AIUsageType.DefensiveBuff, this.gameObject);
            if (defensiveAbility != null) return defensiveAbility;
        }

        // 2. OPPORTUNITY
        if (targetHealth != null && targetHealth.currentHealth / (float)targetHealth.maxHealth < finisherHealthThreshold)
        {
            Ability finisher = FindAbilityByType(AIUsageType.Finisher, currentTarget.gameObject);
            if (finisher != null) return finisher;
        }

        // --- MODIFIED: Use Non-Allocating version ---
        int nearbyEnemies = Physics.OverlapSphereNonAlloc(currentTarget.transform.position, 5f, _aoeBuffer, GetComponent<AITargeting>().playerLayer);
        if (nearbyEnemies >= aoeEnemyThreshold)
        {
            Ability aoeAbility = FindAbilityByType(AIUsageType.AoeDamage, currentTarget.gameObject);
            if (aoeAbility != null) return aoeAbility;
        }

        // 3. STANDARD ROTATION
        float distance = Vector3.Distance(transform.position, currentTarget.position);

        if (!hasUsedInitialAbilities && initialAbilities.Count > 0 && distance >= initialAbilityRange.x && distance <= initialAbilityRange.y)
        {
            Ability opener = ChooseStandardAbility(initialAbilities, currentTarget.gameObject);
            if (opener != null) return opener;
        }

        if (pursuingAbilities.Count > 0 && distance >= pursuingAbilityRange.x && distance <= pursuingAbilityRange.y)
        {
            Ability pursuer = ChooseStandardAbility(pursuingAbilities, currentTarget.gameObject);
            if (pursuer != null) return pursuer;
        }

        // 4. FALLBACK
        return ChooseStandardAbility(abilities, currentTarget.gameObject);
    }

    // --- MODIFIED: Replaced LINQ with 'foreach' loops for zero garbage ---
    private Ability FindAbilityByType(AIUsageType type, GameObject target)
    {
        Ability bestAbility = null;

        // Iterate over all three lists
        foreach (Ability a in initialAbilities)
        {
            if (a != null && a.usageType == type && abilityHolder.CanUseAbility(a, target))
            {
                if (bestAbility == null || a.priority > bestAbility.priority)
                {
                    bestAbility = a;
                }
            }
        }
        foreach (Ability a in pursuingAbilities)
        {
            if (a != null && a.usageType == type && abilityHolder.CanUseAbility(a, target))
            {
                if (bestAbility == null || a.priority > bestAbility.priority)
                {
                    bestAbility = a;
                }
            }
        }
        foreach (Ability a in abilities)
        {
            if (a != null && a.usageType == type && abilityHolder.CanUseAbility(a, target))
            {
                if (bestAbility == null || a.priority > bestAbility.priority)
                {
                    bestAbility = a;
                }
            }
        }

        return bestAbility;
    }

    // --- MODIFIED: Replaced LINQ with 'foreach' loop for zero garbage ---
    private Ability ChooseStandardAbility(List<Ability> abilityList, GameObject target)
    {
        Ability bestAbility = null;
        // We use a list to track abilities with the same highest priority
        List<Ability> bestOptions = new List<Ability>();

        foreach (Ability a in abilityList)
        {
            if (a != null && a.usageType == AIUsageType.StandardDamage && abilityHolder.CanUseAbility(a, target))
            {
                if (bestAbility == null || a.priority > bestAbility.priority)
                {
                    // Found a new best, clear old options
                    bestAbility = a;
                    bestOptions.Clear();
                    bestOptions.Add(a);
                }
                else if (a.priority == bestAbility.priority)
                {
                    // Same priority, add to options
                    bestOptions.Add(a);
                }
            }
        }

        // If there are multiple options with the same priority, pick one at random
        if (bestOptions.Count > 1)
        {
            return bestOptions[Random.Range(0, bestOptions.Count)];
        }

        return bestAbility; // Return the single best, or null
    }
}