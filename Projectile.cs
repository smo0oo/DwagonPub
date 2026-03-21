using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Debug")]
    public bool debugMode = false;

    [Header("Settings")]
    public float speed = 20f;
    public float lifetime = 5f;

    [Tooltip("Layers that trigger the projectile's impact.")]
    public LayerMask collisionLayers = -1;

    [Tooltip("Layers to apply damage/effects to when exploding.")]
    public LayerMask damageLayers = -1;

    [Header("Grenade Settings (AAA)")]
    [Tooltip("If the Ability Type is 'Grenade', how high should the parabolic arc be?")]
    public float grenadeArcHeight = 3f;

    [Header("Visuals")]
    public bool preWarmVFX = true;
    public float preWarmSeconds = 0.2f;

    [Tooltip("If true, the impact VFX ignores the projectile's rotation and spawns flat (identity rotation). Essential for arc grenades!")]
    public bool decoupleImpactRotation = false;

    [HideInInspector]
    public GameObject vfxOverride = null;

    private Ability sourceAbility;
    private GameObject caster;
    private int casterLayer;
    private float lifetimeTimer;
    private PooledObject pooledObject;
    private int activationTriggerLayer = -1;

    private HashSet<CharacterRoot> hitMemory = new HashSet<CharacterRoot>();

    // --- Grenade State Data ---
    private bool isGrenade = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float flightDuration;
    private float flightTimePassed;

    void Awake()
    {
        activationTriggerLayer = LayerMask.NameToLayer("ActivationTrigger");
    }

    void OnEnable()
    {
        if (pooledObject == null) pooledObject = GetComponent<PooledObject>();
    }

    public void Initialize(Ability ability, GameObject caster, int layer, Vector3 targetPos = default)
    {
        this.sourceAbility = ability;
        this.caster = caster;
        this.casterLayer = layer;
        this.lifetimeTimer = lifetime;
        this.targetPosition = targetPos;
        this.startPosition = transform.position;

        hitMemory.Clear();

        if (this.sourceAbility != null && this.sourceAbility.abilityType == AbilityType.Grenade)
        {
            isGrenade = true;
            float distance = Vector2.Distance(new Vector2(startPosition.x, startPosition.z), new Vector2(targetPosition.x, targetPosition.z));
            flightDuration = distance > 0.1f ? distance / speed : 0.1f;
            flightTimePassed = 0f;
        }
        else
        {
            isGrenade = false;
        }

        if (this.caster != null && this.sourceAbility != null)
        {
            bool hasHostileEffects = this.sourceAbility.hostileEffects != null && this.sourceAbility.hostileEffects.Count > 0;
            bool hasFriendlyEffects = this.sourceAbility.friendlyEffects != null && this.sourceAbility.friendlyEffects.Count > 0;

            PartyMemberTargeting partyTargeting = this.caster.GetComponentInParent<PartyMemberTargeting>();
            AITargeting aiTargeting = this.caster.GetComponentInParent<AITargeting>();

            this.collisionLayers = 0;
            this.damageLayers = 0;

            if (partyTargeting != null)
            {
                this.collisionLayers |= partyTargeting.obstacleLayers;
                if (hasHostileEffects)
                {
                    this.damageLayers |= partyTargeting.enemyLayer;
                    this.collisionLayers |= partyTargeting.enemyLayer;
                }
                if (hasFriendlyEffects)
                {
                    int playerMask = 1 << LayerMask.NameToLayer("Player");
                    int friendlyMask = 1 << LayerMask.NameToLayer("Friendly");
                    int combinedFriendly = playerMask | friendlyMask;

                    this.damageLayers |= combinedFriendly;
                    this.collisionLayers |= combinedFriendly;
                }
            }
            else if (aiTargeting != null)
            {
                this.collisionLayers |= aiTargeting.obstacleLayers;
                if (hasHostileEffects)
                {
                    this.damageLayers |= aiTargeting.playerLayer;
                    this.collisionLayers |= aiTargeting.playerLayer;
                }
                if (hasFriendlyEffects)
                {
                    int enemyMask = 1 << LayerMask.NameToLayer("Enemy");
                    this.damageLayers |= enemyMask;
                    this.collisionLayers |= enemyMask;
                }
            }
        }

        if (preWarmVFX)
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles) { ps.Clear(); ps.Play(); ps.Simulate(preWarmSeconds, true, false); }
        }
    }

    void Update()
    {
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f) { Terminate(); return; }

        if (isGrenade)
        {
            flightTimePassed += Time.deltaTime;
            float progress = Mathf.Clamp01(flightTimePassed / flightDuration);

            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, progress);
            currentPos.y += Mathf.Sin(progress * Mathf.PI) * grenadeArcHeight;

            Vector3 direction = currentPos - transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            transform.position = currentPos;

            if (progress >= 1f)
            {
                ProcessImpact(null);
            }
        }
        else
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (sourceAbility == null) return;
        if (other.gameObject.layer == LayerMask.NameToLayer("ClothPhysics")) return;
        if (activationTriggerLayer != -1 && other.gameObject.layer == activationTriggerLayer) return;

        if (((1 << other.gameObject.layer) & collisionLayers) == 0) return;

        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        if (debugMode)
        {
            string rootName = hitCharacterRoot != null ? hitCharacterRoot.name : "Floor/Environment";
            Debug.Log($"[Projectile] TOUCHED: '{other.name}' | CharacterRoot: {rootName} | Layer: {other.gameObject.layer}");
        }

        if (caster != null && hitCharacterRoot != null)
        {
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();

            if (casterRoot != null && hitCharacterRoot == casterRoot)
            {
                return;
            }
        }

        ProcessImpact(hitCharacterRoot);
    }

    private void ProcessImpact(CharacterRoot hitCharacterRoot)
    {
        float effectiveRadius = GetDynamicExplosionRadius();

        if (hitCharacterRoot != null)
        {
            if (hitMemory.Contains(hitCharacterRoot)) return;
            hitMemory.Add(hitCharacterRoot);

            if (effectiveRadius > 0f)
            {
                Explode(effectiveRadius);
            }
            else
            {
                SpawnImpactVFX();
                if (((1 << hitCharacterRoot.gameObject.layer) & damageLayers) != 0)
                {
                    ApplyEffectsToTarget(hitCharacterRoot);
                }
            }

            if (sourceAbility != null && !sourceAbility.piercesEnemies)
            {
                Terminate();
            }
        }
        else
        {
            if (effectiveRadius > 0f) Explode(effectiveRadius);
            else SpawnImpactVFX();

            Terminate();
        }
    }

    private float GetDynamicExplosionRadius()
    {
        if (sourceAbility == null) return 0f;
        if (sourceAbility.aoeRadius > 0f) return sourceAbility.aoeRadius;

        float maxSplash = 0f;

        if (sourceAbility.hostileEffects != null)
        {
            foreach (var effect in sourceAbility.hostileEffects)
            {
                if (effect is DamageEffect dmg && dmg.isSplash) maxSplash = Mathf.Max(maxSplash, dmg.splashRadius);
            }
        }

        if (sourceAbility.friendlyEffects != null)
        {
            foreach (var effect in sourceAbility.friendlyEffects)
            {
                if (effect is DamageEffect dmg && dmg.isSplash) maxSplash = Mathf.Max(maxSplash, dmg.splashRadius);
            }
        }

        return maxSplash;
    }

    private void Explode(float radius)
    {
        SpawnImpactVFX();

        // --- AAA FIX: Use the new router for impact explosions! ---
        if (sourceAbility != null && sourceAbility.impactSound != null)
            SFXManager.PlayAtPoint(sourceAbility.impactSound, transform.position);

        Collider[] hits = Physics.OverlapSphere(transform.position, radius, damageLayers);
        HashSet<CharacterRoot> affectedTargets = new HashSet<CharacterRoot>();

        foreach (var hit in hits)
        {
            if (hit.gameObject.layer == LayerMask.NameToLayer("ClothPhysics")) continue;
            if (activationTriggerLayer != -1 && hit.gameObject.layer == activationTriggerLayer) continue;

            CharacterRoot target = hit.GetComponentInParent<CharacterRoot>();
            if (target == null || affectedTargets.Contains(target)) continue;

            affectedTargets.Add(target);
            ApplyEffectsToTarget(target);
        }
    }

    private void ApplyEffectsToTarget(CharacterRoot target)
    {
        if (sourceAbility == null) return;

        bool isAlly = false;
        if (casterLayer == LayerMask.NameToLayer("Player") || casterLayer == LayerMask.NameToLayer("Friendly"))
        {
            isAlly = (target.gameObject.layer == LayerMask.NameToLayer("Player") || target.gameObject.layer == LayerMask.NameToLayer("Friendly"));
        }
        else
        {
            isAlly = (target.gameObject.layer == casterLayer);
        }

        var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

        if (effectsToApply != null)
        {
            foreach (var effect in effectsToApply)
            {
                if (effect != null) effect.Apply(caster, target.gameObject);
            }
        }
    }

    private void SpawnImpactVFX()
    {
        GameObject vfxToUse = vfxOverride != null ? vfxOverride : (sourceAbility != null ? sourceAbility.hitVFX : null);
        if (vfxToUse == null) return;

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = Quaternion.identity;

        if (sourceAbility != null)
        {
            if (decoupleImpactRotation)
            {
                spawnPos += sourceAbility.hitVFXPositionOffset;
                spawnRot = Quaternion.Euler(sourceAbility.hitVFXRotationOffset);
            }
            else
            {
                spawnPos += (transform.rotation * sourceAbility.hitVFXPositionOffset);
                spawnRot = transform.rotation * Quaternion.Euler(sourceAbility.hitVFXRotationOffset);
            }
        }

        GameObject vfx = ObjectPooler.instance.Get(vfxToUse, spawnPos, spawnRot);
        if (vfx != null) vfx.SetActive(true);
    }

    private void Terminate()
    {
        if (pooledObject != null) pooledObject.ReturnToPool();
        else Destroy(gameObject);
    }
}