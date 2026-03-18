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

    [Header("Visuals")]
    public bool preWarmVFX = true;
    public float preWarmSeconds = 0.2f;

    [HideInInspector]
    public GameObject vfxOverride = null;

    private Ability sourceAbility;
    private GameObject caster;
    private int casterLayer;
    private float lifetimeTimer;
    private PooledObject pooledObject;
    private int activationTriggerLayer = -1;

    // --- AAA FIX: Memory array to prevent multi-hitting the same target during Pierce ---
    private HashSet<CharacterRoot> hitMemory = new HashSet<CharacterRoot>();

    void Awake()
    {
        activationTriggerLayer = LayerMask.NameToLayer("ActivationTrigger");
    }

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

        // Reset the memory array when the projectile is pulled from the Object Pool!
        hitMemory.Clear();

        if (this.caster != null)
        {
            PartyMemberTargeting partyTargeting = this.caster.GetComponentInParent<PartyMemberTargeting>();
            if (partyTargeting != null)
            {
                this.damageLayers |= partyTargeting.enemyLayer;
                this.collisionLayers |= partyTargeting.enemyLayer;
                this.collisionLayers |= partyTargeting.obstacleLayers;
            }
            else
            {
                AITargeting aiTargeting = this.caster.GetComponentInParent<AITargeting>();
                if (aiTargeting != null)
                {
                    this.damageLayers |= aiTargeting.playerLayer;
                    this.collisionLayers |= aiTargeting.playerLayer;
                    this.collisionLayers |= aiTargeting.obstacleLayers;
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
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (sourceAbility == null) return;
        if (other.gameObject.layer == LayerMask.NameToLayer("ClothPhysics")) return;

        if (activationTriggerLayer != -1 && other.gameObject.layer == activationTriggerLayer) return;

        // If the layer isn't even in our collision mask, ignore it completely
        if (((1 << other.gameObject.layer) & collisionLayers) == 0) return;

        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        if (debugMode)
        {
            string rootName = hitCharacterRoot != null ? hitCharacterRoot.name : "Floor/Environment";
            Debug.Log($"[Projectile] TOUCHED: '{other.name}' | CharacterRoot: {rootName} | Layer: {other.gameObject.layer}");
        }

        if (caster != null)
        {
            CharacterRoot casterRoot = caster.GetComponentInParent<CharacterRoot>();
            if (casterRoot != null && hitCharacterRoot == casterRoot) return;
        }

        if (hitCharacterRoot != null && sourceAbility != null)
        {
            if (casterLayer == hitCharacterRoot.gameObject.layer && (sourceAbility.friendlyEffects == null || sourceAbility.friendlyEffects.Count == 0))
            {
                if (debugMode) Debug.Log($"[Projectile] IGNORED: Ally '{hitCharacterRoot.name}' (No Friendly Effects).");
                return;
            }
        }

        float effectiveRadius = GetDynamicExplosionRadius();

        // --- AAA FIX: The Pierce Logic Split ---
        if (hitCharacterRoot != null)
        {
            // IF WE HIT A VALID ENEMY

            // 1. Check if we already hit them. If yes, ignore them and keep flying!
            if (hitMemory.Contains(hitCharacterRoot)) return;
            hitMemory.Add(hitCharacterRoot);

            // 2. Deal Damage/Explode
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

            // 3. To Pierce or Not To Pierce
            if (!sourceAbility.piercesEnemies)
            {
                Terminate();
            }
        }
        else
        {
            // IF WE HIT A WALL, DITHER LAYER, OR ENVIRONMENT

            if (effectiveRadius > 0f)
            {
                Explode(effectiveRadius);
            }
            else
            {
                SpawnImpactVFX();
            }

            // A wall ALWAYS stops a projectile, even if it pierces enemies!
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
                if (effect is DamageEffect dmg && dmg.isSplash)
                {
                    maxSplash = Mathf.Max(maxSplash, dmg.splashRadius);
                }
            }
        }

        if (sourceAbility.friendlyEffects != null)
        {
            foreach (var effect in sourceAbility.friendlyEffects)
            {
                if (effect is DamageEffect dmg && dmg.isSplash)
                {
                    maxSplash = Mathf.Max(maxSplash, dmg.splashRadius);
                }
            }
        }

        return maxSplash;
    }

    private void Explode(float radius)
    {
        SpawnImpactVFX();
        if (sourceAbility != null && sourceAbility.impactSound != null)
            AudioSource.PlayClipAtPoint(sourceAbility.impactSound, transform.position);

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

        bool isAlly = casterLayer == target.gameObject.layer;
        var effects = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

        if (effects == null) return;

        foreach (var effect in effects)
        {
            if (effect != null) effect.Apply(caster, target.gameObject);
        }
    }

    private void SpawnImpactVFX()
    {
        GameObject vfxToUse = vfxOverride != null ? vfxOverride : (sourceAbility != null ? sourceAbility.hitVFX : null);
        if (vfxToUse == null) return;

        Vector3 spawnPos = transform.position + (transform.rotation * sourceAbility.hitVFXPositionOffset);
        Quaternion spawnRot = transform.rotation * Quaternion.Euler(sourceAbility.hitVFXRotationOffset);

        GameObject vfx = ObjectPooler.instance.Get(vfxToUse, spawnPos, spawnRot);
        if (vfx != null) vfx.SetActive(true);
    }

    private void Terminate()
    {
        if (pooledObject != null) pooledObject.ReturnToPool();
        else Destroy(gameObject);
    }
}