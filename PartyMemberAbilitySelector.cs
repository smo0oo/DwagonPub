using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PartyMemberAbilitySelector : MonoBehaviour
{
    [Header("Tactical Thresholds")]
    public int aoeDamageEnemyThreshold = 3;
    public int aoeHealAllyThreshold = 3;

    private PlayerStats playerStats;
    private PlayerAbilityHolder abilityHolder;
    private PartyMemberAI selfAI;
    private PartyMemberTargeting selfTargeting;
    private PlayerMovement playerMovement;

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
            playerMovement = root.PlayerMovement;
        }
        selfTargeting = GetComponent<PartyMemberTargeting>();
    }

    public Ability SelectBestAbility(GameObject target)
    {
        if (playerStats == null || abilityHolder == null || selfAI == null || selfTargeting == null) return null;

        var allKnownAbilities = playerStats.knownAbilities;
        _usableAbilitiesBuffer.Clear();

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

        if (playerMovement != null && playerMovement.defaultAttackAbility != null)
        {
            if (!_usableAbilitiesBuffer.Contains(playerMovement.defaultAttackAbility))
            {
                if (abilityHolder.CanUseAbility(playerMovement.defaultAttackAbility, target))
                {
                    _usableAbilitiesBuffer.Add(playerMovement.defaultAttackAbility);
                }
            }
        }

        if (_usableAbilitiesBuffer.Count == 0) return null;

        bool isTargetAlly = target != null && target.layer == gameObject.layer;

        if (isTargetAlly)
        {
            if (playerStats.characterClass.aiRole == PlayerClass.AILogicRole.Support)
            {
                int woundedAllies = 0;
                foreach (var ai in PartyAIManager.instance.AllPartyAIs)
                {
                    if (ai == null) continue;
                    CharacterRoot allyRoot = ai.GetComponent<CharacterRoot>();
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
                if (singleHeal != null)
                {
                    return singleHeal;
                }

                Ability emergencyAoeHeal = FindAbilityByType(_usableAbilitiesBuffer, AIUsageType.AoeHeal, target);
                if (emergencyAoeHeal != null)
                {
                    return emergencyAoeHeal;
                }
            }
            return null;
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