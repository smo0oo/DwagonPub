using UnityEngine;

public class DungeonExit : MonoBehaviour, IInteractable
{
    [Header("Exit Settings")]
    public string hoverText = "Return";
    public float interactionDistance = 3.0f;

    [Header("Destination (Manual Fallback)")]
    [Tooltip("Only used if Dual Mode is inactive AND GameManager has no record of the previous scene.")]
    public string fallbackSceneName = "WorldMap_A";
    public string spawnPointID = "Entrance"; // Default spawn ID for returns

    [Header("Validation")]
    [Tooltip("If true, players cannot leave until all enemies (or the Boss) are dead.")]
    public bool requireBossDefeated = true;

    private BossTrigger sceneBoss;

    void Start()
    {
        // Replaced obsolete FindObjectOfType with FindFirstObjectByType
        sceneBoss = FindFirstObjectByType<BossTrigger>();
    }

    public void Interact(GameObject interactor)
    {
        // 1. Check Boss Condition
        if (requireBossDefeated && sceneBoss != null)
        {
            Health bossHealth = sceneBoss.GetComponent<Health>();
            if (bossHealth != null && bossHealth.currentHealth > 0)
            {
                string msg = "Defeat the Boss first!";
                if (FloatingTextManager.instance != null)
                    FloatingTextManager.instance.ShowEvent(msg, transform.position + Vector3.up * 2);
                Debug.Log(msg);
                return;
            }
        }

        // 2. Dual Mode Logic (Priority)
        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            Debug.Log("Dual Mode Active: Completing Run and returning to Dome...");
            DualModeManager.instance.CompleteDungeonRun();
            return;
        }

        // 3. Dynamic Return Logic (Standard Gameplay)
        string targetScene = GameManager.instance != null ? GameManager.instance.previousSceneName : null;

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogWarning("No previous scene recorded. Using manual fallback.");
            targetScene = fallbackSceneName;
        }

        if (!string.IsNullOrEmpty(targetScene))
        {
            Debug.Log($"Returning to previous scene: {targetScene}");
            if (GameManager.instance != null)
            {
                GameManager.instance.LoadLevel(targetScene, spawnPointID);
            }
        }
        else
        {
            Debug.LogError("DungeonExit: No target scene found (Previous Scene is null AND Fallback is null).");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}