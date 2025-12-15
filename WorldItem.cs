using UnityEngine;
using Unity.VisualScripting;

[RequireComponent(typeof(Collider))]
public class WorldItem : MonoBehaviour, IInteractable
{
    [Header("Item Data")]
    public ItemData itemData;
    public int quantity = 1;

    [Header("Game State References")]
    public string inventoryVariableName = "Inventory";

    private bool isBeingPickedUp = false;

    public void Interact(GameObject interactor)
    {
        if (isBeingPickedUp) return;
        isBeingPickedUp = true;

        if (InventoryManager.instance != null)
        {
            // --- FIX: Route EVERYTHING through the Manager ---
            // This allows InventoryManager to decide if it goes to:
            // 1. The Wagon (Resources)
            // 2. The Loot Bag (Dual Mode)
            // 3. The Player Inventory (Standard)
            bool success = InventoryManager.instance.HandleLoot(itemData, quantity);

            if (success)
            {
                Destroy(gameObject);
                return;
            }
            // -------------------------------------------------
        }

        // Fallback: If InventoryManager is missing or returned false (e.g. full), 
        // try adding directly to the interactor as a last resort.
        AddToSpecificInventory(interactor);
    }

    private void AddToSpecificInventory(GameObject interactor)
    {
        if (interactor == null)
        {
            isBeingPickedUp = false;
            return;
        }

        object inventoryVar = Variables.Object(interactor).Get(inventoryVariableName);
        if (inventoryVar == null)
        {
            isBeingPickedUp = false;
            return;
        }

        Inventory playerInventory = null;
        if (inventoryVar is GameObject inventoryGO) playerInventory = inventoryGO.GetComponent<Inventory>();
        else if (inventoryVar is Inventory inventoryComp) playerInventory = inventoryComp;

        if (playerInventory != null)
        {
            if (playerInventory.AddItem(itemData, quantity))
            {
                if (InventoryUIController.instance != null && PartyManager.instance != null && interactor == PartyManager.instance.ActivePlayer)
                {
                    InventoryUIController.instance.RefreshAllPlayerDisplays(PartyManager.instance.ActivePlayer);
                }
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory is full.");
                isBeingPickedUp = false;
            }
        }
        else
        {
            isBeingPickedUp = false;
        }
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger) col.isTrigger = true;
    }
}