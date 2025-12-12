using UnityEngine;
using TMPro;

/// <summary>
/// Manages the world-space UI for a WorldItem, ensuring it faces the camera.
/// Now updates on a configurable frame interval for better performance.
/// </summary>
public class WorldItemUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The TextMeshPro component used to display the item's name.")]
    public TextMeshProUGUI nameText;

    [Header("Performance")]
    [Tooltip("How many frames to wait before updating the rotation. 1 = every frame, 4 = every 4th frame.")]
    [Range(1, 60)]
    public int updateInterval = 4;

    private Transform cameraTransform;
    private int frameOffset;

    void Start()
    {
        // Find the main camera in the scene.
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("WorldItemUI: No main camera found in the scene! The UI will not face the camera.");
        }

        // Get the ItemData from the parent WorldItem component to set the name.
        WorldItem parentItem = GetComponentInParent<WorldItem>();
        if (parentItem != null && parentItem.itemData != null)
        {
            if (nameText != null)
            {
                nameText.text = parentItem.itemData.displayName;
            }
        }

        // Assign a random offset so not all UIs update on the exact same frame.
        frameOffset = Random.Range(0, updateInterval);
    }

    void LateUpdate()
    {
        // Only run the update logic on frames that match our interval.
        if (cameraTransform != null && (Time.frameCount + frameOffset) % updateInterval == 0)
        {
            // The UI should look at the camera's position.
            transform.LookAt(transform.position + cameraTransform.rotation * Vector3.forward,
                             cameraTransform.rotation * Vector3.up);
        }
    }
}
