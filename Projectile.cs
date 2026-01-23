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
    private PooledObject pooledObject;

    void Awake()
    {
        // GetComponent<PooledObject>() has been REMOVED from Awake() to avoid race conditions with Pooler
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
            if (pooledObject != null) pooledObject.ReturnToPool();
            else Destroy(gameObject);
            return;
        }

        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        // 1. Hit Wall / Environment
        if (hitCharacterRoot == null)
        {
            if (sourceAbility != null && sourceAbility.hitVFX != null)
            {
                // AAA FIX: Capture and Activate
                GameObject vfx = ObjectPooler.instance.Get(sourceAbility.hitVFX, transform.position, Quaternion.identity);
                if (vfx != null) vfx.SetActive(true);
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
            if (caster == null)
            {
                if (pooledObject != null) pooledObject.ReturnToPool();
                else Destroy(gameObject);
                return;
            }

            int targetLayer = hitCharacterRoot.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;

            if (isAlly && (sourceAbility.friendlyEffects == null || sourceAbility.friendlyEffects.Count == 0))
            {
                return;
            }

            // Hit Logic
            if (sourceAbility.hitVFX != null)
            {
                // AAA FIX: Capture and Activate
                GameObject vfx = ObjectPooler.instance.Get(sourceAbility.hitVFX, transform.position, Quaternion.identity);
                if (vfx != null) vfx.SetActive(true);
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