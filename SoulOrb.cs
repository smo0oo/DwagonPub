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

        if (DualModeManager.instance != null && memberIndexToRevive != -1)
        {
            isCollected = true;

            // --- AAA POLISH: Play VFX and Sound ---
            if (pickupVFX != null)
            {
                // Spawn the VFX exactly where the orb is
                GameObject vfx = Instantiate(pickupVFX, transform.position, Quaternion.identity);

                // Backup cleanup: destroys the VFX object after 5 seconds just in case 
                // the particle system doesn't have an auto-destroy script attached to it.
                Destroy(vfx, 5f);
            }

            if (pickupSound != null)
            {
                // Plays the sound at the orb's location in 3D space
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }
            // --------------------------------------

            // Execute the revival
            DualModeManager.instance.ReviveMember(memberIndexToRevive, transform.position);

            if (FloatingTextManager.instance != null)
                FloatingTextManager.instance.ShowEvent("Soul Restored!", transform.position + Vector3.up * 2);

            // Destroy the orb itself
            Destroy(gameObject);
        }
        else
        {
            Debug.LogError($"SoulOrb Error: Invalid member index ({memberIndexToRevive}) or missing Manager.");
        }
    }
}