using UnityEngine;
using System.Linq;

public class AITargeting : MonoBehaviour
{
    [Header("Sensing Settings")]
    public float detectionRadius = 20f;
    public LayerMask playerLayer;
    public LayerMask domeMarkerLayer;

    // --- NEW: A dedicated layer mask for things that block sight ---
    [Tooltip("Set this to all layers that should block line of sight (e.g., Default, Walls, Terrain).")]
    public LayerMask obstacleLayers;

    // --- NEW: Buffer for Non-Allocating Physics ---
    private Collider[] _targetBuffer = new Collider[50];

    public Transform FindBestTarget()
    {
        LayerMask combinedMask = playerLayer | domeMarkerLayer;

        // --- MODIFIED: Use Non-Allocating version ---
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _targetBuffer, combinedMask);

        if (hitCount == 0) return null;

        Transform bestTarget = null;
        float bestScore = -1f;

        // --- MODIFIED: Replaced LINQ with a 'for' loop for zero garbage ---
        for (int i = 0; i < hitCount; i++)
        {
            var targetCollider = _targetBuffer[i];

            Vector3 origin = transform.position + Vector3.up;
            Vector3 targetPos = targetCollider.transform.position + Vector3.up;
            Vector3 direction = targetPos - origin;

            if (Physics.Raycast(origin, direction.normalized, direction.magnitude, obstacleLayers))
            {
                continue;
            }

            float score = 1.0f / (1.0f + Vector3.Distance(transform.position, targetCollider.transform.position));
            if (targetCollider.CompareTag("DomeMarker"))
            {
                score *= 0.9f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = targetCollider.transform;
            }
        }
        return bestTarget;
    }
}