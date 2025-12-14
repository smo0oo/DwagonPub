using UnityEngine;

public class DungeonExit : MonoBehaviour, IInteractable
{
    [Header("Exit Settings")]
    public string hoverText = "Return to Dome";
    public float interactionDistance = 3.0f;

    [Header("Validation")]
    [Tooltip("If true, players cannot leave until all enemies (or the Boss) are dead.")]
    public bool requireBossDefeated = true;

    // Helper to find if the boss is still alive
    private BossTrigger sceneBoss;

    void Start()
    {
        // Try to find a boss in the scene automatically
        sceneBoss = FindObjectOfType<BossTrigger>();
    }

    public void Interact(GameObject interactor)
    {
        if (requireBossDefeated && sceneBoss != null)
        {
            // Check if boss is actually dead (Health <= 0)
            Health bossHealth = sceneBoss.GetComponent<Health>();
            if (bossHealth != null && bossHealth.currentHealth > 0)
            {
                if (FloatingTextManager.instance != null)
                {
                    FloatingTextManager.instance.ShowEvent("Defeat the Boss first!", transform.position + Vector3.up * 2);
                }
                return;
            }
        }

        // Trigger Victory
        if (DualModeManager.instance != null)
        {
            DualModeManager.instance.CompleteDungeonRun();
        }
        else
        {
            Debug.LogError("DungeonExit: DualModeManager is missing!");
        }
    }

    // Optional: Draw gizmo for interaction range
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}