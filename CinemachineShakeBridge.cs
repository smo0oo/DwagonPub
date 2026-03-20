using UnityEngine;
using System.Collections;
using Cinemachine; // If you ever upgrade to Unity 6, you may need to change this to Unity.Cinemachine!

[RequireComponent(typeof(CinemachineImpulseSource))]
public class CinemachineShakeBridge : MonoBehaviour
{
    private CinemachineImpulseSource impulseSource;

    void Awake()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    void OnEnable()
    {
        PlayerAbilityHolder.OnCameraShakeRequest += TriggerCinemachineShake;
    }

    void OnDisable()
    {
        PlayerAbilityHolder.OnCameraShakeRequest -= TriggerCinemachineShake;
    }

    private void TriggerCinemachineShake(float intensity, float duration, Vector3 epicenter)
    {
        if (impulseSource == null) return;

        // Start the sustained rumble!
        StartCoroutine(SustainedRumble(intensity, duration, epicenter));
    }

    private IEnumerator SustainedRumble(float intensity, float duration, Vector3 epicenter)
    {
        float elapsed = 0f;

        // If duration is effectively 0, just fire a single sharp bump
        if (duration <= 0.1f)
        {
            FirePulse(intensity, epicenter);
            yield break;
        }

        // Fire continuous micro-impulses for the exact duration of the Ability slider
        while (elapsed < duration)
        {
            FirePulse(intensity, epicenter);

            // Fire a pulse every 0.1 seconds to create a continuous overlapping rumble
            float tickRate = 0.1f;
            elapsed += tickRate;
            yield return new WaitForSeconds(tickRate);
        }
    }

    private void FirePulse(float intensity, Vector3 epicenter)
    {
        // Add slight randomness to the direction so the camera vibrates chaotically instead of just panning down
        Vector3 shakeVelocity = new Vector3(Random.Range(-1f, 1f), -1f, Random.Range(-1f, 1f)).normalized * intensity;

        // Check if this is a "GlobalCamera" shake (bypassing distance logic)
        if (Camera.main != null && epicenter == Camera.main.transform.position)
        {
            impulseSource.GenerateImpulse(shakeVelocity);
        }
        else
        {
            // Otherwise, generate it at the specific world location
            impulseSource.GenerateImpulseAt(epicenter, shakeVelocity);
        }
    }
}