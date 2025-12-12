using UnityEngine;
using PixelCrushers.DialogueSystem;

// --- MODIFIED: Now implements the IInteractable interface ---
public class NPCInteraction : MonoBehaviour, IInteractable
{
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

    // --- MODIFIED: Method renamed from PlayerInteract to Interact ---
    public void Interact(GameObject interactor)
    {
        if (dialogueSystemTrigger != null)
        {
            dialogueSystemTrigger.OnUse(interactor.transform);
        }
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