using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Projectile : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 20f;
    public float lifetime = 5f;

    // Data is now private
    private Ability sourceAbility;
    private GameObject caster;
    private int casterLayer;

    private float lifetimeTimer;
    private PooledObject pooledObject; // Reference to our helper script

    void Awake()
    {
        // GetComponent<PooledObject>() has been REMOVED from Awake()
    }

    // --- FIX: Added OnEnable() ---
    void OnEnable()
    {
        // Get the component here instead. This runs after the pooler has added it.
        if (pooledObject == null)
        {
            pooledObject = GetComponent<PooledObject>();
        }
    }
    // --- END FIX ---

    /// <summary>
    /// Initializes the projectile with fresh data every time it's pulled from the pool.
    /// </summary>
    public void Initialize(Ability ability, GameObject caster, int layer)
    {
        this.sourceAbility = ability;
        this.caster = caster;
        this.casterLayer = layer;
        this.lifetimeTimer = lifetime;
    }

    void Update()
    {
        // Manual lifetime countdown
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            // This check will now succeed
            if (pooledObject != null) pooledObject.ReturnToPool();
            return;
        }

        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        CharacterRoot hitCharacterRoot = other.GetComponentInParent<CharacterRoot>();

        if (hitCharacterRoot == null)
        {
            if (sourceAbility != null && sourceAbility.hitVFX != null)
            {
                ObjectPooler.instance.Get(sourceAbility.hitVFX, transform.position, Quaternion.identity);
            }
            if (pooledObject != null) pooledObject.ReturnToPool();
            return;
        }

        if (caster != null && hitCharacterRoot == caster.GetComponentInParent<CharacterRoot>())
        {
            return;
        }

        if (sourceAbility != null)
        {
            if (caster == null)
            {
                if (pooledObject != null) pooledObject.ReturnToPool();
                return;
            }

            if (sourceAbility.hitVFX != null)
            {
                ObjectPooler.instance.Get(sourceAbility.hitVFX, transform.position, Quaternion.identity);
            }

            int targetLayer = hitCharacterRoot.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;
            var effectsToApply = isAlly ? sourceAbility.friendlyEffects : sourceAbility.hostileEffects;

            foreach (var effect in effectsToApply)
            {
                if (effect == null) continue;
                effect.Apply(caster, hitCharacterRoot.gameObject);
            }
        }

        // This check will also succeed
        if (pooledObject != null) pooledObject.ReturnToPool();
    }
}