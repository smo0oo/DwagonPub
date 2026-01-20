using UnityEngine;
using System.Collections;

public class CameraShakeListener : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Multiplier for all shakes. Set to 0 to disable screenshake globally.")]
    public float globalShakeMultiplier = 1.0f;

    // Internal state
    private float currentShakeDuration = 0f;
    private float currentShakeIntensity = 0f;
    private float initialShakeDuration = 0f;

    private Vector3 originalLocalPosition;
    private bool isShaking = false;

    void OnEnable()
    {
        // Subscribe to the event from PlayerAbilityHolder
        PlayerAbilityHolder.OnCameraShakeRequest += TriggerShake;
    }

    void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        PlayerAbilityHolder.OnCameraShakeRequest -= TriggerShake;
    }

    private void TriggerShake(float intensity, float duration)
    {
        if (globalShakeMultiplier <= 0) return;

        // If we are already shaking, only overwrite if the new shake is stronger
        if (isShaking && intensity < currentShakeIntensity) return;

        currentShakeIntensity = intensity * globalShakeMultiplier;
        currentShakeDuration = duration;
        initialShakeDuration = duration;

        if (!isShaking)
        {
            StartCoroutine(ShakeRoutine());
        }
    }

    private IEnumerator ShakeRoutine()
    {
        isShaking = true;
        // We act on LocalPosition so we don't fight the CameraController's World Position updates
        originalLocalPosition = transform.localPosition;

        while (currentShakeDuration > 0)
        {
            // Generate a random offset inside a sphere
            Vector3 randomPoint = Random.insideUnitSphere * currentShakeIntensity;

            // Apply the shake offset to the local position
            transform.localPosition = originalLocalPosition + randomPoint;

            // Reduce timer
            currentShakeDuration -= Time.deltaTime;

            // Optional: Dampen the intensity over time (Linear Falloff)
            float dampeningFactor = currentShakeDuration / initialShakeDuration;
            float currentFrameIntensity = currentShakeIntensity * dampeningFactor;

            yield return null;
        }

        // Reset to exact center when done
        transform.localPosition = originalLocalPosition;
        isShaking = false;
    }
}