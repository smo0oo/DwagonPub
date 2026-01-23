using UnityEngine;
using UnityEngine.AI;

public class TownCharacterSpawnPoint : MonoBehaviour
{
    [Header("Party Configuration")]
    [Tooltip("The index of the party member who should spawn here (e.g., 1 for the second party member, 2 for the third). Do not use 0 (active player).")]
    public int partyMemberIndex;

    [Header("Grounding Settings")]
    [Tooltip("Layers to consider as solid ground (Default, Terrain, etc).")]
    public LayerMask groundLayer = ~0; // Default to Everything

    [Tooltip("How far down to check for the ground from the spawn point's position.")]
    public float maxDropHeight = 50.0f;

    [Tooltip("Once ground is found, how far to search for a valid NavMesh point around the impact spot.")]
    public float navMeshSnapRadius = 2.0f;

    private void Start()
    {
        // We run this in Start to ensure the PartyManager has initialized its list.
        PositionPartyMember();
    }

    public void PositionPartyMember()
    {
        if (PartyManager.instance == null)
        {
            Debug.LogWarning("[TownCharacterSpawnPoint] PartyManager instance not found.");
            return;
        }

        // Validate index
        if (partyMemberIndex < 0 || partyMemberIndex >= PartyManager.instance.partyMembers.Count)
        {
            // This is common if you have spawn points for 4 members but only 2 are in the party.
            // We just fail silently or log a distinct message.
            return;
        }

        GameObject member = PartyManager.instance.partyMembers[partyMemberIndex];
        if (member == null) return;

        // --- AAA Grounding Logic ---
        Vector3 targetPosition = transform.position;

        // 1. Raycast downwards to find physical ground
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, maxDropHeight, groundLayer))
        {
            // 2. Check for NavMesh at the impact point
            if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navMeshSnapRadius, NavMesh.AllAreas))
            {
                targetPosition = navHit.position;
            }
            else
            {
                // Fallback to physics position
                targetPosition = hit.point;
            }
        }
        else
        {
            // 3. Fallback: Check NavMesh column directly below if Raycast failed
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit navHit, maxDropHeight, NavMesh.AllAreas))
            {
                targetPosition = navHit.position;
            }
        }

        // --- Apply Position ---

        // Ensure the game object is active (in case it was disabled from a previous scene)
        member.SetActive(true);

        // Teleport logic
        NavMeshAgent agent = member.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(targetPosition);
            // Reset velocity to prevent sliding
            agent.velocity = Vector3.zero;
            if (agent.hasPath) agent.ResetPath();
        }

        // Force transform update just in case Agent isn't active or fails
        member.transform.position = targetPosition;
        member.transform.rotation = transform.rotation;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.4f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.0f);

        // Grounding Drop Visualization
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawLine(transform.position, transform.position + (Vector3.down * 5.0f));

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Party Member: {partyMemberIndex}");
#endif
    }
}