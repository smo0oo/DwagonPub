using UnityEngine;
using System.Collections;
using Unity.Mathematics;
using UnityEngine.Splines;

public class WagonController : MonoBehaviour
{
    public bool IsTraveling { get; private set; } = false;
    public float TravelProgress { get; private set; } = 0f;
    public bool IsPaused { get; private set; } = false;

    private Coroutine _activeJourneyCoroutine;

    void OnEnable()
    {
        if (WorldMapManager.instance != null)
        {
            WorldMapManager.instance.wagonController = this;
            WorldMapManager.instance.LinkWagonToCamera();
        }
    }

    public void PauseJourney()
    {
        IsPaused = true;
    }

    public void ResumeJourney()
    {
        IsPaused = false;
    }

    public Coroutine StartJourney(SplineContainer spline, float durationInSeconds, bool reverse = false, float yRotationOffset = 0f)
    {
        if (_activeJourneyCoroutine != null)
        {
            StopCoroutine(_activeJourneyCoroutine);
        }
        _activeJourneyCoroutine = StartCoroutine(JourneyCoroutine(spline, durationInSeconds, reverse, yRotationOffset));
        return _activeJourneyCoroutine;
    }

    private IEnumerator JourneyCoroutine(SplineContainer spline, float duration, bool reverse, float yOffset)
    {
        if (spline == null || duration <= 0f)
        {
            _activeJourneyCoroutine = null;
            yield break;
        }

        IsTraveling = true;
        IsPaused = false;
        TravelProgress = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            // --- Pause Check ---
            if (IsPaused)
            {
                yield return null;
                continue;
            }

            elapsedTime += Time.deltaTime;
            TravelProgress = Mathf.Clamp01(elapsedTime / duration);

            float evaluatedProgress = reverse ? 1f - TravelProgress : TravelProgress;
            spline.Evaluate(evaluatedProgress, out float3 position, out float3 tangent, out float3 upVector);

            transform.position = position;

            if (!math.all(tangent == 0))
            {
                Vector3 lookDirection = reverse ? -tangent : tangent;
                Quaternion baseRotation = Quaternion.LookRotation(lookDirection, upVector);
                transform.rotation = baseRotation * Quaternion.Euler(0, yOffset, 0);
            }
            yield return null;
        }

        TravelProgress = 1f;
        float finalProgress = reverse ? 0f : 1f;
        spline.Evaluate(finalProgress, out float3 endPosition, out float3 endTangent, out float3 endUp);

        transform.position = endPosition;
        if (!math.all(endTangent == 0))
        {
            Vector3 finalLook = reverse ? -endTangent : endTangent;
            transform.rotation = Quaternion.LookRotation(finalLook, endUp) * Quaternion.Euler(0, yOffset, 0);
        }

        IsTraveling = false;
        _activeJourneyCoroutine = null;
    }
}