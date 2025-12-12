using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class CamOcclusionDither : MonoBehaviour
{
    [Header("Core References")]
    [Tooltip("The main gameplay camera.")]
    public Camera mainCamera;

    [Header("Settings")]
    [Tooltip("How often (in seconds) the script checks for occluding objects.")]
    public float checkInterval = 0.1f;
    [Tooltip("The layers that this script should treat as occluders (e.g., 'Roofs', 'Walls').")]
    public LayerMask occlusionLayers;
    [Tooltip("How thick the raycast is. A larger value will catch objects near the line of sight.")]
    public float castRadius = 0.5f;
    [Tooltip("How quickly the objects fade in and out.")]
    public float fadeDuration = 0.25f;

    [Tooltip("An offset from the player's pivot point to target the raycast (e.g., Y=1 aims for the chest).")]
    public Vector3 playerTargetOffset = new Vector3(0, 1f, 0);

    private Transform playerTransform;
    private Dictionary<Renderer, Coroutine> ditheredObjects = new Dictionary<Renderer, Coroutine>();
    private static readonly int DitherThresholdID = Shader.PropertyToID("_DitherThreshold");

    // --- NEW: Buffer for Non-Allocating Physics ---
    private RaycastHit[] _occlusionBuffer = new RaycastHit[25];

    void Start()
    {
        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            HandleActivePlayerChanged(PartyManager.instance.ActivePlayer);
        }
        StartCoroutine(OcclusionCheckRoutine());
    }

    private void HandleActivePlayerChanged(GameObject newPlayer)
    {
        if (newPlayer != null)
        {
            playerTransform = newPlayer.transform;
        }
    }

    private IEnumerator OcclusionCheckRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            if (playerTransform != null && mainCamera != null)
            {
                HashSet<Renderer> shouldBeDithered = new HashSet<Renderer>();
                Vector3 targetPosition = playerTransform.position + playerTargetOffset;
                Vector3 direction = (targetPosition - mainCamera.transform.position).normalized;
                float distance = Vector3.Distance(mainCamera.transform.position, targetPosition);

                Debug.DrawRay(mainCamera.transform.position, direction * distance, Color.green, checkInterval);

                // --- MODIFIED: Use Non-Allocating version ---
                int hitCount = Physics.SphereCastNonAlloc(mainCamera.transform.position, castRadius, direction, _occlusionBuffer, distance, occlusionLayers);

                for (int i = 0; i < hitCount; i++)
                {
                    var hit = _occlusionBuffer[i];
                    Renderer hitRenderer = hit.collider.GetComponent<Renderer>();
                    if (hitRenderer != null)
                    {
                        shouldBeDithered.Add(hitRenderer);
                    }
                }

                var objectsToFadeOut = ditheredObjects.Keys.ToList();
                foreach (Renderer renderer in objectsToFadeOut)
                {
                    if (renderer != null && !shouldBeDithered.Contains(renderer))
                    {
                        Fade(renderer, 1f); // Fade back to solid
                    }
                }

                foreach (Renderer renderer in shouldBeDithered)
                {
                    if (renderer != null && !ditheredObjects.ContainsKey(renderer))
                    {
                        Fade(renderer, 0f); // Fade to dithered
                    }
                }
            }
        }
    }

    private void Fade(Renderer renderer, float targetValue)
    {
        if (ditheredObjects.ContainsKey(renderer))
        {
            if (ditheredObjects[renderer] != null) StopCoroutine(ditheredObjects[renderer]);
        }

        Coroutine newFadeRoutine = StartCoroutine(FadeDither(renderer, targetValue));
        ditheredObjects[renderer] = newFadeRoutine;
    }

    private IEnumerator FadeDither(Renderer renderer, float targetValue)
    {
        Material material = renderer.material;
        float startValue = material.GetFloat(DitherThresholdID);
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float newThreshold = Mathf.Lerp(startValue, targetValue, timer / fadeDuration);
            material.SetFloat(DitherThresholdID, newThreshold);
            yield return null;
        }

        material.SetFloat(DitherThresholdID, targetValue);

        if (targetValue >= 1f)
        {
            ditheredObjects.Remove(renderer);
        }
    }

    void OnEnable() { PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged; }
    void OnDisable() { PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged; }
}