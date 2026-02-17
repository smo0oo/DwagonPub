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

    public void OnPointerDown(PointerEventData eventData)
    {
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

    public void OnDropSuccess(IDropTarget target)
    {
        if (target is EquipmentSlot) return;

        if (target is TrashSlot)
        {
            parentInventory.SetItemStack(slotIndex, new ItemStack(null, 0));
        }
    }

    public bool CanReceiveDrop(object item) => item is ItemStack;

    public void OnDrop(object item) { }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (parentInventory != null && eventData.button == PointerEventData.InputButton.Right && parentInventory.items[slotIndex]?.itemData != null)
        {
            inventoryManager.OpenContextMenu(this);
        }
    }

    // --- DRAG IMPLEMENTATION ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parentInventory == null || eventData.button != PointerEventData.InputButton.Left) return;
        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();

        ItemStack item = parentInventory.items[slotIndex];
        if (item != null && item.itemData != null)
        {
            // Allow clicks to pass through this slot so we can drop it on others
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

            // Start the drag on the controller
            dragDropController.OnBeginDrag(this, item.itemData.icon);

            inventoryManager.HideTooltip();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // INTENTIONALLY EMPTY
        // The UIDragDropController now handles movement in LateUpdate.
        // We keep this empty method so Unity still detects this object as draggable.
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
        if (dragDropController == null) dragDropController = Object.FindFirstObjectByType<UIDragDropController>();
        if (dragDropController == null || dragDropController.currentSource == null) return;

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

            dragDropController.NotifyDropSuccessful(this);
        }
        else if (dragDropController.currentSource is EquipmentSlot sourceEquipSlot)
        {
            equipmentManager.UnequipItemToSpecificSlot(sourceEquipSlot.slotType, this.parentInventory, this.slotIndex);
            dragDropController.NotifyDropSuccessful(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
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