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
    private bool hasStarted = false;

    // --- TIMING FIX: Use Start() for first initialization ---
    private void Start()
    {
        hasStarted = true;
        RegisterWithManager();
    }

    private void OnEnable()
    {
        // Only register in OnEnable if we have ALREADY run Start() once.
        // This prevents the "Creation Race Condition" but still supports Object Pooling.
        if (hasStarted)
        {
            RegisterWithManager();
        }
    }

    private void RegisterWithManager()
    {
        if (LootLabelManager.instance != null)
        {
            // If the manager already has us, this forces a refresh of the name
            LootLabelManager.instance.RegisterOrUpdateItem(this);
        }
    }
    // --------------------------------------------------------

    private void OnDestroy()
    {
        if (LootLabelManager.instance != null)
        {
            LootLabelManager.instance.UnregisterItem(this);
        }
    }

    private void OnDisable()
    {
        if (LootLabelManager.instance != null)
        {
            LootLabelManager.instance.UnregisterItem(this);
        }
    }

    public void Interact(GameObject interactor)
    {
        if (isBeingPickedUp) return;
        isBeingPickedUp = true;

        if (InventoryManager.instance != null)
        {
            bool success = InventoryManager.instance.HandleLoot(itemData, quantity);
            if (success)
            {
                Destroy(gameObject);
                return;
            }
        }

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