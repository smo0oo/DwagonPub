using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// A debugging tool that constantly reports the name of the topmost UI element
/// under the mouse cursor to the console. This helps diagnose raycasting issues.
/// </summary>
public class UIHoverDebugger : MonoBehaviour
{
    // A reference to the Event System in the scene.
    private EventSystem eventSystem;

    // We store the last object we hovered over to prevent spamming the console every frame.
    private GameObject lastHoveredObject = null;

    void Start()
    {
        // Find the Event System in the scene.
        eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogError("UIHoverDebugger: No Event System found in the scene. This script requires an Event System to function.");
            this.enabled = false; // Disable the script if there's no Event System.
        }
    }

    void Update()
    {
        // Create a pointer event data object for the current mouse position.
        PointerEventData pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = Input.mousePosition;

        // Create a list to receive the results of the raycast.
        List<RaycastResult> results = new List<RaycastResult>();

        // Perform the raycast against all graphics in the scene.
        eventSystem.RaycastAll(pointerEventData, results);

        // Check if the raycast hit any UI elements.
        if (results.Count > 0)
        {
            // The first result in the list is the topmost element.
            GameObject currentHoveredObject = results[0].gameObject;

            // Only log to the console if the hovered object has changed.
            if (currentHoveredObject != lastHoveredObject)
            {
                Debug.Log($"<color=cyan>UI Hover: Now hovering over '{currentHoveredObject.name}'</color>");
                lastHoveredObject = currentHoveredObject;
            }
        }
        else
        {
            // If we are not hovering over any UI elements.
            if (lastHoveredObject != null)
            {
                Debug.Log("<color=grey>UI Hover: Mouse is no longer over any UI elements.</color>");
                lastHoveredObject = null;
            }
        }
    }
}
