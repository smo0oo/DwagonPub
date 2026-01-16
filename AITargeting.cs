using UnityEngine;
using System.Collections.Generic;

public class AITargeting : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRadius = 15f; // Renamed back from lookRadius
    public LayerMask playerLayer;       // Renamed back from targetLayers
    public LayerMask obstacleLayers;
    public bool checkLineOfSight = true;

    [Header("Behavior")]
    public float targetRefreshRate = 0.5f;

    // --- Optimization Caches ---
    private float sqrLookRadius;
    private Collider[] hitColliders = new Collider[20];

    void Awake()
    {
        sqrLookRadius = detectionRadius * detectionRadius;
    }

    public Transform FindBestTarget(Transform currentTarget)
    {
        // 1. Fast Distance Check
        if (currentTarget != null)
        {
            float sqrDist = (currentTarget.position - transform.position).sqrMagnitude;
            if (sqrDist > sqrLookRadius)
            {
                currentTarget = null;
            }
            else
            {
                // Fix: Check health directly in case isDead is private
                Health h = currentTarget.GetComponent<CharacterRoot>()?.Health ?? currentTarget.GetComponent<Health>();
                if (h == null || h.currentHealth <= 0 || h.isDowned) currentTarget = null;
            }
        }

        if (currentTarget != null) return currentTarget;

        // 2. Overlap Sphere
        int numFound = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, hitColliders, playerLayer);

        Transform bestTarget = null;
        float closestSqrDistance = Mathf.Infinity;

        for (int i = 0; i < numFound; i++)
        {
            Collider col = hitColliders[i];
            if (col == null) continue;

            Transform potentialTarget = col.transform;
            if (potentialTarget == transform || potentialTarget.IsChildOf(transform)) continue;

            Vector3 directionToTarget = potentialTarget.position - transform.position;
            float dSqrToTarget = directionToTarget.sqrMagnitude;

            if (dSqrToTarget > closestSqrDistance) continue;

            // Component Cache
            CharacterRoot targetRoot = potentialTarget.GetComponent<CharacterRoot>();
            Health targetHealth = targetRoot != null ? targetRoot.Health : potentialTarget.GetComponent<Health>();

            // Fix: Check health <= 0 instead of isDead
            if (targetHealth == null || targetHealth.currentHealth <= 0 || targetHealth.isDowned) continue;

            // Line of Sight
            if (checkLineOfSight)
            {
                float dist = Mathf.Sqrt(dSqrToTarget);
                if (Physics.Raycast(transform.position + Vector3.up, directionToTarget.normalized, out RaycastHit hit, dist, obstacleLayers))
                {
                    if (hit.transform != potentialTarget && !hit.transform.IsChildOf(potentialTarget))
                    {
                        continue;
                    }
                }
            }

            closestSqrDistance = dSqrToTarget;
            bestTarget = potentialTarget;
        }

        return bestTarget;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}