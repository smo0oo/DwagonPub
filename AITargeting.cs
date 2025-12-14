using UnityEngine;
using System.Linq;

public class AITargeting : MonoBehaviour
{
    [Header("Sensing Settings")]
    public float detectionRadius = 20f;
    public LayerMask playerLayer;
    public LayerMask domeMarkerLayer;

    [Header("Siege Settings")]
    public bool checkLineOfSight = true;
    public float domePriorityMultiplier = 5.0f;
    public LayerMask obstacleLayers;

    private Collider[] _targetBuffer = new Collider[50];

    public Transform FindBestTarget()
    {
        LayerMask combinedMask = playerLayer | domeMarkerLayer;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _targetBuffer, combinedMask);

        if (hitCount == 0) return null;

        Transform bestTarget = null;
        float bestScore = -1f;

        for (int i = 0; i < hitCount; i++)
        {
            var targetCollider = _targetBuffer[i];

            // --- FIX: Robust Health Check for Downed State ---
            Health targetHealth = targetCollider.GetComponent<Health>();
            if (targetHealth == null) targetHealth = targetCollider.GetComponentInParent<Health>();

            if (targetHealth != null && targetHealth.isDowned) continue; // IGNORE DEAD
            // -------------------------------------------------

            if (checkLineOfSight)
            {
                Vector3 origin = transform.position + Vector3.up;
                Vector3 targetPos = targetCollider.transform.position + Vector3.up;
                Vector3 direction = targetPos - origin;

                if (Physics.Raycast(origin, direction.normalized, direction.magnitude, obstacleLayers))
                {
                    continue;
                }
            }

            float score = 1.0f / (1.0f + Vector3.Distance(transform.position, targetCollider.transform.position));
            if (targetCollider.CompareTag("DomeMarker")) score *= domePriorityMultiplier;

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = targetCollider.transform;
            }
        }
        return bestTarget;
    }
}