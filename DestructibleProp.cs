using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(LootGenerator))]
public class DestructibleProp : MonoBehaviour
{
    [Header("Destruction Settings")]
    [Tooltip("The prefab to instantiate when destroyed (e.g., fractured barrel pieces).")]
    public GameObject fracturedPrefab;
    [Tooltip("Force to apply to the fractured pieces to simulate impact.")]
    public float explosionForce = 5f;
    [Tooltip("Radius of the explosion force applied to pieces.")]
    public float explosionRadius = 2f;

    [Header("Audio")]
    public AudioClip breakSound;
    [Range(0f, 1f)] public float soundVolume = 1f;

    [Header("Hit Feedback")]
    [Tooltip("How much the object shakes/wobbles when hit.")]
    public float shakeIntensity = 0.15f;
    public float shakeDuration = 0.2f;

    // Internal State
    private Health health;
    private Collider propCollider;
    private Renderer[] propRenderers;
    private Vector3 originalPosition;
    private float shakeTimer = 0f;
    private int lastHealth;

    void Awake()
    {
        health = GetComponent<Health>();
        propCollider = GetComponent<Collider>();
        propRenderers = GetComponentsInChildren<Renderer>();
        originalPosition = transform.localPosition;
        lastHealth = health.maxHealth;
    }

    void OnEnable()
    {
        health.OnDeath += HandleDeath;
        health.OnHealthChanged += HandleHealthChanged;
    }

    void OnDisable()
    {
        health.OnDeath -= HandleDeath;
        health.OnHealthChanged -= HandleHealthChanged;
    }

    void Update()
    {
        // AAA Polish: Procedural shake animation when hit
        if (shakeTimer > 0)
        {
            transform.localPosition = originalPosition + Random.insideUnitSphere * shakeIntensity;
            shakeTimer -= Time.deltaTime;

            // Reset to exact position when shake ends
            if (shakeTimer <= 0) transform.localPosition = originalPosition;
        }
    }

    private void HandleHealthChanged()
    {
        // If health decreased, we took damage -> Trigger Shake
        if (health.currentHealth < lastHealth)
        {
            shakeTimer = shakeDuration;
        }
        lastHealth = health.currentHealth;
    }

    private void HandleDeath()
    {
        // 1. Play Sound
        if (breakSound != null)
        {
            AudioSource.PlayClipAtPoint(breakSound, transform.position, soundVolume);
        }

        // 2. Spawn Fractured Version (The Debris)
        if (fracturedPrefab != null)
        {
            GameObject debris = Instantiate(fracturedPrefab, transform.position, transform.rotation);

            // Apply physics explosion to the debris pieces for "Impact" feel
            Rigidbody[] rbs = debris.GetComponentsInChildren<Rigidbody>();
            foreach (var rb in rbs)
            {
                rb.AddExplosionForce(explosionForce, transform.position, explosionRadius, 1f, ForceMode.Impulse);
            }

            // Cleanup debris after 5 seconds so the level doesn't get cluttered
            Destroy(debris, 5f);
        }

        // 3. Hide the intact object immediately
        // We do not Destroy(gameObject) instantly because Health.cs needs time to finish its logic (dropping loot)
        if (propCollider) propCollider.enabled = false;
        foreach (var r in propRenderers) r.enabled = false;

        // Disable this script to stop the Update loop
        this.enabled = false;
    }
}