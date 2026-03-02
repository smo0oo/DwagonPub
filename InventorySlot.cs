using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IDropHandler, IDragSource, IDropTarget, IPointerDownHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;

    public int slotIndex { get; private set; }
    public Inventory parentInventory { get; private set; }

    private EquipmentManager equipmentManager;
    private UIDragDropController dragDropController;
    private CanvasGroup canvasGroup;

    private InventoryManager _inventoryManager;
    private InventoryManager inventoryManager
    {
        get
        {
            if (_inventoryManager == null) _inventoryManager = InventoryManager.instance;
            return _inventoryManager;
        }
        set => _inventoryManager = value;
    }

    public void Initialize(InventoryManager manager, int index, Inventory parentInv)
    {
        this.inventoryManager = manager;
        this.slotIndex = index;
        this.parentInventory = parentInv;

        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();
        canvasGroup = GetComponent<CanvasGroup>();
        equipmentManager = EquipmentManager.instance;
    }

    // --- THE FIX: Forces raw interfaces to obey CanvasGroup rules ---
    private bool IsInteractable()
    {
        CanvasGroup[] groups = GetComponentsInParent<CanvasGroup>();
        foreach (var group in groups)
        {
            if (!group.interactable) return false;
            if (group.ignoreParentGroups) break;
        }
        return true;
    }
    // ----------------------------------------------------------------

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable()) return;
        // Claims the click so OnBeginDrag fires reliably
    }

    public void UpdateSlot(ItemStack itemStack)
    {
        if (itemStack != null && itemStack.itemData != null && itemStack.quantity > 0)
        {
            iconImage.sprite = itemStack.itemData.icon;
            iconImage.enabled = true;
            quantityText.text = itemStack.quantity > 1 ? itemStack.quantity.ToString() : "";
        }
        else
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            quantityText.text = "";
        }
    }

    public object GetItem()
    {
        if (parentInventory == null || slotIndex >= parentInventory.items.Count) return null;
        return parentInventory.items[slotIndex];
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();
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
        return item is ItemStack;
    }

    public void OnDrop(object item)
    {
        if (dragDropController.currentSource is InventorySlot sourceSlot)
        {
            Inventory sourceInventory = sourceSlot.parentInventory;
            Inventory targetInventory = this.parentInventory;
            int fromIndex = sourceSlot.slotIndex;
            int toIndex = this.slotIndex;

            ItemStack sourceStack = sourceInventory.items[fromIndex];
            ItemStack targetStack = targetInventory.items[toIndex];

            sourceInventory.SetItemStack(fromIndex, targetStack);
            targetInventory.SetItemStack(toIndex, sourceStack);
        }
        else if (dragDropController.currentSource is EquipmentSlot sourceEquipSlot)
        {
            equipmentManager.UnequipItemToSpecificSlot(sourceEquipSlot.slotType, this.parentInventory, this.slotIndex);
        }
    }

    public void OnDropSuccess(IDropTarget target)
    {
        if (target is EquipmentSlot) return;

        if (target is TrashSlot)
        {
            parentInventory.SetItemStack(slotIndex, new ItemStack(null, 0));
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        if (parentInventory != null && eventData.button == PointerEventData.InputButton.Right && parentInventory.items[slotIndex]?.itemData != null)
        {
            inventoryManager.OpenContextMenu(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        if (parentInventory == null || eventData.button != PointerEventData.InputButton.Left) return;

        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();

        ItemStack item = parentInventory.items[slotIndex];
        if (item != null && item.itemData != null)
        {
            canvasGroup.blocksRaycasts = false;
            dragDropController.OnBeginDrag(this, item.itemData.icon);
            inventoryManager.HideTooltip();
        }
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            canvasGroup.blocksRaycasts = true;
            if (dragDropController != null) dragDropController.OnEndDrag();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();

        bool isDragging = dragDropController != null && dragDropController.currentSource != null;

        if (!isDragging && parentInventory != null && parentInventory.items[slotIndex] != null && parentInventory.items[slotIndex].itemData != null)
        {
            inventoryManager.ShowTooltip(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inventoryManager.HideTooltip();
    }
}