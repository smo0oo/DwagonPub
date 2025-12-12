using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the right-click context menu for a trade slot, with different options for player and NPC.
/// </summary>
public class TradeContextMenu : MonoBehaviour
{
    [Header("UI References")]
    public Button splitButton;
    public Button destroyButton;
    public Button dropButton;
    public Button sellButton; // New button
    public Button buyButton;  // New button

    private TradeManager tradeManager;
    private TradeSlot sourceSlot;

    public void Initialize(TradeManager manager)
    {
        tradeManager = manager;
        gameObject.SetActive(false); // Start hidden
    }

    public void Open(TradeSlot slot)
    {
        sourceSlot = slot;
        gameObject.SetActive(true);
        transform.position = Input.mousePosition;

        // Make sure all buttons are off before we start
        splitButton.gameObject.SetActive(false);
        destroyButton.gameObject.SetActive(false);
        dropButton.gameObject.SetActive(false);
        sellButton.gameObject.SetActive(false);
        buyButton.gameObject.SetActive(false);

        // Get the item stack to check its properties
        ItemStack itemStack = slot.GetItemStack();
        if (itemStack == null || itemStack.itemData == null)
        {
            Close();
            return;
        }

        // Configure buttons based on whether it's a player or NPC slot
        if (slot.isPlayerSlot)
        {
            // Show buttons relevant to the player's inventory
            dropButton.gameObject.SetActive(true);
            destroyButton.gameObject.SetActive(true);
            sellButton.gameObject.SetActive(true);
            if (itemStack.quantity > 1)
            {
                splitButton.gameObject.SetActive(true);
            }
        }
        else // It's an NPC slot
        {
            // Show buttons relevant to the NPC's inventory
            buyButton.gameObject.SetActive(true);
        }

        // Add listeners
        splitButton.onClick.AddListener(OnSplitClicked);
        destroyButton.onClick.AddListener(OnDestroyClicked);
        dropButton.onClick.AddListener(OnDropClicked);
        sellButton.onClick.AddListener(OnSellClicked);
        buyButton.onClick.AddListener(OnBuyClicked);
    }

    // --- New Click Handlers ---
    private void OnSellClicked()
    {
        tradeManager.SellItem(sourceSlot);
        Close();
    }

    private void OnBuyClicked()
    {
        tradeManager.BuyItem(sourceSlot);
        Close();
    }

    // --- Existing Click Handlers ---
    private void OnSplitClicked()
    {
        // This now calls the new method on the TradeManager
        tradeManager.OpenStackSplitter(sourceSlot);
        Close();
    }

    private void OnDestroyClicked()
    {
        tradeManager.DestroyItem(sourceSlot);
        Close();
    }

    private void OnDropClicked()
    {
        tradeManager.DropItem(sourceSlot);
        Close();
    }

    public void Close()
    {
        // Important to remove all listeners
        splitButton.onClick.RemoveAllListeners();
        destroyButton.onClick.RemoveAllListeners();
        dropButton.onClick.RemoveAllListeners();
        sellButton.onClick.RemoveAllListeners();
        buyButton.onClick.RemoveAllListeners();
        gameObject.SetActive(false);
    }
}