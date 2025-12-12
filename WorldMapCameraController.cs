using UnityEngine;
using Cinemachine;

public class WorldMapCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The virtual camera that will be controlled.")]
    public CinemachineVirtualCamera vcam;

    [Header("Settings")]
    [Tooltip("How fast the camera rotates.")]
    public float rotationSpeed = 10f;
    [Tooltip("How fast the camera zooms.")]
    public float zoomSpeed = 5f;
    [Tooltip("The closest the camera can zoom in (a smaller Z offset).")]
    public float minZoom = -5f;
    [Tooltip("The furthest the camera can zoom out (a larger Z offset).")]
    public float maxZoom = -20f;

    private CinemachineOrbitalTransposer orbitalTransposer;

    void Start()
    {
        if (vcam != null)
        {
            // Get the Orbital Transposer component from the virtual camera
            orbitalTransposer = vcam.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        }
    }

    void Update()
    {
        // Ensure we have the component before trying to control it
        if (orbitalTransposer == null) return;

        //--- Rotation ---
        // Check if the middle mouse button (button 2) is being held down
        if (Input.GetMouseButton(2))
        {
            // Get the horizontal mouse movement and apply it to the camera's X-axis
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            orbitalTransposer.m_XAxis.Value += mouseX;
        }

        //--- Zoom ---
        // Get the mouse scroll wheel input
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            // Adjust the camera's Z offset (distance) based on the scroll input
            float newZoom = orbitalTransposer.m_FollowOffset.z + (scroll * zoomSpeed);

            // Clamp the value between our min and max zoom distances
            orbitalTransposer.m_FollowOffset.z = Mathf.Clamp(newZoom, maxZoom, minZoom);
        }
    }
}