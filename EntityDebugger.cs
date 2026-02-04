using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("Debugging/Entity Debugger")]
public class EntityDebugger : MonoBehaviour
{
    // Common Components
    [HideInInspector] public Health healthComponent;
    [HideInInspector] public NavMeshAgent navAgent;

    // Enemy Specific Components
    [HideInInspector] public EnemyAI aiComponent;
    [HideInInspector] public EnemyAbilityHolder abilityHolder;

    // Player Specific Components (NEW)
    [HideInInspector] public PlayerMovement playerMovement;
    [HideInInspector] public PlayerAbilityHolder playerAbilityHolder;

    // Toggle for drawing in-world gizmos
    public bool showWorldGizmos = true;
    public Color gizmoColor = Color.green;

    private void OnEnable()
    {
        // Auto-discover components
        healthComponent = GetComponent<Health>();
        navAgent = GetComponent<NavMeshAgent>();

        // Try Enemy First
        aiComponent = GetComponent<EnemyAI>();
        abilityHolder = GetComponent<EnemyAbilityHolder>();

        // Try Player Second
        playerMovement = GetComponent<PlayerMovement>();
        playerAbilityHolder = GetComponent<PlayerAbilityHolder>();

        if (playerMovement != null) gizmoColor = Color.cyan; // Distinct color for players
    }

    private void Update()
    {
        // Debugger logic is handled in the Editor script
    }

    private void OnDrawGizmos()
    {
        if (!showWorldGizmos || Application.isPlaying == false) return;

        Gizmos.color = gizmoColor;
        Vector3 textPos = transform.position + Vector3.up * 2.5f;
        Gizmos.DrawWireSphere(textPos, 0.2f);
    }
}