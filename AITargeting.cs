using UnityEngine;
using System.Collections.Generic;

public class AITargeting : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionRadius = 15f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayers;
    public bool checkLineOfSight = true;

    [Header("Behavior")]
    public float targetRefreshRate = 0.5f;

    // Controlled by Spawner
    [Tooltip("If true, prioritizes DomeMarker tag and ignores health checks for markers.")]
    public bool prioritizeDomeMarkers = false;

    private float sqrLookRadius;
    private Collider[] hitColliders = new Collider[20];

    void Awake()
    {
        sqrLookRadius = detectionRadius * detectionRadius;
    }

    public Transform FindBestTarget(Transform currentTarget)
    {
        // 1. Evaluate Current Target
        if (currentTarget != null)
        {
            float sqrDist = (currentTarget.position - transform.position).sqrMagnitude;

            // STICKY TARGET LOGIC: If targeting a Dome Marker, never let go unless out of range.
            if (prioritizeDomeMarkers && currentTarget.CompareTag("DomeMarker"))
            {
                if (sqrDist <= sqrLookRadius) return currentTarget;
                else currentTarget = null;
            }
            else
            {
                // Standard Logic for Players
                if (sqrDist > sqrLookRadius)
                {
                    currentTarget = null;
                }
                else
                {
                    Health h = currentTarget.GetComponent<CharacterRoot>()?.Health ?? currentTarget.GetComponent<Health>();
                    if (h == null || h.currentHealth <= 0 || h.isDowned) currentTarget = null;
                }
            }
        }

        if (currentTarget != null) return currentTarget;

        // 2. SCAN
        // Note: DomeWaveSpawner has updated 'playerLayer' to include the DomeMarker layer, so Physics sees them now.
        int numFound = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, hitColliders, playerLayer);

        Transform bestTarget = null;
        float closestSqrDistance = Mathf.Infinity;
        Transform bestDomeTarget = null;
        float closestDomeDist = Mathf.Infinity;

        for (int i = 0; i < numFound; i++)
        {
            Collider col = hitColliders[i];
            if (col == null) continue;

            Transform potentialTarget = col.transform;
            if (potentialTarget == transform || potentialTarget.IsChildOf(transform)) continue;

            Vector3 directionToTarget = potentialTarget.position - transform.position;
            float dSqrToTarget = directionToTarget.sqrMagnitude;

            // --- PRIORITY CHECK: Is it a Dome Marker? ---
            if (prioritizeDomeMarkers && potentialTarget.CompareTag("DomeMarker"))
            {
                if (dSqrToTarget < closestDomeDist)
                {
                    closestDomeDist = dSqrToTarget;
                    bestDomeTarget = potentialTarget;
                }
                continue; // Found a priority target, skip standard player checks for this collider
            }
            // --------------------------------------------

            if (dSqrToTarget > closestSqrDistance) continue;

            // Standard Player Checks
            CharacterRoot targetRoot = potentialTarget.GetComponent<CharacterRoot>();
            Health targetHealth = targetRoot != null ? targetRoot.Health : potentialTarget.GetComponent<Health>();

            if (targetHealth == null || targetHealth.currentHealth <= 0 || targetHealth.isDowned) continue;

            if (checkLineOfSight)
            {
                float dist = Mathf.Sqrt(dSqrToTarget);
                if (Physics.Raycast(transform.position + Vector3.up, directionToTarget.normalized, out RaycastHit hit, dist, obstacleLayers))
                {
                    if (hit.transform != potentialTarget && !hit.transform.IsChildOf(potentialTarget)) continue;
                }
            }

            closestSqrDistance = dSqrToTarget;
            bestTarget = potentialTarget;
        }

        // If we found a Dome Marker, RETURN IT immediately, ignoring any players.
        if (bestDomeTarget != null) return bestDomeTarget;

        return bestTarget;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}