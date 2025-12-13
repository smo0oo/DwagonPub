using UnityEngine;

// --- MODIFIED: Implements IInteractable with a custom distance property ---
[RequireComponent(typeof(Collider))]
public class WorldMapExit : MonoBehaviour, IInteractable
{
    [Header("Interaction Settings")]
    [Tooltip("How close (in meters) the player needs to be to activate the exit. Default is 4m.")]
    public float activationDistance = 4.0f;

    [Tooltip("OPTIONAL: A specific spot to walk to. If null, the player walks to this object's center.")]
    public Transform interactionPoint;

    // --- MODIFIED: Method renamed from PlayerInteract to Interact ---
    public void Interact(GameObject interactor)
    {
        if (GameManager.instance != null && !GameManager.instance.IsTransitioning)
        {
            GameManager.instance.ReturnToWorldMap();
        }
    }

    private void OnValidate()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}