using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// A temporary debugging tool to test Physics.Raycast on left-click.
/// This helps diagnose issues with raycasts not hitting desired objects.
/// </summary>
public class RaycastDebugger : MonoBehaviour
{
    [Tooltip("Set this to the same layers you are trying to hit with your other scripts.")]
    public LayerMask layersToTest;

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("RaycastDebugger: No camera tagged 'MainCamera' found in the scene!", this);
            this.enabled = false;
        }
    }

    void Update()
    {
        // We only fire the test on a left mouse click.
        if (Input.GetMouseButtonDown(0))
        {
            // First, check if the mouse is over a UI element, just like our HoverInfoUI script does.
            if (EventSystem.current.IsPointerOverGameObject())
            {
                Debug.LogWarning("Raycast Test Aborted: Mouse is currently over a UI element.");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, layersToTest, QueryTriggerInteraction.Collide))
            {
                // If we hit something, log its name and layer.
                string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
                Debug.Log($"<color=green>Raycast Hit Success!</color> Object: <b>{hit.collider.gameObject.name}</b> on Layer: <b>{layerName}</b>");
            }
            else
            {
                // If we didn't hit anything on the specified layers.
                Debug.Log("<color=red>Raycast Hit Failed.</color> The ray did not hit any colliders on the selected layers.");
            }
        }
    }
}