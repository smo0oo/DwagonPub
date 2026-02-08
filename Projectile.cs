using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Debug")]
    public bool debugMode = true; // Uncheck this later to stop spamming console

    [Header("Settings")]
    public float speed = 20f;
    public float lifetime = 5f;

    [Tooltip("Layers that trigger the projectile's impact.")]
    public LayerMask collisionLayers = -1;

    [Tooltip("Layers to apply damage/effects to when exploding.")]
    public LayerMask damageLayers = -1;

    [Header("Visuals")]
    public bool preWarmVFX = true;
    public float preWarmSeconds = 0.2f;

    private Ability sourceAbility;
    private GameObject caster;
    private int casterLayer;
    private float lifetimeTimer;
    private PooledObject pooledObject;

    void OnEnable()
    {
        if (pooledObject == null) pooledObject = GetComponent<PooledObject>();
    }

    public void Initialize(Ability ability, GameObject caster, int layer)
    {
        this.sourceAbility = ability;
        this.caster = caster;
        this.casterLayer = layer;
        this.lifetimeTimer = lifetime;

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
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (debugMode) Debug.Log($"[Projectile] TOUCHED: '{other.name}' | Layer: {other.gameObject.layer} | IsTrigger: {other.isTrigger} | Root: {other.transform.root.name}");

        // [CHECK 1] Activation Trigger / Aggro Range
        if (other.gameObject.layer == 21)
        {
            if (debugMode) Debug.Log($"[Projectile] IGNORED: '{other.name}' is on Layer 21 (ActivationTrigger). Flying through...");
            return;
        }

        // [CHECK 2] Collision Layer Mask
        if (((1 << other.gameObject.layer) & collisionLayers) == 0)
        {
            if (debugMode) Debug.Log($"[Projectile] IGNORED: '{other.name}' (Layer {other.gameObject.layer}) is not in the Collision Mask.");
            return;
        }

        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        // [CHECK 3] Self-Hit check
        if (caster != null)
        {
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
            if (casterRoot != null && hitCharacterRoot == casterRoot)
            {
                // Don't log self-hits, they happen constantly on spawn
                return;
            }
        }

        // [CHECK 4] Ally Pass-Through
        if (hitCharacterRoot != null && sourceAbility != null)
        {
            if (casterLayer == hitCharacterRoot.gameObject.layer && (sourceAbility.friendlyEffects == null || sourceAbility.friendlyEffects.Count == 0))
            {
                if (debugMode) Debug.Log($"[Projectile] IGNORED: '{other.name}' is an Ally and ability has no friendly effects.");
                return;
            }
        }

        // --- IMPACT ---

        // AOE Logic
        if (sourceAbility != null && sourceAbility.aoeRadius > 0)
        {
            if (debugMode) Debug.Log($"[Projectile] EXPLODING on '{other.name}' (AOE Radius: {sourceAbility.aoeRadius})");
            Explode();
            Terminate();
            return;
        }

        // Single Target Logic
        SpawnImpactVFX();
        if (hitCharacterRoot != null)
        {
            if (debugMode) Debug.Log($"[Projectile] DIRECT HIT on '{hitCharacterRoot.name}'. Applying Effects...");
            ApplyEffectsToTarget(hitCharacterRoot);
        }
        else
        {
            if (debugMode) Debug.Log($"[Projectile] HIT '{other.name}' (Non-Character). Destroying.");
        }

        Terminate();
    }

    private void Explode()
    {
        SpawnImpactVFX();
        if (sourceAbility != null && sourceAbility.impactSound != null)
            AudioSource.PlayClipAtPoint(sourceAbility.impactSound, transform.position);

        Collider[] hits = Physics.OverlapSphere(transform.position, sourceAbility.aoeRadius, damageLayers);
        HashSet<CharacterRoot> affectedTargets = new HashSet<CharacterRoot>();

        if (debugMode) Debug.Log($"[Projectile] AOE Scan found {hits.Length} colliders.");

        foreach (var hit in hits)
        {
            // Skip Aggro Triggers
            if (hit.gameObject.layer == 21) continue;

            CharacterRoot target = hit.GetComponentInParent<CharacterRoot>();
            if (target == null || affectedTargets.Contains(target)) continue;

            if (debugMode) Debug.Log($"[Projectile] AOE HIT: {target.name}");
            affectedTargets.Add(target);
            ApplyEffectsToTarget(target);
        }
    }

    private void ApplyEffectsToTarget(CharacterRoot target)
    {
        if (caster == null || sourceAbility == null) return;
        bool isAlly = casterLayer == target.gameObject.layer;
        var effects = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

        if (effects == null || effects.Count == 0)
        {
            if (debugMode) Debug.Log($"[Projectile] WARNING: No effects found to apply to {target.name} (IsAlly: {isAlly})");
            return;
        }

        foreach (var effect in effects)
        {
            if (effect != null)
            {
                // if (debugMode) Debug.Log($"[Projectile] Applying Effect: {effect.GetType().Name}");
                effect.Apply(caster, target.gameObject);
            }
        }
    }

    private void SpawnImpactVFX()
    {
        if (sourceAbility == null || sourceAbility.hitVFX == null) return;
        Vector3 spawnPos = transform.position + (transform.rotation * sourceAbility.hitVFXPositionOffset);
        Quaternion spawnRot = transform.rotation * Quaternion.Euler(sourceAbility.hitVFXRotationOffset);
        GameObject vfx = ObjectPooler.instance.Get(sourceAbility.hitVFX, spawnPos, spawnRot);
        if (vfx != null) vfx.SetActive(true);
    }

    private void Terminate()
    {
        if (pooledObject != null) pooledObject.ReturnToPool();
        else Destroy(gameObject);
    }
}