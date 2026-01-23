using UnityEngine;
using UnityEngine.AI;

public class PlayerSpawnPoint : MonoBehaviour
{
    [Header("Core Settings")]
    [Tooltip("Unique ID used by GameManager to find this specific spawn point during scene transitions.")]
    public string spawnPointID;

    [Header("Grounding Settings")]
    [Tooltip("Layers to consider as solid ground (Default, Terrain, etc).")]
    public LayerMask groundLayer = ~0; // Default to Everything

    [Tooltip("How far down to check for the ground. Increase this if your spawn points are very high up.")]
    public float maxDropHeight = 50.0f;

    [Tooltip("Once ground is found, how far to search for a valid NavMesh point around the impact spot.")]
    public float navMeshSnapRadius = 2.0f;

    public void SpawnPlayer(GameObject playerPrefab)
    {
        Vector3 targetPosition = transform.position;
        Quaternion targetRotation = transform.rotation;

        // 1. Raycast downwards to find the physical floor
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, maxDropHeight, groundLayer))
        {
            // 2. We hit ground, now check if there is a NavMesh nearby that point
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navMeshSnapRadius, NavMesh.AllAreas))
            {
                targetPosition = navHit.position;
            }
            else
            {
                // Found physics ground but no NavMesh? Spawn on the physics ground.
                targetPosition = hit.point;
                Debug.LogWarning($"[PlayerSpawnPoint] {spawnPointID}: Found ground but NO NavMesh within {navMeshSnapRadius}m. Snapping to collider surface.");
            }
        }
        else
        {
            // Raycast missed entirely? Fallback to just checking NavMesh in a column below
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, maxDropHeight, NavMesh.AllAreas))
            {
                // Note: SamplePosition with large radius is risky (might grab ceiling), but better than air.
                targetPosition = navHit.position;
            }
            else
            {
                Debug.LogWarning($"[PlayerSpawnPoint] {spawnPointID}: CRITICAL - No Ground or NavMesh found below spawn point. Player will float.");
            }
        }

        // 3. Instantiate
        GameObject player = Instantiate(playerPrefab, targetPosition, targetRotation);

        // 4. Force NavMeshAgent to accept position immediately
        // (Setting transform.position is often ignored if Agent is active, Warp is required)
        NavMeshAgent agent = player.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(targetPosition);

            // Double-tap: Occasionally Warp needs a frame, ensuring Transform is set helps prevents "flicker"
            player.transform.position = targetPosition;
        }
    }

    private void OnDrawGizmos()
    {
        // Draw Spawn Ball
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.4f);

        // Draw Direction Arrow
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);

        // Draw Grounding Ray Visualization
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3.down * 5.0f)); // Show first 5m of drop

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up, $"ID: {spawnPointID}");
#endif
    }
}