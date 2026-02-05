using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 20f;
    public float lifetime = 5f;

    [Tooltip("Layers that trigger the projectile's impact. Set this to include Terrain, Enemies, etc.")]
    public LayerMask collisionLayers = -1; // Default to Everything

    [Tooltip("Layers to apply damage/effects to when exploding (if AOE). Defaults to Everything, but filtered by CharacterRoot.")]
    public LayerMask damageLayers = -1;

    [Header("Visuals")]
    [Tooltip("If true, simulates particle systems forward by 'preWarmSeconds' on spawn. Useful for fireballs that need to look 'full' instantly.")]
    public bool preWarmVFX = true;
    public float preWarmSeconds = 0.2f;

    // Data is now private
    private Ability sourceAbility;
    private GameObject caster;
    private int casterLayer;

    private float lifetimeTimer;
    private PooledObject pooledObject;

    void Awake()
    {
        // GetComponent<PooledObject>() removed from Awake to avoid race conditions
    }

    void OnEnable()
    {
        if (pooledObject == null)
        {
            pooledObject = GetComponent<PooledObject>();
        }
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
            foreach (var ps in particles)
            {
                ps.Clear();
                ps.Play();
                ps.Simulate(preWarmSeconds, true, false);
            }
        }
    }

    void Update()
    {
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            Terminate();
            return;
        }

        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 1. Layer Mask Check (What stops the projectile?)
        if (((1 << other.gameObject.layer) & collisionLayers) == 0)
        {
            return;
        }

        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        // 2. Hit Caster (Always Ignore Self)
        if (caster != null)
        {
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
            if (casterRoot != null && hitCharacterRoot == casterRoot) return;
        }

        // 3. Ally Pass-Through Check
        // If we hit a character, check if it's an ally and if we have effects for them.
        // If not (e.g. shooting a fireball through a friend), ignore the collision.
        if (hitCharacterRoot != null && sourceAbility != null)
        {
            int targetLayer = hitCharacterRoot.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;

            if (isAlly && (sourceAbility.friendlyEffects == null || sourceAbility.friendlyEffects.Count == 0))
            {
                return;
            }
        }

        // 4. AOE Explosion Logic
        // If ability has a radius, hitting ANYTHING (Ground, Wall, Enemy) triggers the explosion.
        if (sourceAbility != null && sourceAbility.aoeRadius > 0)
        {
            Explode();
            Terminate();
            return;
        }

        // 5. Single Target Logic (No AOE)
        SpawnImpactVFX();

        // If we hit a character directly, apply effects only to them
        if (hitCharacterRoot != null)
        {
            ApplyEffectsToTarget(hitCharacterRoot);
        }

        Terminate();
    }

    private void Explode()
    {
        // 1. VFX & Sound
        SpawnImpactVFX();

        if (sourceAbility != null && sourceAbility.impactSound != null)
        {
            AudioSource.PlayClipAtPoint(sourceAbility.impactSound, transform.position);
        }

        // 2. Find Targets
        // Use transform.position as the center of the explosion
        Collider[] hits = Physics.OverlapSphere(transform.position, sourceAbility.aoeRadius, damageLayers);
        HashSet<CharacterRoot> affectedTargets = new HashSet<CharacterRoot>();

        foreach (var hit in hits)
        {
            CharacterRoot target = hit.GetComponentInParent<CharacterRoot>();

            // Skip non-characters or targets we've already hit (to avoid double damage on multiple colliders)
            if (target == null || affectedTargets.Contains(target)) continue;

            affectedTargets.Add(target);
            ApplyEffectsToTarget(target);
        }
    }

    private void ApplyEffectsToTarget(CharacterRoot target)
    {
        if (caster == null || sourceAbility == null) return;

        int targetLayer = target.gameObject.layer;
        bool isAlly = casterLayer == targetLayer;

        var effects = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;
        if (effects == null) return;

        foreach (var effect in effects)
        {
            if (effect != null) effect.Apply(caster, target.gameObject);
        }
    }

    private void SpawnImpactVFX()
    {
        if (sourceAbility == null || sourceAbility.hitVFX == null) return;

        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = Quaternion.identity;

        // Apply Offsets relative to projectile orientation
        spawnRot = transform.rotation * Quaternion.Euler(sourceAbility.hitVFXRotationOffset);
        spawnPos = transform.position + (transform.rotation * sourceAbility.hitVFXPositionOffset);

        GameObject vfx = ObjectPooler.instance.Get(sourceAbility.hitVFX, spawnPos, spawnRot);
        if (vfx != null) vfx.SetActive(true);
    }

    private void Terminate()
    {
        if (pooledObject != null) pooledObject.ReturnToPool();
        else Destroy(gameObject);
    }
}