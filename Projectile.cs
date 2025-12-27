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

        // 1. Hit Wall / Environment (No CharacterRoot found)
        if (hitCharacterRoot == null)
        {
            if (sourceAbility != null && sourceAbility.hitVFX != null)
            {
                ObjectPooler.instance.Get(sourceAbility.hitVFX, transform.position, Quaternion.identity);
            }
            if (pooledObject != null) pooledObject.ReturnToPool();
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
                return;
            }

            int targetLayer = hitCharacterRoot.gameObject.layer;
            bool isAlly = casterLayer == targetLayer;

            // --- FIX START: Pass through allies if there are no friendly effects ---
            // If we hit an ally, AND this ability does nothing to allies (e.g. no Heal), ignore the collision.
            if (isAlly && (sourceAbility.friendlyEffects == null || sourceAbility.friendlyEffects.Count == 0))
            {
                return; // Return implies "Keep flying, do not destroy"
            }
            // --- FIX END ---

            // If we are here, we either hit an Enemy OR an Ally with a valid effect (like a Heal)
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
        // We only reach this line if we hit a valid target (Enemy) or an Ally we successfully buffed/healed
        if (pooledObject != null) pooledObject.ReturnToPool();
    }
}