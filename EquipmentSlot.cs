using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class EquipmentSlot : MonoBehaviour, IPointerClickHandler, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IDropTarget, IBeginDragHandler, IDragHandler, IEndDragHandler, IDragSource
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
    }

    void Start()
    {
        dragDropController = FindAnyObjectByType<UIDragDropController>();
        canvasGroup = GetComponent<CanvasGroup>();

        // --- THIS BLOCK IS NOW UPDATED ---
        // If the manager hasn't been set, get it from the static instance.
        if (equipmentManager == null)
        {
            equipmentManager = EquipmentManager.instance;
        }
        // --- END OF UPDATE ---
    }

    // ... (The rest of the script remains unchanged) ...
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

    public object GetItem()
    {
        if (parentEquipment == null) return null;
        parentEquipment.equippedItems.TryGetValue(slotType, out ItemStack item);
        return item;
    }

    public void OnDropSuccess(IDropTarget target)
    {
        // Logic handled by target
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (parentEquipment == null) return;
        if (eventData.button == PointerEventData.InputButton.Right && parentEquipment.equippedItems.ContainsKey(slotType) && parentEquipment.equippedItems[slotType] != null)
        {
            equipmentManager.HandleRightClick(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parentEquipment == null) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        parentEquipment.equippedItems.TryGetValue(slotType, out ItemStack item);
        if (item != null && item.itemData != null)
        {
            canvasGroup.blocksRaycasts = false;
            dragDropController.OnBeginDrag(this, item.itemData.icon);
        }
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            canvasGroup.blocksRaycasts = true;
            dragDropController.OnEndDrag();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (equipmentManager == null) return;
        equipmentManager.ShowTooltip(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (equipmentManager == null) return;
        equipmentManager.HideTooltip();
    }
}