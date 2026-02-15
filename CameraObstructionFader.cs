using UnityEngine;
using System.Collections.Generic;

public class CameraObstructionFader : MonoBehaviour
{
    [Header("Settings")]
    public LayerMask ditherLayer;  // Layer 22 (Dither)
    public float checkRadius = 0.5f;
    public float fadeSpeed = 10f;
    [Range(0f, 1f)] public float targetDither = 0.2f; // 0 = Invisible, 1 = Solid

    // Internal State
    private Transform _currentPlayerTarget;
    private Dictionary<Renderer, float> _fadingObjects = new Dictionary<Renderer, float>();
    private List<Renderer> _hitRenderers = new List<Renderer>();
    private MaterialPropertyBlock _propBlock;
    private int _ditherID;

    private void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
        // Matches the float property in your Shader Graph
        _ditherID = Shader.PropertyToID("_Dither");
    }

    private void Start()
    {
        // 1. Grab initial player if PartyManager exists
        if (PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            _currentPlayerTarget = PartyManager.instance.ActivePlayer.transform;
        }
    }

    private void OnEnable()
    {
        // 2. Subscribe to switching events
        PartyManager.OnActivePlayerChanged += HandleActivePlayerChanged;
    }

    private void OnDisable()
    {
        PartyManager.OnActivePlayerChanged -= HandleActivePlayerChanged;
    }

    private void HandleActivePlayerChanged(GameObject newPlayer)
    {
        if (newPlayer != null)
        {
            _currentPlayerTarget = newPlayer.transform;
        }
    }

    private void LateUpdate()
    {
        if (_currentPlayerTarget == null) return;

        // 1. CLEAR PREVIOUS FRAME HITS
        _hitRenderers.Clear();

        // 2. SPHERECAST FROM CAMERA TO PLAYER
        Vector3 dir = _currentPlayerTarget.position - transform.position;
        float dist = dir.magnitude;

        // Cast a thick ray (SphereCast) to find walls
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, checkRadius, dir, dist, ditherLayer);

        // 3. IDENTIFY HITS
        foreach (var hit in hits)
        {
            Renderer r = hit.collider.GetComponent<Renderer>();
            if (r != null)
            {
                _hitRenderers.Add(r);
                if (!_fadingObjects.ContainsKey(r))
                {
                    // If we just hit a new wall, verify its current dither value first
                    // so we don't snap it if it was already partially faded
                    r.GetPropertyBlock(_propBlock);
                    float startVal = 1f;
                    // (Optional: You could read the current value from the renderer here if needed)

                    _fadingObjects.Add(r, startVal);
                }
            }
        }

        // 4. PROCESS FADES
        List<Renderer> toRemove = new List<Renderer>();
        // Create a separate list of keys to iterate safely while modifying the dictionary
        List<Renderer> trackedRenderers = new List<Renderer>(_fadingObjects.Keys);

        foreach (Renderer r in trackedRenderers)
        {
            if (r == null) { toRemove.Add(r); continue; }

            bool isHit = _hitRenderers.Contains(r);
            float currentVal = _fadingObjects[r];

            // If Hit -> Fade down to targetDither. If Clear -> Fade back to 1.0
            float target = isHit ? targetDither : 1.0f;

            // Smoothly move the value
            float newVal = Mathf.MoveTowards(currentVal, target, Time.deltaTime * fadeSpeed);
            _fadingObjects[r] = newVal;

            // Apply to Shader
            r.GetPropertyBlock(_propBlock);
            _propBlock.SetFloat(_ditherID, newVal);
            r.SetPropertyBlock(_propBlock);

            // If object is fully solid (1.0) and not being hit, stop tracking it to save performance
            if (!isHit && Mathf.Approximately(newVal, 1.0f))
            {
                toRemove.Add(r);
            }
        }

        // 5. CLEANUP
        foreach (var r in toRemove)
        {
            _fadingObjects.Remove(r);
        }
    }
}