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

        GameObject closestEnemy = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _enemyBuffer[i];

            // 1. Resolve Health (Handle collider on child/parent)
            Health h = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();

            // 2. Skip if no health or already dead
            if (h == null || h.isDowned || h.currentHealth <= 0) continue;

            // 3. Line of Sight Check
            if (!HasLineOfSight(col.transform))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, col.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestEnemy = h.gameObject; // Target the object with Health
            }
        }

        return closestEnemy;
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