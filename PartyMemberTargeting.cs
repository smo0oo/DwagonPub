using UnityEngine;
using System.Linq;

public class PartyMemberTargeting : MonoBehaviour
{
    [Header("Sensing Settings")]
    public float aggressiveScanRadius = 20f;
    public LayerMask enemyLayer;
    public LayerMask obstacleLayers; // For Line of Sight

    [Header("Healing Logic")]
    [Range(0f, 1f)]
    public float healThreshold = 0.75f;

    // --- NEW: Buffer for Non-Allocating Physics ---
    private Collider[] _enemyBuffer = new Collider[50];

    /// <summary>
    /// Finds the most wounded ally in the party who is below the heal threshold.
    /// </summary>
    // --- MODIFIED: Replaced LINQ with 'foreach' loop for zero garbage ---
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

            float healthPercent = (float)healthComponent.currentHealth / healthComponent.maxHealth;
            if (healthPercent < healThreshold && healthPercent < lowestHealthPercent)
            {
                lowestHealthPercent = healthPercent;
                mostWounded = ai;
            }
        }

        return mostWounded;
    }

    /// <summary>
    /// Finds the nearest enemy within the scan radius that has a clear line of sight.
    /// </summary>
    // --- MODIFIED: Replaced LINQ and OverlapSphere with Non-Alloc version ---
    public GameObject FindNearestEnemy()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, aggressiveScanRadius, _enemyBuffer, enemyLayer);

        GameObject closestEnemy = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            if (!HasLineOfSight(_enemyBuffer[i].transform))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, _enemyBuffer[i].transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestEnemy = _enemyBuffer[i].gameObject;
            }
        }

        return closestEnemy;
    }

    /// <summary>
    /// Checks if there is a clear line of sight to a given target.
    /// </summary>
    public bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 targetPosition = target.position + Vector3.up;
        Vector3 direction = targetPosition - origin;

        if (Physics.Raycast(origin, direction.normalized, direction.magnitude, obstacleLayers))
        {
            return false; // Path is blocked
        }
        return true; // Path is clear
    }
}