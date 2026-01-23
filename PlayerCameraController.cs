using UnityEngine;
using Cinemachine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your main gameplay Cinemachine Virtual Camera here.")]
    public CinemachineVirtualCamera gameplayCamera;

    [Header("Zoom Settings")]
    [Tooltip("How fast the camera moves between the zoom points.")]
    public float zoomSpeed = 5f;

    [Tooltip("The camera's offset from the player when fully zoomed OUT.")]
    public Vector3 zoomedOutOffset = new Vector3(0, 10, -20);

    [Tooltip("The camera's offset from the player when fully zoomed IN.")]
    public Vector3 zoomedInOffset = new Vector3(0, 1, 0);

    // --- NEW: Default Zoom Level Configuration ---
    [Range(0f, 1f)]
    [Tooltip("The starting zoom level (0 = Fully Out, 1 = Fully In).")]
    public float defaultZoomLevel = 0.5f;
    // ---------------------------------------------

    // This tracks the current zoom level, from 0 (out) to 1 (in).
    private float currentZoom = 0f;

    private CinemachineTransposer transposer;

    void Start()
    {
        if (gameplayCamera != null)
        {
            transposer = gameplayCamera.GetCinemachineComponent<CinemachineTransposer>();

            // Set the initial camera position based on the defaultZoomLevel
            if (transposer != null)
            {
                currentZoom = defaultZoomLevel; // Initialize our tracker
                transposer.m_FollowOffset = Vector3.Lerp(zoomedOutOffset, zoomedInOffset, currentZoom);
            }
        }

        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            HandleActivePlayerChanged(PartyManager.instance.ActivePlayer);
        }
    }

    void OnEnable()
    {
        PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;
    }

    void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
    }

    private void HandleActivePlayerChanged(GameObject newPlayer)
    {
        if (gameplayCamera != null && newPlayer != null)
        {
            gameplayCamera.Follow = newPlayer.transform;
            gameplayCamera.LookAt = newPlayer.transform;
        }
    }

    void Update()
    {
        if (transposer == null) return;

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0)
        {
            currentZoom += scrollInput * zoomSpeed * Time.deltaTime;

            currentZoom = Mathf.Clamp01(currentZoom);

            Vector3 newOffset = Vector3.Lerp(zoomedOutOffset, zoomedInOffset, currentZoom);

            transposer.m_FollowOffset = newOffset;
        }
    }
}