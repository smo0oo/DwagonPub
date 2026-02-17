using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(RectTransform))]
public class UIDragDropController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    public Image itemIcon;

    [HideInInspector] public IDragSource currentSource; // Stores who started the drag
    private bool _dropSuccessful = false;

    // --- STATE VARIABLES ---
    private Transform _originalParent;
    private int _originalSiblingIndex;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Canvas _rootCanvas;
    private Canvas _dragCanvas; // [NEW] The temporary canvas for sorting

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();

        // Find the absolute root canvas
        Canvas[] parentCanvases = GetComponentsInParent<Canvas>();
        if (parentCanvases.Length > 0)
        {
            _rootCanvas = parentCanvases[parentCanvases.Length - 1];
        }
    }

    // ---------------------------------------------------------
    // API: The Custom Overloads Your Code Calls
    // ---------------------------------------------------------

    public void OnBeginDrag(IDragSource source, Sprite icon)
    {
        currentSource = source;
        if (icon != null && itemIcon != null) itemIcon.sprite = icon;
        StartDragLogic(null);
    }

    public void OnEndDrag()
    {
        OnEndDrag(null);
    }

    public void NotifyDropSuccessful(bool success)
    {
        _dropSuccessful = success;
    }

    // ---------------------------------------------------------
    // DRAG LOGIC
    // ---------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        StartDragLogic(eventData);
    }

    private void StartDragLogic(PointerEventData eventData)
    {
        _dropSuccessful = false;
        _originalParent = transform.parent;
        _originalSiblingIndex = transform.GetSiblingIndex();

        // 1. Move to Root Canvas (Escape Masks)
        if (_rootCanvas != null)
        {
            transform.SetParent(_rootCanvas.transform, true);
        }

        // 2. [THE FIX] Force "God Layer" Sorting
        // We add a temporary Canvas to this object so it draws above EVERYTHING (including Tooltips)
        _dragCanvas = GetComponent<Canvas>();
        if (_dragCanvas == null) _dragCanvas = gameObject.AddComponent<Canvas>();

        _dragCanvas.overrideSorting = true;
        _dragCanvas.sortingOrder = 30002; // Higher than Tooltip (30000)

        // 3. Make transparent to clicks
        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
        }

        if (TooltipManager.instance != null) TooltipManager.instance.HideTooltip();

        // 4. Snap Position
        if (eventData != null && _rectTransform != null) _rectTransform.position = eventData.position;
        else if (_rectTransform != null) _rectTransform.position = Input.mousePosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_rectTransform != null)
        {
            // Use eventData if available, otherwise fallback to Input (for manual calls)
            if (eventData != null) _rectTransform.position = eventData.position;
            else _rectTransform.position = Input.mousePosition;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 1. Clean up the temporary Canvas (Critical!)
        // We must remove it so the icon renders normally inside the slot later
        if (_dragCanvas != null)
        {
            Destroy(_dragCanvas);
        }

        // 2. Handle Drop Failure
        if (!_dropSuccessful)
        {
            if (transform.parent == _rootCanvas.transform)
            {
                ReturnToOriginalSlot();
            }
        }

        // 3. Restore Input
        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1.0f;
        }

        currentSource = null;
    }

    private void ReturnToOriginalSlot()
    {
        if (_originalParent != null)
        {
            transform.SetParent(_originalParent, true);
            transform.SetSiblingIndex(_originalSiblingIndex);

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }
}