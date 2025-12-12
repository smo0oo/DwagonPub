using UnityEngine;
using Unity.VisualScripting; // Reinstated

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IInteractable
{
    [Header("Item Data")]
    public ItemData itemData;
    public int quantity = 1;

    [Header("Game State References")]
    [Tooltip("The name of the Visual Scripting variable on the player that holds the Inventory reference.")]
    public string inventoryVariableName = "Inventory";

    private bool isBeingPickedUp = false;

    public void Interact(GameObject interactor)
    {
        if (isBeingPickedUp) return;
        isBeingPickedUp = true;

        // ---------------------------------------------------------
        // 1. Check for Resource Item (Fuel/Rations) -> Send to Wagon
        // ---------------------------------------------------------
        if (InventoryManager.instance != null && itemData is ResourceItemData)
        {
            // The Manager handles sending resources to the Wagon directly.
            // We pass the itemData and quantity.
            bool success = InventoryManager.instance.HandleLoot(itemData, quantity);

            if (success)
            {
                Destroy(gameObject);
                return; // Interaction done
            }
            // If failed (e.g. no WagonManager), fall through to try adding to inventory as a backup
        }

        // ---------------------------------------------------------
        // 2. Standard Item -> Add to Specific Interactor's Inventory
        // ---------------------------------------------------------
        if (interactor == null)
        {
            isBeingPickedUp = false;
            return;
        }

        // Use Visual Scripting to find the Inventory component on this specific interactor
        object inventoryVar = Variables.Object(interactor).Get(inventoryVariableName);

        if (inventoryVar == null)
        {
            Debug.LogError($"Could not find inventory variable '{inventoryVariableName}' on player '{interactor.name}'");
            isBeingPickedUp = false;
            return;
        }

        Inventory playerInventory = null;
        if (inventoryVar is GameObject inventoryGO)
        {
            playerInventory = inventoryGO.GetComponent<Inventory>();
        }
        else if (inventoryVar is Inventory inventoryComp)
        {
            playerInventory = inventoryComp;
        }

        if (playerInventory != null)
        {
            if (playerInventory.AddItem(itemData, quantity))
            {
                // Optional: Refresh UI if this happened to be the currently controlled player
                if (InventoryUIController.instance != null && PartyManager.instance != null && interactor == PartyManager.instance.ActivePlayer)
                {
                    InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
                }

                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory is full. Cannot pick up " + itemData.displayName);
                isBeingPickedUp = false;
            }
        }
        else
        {
            Debug.LogError($"Found variable '{inventoryVariableName}' but it was not a valid Inventory or GameObject.");
            isBeingPickedUp = false;
        }
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }
}