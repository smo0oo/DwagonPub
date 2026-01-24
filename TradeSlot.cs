using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Manages the visuals of a single slot in the TRADE UI.
/// It forwards all events to the TradeManager.
/// </summary>
public class TradeSlot : MonoBehaviour, IDropHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;

    public int slotIndex { get; private set; }
    public bool isPlayerSlot { get; private set; }
    private TradeManager tradeManager;
    private ItemStack currentStack;

    public void Initialize(TradeManager manager, int index, bool playerSlot)
    {
        tradeManager = manager;
        slotIndex = index;
        isPlayerSlot = playerSlot;
    }

    public void UpdateSlot(ItemStack itemStack)
    {
        currentStack = itemStack;
        if (itemStack != null && itemStack.itemData != null && itemStack.quantity > 0)
        {
            iconImage.sprite = itemStack.itemData.icon;
            iconImage.enabled = true;
            quantityText.text = itemStack.quantity > 1 ? itemStack.quantity.ToString() : "";
            quantityText.enabled = true;
            iconImage.color = Color.white; // Reset color
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            quantityText.text = "";
            quantityText.enabled = false;
        }
    }

    // --- 2. Affordability Visuals ---
    public void SetAffordability(bool canAfford)
    {
        if (currentStack == null || currentStack.itemData == null) return;

        // Dim the icon if we can't afford it
        iconImage.color = canAfford ? Color.white : new Color(1f, 1f, 1f, 0.4f); // 40% opacity for unaffordable
    }

    public ItemStack GetItemStack() => currentStack;

    #region Event Forwarding

    public void OnPointerClick(PointerEventData eventData)
    {
        if (currentStack == null || currentStack.itemData == null) return;

        // Existing Right Click Logic (Context Menu)
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            tradeManager.OpenContextMenu(this);
        }
        // --- 1. Quick-Action Shortcuts ---
        // Shift + Left Click to instantly Buy/Sell
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                tradeManager.HandleShiftClick(this);
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentStack != null && currentStack.itemData != null)
        {
            tradeManager.HandleBeginDrag(this, eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        tradeManager.HandleDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        tradeManager.HandleEndDrag(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        tradeManager.HandleDrop(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentStack != null && currentStack.itemData != null)
        {
            tradeManager.ShowTooltip(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        tradeManager.HideTooltip();
    }

    #endregion
}