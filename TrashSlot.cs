using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A special slot that destroys any item dropped onto it.
/// Now works with the refactored UIDragDropController system.
/// </summary>
public class TrashSlot : MonoBehaviour, IDropHandler, IDropTarget
{
    [Header("References")]
    [Tooltip("Manually assign the UIDragDropController from your scene here.")]
    public UIDragDropController dragDropController;

    // --- REFACTORED SINGLE ENTRY POINT ---
    /// <summary>
    /// Called by Unity's Event System when a drag operation is dropped here.
    /// This is now the primary entry point for handling a drop.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (dragDropController == null || dragDropController.currentSource == null)
        {
            Debug.LogError("TrashSlot: The UIDragDropController reference has not been assigned in the Inspector!");
            return;
        }

        // Get the item being dragged directly from the controller.
        object draggedItem = dragDropController.currentSource.GetItem();

        // Check if this slot can receive the item.
        if (CanReceiveDrop(draggedItem))
        {
            // The drop is considered successful. We manually notify the controller.
            // The source slot's OnDropSuccess method is responsible for deleting the item from the data.
            dragDropController.NotifyDropSuccessful(this);
        }
    }

    #region IDropTarget Implementation

    /// <summary>
    /// The trash slot can receive any item from the inventory or equipment.
    /// </summary>
    public bool CanReceiveDrop(object item)
    {
        return item is ItemStack;
    }

    /// <summary>
    /// This method is still required to fulfill the IDropTarget interface,
    /// but the primary logic has been moved to OnDrop(PointerEventData).
    /// </summary>
    public void OnDrop(object item)
    {
        // Logic is now handled in the other OnDrop method. This can remain empty.
    }

    #endregion
}
