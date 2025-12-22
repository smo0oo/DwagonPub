using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Collections;

[RequireComponent(typeof(Light))]
[RequireComponent(typeof(HDAdditionalLightData))]
public class HDRPLightningController : MonoBehaviour
{
    [Header("Lightning Settings")]
    [Tooltip("The brightness of the lightning in Lux.")]
    public float lightningIntensity = 100000f; // High value for HDRP Sun
    [Tooltip("How long the flash lasts in seconds.")]
    public float flashDuration = 0.2f;

    [Header("Random Timer Settings")]
    [Tooltip("Enable or disable the automatic random timer.")]
    public bool enableRandomTimer = true;
    [Tooltip("Minimum time between flashes (in seconds). 3 mins = 180.")]
    public float minInterval = 180f;
    [Tooltip("Maximum time between flashes (in seconds). 8 mins = 480.")]
    public float maxInterval = 480f;

    private Light _lightComponent;
    private HDAdditionalLightData _hdLightData;
    private float _timer;
    private bool _isFlashing = false;
    private float _baseIntensity;

    private void Awake()
    {
        _lightComponent = GetComponent<Light>();
        _hdLightData = GetComponent<HDAdditionalLightData>();
    }

    private void Start()
    {
        ResetTimer();
    }

    private void Update()
    {
        // Only run timer if enabled and not currently flashing
        if (enableRandomTimer && !_isFlashing)
        {
            _timer -= Time.deltaTime;

            if (_timer <= 0f)
            {
                TriggerLightning();
                ResetTimer();
            }
        }
    }

    /// <summary>
    /// Resets the countdown to a random value between Min and Max.
    /// </summary>
    private void ResetTimer()
    {
        _timer = Random.Range(minInterval, maxInterval);
    }

    /// <summary>
    /// Public method to be called by Timeline (Signal Receiver) or other scripts.
    /// </summary>
    public void TriggerLightning()
    {
        // Prevent overlapping flashes
        if (_isFlashing) return;

        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        _isFlashing = true;

        // 1. Capture the current 'normal' intensity of the sun
        // We capture it now in case the sun changed slowly over time (e.g. day/night cycle)
        // In HDRP, we usually manipulate the Light component intensity directly, 
        // as HDAdditionalLightData handles the physical unit conversion.
        _baseIntensity = _lightComponent.intensity;

        // 2. Set to Lightning Intensity
        _lightComponent.intensity = lightningIntensity;

        // 3. Wait for the set duration
        yield return new WaitForSeconds(flashDuration);

        // 4. Return to normal
        _lightComponent.intensity = _baseIntensity;

        _isFlashing = false;
    }
}