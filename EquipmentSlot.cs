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

    // THE HARD LOCK (Working for Equipment)
    public bool isLocked = false;

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

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLocked) return;
    }

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
        if (isLocked) return null;
        if (parentEquipment == null) return null;
        parentEquipment.equippedItems.TryGetValue(slotType, out ItemStack item);
        return item;
    }

    public bool CanReceiveDrop(object item)
    {
        if (isLocked) return false;
        if (parentEquipment == null) return false;
        return item is ItemStack itemStack && equipmentManager.IsItemValidForSlot(parentEquipment.gameObject, itemStack.itemData, slotType);
    }

    public void OnDrop(object item)
    {
        if (isLocked) return;
        if (parentEquipment == null) return;
        if (item is ItemStack itemStack)
        {
            equipmentManager.EquipItem(this.parentEquipment, slotType, itemStack);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked) return;

        if (parentEquipment == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();

        parentEquipment.equippedItems.TryGetValue(slotType, out ItemStack item);
        if (item != null && item.itemData != null)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            dragDropController.OnBeginDrag(this, item.itemData.icon);

            if (equipmentManager != null) equipmentManager.HideTooltip();
        }
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            if (dragDropController != null) dragDropController.OnEndDrag();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (isLocked) return;

        if (dragDropController == null || dragDropController.currentSource == null) return;

        object draggedItem = dragDropController.currentSource.GetItem();

        if (CanReceiveDrop(draggedItem))
        {
            OnDrop(draggedItem);
            dragDropController.NotifyDropSuccessful(this);
        }
    }

    public void OnDropSuccess(IDropTarget target) { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isLocked) return;

        if (parentEquipment == null) return;
        if (eventData.button == PointerEventData.InputButton.Right && parentEquipment.equippedItems.ContainsKey(slotType) && parentEquipment.equippedItems[slotType] != null)
        {
            equipmentManager.HandleRightClick(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isLocked) return;

        if (equipmentManager == null) return;
        if (dragDropController != null && dragDropController.currentSource != null) return;

        equipmentManager.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (equipmentManager == null) return;
        equipmentManager.HideTooltip();
    }
}