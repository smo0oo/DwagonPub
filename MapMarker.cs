using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapMarker : MonoBehaviour
{
    [Header("References")]
    public Button button;
    public TextMeshProUGUI label;

    private LocationNode myNode;
    private WorldMapUI uiManager;

    public void Initialize(LocationNode node, WorldMapUI manager)
    {
        myNode = node;
        uiManager = manager;

        if (label != null) label.text = node.locationName;

        // Setup Button
        if (button == null) button = GetComponent<Button>();

        button.onClick.RemoveAllListeners(); // Clear old ones
        button.onClick.AddListener(OnClicked);
    }

    public void OnClicked()
    {
        Debug.Log($"[MapMarker] Clicked: {myNode.locationName}");

        if (uiManager != null)
        {
            uiManager.SelectNode(myNode);
        }
        else
        {
            Debug.LogError("[MapMarker] No UI Manager reference!");
        }
    }
}