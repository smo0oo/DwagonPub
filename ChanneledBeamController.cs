using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ChanneledBeamController : MonoBehaviour
{
    [Header("Beam Settings")]
    [Tooltip("Layers that block the beam (e.g., Ground, Walls).")]
    public LayerMask obstacleLayers; // Renamed from 'groundLayer' for clarity

    [Header("Visuals")]
    [Tooltip("The particle effect to spawn at the target/end point of the beam.")]
    public GameObject endVFXPrefab;
    [Tooltip("Controls the width of the LineRenderer.")]
    public float beamWidth = 1.0f;

    [Tooltip("How many meters per second the beam travels. Set to 0 for instant.")]
    public float beamGrowthSpeed = 40f;

    public Ability sourceAbility;
    public GameObject caster;

    private LineRenderer lineRenderer;
    private PlayerStats casterStats;
    private Health casterHealth;

    private Transform beamTarget;
    private Health targetHealth;

    private GameObject activeEndVFX;

    private float tickTimer;
    private Dictionary<Health, bool> targetsHitThisTick = new Dictionary<Health, bool>();
    private RaycastHit[] _beamHitBuffer = new RaycastHit[25];

    private float currentBeamLength = 0f;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = beamWidth;
    }

    public void Initialize(Ability ability, GameObject casterObject, GameObject targetObject)
    {
        sourceAbility = ability;
        caster = casterObject;

        currentBeamLength = 0f;

        if (targetObject != null)
        {
            beamTarget = targetObject.transform;
            targetHealth = targetObject.GetComponentInChildren<Health>();
        }

        if (caster != null)
        {
            CharacterRoot root = caster.GetComponentInParent<CharacterRoot>();
            if (root != null)
            {
                casterStats = root.GetComponentInChildren<PlayerStats>(true);
                casterHealth = root.GetComponentInChildren<Health>(true);
            }
            else
            {
                Debug.LogError($"ChanneledBeamController could not find a CharacterRoot on the caster '{caster.name}'.", caster);
            }
        }

        if (endVFXPrefab != null)
        {
            activeEndVFX = Instantiate(endVFXPrefab, transform.position, Quaternion.identity);
        }

        UpdateBeamPosition();
    }

    // --- NEW: Public method to change blocking layers at runtime ---
    public void SetObstacleLayers(LayerMask newLayerMask)
    {
        obstacleLayers = newLayerMask;
    }
    // ---------------------------------------------------------------

    public void Interrupt()
    {
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (activeEndVFX != null)
        {
            Destroy(activeEndVFX);
        }
    }

    void Update()
    {
        if (caster == null || casterHealth == null || casterHealth.currentHealth <= 0 || sourceAbility == null || (beamTarget != null && (targetHealth == null || targetHealth.currentHealth <= 0)))
        {
            Destroy(gameObject);
            return;
        }

        if (beamTarget != null)
        {
            float distanceToTarget = Vector3.Distance(caster.transform.position, beamTarget.position);
            if (distanceToTarget > sourceAbility.range)
            {
                Destroy(gameObject);
                return;
            }
        }

        if (beamGrowthSpeed > 0)
        {
            currentBeamLength += beamGrowthSpeed * Time.deltaTime;
        }
        else
        {
            currentBeamLength = float.MaxValue;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = sourceAbility.tickRate;

            if (casterStats != null)
            {
                if (casterStats.currentMana < sourceAbility.manaDrain)
                {
                    Destroy(gameObject);
                    return;
                }
                casterStats.SpendMana(sourceAbility.manaDrain);
            }

            ApplyEffects();
        }

        UpdateBeamPosition();
    }

    private void UpdateBeamPosition()
    {
        if (caster == null || sourceAbility == null) return;

        Transform casterTransform = caster.transform;
        Vector3 worldStartPosition = casterTransform.position + Vector3.up;

        Vector3 fullTargetPosition;
        if (beamTarget != null)
        {
            fullTargetPosition = beamTarget.position + Vector3.up;
        }
        else
        {
            fullTargetPosition = worldStartPosition + (casterTransform.forward * sourceAbility.range);
        }

        Vector3 direction = (fullTargetPosition - worldStartPosition).normalized;
        float totalDistance = Vector3.Distance(worldStartPosition, fullTargetPosition);

        float renderDistance = Mathf.Min(currentBeamLength, totalDistance);

        // --- UPDATED: Use the dynamic 'obstacleLayers' variable ---
        if (Physics.Raycast(worldStartPosition, direction, out RaycastHit hit, renderDistance, obstacleLayers))
        {
            renderDistance = hit.distance;
        }
        // ----------------------------------------------------------

        Vector3 worldEndPosition = worldStartPosition + (direction * renderDistance);

        lineRenderer.SetPosition(0, worldStartPosition);
        lineRenderer.SetPosition(1, worldEndPosition);

        if (activeEndVFX != null)
        {
            activeEndVFX.transform.position = worldEndPosition;
        }
    }

    private void ApplyEffects()
    {
        targetsHitThisTick.Clear();
        Vector3 castStart = lineRenderer.GetPosition(0);
        Vector3 castEnd = lineRenderer.GetPosition(1);

        Vector3 castDirection = (castEnd - castStart).normalized;
        float castDistance = Vector3.Distance(castStart, castEnd);

        if (castDistance < 0.1f) return;

        // Use beamWidth / 2 for the radius to match visual thickness
        float radius = Mathf.Max(0.1f, beamWidth * 0.5f);

        int hitCount = Physics.SphereCastNonAlloc(castStart, radius, castDirection, _beamHitBuffer, castDistance);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _beamHitBuffer[i];

            if (hit.transform.root == caster.transform.root)
            {
                continue;
            }
            Health targetHealth = hit.collider.GetComponentInChildren<Health>();
            if (targetHealth != null && !targetsHitThisTick.ContainsKey(targetHealth))
            {
                targetsHitThisTick.Add(targetHealth, true);

                var casterRoot = caster.GetComponentInParent<CharacterRoot>();
                var targetRoot = hit.collider.GetComponentInParent<CharacterRoot>();

                if (casterRoot != null && targetRoot != null)
                {
                    int casterLayer = casterRoot.gameObject.layer;
                    int targetLayer = targetRoot.gameObject.layer;
                    bool isAlly = casterLayer == targetLayer;
                    var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;
                    foreach (var effect in effectsToApply)
                    {
                        effect.Apply(caster, hit.collider.gameObject);
                    }
                }
            }
        }
    }
}