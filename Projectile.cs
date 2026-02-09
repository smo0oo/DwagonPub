using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Debug")]
    public bool debugMode = true;

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
        // 1. Explicitly ignore ActivationTrigger (Layer 21)
        if (other.gameObject.layer == 21) return;

        // 2. Layer Mask Check
        if (((1 << other.gameObject.layer) & collisionLayers) == 0) return;

        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        if (debugMode)
        {
            string rootName = hitCharacterRoot != null ? hitCharacterRoot.name : "None";
            Debug.Log($"[Projectile] TOUCHED: '{other.name}' | CharacterRoot: {rootName} | Layer: {other.gameObject.layer}");
        }

        // 3. Ignore Caster
        if (caster != null)
        {
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
            if (casterRoot != null && hitCharacterRoot == casterRoot) return;
        }

        // 4. Ally Pass-Through
        if (hitCharacterRoot != null && sourceAbility != null)
        {
            if (casterLayer == hitCharacterRoot.gameObject.layer && (sourceAbility.friendlyEffects == null || sourceAbility.friendlyEffects.Count == 0))
            {
                if (debugMode) Debug.Log($"[Projectile] IGNORED: Ally '{hitCharacterRoot.name}' (No Friendly Effects).");
                return;
            }
        }

        // 5. Explosion
        if (sourceAbility != null && sourceAbility.aoeRadius > 0)
        {
            Explode();
            Terminate();
            return;
        }

        // 6. Single Target
        SpawnImpactVFX();
        if (hitCharacterRoot != null) ApplyEffectsToTarget(hitCharacterRoot);
        Terminate();
    }

    private void Explode()
    {
        SpawnImpactVFX();
        if (sourceAbility != null && sourceAbility.impactSound != null)
            AudioSource.PlayClipAtPoint(sourceAbility.impactSound, transform.position);

        Collider[] hits = Physics.OverlapSphere(transform.position, sourceAbility.aoeRadius, damageLayers);
        HashSet<CharacterRoot> affectedTargets = new HashSet<CharacterRoot>();

        foreach (var hit in hits)
        {
            if (hit.gameObject.layer == 21) continue;

            CharacterRoot target = hit.GetComponentInParent<CharacterRoot>();
            if (target == null || affectedTargets.Contains(target)) continue;

            affectedTargets.Add(target);
            ApplyEffectsToTarget(target);
        }
    }

    private void ApplyEffectsToTarget(CharacterRoot target)
    {
        if (caster == null || sourceAbility == null) return;
        bool isAlly = casterLayer == target.gameObject.layer;
        var effects = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;
        if (effects == null) return;
        foreach (var effect in effects) if (effect != null) effect.Apply(caster, target.gameObject);
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