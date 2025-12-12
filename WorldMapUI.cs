using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class WorldMapUI : MonoBehaviour
{
    [Header("UI Structure")]
    public GameObject mapWindow;
    public ScrollRect mapScrollRect;
    public RectTransform mapContent;

    [Header("Prefabs")]
    public GameObject mapMarkerPrefab;
    public GameObject playerIcon;

    [Header("Controls")]
    public float panSpeed = 50f;
    public float zoomSpeed = 0.5f;
    public float minZoom = 0.5f;
    public float maxZoom = 4.0f;

    [Header("Trip Computer")]
    public TextMeshProUGUI tripInfoText;
    public Button setDestinationButton;

    private List<LocationNode> allNodes;
    private LocationNode selectedMapNode;
    private bool isMapOpen = false;

    private WorldMapCameraController cameraController;

    void Start()
    {
        if (mapWindow != null) mapWindow.SetActive(false);

        allNodes = new List<LocationNode>(FindObjectsByType<LocationNode>(FindObjectsSortMode.None));

        FindCameraController();

        if (setDestinationButton != null)
        {
            setDestinationButton.onClick.RemoveAllListeners();
            setDestinationButton.onClick.AddListener(OnSetDestinationClicked);
            setDestinationButton.interactable = false;
        }

        if (playerIcon != null && mapContent != null)
        {
            playerIcon.transform.SetParent(mapContent, false);
            playerIcon.transform.SetAsLastSibling();
        }

        GenerateMapIcons();
    }

    // --- Helper to calculate position ---
    private Vector2 GetNodeUICoords(LocationNode node)
    {
        if (node.useAutoPosition)
        {
            if (WorldMapManager.instance != null)
            {
                return WorldMapManager.instance.GetNormalizedPosition(node.transform.position);
            }
            return new Vector2(0.5f, 0.5f); // Fallback
        }
        else
        {
            // Convert 0-100 manual range to 0-1
            return node.manualMapCoords / 100f;
        }
    }
    // ------------------------------------

    private void FindCameraController()
    {
        if (cameraController != null) return;
        cameraController = FindAnyObjectByType<WorldMapCameraController>();
        if (cameraController == null && GameManager.instance != null && GameManager.instance.worldMapCamera != null)
        {
            cameraController = GameManager.instance.worldMapCamera.GetComponent<WorldMapCameraController>();
            if (cameraController == null) cameraController = GameManager.instance.worldMapCamera.GetComponentInParent<WorldMapCameraController>();
            if (cameraController == null) cameraController = GameManager.instance.worldMapCamera.GetComponentInChildren<WorldMapCameraController>();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M)) ToggleMap();

        if (isMapOpen)
        {
            HandleMapInput();
            UpdatePlayerIcon();
        }
    }

    public void ToggleMap()
    {
        isMapOpen = !isMapOpen;
        if (mapWindow != null) mapWindow.SetActive(isMapOpen);

        FindCameraController();
        if (cameraController != null) cameraController.enabled = !isMapOpen;

        if (isMapOpen)
        {
            selectedMapNode = null;
            if (tripInfoText != null) tripInfoText.text = "Select a destination...";
            if (setDestinationButton != null) setDestinationButton.interactable = false;

            CenterOnPlayer();
        }
    }

    private void CenterOnPlayer()
    {
        if (WorldMapManager.instance == null || WorldMapManager.instance.currentLocation == null) return;

        // Use helper logic
        Vector2 coords = GetNodeUICoords(WorldMapManager.instance.currentLocation);

        if (mapScrollRect != null)
        {
            mapScrollRect.horizontalNormalizedPosition = coords.x;
            mapScrollRect.verticalNormalizedPosition = coords.y;
        }
    }

    private void HandleMapInput()
    {
        if (Input.GetMouseButton(2))
        {
            float x = Input.GetAxis("Mouse X") * panSpeed;
            float y = Input.GetAxis("Mouse Y") * panSpeed;
            mapContent.anchoredPosition += new Vector2(x, y);
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Vector3 scale = mapContent.localScale;
            float newScaleVal = Mathf.Clamp(scale.x + (scroll * zoomSpeed), minZoom, maxZoom);
            mapContent.localScale = new Vector3(newScaleVal, newScaleVal, 1f);
        }
    }

    private void UpdatePlayerIcon()
    {
        if (WorldMapManager.instance == null) return;

        Vector3 worldPos = Vector3.zero;
        if (WorldMapManager.instance.wagonController != null)
        {
            worldPos = WorldMapManager.instance.wagonController.transform.position;
        }
        else if (WorldMapManager.instance.currentLocation != null)
        {
            worldPos = WorldMapManager.instance.currentLocation.transform.position;
        }

        // Calculate normalized based on World Pos (Auto) or fall back to node (Manual) if needed?
        // Actually, for the wagon moving *between* nodes, we MUST use Auto. 
        // If you use manual coords for nodes, the wagon icon will jump.
        // Assuming WorldMapManager handles interpolation logic via GetNormalizedPosition.

        Vector2 coords = WorldMapManager.instance.GetNormalizedPosition(worldPos);
        SetIconPosition(playerIcon.GetComponent<RectTransform>(), coords);
    }

    private void GenerateMapIcons()
    {
        if (mapMarkerPrefab == null || mapContent == null) return;

        foreach (var node in allNodes)
        {
            if (node.nodeType == NodeType.Waypoint) continue;

            GameObject markerObj = Instantiate(mapMarkerPrefab, mapContent);

            // --- Use Helper ---
            SetIconPosition(markerObj.GetComponent<RectTransform>(), GetNodeUICoords(node));

            MapMarker markerScript = markerObj.GetComponent<MapMarker>();
            if (markerScript == null) markerScript = markerObj.AddComponent<MapMarker>();

            markerScript.Initialize(node, this);
        }
    }

    private void SetIconPosition(RectTransform element, Vector2 coords)
    {
        if (element == null) return;
        float x = (coords.x - 0.5f) * mapContent.rect.width;
        float y = (coords.y - 0.5f) * mapContent.rect.height;
        element.anchoredPosition = new Vector2(x, y);
    }

    public void SelectNode(LocationNode node)
    {
        selectedMapNode = node;

        if (WorldMapManager.instance == null) return;

        var tripData = NavigationComputer.CalculateTrip(WorldMapManager.instance.currentLocation, node);

        if (tripInfoText != null)
        {
            if (tripData.isValid)
            {
                tripInfoText.text = $"<b>To: {node.locationName}</b>\n" +
                                    $"Time: {tripData.totalHours}h\n" +
                                    $"Fuel: {tripData.estimatedFuelCost:F0}\n" +
                                    $"Food: {tripData.estimatedRationsCost:F0}";

                if (setDestinationButton != null) setDestinationButton.interactable = true;
            }
            else
            {
                tripInfoText.text = "Cannot find a path to this location.";
                if (setDestinationButton != null) setDestinationButton.interactable = false;
            }
        }
    }

    public void OnSetDestinationClicked()
    {
        if (selectedMapNode != null && WorldMapManager.instance != null)
        {
            WorldMapManager.instance.SetLongTermDestination(selectedMapNode);
            ToggleMap();
        }
    }
}