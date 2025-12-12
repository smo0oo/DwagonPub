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
        // Dragging from the ability book is a "copy", so we do nothing here.
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (assignedAbility != null)
        {
            canvasGroup.blocksRaycasts = false;
            dragDropController.OnBeginDrag(this, assignedAbility.icon);
        }
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        dragDropController.OnEndDrag();
    }

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