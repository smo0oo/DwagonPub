using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ItemContextMenu : MonoBehaviour
{
    [Header("UI References")]
    public Button useButton; // <-- NEW
    public Button splitButton;
    public Button destroyButton;
    public Button dropButton;
    public Button sendButton;
    public Button unequipButton;
    public GameObject sendPlayerButtonsContainer;
    public Button[] sendPlayerButtons;

    private InventoryManager inventoryManager;
    private InventorySlot sourceSlot;
    private bool isForEquippedItem;

    public void Initialize(InventoryManager manager)
    {
        inventoryManager = manager;
    }

    public void Open(InventorySlot slot)
    {
        isForEquippedItem = false;
        sourceSlot = slot;
        gameObject.SetActive(true);
        transform.position = Input.mousePosition;

        ItemStack itemStack = inventoryManager.GetItemStackInSlot(slot.slotIndex);
        if (itemStack == null || itemStack.itemData == null)
        {
            Close();
            return;
        }

        // Configure buttons
        useButton.gameObject.SetActive(itemStack.itemData.itemType == ItemType.Consumable);
        splitButton.gameObject.SetActive(itemStack.quantity > 1);
        unequipButton.gameObject.SetActive(false);
        sendPlayerButtonsContainer.SetActive(false);
        sendButton.gameObject.SetActive(true);
        dropButton.gameObject.SetActive(true);
        destroyButton.gameObject.SetActive(true);

        // Add listeners
        useButton.onClick.AddListener(OnUseClicked);
        splitButton.onClick.AddListener(OnSplitClicked);
        destroyButton.onClick.AddListener(OnDestroyClicked);
        dropButton.onClick.AddListener(OnDropClicked);
        sendButton.onClick.AddListener(() => sendPlayerButtonsContainer.SetActive(!sendPlayerButtonsContainer.activeSelf));
        SetupSendButtons();
    }

    public void OpenForEquippedItem(ItemStack item)
    {
        isForEquippedItem = true;
        sourceSlot = null;
        gameObject.SetActive(true);
        transform.position = Input.mousePosition;

        useButton.gameObject.SetActive(false);
        splitButton.gameObject.SetActive(false);
        sendButton.gameObject.SetActive(false);
        sendPlayerButtonsContainer.SetActive(false);
        unequipButton.gameObject.SetActive(true);
        dropButton.gameObject.SetActive(true);
        destroyButton.gameObject.SetActive(true);

        destroyButton.onClick.AddListener(OnDestroyClicked);
        dropButton.onClick.AddListener(OnDropClicked);
        unequipButton.onClick.AddListener(OnUnequipClicked);
    }

    private void OnUseClicked()
    {
        inventoryManager.UseItem(sourceSlot);
        Close();
    }

    // ... (rest of ItemContextMenu.cs is unchanged) ...
    private void SetupSendButtons()
    {
        List<Inventory> playerInventories = inventoryManager.GetAllPlayerInventories();
        Inventory currentInventory = inventoryManager.GetCurrentInventory();
        for (int i = 0; i < sendPlayerButtons.Length; i++)
        {
            if (i >= playerInventories.Count || playerInventories[i] == currentInventory)
            {
                sendPlayerButtons[i].gameObject.SetActive(false);
            }
            else
            {
                sendPlayerButtons[i].gameObject.SetActive(true);
                int playerIndex = i;
                sendPlayerButtons[i].onClick.RemoveAllListeners();
                sendPlayerButtons[i].onClick.AddListener(() => OnSendClicked(playerIndex));
            }
        }
    }
    private void OnSplitClicked()
    {
        inventoryManager.OpenStackSplitter(sourceSlot);
        Close();
    }
    private void OnDestroyClicked()
    {
        if (isForEquippedItem)
        {
            inventoryManager.DestroyEquippedItem();
        }
        else
        {
            inventoryManager.DestroyItem(sourceSlot);
        }
        Close();
    }
    private void OnDropClicked()
    {
        if (isForEquippedItem)
        {
            inventoryManager.DropEquippedItem();
        }
        else
        {
            inventoryManager.DropItem(sourceSlot);
        }
        Close();
    }
    private void OnUnequipClicked()
    {
        inventoryManager.UnequipItem();
        Close();
    }
    private void OnSendClicked(int playerIndex)
    {
        inventoryManager.SendItem(sourceSlot, playerIndex);
        Close();
    }
    public void Close()
    {
        useButton.onClick.RemoveAllListeners();
        splitButton.onClick.RemoveAllListeners();
        destroyButton.onClick.RemoveAllListeners();
        dropButton.onClick.RemoveAllListeners();
        sendButton.onClick.RemoveAllListeners();
        if (unequipButton) unequipButton.onClick.RemoveAllListeners();
        foreach (var button in sendPlayerButtons)
        {
            button.onClick.RemoveAllListeners();
        }
        gameObject.SetActive(false);
    }
}