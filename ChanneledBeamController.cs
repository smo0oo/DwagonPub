using UnityEngine;
using System.Collections.Generic;
using UnityEngine.VFX;

public class ChanneledBeamController : MonoBehaviour
{
    [Header("Beam Settings")]
    public LayerMask obstacleLayers;

    [Header("Visuals - Main Beam")]
    [Tooltip("The main beam VFX prefab (VFX Graph, Mesh, or Particle System).")]
    public GameObject beamVFXPrefab;
    [Tooltip("If using VFX Graph, the name of the Float property controlling the physical length.")]
    public string vfxLengthProperty = "BeamLength";
    [Tooltip("If true, stretches the Z-axis of the beam's Transform instead of passing a VFX property. (Use this for meshes/shuriken)")]
    public bool scaleTransformZ = false;

    [Header("Visuals - Impact")]
    [Tooltip("The particle effect to spawn at the target/end point of the beam.")]
    public GameObject endVFXPrefab;

    [Header("Dimensions & Speed")]
    [Tooltip("Controls the radius of the damage/hit detection cylinder.")]
    public float beamHitboxRadius = 1.0f;
    [Tooltip("How many meters per second the beam travels. Set to 0 for instant.")]
    public float beamGrowthSpeed = 40f;

    public Ability sourceAbility;
    public GameObject caster;

    private CharacterRoot casterRoot;
    private PlayerStats casterStats;
    private Health casterHealth;

    private Transform beamTarget;
    private Health targetHealth;

    private Transform emissionPoint;

    // --- VFX Instances ---
    private GameObject activeBeamInstance;
    private VisualEffect activeBeamVFX;
    private GameObject activeEndVFX;

    private float tickTimer;
    private Dictionary<Health, bool> targetsHitThisTick = new Dictionary<Health, bool>();
    private RaycastHit[] _beamHitBuffer = new RaycastHit[25];

    private float currentBeamLength = 0f;

    // Cached positions
    private Vector3 currentStartPos;
    private Vector3 currentEndPos;

    // --- CANCELLATION TRACKERS ---
    private float channelStartTime;
    private Vector3 initialCasterPosition;

    public void Initialize(Ability ability, GameObject casterObject, GameObject targetObject, Transform customSpawnPoint = null)
    {
        sourceAbility = ability;
        caster = casterObject;
        emissionPoint = customSpawnPoint;

        currentBeamLength = 0f;

        channelStartTime = Time.time;
        initialCasterPosition = casterObject != null ? casterObject.transform.position : Vector3.zero;

        if (targetObject != null)
        {
            beamTarget = targetObject.transform;
            targetHealth = targetObject.GetComponentInChildren<Health>();
        }

        if (caster != null)
        {
            // Cache the Root to properly verify the Active Player
            casterRoot = caster.GetComponentInParent<CharacterRoot>();
            if (casterRoot != null)
            {
                casterStats = casterRoot.GetComponentInChildren<PlayerStats>(true);
                casterHealth = casterRoot.GetComponentInChildren<Health>(true);
            }
            else
            {
                Debug.LogError($"ChanneledBeamController could not find a CharacterRoot on the caster '{caster.name}'.", caster);
            }
        }

        // Instantiate Main Beam
        if (beamVFXPrefab != null)
        {
            activeBeamInstance = Instantiate(beamVFXPrefab, transform.position, Quaternion.identity);
            activeBeamVFX = activeBeamInstance.GetComponent<VisualEffect>();
        }

        // Instantiate Impact Effect
        if (endVFXPrefab != null)
        {
            activeEndVFX = Instantiate(endVFXPrefab, transform.position, Quaternion.identity);
        }

        UpdateBeamPosition();
    }

    public void Interrupt()
    {
        // --- THE UN-STUN FIX ---
        // We actively force the player's ability system to release the casting lock
        if (casterRoot != null)
        {
            // Broadcasts blindly to PlayerAbilityHolder to cancel the cast
            casterRoot.BroadcastMessage("CancelCast", SendMessageOptions.DontRequireReceiver);
            casterRoot.BroadcastMessage("InterruptCast", SendMessageOptions.DontRequireReceiver);

            // Force the animator out of the channeled pose
            if (casterRoot.Animator != null)
            {
                casterRoot.Animator.Play("Idle");
            }
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Clean up Impact VFX
        if (activeEndVFX != null)
        {
            VFXGraphCleaner endCleaner = activeEndVFX.GetComponent<VFXGraphCleaner>();
            if (endCleaner != null) endCleaner.StopAndFade();
            else Destroy(activeEndVFX);
        }

        // Clean up Main Beam VFX
        if (activeBeamInstance != null)
        {
            VFXGraphCleaner beamCleaner = activeBeamInstance.GetComponent<VFXGraphCleaner>();
            if (beamCleaner != null) beamCleaner.StopAndFade();
            else Destroy(activeBeamInstance);
        }
    }

    void Update()
    {
        if (caster == null || casterHealth == null || casterHealth.currentHealth <= 0 || sourceAbility == null || (beamTarget != null && (targetHealth == null || targetHealth.currentHealth <= 0)))
        {
            Interrupt();
            return;
        }

        // --- 2. PLAYER INTENT CANCELLATION ---
        if (Time.time > channelStartTime + 0.25f)
        {
            // FIX: Compare against casterRoot.gameObject, NOT the visual model!
            if (casterRoot != null && PartyManager.instance != null && PartyManager.instance.ActivePlayer == casterRoot.gameObject)
            {
                bool clicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                bool movedWASD = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;

                if (clicked || movedWASD)
                {
                    Interrupt();
                    return;
                }
            }

            // Cancel if physically displaced (knockback, sliding, etc)
            if (Vector3.Distance(caster.transform.position, initialCasterPosition) > 0.5f)
            {
                Interrupt();
                return;
            }
        }
        // -----------------------------------------

        if (beamTarget != null)
        {
            float distanceToTarget = Vector3.Distance(caster.transform.position, beamTarget.position);
            if (distanceToTarget > sourceAbility.range)
            {
                Interrupt();
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
                    Interrupt();
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

        if (emissionPoint != null) currentStartPos = emissionPoint.position;
        else currentStartPos = caster.transform.position + Vector3.up;

        Vector3 fullTargetPosition;
        if (beamTarget != null) fullTargetPosition = beamTarget.position + Vector3.up;
        else fullTargetPosition = currentStartPos + (caster.transform.forward * sourceAbility.range);

        Vector3 direction = (fullTargetPosition - currentStartPos).normalized;
        float totalDistance = Vector3.Distance(currentStartPos, fullTargetPosition);

        float renderDistance = Mathf.Min(currentBeamLength, totalDistance);

        // Raycast to stop the beam if it hits a wall
        if (Physics.Raycast(currentStartPos, direction, out RaycastHit hit, renderDistance, obstacleLayers))
        {
            renderDistance = hit.distance;
        }

        currentEndPos = currentStartPos + (direction * renderDistance);

        // --- UPDATE THE MAIN BEAM VFX ---
        if (activeBeamInstance != null && renderDistance > 0.1f)
        {
            // Position the root of the beam at the caster's hand and aim it at the target
            activeBeamInstance.transform.position = currentStartPos;
            activeBeamInstance.transform.rotation = Quaternion.LookRotation(direction);

            // Scale or Set Property based on configuration
            if (scaleTransformZ)
            {
                Vector3 currentScale = activeBeamInstance.transform.localScale;
                activeBeamInstance.transform.localScale = new Vector3(currentScale.x, currentScale.y, renderDistance);
            }
            else if (activeBeamVFX != null && activeBeamVFX.HasFloat(vfxLengthProperty))
            {
                activeBeamVFX.SetFloat(vfxLengthProperty, renderDistance);
            }
        }

        // Position the Impact VFX
        if (activeEndVFX != null)
        {
            activeEndVFX.transform.position = currentEndPos;
            if (direction != Vector3.zero) activeEndVFX.transform.rotation = Quaternion.LookRotation(-direction);
        }
    }

    private void ApplyEffects()
    {
        targetsHitThisTick.Clear();
        Vector3 castDirection = (currentEndPos - currentStartPos).normalized;
        float castDistance = Vector3.Distance(currentStartPos, currentEndPos);

        if (castDistance < 0.1f) return;

        float radius = Mathf.Max(0.1f, beamHitboxRadius * 0.5f);

        int hitCount = Physics.SphereCastNonAlloc(currentStartPos, radius, castDirection, _beamHitBuffer, castDistance);

        for (int i = 0; i < hitCount; i++)
        {
            var hit = _beamHitBuffer[i];

            if (hit.transform.root == caster.transform.root) continue;

            Health tHealth = hit.collider.GetComponentInChildren<Health>();
            if (tHealth != null && !targetsHitThisTick.ContainsKey(tHealth))
            {
                targetsHitThisTick.Add(tHealth, true);

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