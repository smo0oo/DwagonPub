using UnityEngine;
using PixelCrushers.DialogueSystem;

public enum InteractionDirection
{
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest
}

public class NPCInteraction : MonoBehaviour, IInteractable
{
    [Header("Interaction Positioning")]
    [Tooltip("Where should the player stand relative to the NPC? (World Space)")]
    public InteractionDirection playerStandDirection = InteractionDirection.South;

    [Tooltip("How far away from the NPC center should the player stand?")]
    public float interactionDistance = 2.0f;

    private Inventory npcInventory;
    private DialogueSystemTrigger dialogueSystemTrigger;

    void Awake()
    {
        npcInventory = GetComponentInChildren<Inventory>();
        dialogueSystemTrigger = GetComponent<DialogueSystemTrigger>();

        if (dialogueSystemTrigger == null)
        {
            Debug.LogWarning("NPCInteraction: No 'DialogueSystemTrigger' component found on " + gameObject.name, this);
        }
    }

    public void Interact(GameObject interactor)
    {
        if (dialogueSystemTrigger != null)
        {
            dialogueSystemTrigger.OnUse(interactor.transform);
        }
    }

    /// <summary>
    /// Calculates the world position where the player should stand.
    /// </summary>
    public Vector3 GetInteractionPosition()
    {
        Vector3 offset = Vector3.back; // Default South

        switch (playerStandDirection)
        {
            case InteractionDirection.North: offset = Vector3.forward; break;
            case InteractionDirection.NorthEast: offset = (Vector3.forward + Vector3.right).normalized; break;
            case InteractionDirection.East: offset = Vector3.right; break;
            case InteractionDirection.SouthEast: offset = (Vector3.back + Vector3.right).normalized; break;
            case InteractionDirection.South: offset = Vector3.back; break;
            case InteractionDirection.SouthWest: offset = (Vector3.back + Vector3.left).normalized; break;
            case InteractionDirection.West: offset = Vector3.left; break;
            case InteractionDirection.NorthWest: offset = (Vector3.forward + Vector3.left).normalized; break;
        }

        return transform.position + (offset * interactionDistance);
    }

    public void StartTrade()
    {
        if (npcInventory == null)
        {
            Debug.LogError("Cannot start trade, NPC inventory not found on " + gameObject.name, this);
            return;
        }

        GameObject playerObject = PartyManager.instance.ActivePlayer;
        if (playerObject == null) return;

        Inventory playerInventory = playerObject.GetComponentInChildren<Inventory>();
        if (playerInventory != null && TradeManager.instance != null)
        {
            DialogueManager.StopConversation();
            TradeManager.instance.StartTradeSession(playerInventory, npcInventory, playerObject);
        }
    }
}