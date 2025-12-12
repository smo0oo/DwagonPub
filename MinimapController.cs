using UnityEngine;

public class MinimapController : MonoBehaviour
{
    public static MinimapController instance;

    [Header("References")]
    [Tooltip("The top-down orthographic camera used for the minimap.")]
    public Camera minimapCamera;

    private Transform playerTransform;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    void OnEnable()
    {
        PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;
    }

    void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
    }

    void Start()
    {
        // Set the initial player on start
        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            HandleActivePlayerChanged(PartyManager.instance.ActivePlayer);
        }
    }

    private void HandleActivePlayerChanged(GameObject newPlayer)
    {
        if (newPlayer != null)
        {
            playerTransform = newPlayer.transform;
        }
        else
        {
            playerTransform = null;
        }
    }

    void LateUpdate()
    {
        // If there's no player or camera, do nothing.
        if (playerTransform == null || minimapCamera == null) return;

        // Create a new position for the camera that matches the player's X and Z.
        Vector3 cameraPosition = playerTransform.position;
        // Keep the camera's original height (Y position).
        cameraPosition.y = minimapCamera.transform.position.y;

        // Apply the new position to the camera.
        minimapCamera.transform.position = cameraPosition;
    }
}