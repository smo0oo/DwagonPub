using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class AbilitySlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IDragSource
{
    [Header("UI References")]
    public Image iconImage;
    private Ability assignedAbility;
    private InventoryManager inventoryManager;
    private UIDragDropController dragDropController;
    private CanvasGroup canvasGroup;

    void Start()
    {
        inventoryManager = InventoryManager.instance;
        dragDropController = FindAnyObjectByType<UIDragDropController>();
        canvasGroup = GetComponent<CanvasGroup>();
    }

    public void SetAbility(Ability ability)
    {
        assignedAbility = ability;
        if (iconImage != null)
        {
            iconImage.sprite = ability.icon;
            iconImage.enabled = true;
        }
    }

    public object GetItem() => assignedAbility;

    public void OnDropSuccess(IDropTarget target)
    {
        // Dragging from the ability book is a "copy" operation, so we do nothing to the source.
    }

    // --- DRAG IMPLEMENTATION ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (assignedAbility != null)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;

            // Start the drag
            if (dragDropController != null)
                dragDropController.OnBeginDrag(this, assignedAbility.icon);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // INTENTIONALLY EMPTY
        // Movement is handled by UIDragDropController.LateUpdate
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        if (dragDropController != null) dragDropController.OnEndDrag();
    }

    // ---------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (inventoryManager != null && assignedAbility != null)
        {
            inventoryManager.ShowTooltipForAbility(assignedAbility);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (inventoryManager != null)
        {
            inventoryManager.HideTooltip();
        }
    }
}