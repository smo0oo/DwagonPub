using UnityEngine;
using Unity.VisualScripting;

public class InventoryTester : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The name of the Object Variable on your player prefab that references the inventory GameObject.")]
    public string inventoryVariableName = "Inventory";

    [Header("Items to Add")]
    public ItemData appleItem;
    public ItemData swordItem;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("--- Key '1' pressed. Attempting to add Apple... ---");
            AddItemToCurrentPlayer(appleItem);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("--- Key '2' pressed. Attempting to add Sword... ---");
            AddItemToCurrentPlayer(swordItem);
        }
    }

    private void AddItemToCurrentPlayer(ItemData item)
    {
        // --- FIX: Get the current player directly from the PartyManager ---
        if (PartyManager.instance == null)
        {
            Debug.LogError("TESTER FAILED: The PartyManager instance could not be found!");
            return;
        }
        GameObject currentPlayer = PartyManager.instance.ActivePlayer;

        if (currentPlayer == null)
        {
            Debug.LogError("TESTER FAILED: Could not find an ActivePlayer on the PartyManager.");
            return;
        }
        Debug.Log("Tester found CurrentPlayer: " + currentPlayer.name);

        object inventoryVar = Variables.Object(currentPlayer).Get(inventoryVariableName);
        if (inventoryVar == null)
        {
            Debug.LogError($"TESTER FAILED: Could not find an object variable named '{inventoryVariableName}' on the player '{currentPlayer.name}'!");
            return;
        }
        Debug.Log($"Tester found inventory sub-object variable.");

        Inventory playerInventory = null;
        if (inventoryVar is GameObject inventoryGO)
        {
            playerInventory = inventoryGO.GetComponent<Inventory>();
        }
        else if (inventoryVar is Inventory inventoryComp)
        {
            playerInventory = inventoryComp;
        }

        if (playerInventory == null)
        {
            Debug.LogError($"TESTER FAILED: The inventory object does not have an 'Inventory' component attached!");
            return;
        }
        Debug.Log("Tester found Inventory component on " + playerInventory.gameObject.name);

        Debug.Log($"Attempting to add '{item.displayName}' to {currentPlayer.name}'s inventory...");
        bool success = playerInventory.AddItem(item);

        if (success)
        {
            Debug.Log("Successfully added item.");
        }
        else
        {
            Debug.Log("Failed to add item. The inventory is likely full.");
        }
    }
}