using UnityEngine;
using UnityEngine.AI; // Required for NavMeshHit

/// <summary>
/// A simple marker component that identifies a spawn location for the player party.
/// Now includes a visual gizmo to help with accurate placement on the NavMesh.
/// </summary>
public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("A unique ID for this spawn point (e.g., 'FromWestRoad', 'MainGate'). This must match the ID set in the LocationNode on the World Map.")]
    public string spawnPointID;

    // This method draws helpers in the Scene view so you don't have to run the game to check placement.
    private void OnDrawGizmos()
    {
        // Find the closest point on the NavMesh to this spawn point's position
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
        {
            // Draw a green sphere at the valid, snapped position where the player will actually land.
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(hit.position, 0.5f);

            // Draw a red sphere at this object's actual, potentially invalid, position.
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // Draw a line connecting the two, making it easy to see the offset.
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, hit.position);
        }
        else
        {
            // If no valid NavMesh point is found nearby, draw a large red cube to indicate a major problem.
            Gizmos.color = Color.red;
            Gizmos.DrawCube(transform.position, Vector3.one * 2);
        }
    }
}