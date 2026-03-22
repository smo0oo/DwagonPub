using UnityEngine;
using System.Collections.Generic;
using UnityEngine.VFX;

public class ChanneledBeamController : MonoBehaviour
{
    [Header("Beam Settings")]
    public LayerMask obstacleLayers;

    [Header("Visuals - Main Beam")]
    public GameObject beamVFXPrefab;
    public string vfxLengthProperty = "BeamLength";
    public bool scaleTransformZ = false;

    [Header("Visuals - Impact")]
    public GameObject endVFXPrefab;

    [Header("Dimensions & Speed")]
    public float beamHitboxRadius = 1.0f;
    public float beamGrowthSpeed = 40f;

    public Ability sourceAbility;
    public GameObject caster;

    private CharacterRoot casterRoot;
    private PlayerStats casterStats;
    private Health casterHealth;

    private Transform beamTarget;
    private Health targetHealth;
    private Transform emissionPoint;

    private GameObject activeBeamInstance;
    private VisualEffect activeBeamVFX;
    private GameObject activeEndVFX;
    private AudioSource loopingAudioSource;

    private float tickTimer;
    private Dictionary<Health, bool> targetsHitThisTick = new Dictionary<Health, bool>();
    private RaycastHit[] _beamHitBuffer = new RaycastHit[25];

    private float currentBeamLength = 0f;
    private Vector3 currentStartPos;
    private Vector3 currentEndPos;

    private float channelStartTime;
    private Vector3 initialCasterPosition;
    private bool isInterrupting = false;

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
            casterRoot = caster.GetComponentInParent<CharacterRoot>();
            if (casterRoot != null)
            {
                casterStats = casterRoot.GetComponentInChildren<PlayerStats>(true);
                casterHealth = casterRoot.GetComponentInChildren<Health>(true);
            }
        }

        if (beamVFXPrefab != null)
        {
            activeBeamInstance = Instantiate(beamVFXPrefab, transform.position, Quaternion.identity);
            activeBeamVFX = activeBeamInstance.GetComponent<VisualEffect>();
        }

        if (endVFXPrefab != null) activeEndVFX = Instantiate(endVFXPrefab, transform.position, Quaternion.identity);

        if (sourceAbility != null && sourceAbility.castSound != null)
        {
            loopingAudioSource = gameObject.AddComponent<AudioSource>();
            loopingAudioSource.clip = sourceAbility.castSound;
            loopingAudioSource.loop = true;
            loopingAudioSource.spatialBlend = 1.0f;
            loopingAudioSource.rolloffMode = AudioRolloffMode.Linear;

            if (SFXManager.instance != null)
            {
                loopingAudioSource.minDistance = SFXManager.instance.globalMinDistance;
                loopingAudioSource.maxDistance = SFXManager.instance.globalMaxDistance;
                loopingAudioSource.outputAudioMixerGroup = SFXManager.instance.sfxMixerGroup;
            }
            else
            {
                loopingAudioSource.minDistance = 5f;
                loopingAudioSource.maxDistance = 40f;
            }

            loopingAudioSource.Play();
        }

        UpdateBeamPosition();
    }

    public void Interrupt()
    {
        if (isInterrupting) return;
        isInterrupting = true;

        if (casterRoot != null)
        {
            casterRoot.BroadcastMessage("CancelCast", SendMessageOptions.DontRequireReceiver);
        }

        Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (loopingAudioSource != null) loopingAudioSource.Stop();

        if (activeEndVFX != null)
        {
            VFXGraphCleaner endCleaner = activeEndVFX.GetComponent<VFXGraphCleaner>();
            if (endCleaner != null) endCleaner.StopAndFade();
            else Destroy(activeEndVFX);
        }

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

        if (Time.time > channelStartTime + 0.25f)
        {
            if (casterRoot != null && PartyManager.instance != null && PartyManager.instance.ActivePlayer == casterRoot.gameObject)
            {
                bool clicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
                bool movedWASD = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
                if (clicked || movedWASD) { Interrupt(); return; }
            }

            if (Vector3.Distance(caster.transform.position, initialCasterPosition) > 0.5f) { Interrupt(); return; }
        }

        if (beamTarget != null)
        {
            float distanceToTarget = Vector3.Distance(caster.transform.position, beamTarget.position);
            if (distanceToTarget > sourceAbility.range) { Interrupt(); return; }
        }

        if (beamGrowthSpeed > 0) currentBeamLength += beamGrowthSpeed * Time.deltaTime;
        else currentBeamLength = float.MaxValue;

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = sourceAbility.tickRate;
            if (casterStats != null)
            {
                if (casterStats.currentMana < sourceAbility.manaDrain) { Interrupt(); return; }
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

        // --- THE FIX: The beam rigidly inherits the exact rotation of the caster's body! ---
        Vector3 direction = caster.transform.forward;

        Vector3 fullTargetPosition = currentStartPos + (direction * sourceAbility.range);

        float totalDistance = Vector3.Distance(currentStartPos, fullTargetPosition);
        float renderDistance = Mathf.Min(currentBeamLength, totalDistance);

        if (Physics.Raycast(currentStartPos, direction, out RaycastHit hit, renderDistance, obstacleLayers))
        {
            renderDistance = hit.distance;
        }

        currentEndPos = currentStartPos + (direction * renderDistance);

        if (activeBeamInstance != null && renderDistance > 0.1f)
        {
            activeBeamInstance.transform.position = currentStartPos;
            activeBeamInstance.transform.rotation = Quaternion.LookRotation(direction);

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
                    bool isAlly = casterRoot.gameObject.layer == targetRoot.gameObject.layer;
                    var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;
                    foreach (var effect in effectsToApply) effect.Apply(caster, hit.collider.gameObject);
                }
            }
        }
    }
}