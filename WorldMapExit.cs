using UnityEngine;

// --- MODIFIED: Now implements the IInteractable interface ---
[RequireComponent(typeof(Collider))]
public class WorldMapExit : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [Tooltip("The specific spot the player should walk to in order to use this exit. If null, the object's pivot point is used.")]
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