using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// --- AAA FIX: TACTICAL GAMBIT SYSTEM ---
public enum TacticalPriority
{
    Boss,   // Highest Threat (e.g., Bosses, Leaders)
    Medic,  // High Threat (e.g., Enemy Healers)
    Ranged, // High Threat (e.g., Mages, Archers, Snipers)
    Melee,  // Standard (e.g., Standard Grunts, Tanks)
    Low     // Last Resort (e.g., Summoned Minions, Weak critters)
}
// ---------------------------------------

public class PartyMemberTargeting : MonoBehaviour
{
    [Header("Sensing Settings")]
    public float aggressiveScanRadius = 20f;
    public LayerMask enemyLayer;
    public LayerMask obstacleLayers; // For Line of Sight

    [Header("Tactical Priorities (AAA)")]
    [Tooltip("The AI will completely clear out enemies of the first rank before moving to the next rank. Drag to reorder!")]
    public List<TacticalPriority> priorityOrder = new List<TacticalPriority> {
        TacticalPriority.Boss,
        TacticalPriority.Medic,
        TacticalPriority.Ranged,
        TacticalPriority.Melee,
        TacticalPriority.Low
    };

    [Header("Healing Logic")]
    [Range(0f, 1f)]
    public float healThreshold = 0.75f;

    private Collider[] _enemyBuffer = new Collider[50];

    public PartyMemberAI FindWoundedAlly()
    {
        if (PartyAIManager.instance == null) return null;

        PartyMemberAI mostWounded = null;
        float lowestHealthPercent = 1f;

        foreach (var ai in PartyAIManager.instance.AllPartyAIs)
        {
            if (ai == null) continue;

            Health healthComponent = ai.GetComponent<Health>();
            if (healthComponent == null) continue;

            // Ignore dead allies
            if (healthComponent.isDowned || healthComponent.currentHealth <= 0) continue;

            float healthPercent = (float)healthComponent.currentHealth / healthComponent.maxHealth;
            if (healthPercent < healThreshold && healthPercent < lowestHealthPercent)
            {
                lowestHealthPercent = healthPercent;
                mostWounded = ai;
            }
        }

        return mostWounded;
    }

    public GameObject FindNearestEnemy()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, aggressiveScanRadius, _enemyBuffer, enemyLayer);

        List<EnemyAI> validEnemies = new List<EnemyAI>();

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _enemyBuffer[i];

            // 1. Resolve Health
            Health h = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();

            // 2. Skip if no health or already dead
            if (h == null || h.isDowned || h.currentHealth <= 0) continue;

            // 3. Line of Sight Check
            if (!HasLineOfSight(col.transform)) continue;

            // 4. Grab the EnemyAI component
            EnemyAI enemy = col.GetComponent<EnemyAI>() ?? col.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                validEnemies.Add(enemy);
            }
        }

        if (validEnemies.Count == 0) return null;

        // --- AAA FIX: EVALUATE TARGETS BY PRIORITY RANK ---
        // We loop through what the Party Member CARES about first.
        foreach (TacticalPriority rank in priorityOrder)
        {
            EnemyAI bestEnemyInRank = null;
            float minDistance = float.MaxValue;

            foreach (var enemy in validEnemies)
            {
                if (enemy.priorityRank == rank)
                {
                    float distance = Vector3.Distance(transform.position, enemy.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestEnemyInRank = enemy;
                    }
                }
            }

            // If we found ANY valid enemy matching this specific priority rank, 
            // lock onto the closest one immediately and ignore all lower ranks!
            if (bestEnemyInRank != null)
            {
                return bestEnemyInRank.gameObject;
            }
        }
        // --------------------------------------------------

        return null;
    }

    public bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 targetPosition = target.position + Vector3.up;
        Vector3 direction = targetPosition - origin;
        float dist = direction.magnitude;

        // Perform Raycast
        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, dist, obstacleLayers))
        {
            // If we hit the target (or a child of the target), it is NOT blocked.
            if (hit.transform == target || hit.transform.IsChildOf(target))
            {
                return true;
            }

            // If we hit something else, it IS blocked.
            return false;
        }

        return true; // Path is clear
    }
}