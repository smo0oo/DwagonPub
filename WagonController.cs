using UnityEngine;
using System.Collections;
using Unity.Mathematics;
using UnityEngine.Splines;

public class WagonController : MonoBehaviour
{
    public bool IsTraveling { get; private set; } = false;
    public float TravelProgress { get; private set; } = 0f;
    public bool IsPaused { get; private set; } = false;
    public SplineContainer CurrentSpline { get; private set; }
    public bool IsReversing { get; private set; }

    private Coroutine _activeJourneyCoroutine;
    private Camera mainCamera;
    private LocationNode currentNode;

    void OnEnable()
    {
        if (WorldMapManager.instance != null)
        {
            WorldMapManager.instance.wagonController = this;
            WorldMapManager.instance.LinkWagonToCamera();
        }
    }

    void Start()
    {
        mainCamera = Camera.main;
        if (WorldMapManager.instance != null)
        {
            currentNode = WorldMapManager.instance.currentLocation;
        }
    }

    void Update()
    {
        if (IsTraveling) return;
        if (WagonWorkshopUI.instance != null && WagonWorkshopUI.instance.IsShopOpen()) return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleInput();
        }
    }

    private void HandleInput()
    {
        if (mainCamera == null) mainCamera = Camera.main;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            LocationNode targetNode = hit.collider.GetComponent<LocationNode>();

            // If we clicked a valid DIFFERENT node
            if (targetNode != null && currentNode != null && targetNode != currentNode)
            {
                // DELEGATE TO MANAGER
                if (WorldMapManager.instance != null)
                {
                    WorldMapManager.instance.OnLocationClicked(targetNode);
                }
            }
        }
    }

    // --- MOVEMENT SYSTEM ---

    public void PauseJourney() { IsPaused = true; }
    public void ResumeJourney() { IsPaused = false; }

    public void RestorePosition(SplineContainer spline, float progress, bool reverse)
    {
        if (spline == null) return;
        CurrentSpline = spline;
        TravelProgress = Mathf.Clamp01(progress);
        IsReversing = reverse;
        float evaluatedProgress = reverse ? 1f - TravelProgress : TravelProgress;
        spline.Evaluate(evaluatedProgress, out float3 position, out float3 tangent, out float3 upVector);
        transform.position = position;
        if (!math.all(tangent == 0))
        {
            Vector3 lookDirection = reverse ? -tangent : tangent;
            transform.rotation = Quaternion.LookRotation(lookDirection, upVector);
        }
    }

    public Coroutine StartJourney(SplineContainer spline, float durationInSeconds, bool reverse = false, float yRotationOffset = 0f, float startProgress = 0f)
    {
        if (_activeJourneyCoroutine != null) StopCoroutine(_activeJourneyCoroutine);
        CurrentSpline = spline;
        IsReversing = reverse;
        _activeJourneyCoroutine = StartCoroutine(JourneyCoroutine(spline, durationInSeconds, reverse, yRotationOffset, startProgress));
        return _activeJourneyCoroutine;
    }

    private IEnumerator JourneyCoroutine(SplineContainer spline, float duration, bool reverse, float yOffset, float startProgress)
    {
        if (spline == null || duration <= 0f)
        {
            _activeJourneyCoroutine = null;
            yield break;
        }

        IsTraveling = true;
        IsPaused = false;
        TravelProgress = startProgress;
        float elapsedTime = duration * startProgress;

        while (elapsedTime < duration)
        {
            if (IsPaused) { yield return null; continue; }
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
        CurrentSpline = null;
        _activeJourneyCoroutine = null;
    }

    public void SetCurrentNode(LocationNode node)
    {
        currentNode = node;
    }
}