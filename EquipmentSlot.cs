using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class EquipmentSlot : MonoBehaviour, IPointerClickHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IDropTarget, IBeginDragHandler, IDragHandler, IEndDragHandler, IDragSource, IPointerDownHandler
{
    [Header("Slot Properties")]
    public EquipmentType slotType;

    [Header("UI References")]
    public Image iconImage;

    public EquipmentManager equipmentManager { get; private set; }
    public PlayerEquipment parentEquipment { get; private set; }
    private UIDragDropController dragDropController;
    private CanvasGroup canvasGroup;

    public void Initialize(EquipmentManager manager, PlayerEquipment parent)
    {
        equipmentManager = manager;
        parentEquipment = parent;

        canvasGroup = GetComponent<CanvasGroup>();
        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();
    }

    void Start()
    {
        if (equipmentManager == null) equipmentManager = EquipmentManager.instance;
        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();

        canvasGroup = GetComponent<CanvasGroup>();
    }

    // --- FIX: Required for OnBeginDrag to fire reliably ---
    public void OnPointerDown(PointerEventData eventData)
    {
        // Claims the pointer press so Unity knows to send Drag events here
    }
    // -----------------------------------------------------

    public void UpdateSlot(ItemStack item)
    {
        if (item != null && item.itemData != null)
        {
            iconImage.sprite = item.itemData.icon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }
    }

    public object GetItem()
    {
        if (parentEquipment == null) return null;
        parentEquipment.equippedItems.TryGetValue(slotType, out ItemStack item);
        return item;
    }

    // --- DRAG IMPLEMENTATION ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parentEquipment == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // Safety check
        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();

        parentEquipment.equippedItems.TryGetValue(slotType, out ItemStack item);
        if (item != null && item.itemData != null)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

            // Pass the source (this) and the icon sprite to the controller
            dragDropController.OnBeginDrag(this, item.itemData.icon);

            // Optional: Hide tooltip
            if (equipmentManager != null) equipmentManager.HideTooltip();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // INTENTIONALLY EMPTY
        // Movement is handled by UIDragDropController.LateUpdate
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            if (dragDropController != null) dragDropController.OnEndDrag();
        }
    }

    // ---------------------------

    public void OnDrop(PointerEventData eventData)
    {
        if (dragDropController == null || dragDropController.currentSource == null) return;

        object draggedItem = dragDropController.currentSource.GetItem();

        if (CanReceiveDrop(draggedItem))
        {
            OnDrop(draggedItem);
            dragDropController.NotifyDropSuccessful(this);
        }
    }

    public bool CanReceiveDrop(object item)
    {
        if (parentEquipment == null) return false;
        return item is ItemStack itemStack && equipmentManager.IsItemValidForSlot(parentEquipment.gameObject, itemStack.itemData, slotType);
    }

    public void OnDrop(object item)
    {
        if (parentEquipment == null) return;
        if (item is ItemStack itemStack)
        {
            equipmentManager.EquipItem(this.parentEquipment, slotType, itemStack);
        }
    }

    public void OnDropSuccess(IDropTarget target)
    {
        // Logic usually handled by the target slot (e.g., swapping items)
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (parentEquipment == null) return;
        if (eventData.button == PointerEventData.InputButton.Right && parentEquipment.equippedItems.ContainsKey(slotType) && parentEquipment.equippedItems[slotType] != null)
        {
            equipmentManager.HandleRightClick(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (equipmentManager == null) return;

        // Don't show tooltip if dragging
        if (dragDropController != null && dragDropController.currentSource != null) return;

        equipmentManager.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (equipmentManager == null) return;
        equipmentManager.HideTooltip();
    }
}