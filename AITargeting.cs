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

    // --- NEW: Priority Flag ---
    [Tooltip("If true, ignores layer masks and explicitly hunts objects tagged 'DomeMarker'.")]
    public bool prioritizeDomeMarkers = false;
    // --------------------------

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

            // SIEGE LOGIC: If we are locked onto the Dome, never let go unless it's destroyed (null)
            if (prioritizeDomeMarkers && currentTarget.CompareTag("DomeMarker"))
            {
                if (sqrDist <= sqrLookRadius) return currentTarget; // Keep attacking
                else currentTarget = null; // Lost range
            }
            else
            {
                // NORMAL LOGIC: Check distance and health
                bool lostTarget = sqrDist > sqrLookRadius;
                if (!lostTarget)
                {
                    Health h = currentTarget.GetComponent<CharacterRoot>()?.Health ?? currentTarget.GetComponent<Health>();
                    if (h == null || h.currentHealth <= 0 || h.isDowned) lostTarget = true;
                }
                if (lostTarget) currentTarget = null;
            }
        }

        if (currentTarget != null) return currentTarget;

        // 2. SIEGE SEARCH: Look for Tag "DomeMarker" BEFORE looking for players
        if (prioritizeDomeMarkers)
        {
            GameObject bestMarker = FindClosestWithTag("DomeMarker");
            if (bestMarker != null)
            {
                float dSqr = (bestMarker.transform.position - transform.position).sqrMagnitude;

                // If in range...
                if (dSqr <= sqrLookRadius)
                {
                    // ...and visible (or LOS disabled)
                    if (!checkLineOfSight || CheckLineOfSight(bestMarker.transform, Mathf.Sqrt(dSqr)))
                    {
                        return bestMarker.transform; // FOUND DOME!
                    }
                }
            }
        }

        // 3. NORMAL SEARCH: Look for Players via LayerMask
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

            CharacterRoot targetRoot = potentialTarget.GetComponent<CharacterRoot>();
            Health targetHealth = targetRoot != null ? targetRoot.Health : potentialTarget.GetComponent<Health>();

            if (targetHealth == null || targetHealth.currentHealth <= 0 || targetHealth.isDowned) continue;

            if (checkLineOfSight)
            {
                if (!CheckLineOfSight(potentialTarget, Mathf.Sqrt(dSqrToTarget))) continue;
            }

            closestSqrDistance = dSqrToTarget;
            bestTarget = potentialTarget;
        }

        return bestTarget;
    }

    private bool CheckLineOfSight(Transform target, float dist)
    {
        if (!checkLineOfSight) return true;

        Vector3 dir = (target.position - transform.position).normalized;
        if (Physics.Raycast(transform.position + Vector3.up, dir, out RaycastHit hit, dist, obstacleLayers))
        {
            if (hit.transform != target && !hit.transform.IsChildOf(target)) return false;
        }
        return true;
    }

    private GameObject FindClosestWithTag(string tag)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        if (objects == null || objects.Length == 0) return null;

        GameObject closest = null;
        float minDistSqr = Mathf.Infinity;
        Vector3 pos = transform.position;

        foreach (GameObject obj in objects)
        {
            float distSqr = (obj.transform.position - pos).sqrMagnitude;
            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closest = obj;
            }
        }
        return closest;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}