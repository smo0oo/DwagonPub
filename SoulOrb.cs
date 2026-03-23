using UnityEngine;

public class SoulOrb : MonoBehaviour, IInteractable
{
    public int memberIndexToRevive = -1; // Default to invalid

    [Header("Effects")]
    [Tooltip("The particle effect prefab to spawn when the orb is collected.")]
    public GameObject pickupVFX;

    [Tooltip("The sound to play when the orb is collected.")]
    public AudioClip pickupSound;

    // Safety flag to prevent spam-clicking
    private bool isCollected = false;

    public void Interact(GameObject interactor)
    {
        if (isCollected) return;

        Debug.Log($"[Soul Orb] Interacted! Attempting to revive Party Index: {memberIndexToRevive}");

        // Ensure we have a valid index before proceeding
        if (memberIndexToRevive != -1)
        {
            isCollected = true;

            // --- AAA POLISH: Play VFX and Sound ---
            if (pickupVFX != null)
            {
                GameObject vfx = Instantiate(pickupVFX, transform.position, Quaternion.identity);
                Destroy(vfx, 5f);
            }

            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }
            // --------------------------------------

            // 1. If Dual Mode is active, use its specialized logic
            if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
            {
                Debug.Log("[Soul Orb] Routing to DualModeManager.");
                DualModeManager.instance.ReviveMember(memberIndexToRevive, transform.position);
            }
            // 2. Otherwise, use the Universal Party Revival!
            else if (PartyManager.instance != null)
            {
                Debug.Log("[Soul Orb] Routing to PartyManager.");
                PartyManager.instance.RevivePartyMember(memberIndexToRevive, transform.position);
            }
            else
            {
                Debug.LogError("[Soul Orb] CRITICAL: No active Manager found to process the revival!");
            }

            if (FloatingTextManager.instance != null)
                FloatingTextManager.instance.ShowEvent("Soul Restored!", transform.position + Vector3.up * 2);

            // Destroy the orb itself
            Destroy(gameObject);
        }
        else
        {
            Debug.LogError($"[Soul Orb] Interaction failed! memberIndexToRevive is still -1. The spawner failed to assign an ID!");
        }
    }
}