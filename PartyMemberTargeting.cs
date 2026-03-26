using UnityEngine;
using System.Collections.Generic;

public enum TacticalPriority
{
    Boss,
    Medic,
    Ranged,
    Melee,
    Low
}

public class PartyMemberTargeting : MonoBehaviour
{
    [Header("Sensing Settings")]
    public float aggressiveScanRadius = 20f;
    public LayerMask enemyLayer;
    public LayerMask obstacleLayers;

    [Header("Tactical Priorities (AAA)")]
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

    // --- OPTIMIZATION CACHES ---
    private Collider[] _enemyBuffer = new Collider[50];
    private List<EnemyAI> _validEnemiesCache = new List<EnemyAI>(50); // Reused list to prevent GC Spikes

    public PartyMemberAI FindWoundedAlly()
    {
        if (PartyAIManager.instance == null || PartyAIManager.instance.AllPartyAIs == null)
            return null;

        PartyMemberAI mostWounded = null;
        float lowestHealthPercent = 1f;

        foreach (var ai in PartyAIManager.instance.AllPartyAIs)
        {
            if (ai == null || !ai.gameObject.activeInHierarchy) continue;

            // OPTIMIZATION: TryGetComponent is much faster than GetComponent
            if (!ai.TryGetComponent<Health>(out Health healthComponent))
            {
                CharacterRoot root = ai.GetComponentInParent<CharacterRoot>();
                if (root != null) healthComponent = root.Health;
            }

            if (healthComponent == null || healthComponent.isDowned || healthComponent.currentHealth <= 0) continue;

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
        // OPTIMIZATION: Clear the existing list instead of making a new one (Zero Garbage)
        _validEnemiesCache.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, aggressiveScanRadius, _enemyBuffer, enemyLayer);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _enemyBuffer[i];

            if (!col.TryGetComponent<Health>(out Health h))
            {
                h = col.GetComponentInParent<Health>();
            }

            if (h == null || h.isDowned || h.currentHealth <= 0) continue;
            if (!HasLineOfSight(col.transform)) continue;

            if (!col.TryGetComponent<EnemyAI>(out EnemyAI enemy))
            {
                enemy = col.GetComponentInParent<EnemyAI>();
            }

            if (enemy != null) _validEnemiesCache.Add(enemy);
        }

        if (_validEnemiesCache.Count == 0) return null;

        foreach (TacticalPriority rank in priorityOrder)
        {
            EnemyAI bestEnemyInRank = null;
            float minDistance = float.MaxValue;

            // Iterate over our cached list instead of creating a new one
            foreach (var enemy in _validEnemiesCache)
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

            if (bestEnemyInRank != null) return bestEnemyInRank.gameObject;
        }

        return null;
    }

    public bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 targetPosition = target.position + Vector3.up;
        Vector3 direction = targetPosition - origin;
        float dist = direction.magnitude;

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, dist, obstacleLayers))
        {
            if (hit.transform == target || hit.transform.IsChildOf(target)) return true;
            return false;
        }
        return true;
    }
}