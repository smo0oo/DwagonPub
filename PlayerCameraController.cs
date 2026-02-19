using UnityEngine;
using Cinemachine;
using System.Collections;

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

    // --- Default Zoom Configuration ---
    [Range(0f, 1f)]
    [Tooltip("The starting zoom level (0 = Fully Out, 1 = Fully In).")]
    public float defaultZoomLevel = 0.5f;

    [Header("Shake Settings")]
    [Tooltip("Maximum distance from the camera to the source for a shake to be felt.")]
    public float maxShakeDistance = 50f;

    // Internal State
    private float currentZoom = 0f;
    private CinemachineTransposer transposer;
    private CinemachineBasicMultiChannelPerlin noisePerlin;
    private Coroutine shakeCoroutine;

    void Start()
    {
        if (gameplayCamera != null)
        {
            transposer = gameplayCamera.GetCinemachineComponent<CinemachineTransposer>();
            noisePerlin = gameplayCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

            // FIX: Ensure shake is OFF at the start
            if (noisePerlin != null)
            {
                noisePerlin.m_AmplitudeGain = 0f;
            }

            // Set the initial camera position based on the defaultZoomLevel
            if (transposer != null)
            {
                currentZoom = defaultZoomLevel;
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
        PlayerAbilityHolder.OnCameraShakeRequest += TriggerShake;
    }

    void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
        PlayerAbilityHolder.OnCameraShakeRequest -= TriggerShake;
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

    // --- Shake Logic ---

    // [FIX] Updated signature to accept Vector3 sourcePosition
    private void TriggerShake(float intensity, float duration, Vector3 sourcePosition)
    {
        // Safety: Ensure we have the noise component
        if (noisePerlin == null)
        {
            if (gameplayCamera != null)
                noisePerlin = gameplayCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

            if (noisePerlin == null) return; // Still null? Exit.
        }

        // [FIX] Distance Check
        float distance = Vector3.Distance(transform.position, sourcePosition);
        if (distance > maxShakeDistance) return;

        // [FIX] Attenuate intensity based on distance
        float distanceFactor = 1f - Mathf.Clamp01(distance / maxShakeDistance);
        float finalIntensity = intensity * distanceFactor;

        if (finalIntensity <= 0.01f) return;

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ProcessShake(finalIntensity, duration));
    }

    private IEnumerator ProcessShake(float intensity, float duration)
    {
        // Turn Shake ON
        noisePerlin.m_AmplitudeGain = intensity;

        // Ensure Frequency is non-zero so the shake actually happens
        if (noisePerlin.m_FrequencyGain == 0f) noisePerlin.m_FrequencyGain = 1f;

        yield return new WaitForSeconds(duration);

        // Turn Shake OFF
        noisePerlin.m_AmplitudeGain = 0f;
        shakeCoroutine = null;
    }
}