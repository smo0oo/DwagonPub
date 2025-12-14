using UnityEngine;

[RequireComponent(typeof(Health))]
public class BossTrigger : MonoBehaviour
{
    [Header("Reward Settings")]
    [Tooltip("The Buff/Ability that will be granted to the Wagon Team upon victory.")]
    public Ability bossBuffReward;

    [Header("Feedback")]
    public string rewardMessage = "Artifact Secured!";

    private Health myHealth;
    private bool hasTriggered = false;

    void Awake()
    {
        myHealth = GetComponent<Health>();
    }

    void OnEnable()
    {
        if (myHealth != null)
        {
            myHealth.OnHealthChanged += HandleHealthChanged;
        }
    }

    void OnDisable()
    {
        if (myHealth != null)
        {
            myHealth.OnHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged()
    {
        // Prevent double triggering
        if (hasTriggered) return;

        if (myHealth.currentHealth <= 0)
        {
            hasTriggered = true;
            GrantReward();
        }
    }

    private void GrantReward()
    {
        if (DualModeManager.instance != null)
        {
            Debug.Log($"Boss Defeated! Storing buff: {bossBuffReward?.displayName}");

            // 1. Store the Buff
            DualModeManager.instance.pendingBossBuff = bossBuffReward;

            // 2. Show Visual Feedback
            if (FloatingTextManager.instance != null)
            {
                // Show slightly above the boss so it's visible
                Vector3 messagePos = transform.position + Vector3.up * 4.0f;
                FloatingTextManager.instance.ShowEvent(rewardMessage, messagePos);
            }
        }
        else
        {
            Debug.LogWarning("Boss Defeated, but DualModeManager instance was null!");
        }
    }
}