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
        currentStack = itemStack; // Keep a reference to the stack
        if (itemStack != null && itemStack.itemData != null && itemStack.quantity > 0)
        {
            iconImage.sprite = itemStack.itemData.icon;
            iconImage.enabled = true;
            quantityText.text = itemStack.quantity > 1 ? itemStack.quantity.ToString() : "";
            quantityText.enabled = true;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            quantityText.text = "";
            quantityText.enabled = false;
        }
    }

    public ItemStack GetItemStack() => currentStack;

    #region Event Forwarding

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (currentStack != null && currentStack.itemData != null)
            {
                tradeManager.OpenContextMenu(this);
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