using UnityEngine;
using System.Linq;

public class AITargeting : MonoBehaviour
{
    [Header("Sensing Settings")]
    public float detectionRadius = 20f;
    public LayerMask playerLayer;
    public LayerMask domeMarkerLayer;

    [Header("Siege Settings")]
    [Tooltip("If false, the AI will detect targets through walls/terrain (useful for Dome Defense).")]
    public bool checkLineOfSight = true;

    [Tooltip("How much more attractive is the Dome compared to a player? >1.0 means prioritize Dome.")]
    public float domePriorityMultiplier = 5.0f; // INCREASED DEFAULT to 5.0 to force Dome targeting

    [Tooltip("Set this to all layers that should block line of sight (e.g., Default, Walls, Terrain).")]
    public LayerMask obstacleLayers;

    // Buffer for Non-Allocating Physics
    private Collider[] _targetBuffer = new Collider[50];

    public Transform FindBestTarget()
    {
        LayerMask combinedMask = playerLayer | domeMarkerLayer;

        // Use Non-Allocating version
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _targetBuffer, combinedMask);

        if (hitCount == 0) return null;

        Transform bestTarget = null;
        float bestScore = -1f;

        for (int i = 0; i < hitCount; i++)
        {
            var targetCollider = _targetBuffer[i];

            if (checkLineOfSight)
            {
                Vector3 origin = transform.position + Vector3.up;
                Vector3 targetPos = targetCollider.transform.position + Vector3.up;
                Vector3 direction = targetPos - origin;

                if (Physics.Raycast(origin, direction.normalized, direction.magnitude, obstacleLayers))
                {
                    continue; // Blocked by obstacle
                }
            }

            // Base score is based on proximity (closer = higher score)
            float score = 1.0f / (1.0f + Vector3.Distance(transform.position, targetCollider.transform.position));

            // Apply priority multiplier
            if (targetCollider.CompareTag("DomeMarker"))
            {
                score *= domePriorityMultiplier; // Multiply by 5.0 (or whatever is set)
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