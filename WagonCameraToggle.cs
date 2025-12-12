using UnityEngine;
using Cinemachine;

public class WagonCameraToggle : MonoBehaviour
{
    // These will be found automatically by the script at runtime.
    private CinemachineVirtualCamera orbitalCamera;
    private CinemachineVirtualCamera wagonCamera;

    private bool isWagonCamActive = false;

    // The priorities we will swap between.
    private const int ACTIVE_PRIORITY = 11;
    private const int INACTIVE_PRIORITY = 9;

    void Start()
    {
        FindCameras();
    }

    private void FindCameras()
    {
        // 1. Get the persistent orbital camera from the GameManager singleton.
        if (GameManager.instance != null)
        {
            orbitalCamera = GameManager.instance.worldMapCamera;
        }

        // 2. Get the wagon camera, which is a child of THIS GameObject.
        // The 'true' parameter is important to find it even if it's inactive.
        wagonCamera = GetComponentInChildren<CinemachineVirtualCamera>(true);

        if (orbitalCamera == null)
        {
            Debug.LogError("WagonCameraToggle: Could not find the Orbital Camera (VCam_WorldMapFollow) reference from GameManager.", this);
        }
        if (wagonCamera == null)
        {
            Debug.LogError("WagonCameraToggle: Could not find the child Virtual Camera (VCam_WagonCloseup) on the Wagon prefab.", this);
        }
    }

    void Update()
    {
        if (orbitalCamera == null || wagonCamera == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isWagonCamActive = !isWagonCamActive;
            UpdateCameraPriorities();
        }
    }

    private void UpdateCameraPriorities()
    {
        if (isWagonCamActive)
        {
            wagonCamera.Priority = ACTIVE_PRIORITY;
            orbitalCamera.Priority = INACTIVE_PRIORITY;
        }
        else
        {
            wagonCamera.Priority = INACTIVE_PRIORITY;
            orbitalCamera.Priority = ACTIVE_PRIORITY;
        }
    }
}