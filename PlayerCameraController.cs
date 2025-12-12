using UnityEngine;
using Cinemachine;

public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    // --- FIX: Corrected the typo in the class name ---
    [Tooltip("Assign your main gameplay Cinemachine Virtual Camera here.")]
    public CinemachineVirtualCamera gameplayCamera;

    [Header("Zoom Settings")]
    [Tooltip("How fast the camera moves between the zoom points.")]
    public float zoomSpeed = 5f;

    [Tooltip("The camera's offset from the player when fully zoomed OUT.")]
    public Vector3 zoomedOutOffset = new Vector3(0, 10, -20);
    [Tooltip("The camera's offset from the player when fully zoomed IN.")]
    public Vector3 zoomedInOffset = new Vector3(0, 1, 0);

    // This tracks the current zoom level, from 0 (out) to 1 (in).
    private float currentZoom = 0f;

    private CinemachineTransposer transposer;

    void Start()
    {
        if (gameplayCamera != null)
        {
            transposer = gameplayCamera.GetCinemachineComponent<CinemachineTransposer>();
            // Set the initial camera position to the fully zoomed-out offset
            if (transposer != null)
            {
                transposer.m_FollowOffset = zoomedOutOffset;
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