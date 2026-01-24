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
    public Button sellButton;
    public Button buyButton;

    private TradeManager tradeManager;
    private TradeSlot sourceSlot;

    public void Initialize(TradeManager manager)
    {
        tradeManager = manager;
        gameObject.SetActive(false);
    }

    public void Open(TradeSlot slot)
    {
        sourceSlot = slot;
        gameObject.SetActive(true);
        transform.position = Input.mousePosition;

        // Reset
        splitButton.gameObject.SetActive(false);
        destroyButton.gameObject.SetActive(false);
        dropButton.gameObject.SetActive(false);
        sellButton.gameObject.SetActive(false);
        buyButton.gameObject.SetActive(false);

        ItemStack itemStack = slot.GetItemStack();
        if (itemStack == null || itemStack.itemData == null)
        {
            Close();
            return;
        }

        if (slot.isPlayerSlot)
        {
            dropButton.gameObject.SetActive(true);
            destroyButton.gameObject.SetActive(true);
            sellButton.gameObject.SetActive(true);
            if (itemStack.quantity > 1)
            {
                splitButton.gameObject.SetActive(true);
            }
        }
        else // NPC slot
        {
            buyButton.gameObject.SetActive(true);
        }

        // Clean Add listeners
        splitButton.onClick.RemoveAllListeners();
        destroyButton.onClick.RemoveAllListeners();
        dropButton.onClick.RemoveAllListeners();
        sellButton.onClick.RemoveAllListeners();
        buyButton.onClick.RemoveAllListeners();

        splitButton.onClick.AddListener(OnSplitClicked);
        destroyButton.onClick.AddListener(OnDestroyClicked);
        dropButton.onClick.AddListener(OnDropClicked);
        sellButton.onClick.AddListener(OnSellClicked);
        buyButton.onClick.AddListener(OnBuyClicked);
    }

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

    private void OnSplitClicked()
    {
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
        gameObject.SetActive(false);
    }
}