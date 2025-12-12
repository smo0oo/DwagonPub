using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ChanneledBeamController : MonoBehaviour
{
    [Header("Beam Settings")]
    public LayerMask groundLayer;

    public Ability sourceAbility;
    public GameObject caster;

    private LineRenderer lineRenderer;
    private PlayerStats casterStats;
    private Health casterHealth;

    private Transform beamTarget;
    private Health targetHealth;

    private float tickTimer;
    private Dictionary<Health, bool> targetsHitThisTick = new Dictionary<Health, bool>();

    // --- NEW: Buffer for Non-Allocating Physics ---
    private RaycastHit[] _beamHitBuffer = new RaycastHit[25];

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
    }

    public void Initialize(Ability ability, GameObject casterObject, GameObject targetObject)
    {
        sourceAbility = ability;
        caster = casterObject;
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
    }

    public void Interrupt()
    {
        Destroy(gameObject);
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
        Vector3 worldStartPosition = casterTransform.position;
        Vector3 worldEndPosition;
        if (beamTarget != null)
        {
            worldEndPosition = beamTarget.position;
        }
        else
        {
            worldEndPosition = worldStartPosition + (casterTransform.forward * sourceAbility.range);
        }
        lineRenderer.SetPosition(0, worldStartPosition);
        lineRenderer.SetPosition(1, worldEndPosition);
    }

    private void ApplyEffects()
    {
        targetsHitThisTick.Clear();
        Vector3 castStart = lineRenderer.GetPosition(0);
        Vector3 castEnd = lineRenderer.GetPosition(1);
        Vector3 castDirection = (castEnd - castStart).normalized;
        float castDistance = Vector3.Distance(castStart, castEnd);

        // --- MODIFIED: Use Non-Allocating version ---
        int hitCount = Physics.SphereCastNonAlloc(castStart, 0.5f, castDirection, _beamHitBuffer, castDistance);

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
                int casterLayer = caster.GetComponentInParent<CharacterRoot>().gameObject.layer;
                int targetLayer = hit.collider.GetComponentInParent<CharacterRoot>().gameObject.layer;
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