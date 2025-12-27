using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 20f;
    public float lifetime = 5f;

    [Header("Visuals")]
    [Tooltip("If true, simulates particle systems forward by 'preWarmSeconds' on spawn. Useful for fireballs that need to look 'full' instantly.")]
    public bool preWarmVFX = true;
    public float preWarmSeconds = 0.2f;

    // Data is now private
    private Ability sourceAbility;
    private GameObject caster;
    private int casterLayer;

    private float lifetimeTimer;
    private PooledObject pooledObject; // Reference to our helper script

    void Awake()
    {
        // GetComponent<PooledObject>() has been REMOVED from Awake() to avoid race conditions with Pooler
    }

    void OnEnable()
    {
        // Get the component here instead. This runs after the pooler has added it.
        if (pooledObject == null)
        {
            pooledObject = GetComponent<PooledObject>();
        }
    }

    /// <summary>
    /// Initializes the projectile with fresh data every time it's pulled from the pool.
    /// </summary>
    public void Initialize(Ability ability, GameObject caster, int layer)
    {
        this.sourceAbility = ability;
        this.caster = caster;
        this.casterLayer = layer;
        this.lifetimeTimer = lifetime;

        // --- FIX: Jump-start the VFX so it looks fully formed immediately ---
        if (preWarmVFX)
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                // Reset first to clear any old state from the pool
                ps.Clear();
                ps.Play();

                // Fast-forward the simulation
                ps.Simulate(preWarmSeconds, true, false);
            }
        }
        // -------------------------------------------------------------------
    }

    void Update()
    {
        // Manual lifetime countdown
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            if (pooledObject != null) pooledObject.ReturnToPool();
            else Destroy(gameObject); // Fallback if not pooled
            return;
        }

        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        // 1. Hit Wall / Environment (No CharacterRoot found)
        if (hitCharacterRoot == null)
        {
            if (sourceAbility != null && sourceAbility.hitVFX != null)
            {
                ObjectPooler.instance.Get(sourceAbility.hitVFX, transform.position, Quaternion.identity);
            }
            if (pooledObject != null) pooledObject.ReturnToPool();
            else Destroy(gameObject);
            return;
        }

        // 2. Hit Caster (Always Ignore)
        if (caster != null && hitCharacterRoot == caster.GetComponentInParent<CharacterRoot>())
        {
            return;
        }

        if (sourceAbility != null)
        {
            // Safety check: if caster is missing, just destroy
            if (caster == null)
            {
                if (pooledObject != null) pooledObject.ReturnToPool();
                else Destroy(gameObject);
                return;
            }

            int targetLayer = hitCharacterRoot.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;

            // Pass through allies if there are no friendly effects
            if (isAlly && (sourceAbility.friendlyEffects == null || sourceAbility.friendlyEffects.Count == 0))
            {
                return; // Return implies "Keep flying, do not destroy"
            }

            // Hit Logic
            if (sourceAbility.hitVFX != null)
            {
                ObjectPooler.instance.Get(sourceAbility.hitVFX, transform.position, Quaternion.identity);
            }

            var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

            foreach (var effect in effectsToApply)
            {
                if (effect == null) continue;
                effect.Apply(caster, hitCharacterRoot.gameObject);
            }
        }

        // 3. Destroy Projectile
        if (pooledObject != null) pooledObject.ReturnToPool();
        else Destroy(gameObject);
    }
}