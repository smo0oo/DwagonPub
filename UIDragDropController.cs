using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(RectTransform))]
public class UIDragDropController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    public Image itemIcon;

    [HideInInspector] public IDragSource currentSource;
    private bool _dropSuccessful = false;
    private bool _isDragging = false;

    // --- STATE VARIABLES ---
    private Transform _originalParent;
    private int _originalSiblingIndex;
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Transform _godLayer;
    private RectTransform _godLayerRect; // Needed for coordinate conversion

    void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();

        // Ensure icon starts hidden
        if (itemIcon != null) itemIcon.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        // --- THE FIX: Robust Coordinate Conversion ---
        if (_isDragging && _rectTransform != null && _godLayerRect != null)
        {
            Vector2 localPoint;
            // Convert Screen Mouse Pos -> Local Point in the God Layer Rect
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _godLayerRect,
                Input.mousePosition,
                null, // null for Screen Space - Overlay (which God Layer is)
                out localPoint))
            {
                _rectTransform.anchoredPosition = localPoint;
            }
        }
    }

    // ---------------------------------------------------------
    // API
    // ---------------------------------------------------------

    public void OnBeginDrag(IDragSource source, Sprite icon)
    {
        currentSource = source;
        if (icon != null && itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.gameObject.SetActive(true);
        }
        StartDragLogic(null);
    }

    public void OnEndDrag() => OnEndDrag(null);
    public void NotifyDropSuccessful(bool success) => _dropSuccessful = success;

    // ---------------------------------------------------------
    // DRAG LOGIC
    // ---------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData) => StartDragLogic(eventData);

    private void StartDragLogic(PointerEventData eventData)
    {
        _dropSuccessful = false;
        _isDragging = true;
        _originalParent = transform.parent;
        _originalSiblingIndex = transform.GetSiblingIndex();

        // 1. Find or Create God Layer (Failsafe)
        if (GameManager.instance != null && GameManager.instance.globalUiOverlay != null)
        {
            _godLayer = GameManager.instance.globalUiOverlay;
        }
        else
        {
            GameObject found = GameObject.Find("GlobalUIOverlay");
            if (found == null)
            {
                found = new GameObject("GlobalUIOverlay");
                Canvas c = found.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 30002;
                found.AddComponent<GraphicRaycaster>();
                DontDestroyOnLoad(found);
            }
            _godLayer = found.transform;
        }

        _godLayerRect = _godLayer.GetComponent<RectTransform>();

        // 2. Move to God Layer
        if (_godLayer != null)
        {
            // 'false' is critical to allow us to reset coordinates manually below
            transform.SetParent(_godLayer, false);
        }

        // 3. Reset Transform & Snap Immediately
        if (_rectTransform != null)
        {
            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.localScale = Vector3.one;

            // Initial snap using the same logic as LateUpdate
            if (_godLayerRect != null)
            {
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_godLayerRect, Input.mousePosition, null, out localPoint))
                {
                    _rectTransform.anchoredPosition = localPoint;
                }
            }
        }

        // 4. Visual Settings
        transform.SetAsLastSibling();
        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
        if (TooltipManager.instance != null) TooltipManager.instance.HideTooltip();
    }

    public void OnDrag(PointerEventData eventData) { }

    public void OnEndDrag(PointerEventData eventData)
    {
        _isDragging = false;

        // 1. Handle Drop Failure
        if (!_dropSuccessful)
        {
            if (transform.parent == _godLayer || transform.parent == null)
            {
                ReturnToOriginalSlot();
            }
        }

        // 2. Cleanup
        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.alpha = 1.0f;
        }

        if (itemIcon != null) itemIcon.gameObject.SetActive(false);

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
                _rectTransform.localScale = Vector3.one;
            }
        }
    }
}