using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PartyMemberAbilitySelector : MonoBehaviour
{
    [Header("Tactical Thresholds")]
    [Tooltip("How many enemies must be grouped together to consider using an AOE ability.")]
    public int aoeDamageEnemyThreshold = 3;
    [Tooltip("How many allies must be wounded to consider using an AOE heal.")]
    public int aoeHealAllyThreshold = 3;

    private PlayerStats playerStats;
    private PlayerAbilityHolder abilityHolder;
    private PartyMemberAI selfAI;
    private PartyMemberTargeting selfTargeting;
    private PlayerMovement playerMovement; // [Added] Reference to get default attack

    // --- Buffers for Non-Allocating Physics and queries ---
    private Collider[] _aoeBuffer = new Collider[50];
    private List<Ability> _usableAbilitiesBuffer = new List<Ability>();

    void Awake()
    {
        CharacterRoot root = GetComponentInParent<CharacterRoot>();
        if (root != null)
        {
            playerStats = root.PlayerStats;
            abilityHolder = root.PlayerAbilityHolder;
            selfAI = root.GetComponent<PartyMemberAI>();
            playerMovement = root.PlayerMovement; // [Added] Cache PlayerMovement
        }
        selfTargeting = GetComponent<PartyMemberTargeting>();
    }

    public Ability SelectBestAbility(GameObject target)
    {
        if (playerStats == null || abilityHolder == null || selfAI == null || selfTargeting == null) return null;

        // [Fixed] Removed the early return for empty knownAbilities. 
        // We must continue to check for the default attack even if the list is null/empty.
        var allKnownAbilities = playerStats.knownAbilities;

        _usableAbilitiesBuffer.Clear();

        // 1. Add Learned Abilities
        if (allKnownAbilities != null)
        {
            foreach (var a in allKnownAbilities)
            {
                if (abilityHolder.CanUseAbility(a, target))
                {
                    _usableAbilitiesBuffer.Add(a);
                }
            }
        }

        // 2. [Added] Add Default Ability (Punch/Weapon Attack) if available
        if (playerMovement != null && playerMovement.defaultAttackAbility != null)
        {
            // Ensure we don't add it twice if it's already in the known list
            if (!_usableAbilitiesBuffer.Contains(playerMovement.defaultAttackAbility))
            {
                if (abilityHolder.CanUseAbility(playerMovement.defaultAttackAbility, target))
                {
                    _usableAbilitiesBuffer.Add(playerMovement.defaultAttackAbility);
                }
            }
        }

        // If we still have no abilities (not even a default punch), we can't do anything.
        if (_usableAbilitiesBuffer.Count == 0) return null;

        // --- TACTICAL HIERARCHY ---

        if (playerStats.characterClass.aiRole == PlayerClass.AILogicRole.Support)
        {
            int woundedAllies = 0;
            foreach (var ai in PartyAIManager.instance.AllPartyAIs)
            {
                if (ai == null) continue;
                CharacterRoot allyRoot = ai.GetComponent<CharacterRoot>();
                // Only count active allies
                if (allyRoot == null || !allyRoot.gameObject.activeInHierarchy) continue;

                Health allyHealth = allyRoot.Health;
                if (allyHealth == null) continue;

                if ((float)allyHealth.currentHealth / allyHealth.maxHealth < selfTargeting.healThreshold)
                {
                    woundedAllies++;
                }
            }

            if (woundedAllies >= aoeHealAllyThreshold)
            {
                Ability aoeHeal = FindAbilityByType(_usableAbilitiesBuffer, AIUsageType.AoeHeal, target);
                if (aoeHeal != null) return aoeHeal;
            }

            Ability singleHeal = FindAbilityByType(_usableAbilitiesBuffer, AIUsageType.SingleTargetHeal, target);
            if (singleHeal != null) return singleHeal;
        }

        if (target != null)
        {
            Health targetHealth = target.GetComponent<Health>();

            if (targetHealth != null && targetHealth.currentHealth / (float)targetHealth.maxHealth < selfAI.finisherThreshold)
            {
                Ability finisher = FindAbilityByType(_usableAbilitiesBuffer, AIUsageType.Finisher, target);
                if (finisher != null) return finisher;
            }

            int nearbyEnemies = Physics.OverlapSphereNonAlloc(target.transform.position, 5f, _aoeBuffer, selfTargeting.enemyLayer);
            if (nearbyEnemies >= aoeDamageEnemyThreshold)
            {
                Ability aoeDamage = FindAbilityByType(_usableAbilitiesBuffer, AIUsageType.AoeDamage, target);
                if (aoeDamage != null) return aoeDamage;
            }
        }

        Ability bestStandardDamage = null;
        foreach (var a in _usableAbilitiesBuffer)
        {
            // Default attacks usually fall under StandardDamage
            if (a.usageType == AIUsageType.StandardDamage)
            {
                if (bestStandardDamage == null || a.priority > bestStandardDamage.priority)
                {
                    bestStandardDamage = a;
                }
            }
        }
        return bestStandardDamage;
    }

    private Ability FindAbilityByType(List<Ability> abilitiesToSearch, AIUsageType type, GameObject target)
    {
        Ability bestAbility = null;
        foreach (var a in abilitiesToSearch)
        {
            if (a.usageType == type && abilityHolder.CanUseAbility(a, target))
            {
                if (bestAbility == null || a.priority > bestAbility.priority)
                {
                    bestAbility = a;
                }
            }
        }
        return bestAbility;
    }
}