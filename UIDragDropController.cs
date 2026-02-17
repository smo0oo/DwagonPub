using UnityEngine;
using UnityEngine.UI;
using Unity.VisualScripting;
using System.Reflection; // Required for the safe input fix

public class UIDragDropController : MonoBehaviour
{
    [Header("UI References")]
    public Image draggedItemIcon;

    [Header("Drag Visuals")]
    [Tooltip("The scale of the icon that follows the cursor during a drag operation.")]
    [Range(0.1f, 1f)]
    public float dragIconScale = 0.25f;

    [Header("Visual Scripting Hooks")]
    public GameObject uiManagerObject;
    public string dragInProgressVariableName = "UIDragInProgress";

    public IDragSource currentSource { get; private set; }
    private bool dropWasSuccessful;

    // --- INPUT SYSTEM CACHING (Performance) ---
    private MethodInfo _newInputReadValue;
    private object _newInputPositionControl;
    private bool _initializedInputCheck = false;

    void Start()
    {
        InitializeInputSystem();

        if (draggedItemIcon != null)
        {
            draggedItemIcon.raycastTarget = false;
            draggedItemIcon.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("[UIDragDropController] 'Dragged Item Icon' is NOT assigned in the Inspector! The icon will not appear.");
        }
    }

    void Update()
    {
        if (draggedItemIcon != null && draggedItemIcon.gameObject.activeSelf)
        {
            draggedItemIcon.transform.position = GetMousePosition();
        }
    }

    // --- THE UNIVERSAL MOUSE FIX ---
    private void InitializeInputSystem()
    {
        // Try to find the New Input System via Reflection so we don't rely on #defines
        try
        {
            System.Type mouseType = System.Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
            if (mouseType != null)
            {
                PropertyInfo currentProp = mouseType.GetProperty("current");
                object currentMouse = currentProp.GetValue(null);
                if (currentMouse != null)
                {
                    PropertyInfo positionProp = mouseType.GetProperty("position");
                    _newInputPositionControl = positionProp.GetValue(currentMouse);
                    _newInputReadValue = _newInputPositionControl.GetType().GetMethod("ReadValue");
                }
            }
        }
        catch
        {
            // Fallback to old system if anything fails
            _newInputReadValue = null;
        }
        _initializedInputCheck = true;
    }

    private Vector2 GetMousePosition()
    {
        if (!_initializedInputCheck) InitializeInputSystem();

        // 1. Try New Input System
        if (_newInputReadValue != null && _newInputPositionControl != null)
        {
            try
            {
                return (Vector2)_newInputReadValue.Invoke(_newInputPositionControl, null);
            }
            catch { /* Ignore and fall through */ }
        }

        // 2. Fallback to Old Input System
        return Input.mousePosition;
    }
    // -------------------------------

    public void OnBeginDrag(IDragSource source, Sprite icon)
    {
        currentSource = source;
        dropWasSuccessful = false;
        SetDragInProgressFlag(true);

        if (draggedItemIcon != null && icon != null)
        {
            draggedItemIcon.sprite = icon;
            draggedItemIcon.gameObject.SetActive(true);

            // Apply scale
            draggedItemIcon.transform.localScale = new Vector3(dragIconScale, dragIconScale, 1f);

            // [FIX] Move to mouse immediately so it doesn't flash at (0,0)
            draggedItemIcon.transform.position = GetMousePosition();
        }
        else if (draggedItemIcon == null)
        {
            Debug.LogError("[UIDragDropController] OnBeginDrag fired, but 'Dragged Item Icon' is NULL. Check the Inspector.");
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