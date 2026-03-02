using UnityEngine;
using PixelCrushers.DialogueSystem;

[RequireComponent(typeof(DialogueSystemTrigger))]
public class PropInteraction : MonoBehaviour, IInteractable
{
    [Tooltip("How close does the player need to be to interact with this prop?")]
    public float interactDistance = 2.0f;

    private DialogueSystemTrigger dialogueSystemTrigger;

    void Awake()
    {
        dialogueSystemTrigger = GetComponent<DialogueSystemTrigger>();
    }

    public void Interact(GameObject interactor)
    {
        if (dialogueSystemTrigger != null)
        {
            // Identify the true player
            GameObject activePlayer = PartyManager.instance != null ? PartyManager.instance.ActivePlayer : interactor;

            // Manually fire the Dialogue System Trigger!
            dialogueSystemTrigger.OnUse(activePlayer.transform);
        }
    }

    /// <summary>
    /// Fallback for the interface. PlayerMovement handles arrival natively, 
    /// but we provide a point just in case.
    /// </summary>
    public Vector3 GetInteractionPosition()
    {
        return transform.position + (transform.forward * interactDistance);
    }
}