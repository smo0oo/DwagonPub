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

    [Header("Aggro Logic")]
    [Tooltip("Multiplier applied to the current target's score to make AI 'stick' to them.")]
    public float stickyAggroMultiplier = 1.25f;

    private Collider[] _targetBuffer = new Collider[50];

    // --- MODIFIED: Accepts the current target to apply sticky bias ---
    public Transform FindBestTarget(Transform currentFocus = null)
    {
        LayerMask combinedMask = playerLayer | domeMarkerLayer;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, detectionRadius, _targetBuffer, combinedMask);

        if (hitCount == 0) return null;

        Transform bestTarget = null;
        float bestScore = -1f;

        for (int i = 0; i < hitCount; i++)
        {
            var targetCollider = _targetBuffer[i];

            // 1. Resolve Health Component
            Health targetHealth = null;
            CharacterRoot root = targetCollider.GetComponentInParent<CharacterRoot>();

            if (root != null)
            {
                targetHealth = root.Health;
            }
            else
            {
                targetHealth = targetCollider.GetComponent<Health>() ?? targetCollider.GetComponentInParent<Health>();
            }

            // 2. Dead/Downed Check
            if (targetHealth != null && (targetHealth.isDowned || targetHealth.currentHealth <= 0))
            {
                continue;
            }

            // 3. Line of Sight Check
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

            // 4. Scoring
            float distance = Vector3.Distance(transform.position, targetCollider.transform.position);
            float score = 1.0f / (1.0f + distance);

            if (targetCollider.CompareTag("DomeMarker")) score *= domePriorityMultiplier;

            // --- NEW: Sticky Aggro Bonus ---
            if (currentFocus != null && targetCollider.transform == currentFocus)
            {
                score *= stickyAggroMultiplier;
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