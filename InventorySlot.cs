using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IDropHandler, IDragSource, IDropTarget
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI quantityText;

    public int slotIndex { get; private set; }
    public Inventory parentInventory { get; private set; }

    private EquipmentManager equipmentManager;
    private UIDragDropController dragDropController;
    private CanvasGroup canvasGroup;

    // --- THIS IS THE KEY CHANGE ---
    // We now use a backing field and a property.
    private InventoryManager _inventoryManager;
    private InventoryManager inventoryManager
    {
        get
        {
            // If the reference is ever missing, grab the singleton instance.
            if (_inventoryManager == null)
            {
                _inventoryManager = InventoryManager.instance;
            }
            return _inventoryManager;
        }
        set => _inventoryManager = value;
    }
    // --- END OF CHANGE ---

    public void Initialize(InventoryManager manager, int index, Inventory parentInv)
    {
        this.inventoryManager = manager; // This sets the backing field via the property's setter
        this.slotIndex = index;
        this.parentInventory = parentInv;

        // These can be initialized here as well for robustness
        this.dragDropController = FindAnyObjectByType<UIDragDropController>();
        this.canvasGroup = GetComponent<CanvasGroup>();
        this.equipmentManager = EquipmentManager.instance;
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
        if (target is EquipmentSlot)
        {
            return;
        }

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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (parentInventory == null || eventData.button != PointerEventData.InputButton.Left) return;
        ItemStack item = parentInventory.items[slotIndex];
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

    public void OnDrop(PointerEventData eventData)
    {
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
        if (parentInventory != null && parentInventory.items[slotIndex] != null && parentInventory.items[slotIndex].itemData != null)
        {
            inventoryManager.ShowTooltip(this);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        inventoryManager.HideTooltip();
    }
}