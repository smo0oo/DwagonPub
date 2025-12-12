using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;

public class UIDragDropController : MonoBehaviour
{
    [Header("UI References")]
    public Image draggedItemIcon;

    // --- NEW: Added a field to control the drag icon's size ---
    [Header("Drag Visuals")]
    [Tooltip("The scale of the icon that follows the cursor during a drag operation.")]
    [Range(0.1f, 1f)]
    public float dragIconScale = 0.25f;

    [Header("Visual Scripting Hooks")]
    public GameObject uiManagerObject;
    public string dragInProgressVariableName = "UIDragInProgress";

    public IDragSource currentSource { get; private set; }
    private bool dropWasSuccessful;

    void Start()
    {
        if (draggedItemIcon != null)
        {
            draggedItemIcon.raycastTarget = false;
            draggedItemIcon.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (draggedItemIcon != null && draggedItemIcon.gameObject.activeSelf)
        {
            draggedItemIcon.transform.position = Input.mousePosition;
        }
    }

    public void OnBeginDrag(IDragSource source, Sprite icon)
    {
        currentSource = source;
        dropWasSuccessful = false;
        SetDragInProgressFlag(true);

        if (draggedItemIcon != null && icon != null)
        {
            draggedItemIcon.sprite = icon;
            draggedItemIcon.gameObject.SetActive(true);
            // --- NEW: Apply the custom scale to the drag icon ---
            draggedItemIcon.transform.localScale = new Vector3(dragIconScale, dragIconScale, 1f);
        }
    }

    public void OnEndDrag()
    {
        if (currentSource != null && dropWasSuccessful)
        {
            // The target now tells the source if the drop was successful.
        }

        currentSource = null;
        if (draggedItemIcon != null)
        {
            draggedItemIcon.gameObject.SetActive(false);
            // --- NEW: Reset the scale for the next drag operation ---
            draggedItemIcon.transform.localScale = Vector3.one;
        }
        SetDragInProgressFlag(false);
    }

    public void NotifyDropSuccessful(IDropTarget target)
    {
        if (currentSource != null)
        {
            currentSource.OnDropSuccess(target);
            dropWasSuccessful = true;
        }
    }

    private void SetDragInProgressFlag(bool value)
    {
        if (uiManagerObject != null)
        {
            Variables.Object(uiManagerObject).Set(dragInProgressVariableName, value);
        }
    }
}