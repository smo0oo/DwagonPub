using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class AreaBombardmentController : MonoBehaviour
{
    [Header("Storm Settings")]
    [Tooltip("How long the bombardment lasts.")]
    public float duration = 5f;
    [Tooltip("The radius of the area.")]
    public float radius = 6f;
    [Tooltip("Optional: Scales the radius based on the Ability's aoeRadius (if set > 0).")]
    public bool useAbilityRadius = true;

    [Header("Spawning")]
    [Tooltip("Projectiles per second.")]
    public float spawnRate = 8f;
    [Tooltip("Height above the ground to spawn projectiles.")]
    public float spawnHeight = 12f;
    [Tooltip("The projectile to rain down. Must have a Projectile component.")]
    public GameObject projectilePrefab;

    [Header("Randomness (AAA Polish)")]
    [Tooltip("Randomize rotation of projectiles slightly for natural variety.")]
    public float rotationVariance = 15f;
    [Tooltip("Randomize spawn timing slightly to prevent robotic rhythmic spawning.")]
    public float timeVariance = 0.05f;

    [Header("Visuals & Audio")]
    public GameObject areaIndicatorVFX;
    [Tooltip("Sound to play on loop while the storm is active.")]
    public AudioClip ambienceLoop;
    public float ambienceFadeDuration = 1.0f;

    // State Data
    private GameObject caster;
    private Ability sourceAbility;
    private int casterLayer;
    private AudioSource audioSource;
    private float stopTime;

    public void Initialize(GameObject caster, Ability ability)
    {
        this.caster = caster;
        this.sourceAbility = ability;
        this.casterLayer = caster.GetComponentInParent<CharacterRoot>()?.gameObject.layer ?? caster.layer;

        // Override radius if the ability defines a specific AOE
        if (useAbilityRadius && ability.aoeRadius > 0)
            this.radius = ability.aoeRadius;

        // Setup Audio
        audioSource = GetComponent<AudioSource>();
        if (ambienceLoop != null)
        {
            audioSource.clip = ambienceLoop;
            audioSource.loop = true;
            audioSource.volume = 0;
            audioSource.Play();
            StartCoroutine(FadeAudio(0, 1, ambienceFadeDuration)); // Fade In
        }

        // Setup Visuals
        if (areaIndicatorVFX != null)
        {
            var vfx = Instantiate(areaIndicatorVFX, transform.position, Quaternion.identity, transform);
            // Assuming visuals are normalized to 1 unit size, scale them to match radius
            vfx.transform.localScale = new Vector3(radius * 2, 1, radius * 2);
        }

        // Start Logic
        stopTime = Time.time + duration;
        StartCoroutine(BombardmentRoutine());

        // Auto Cleanup: Destroy this controller object after duration + buffer for fading
        Destroy(gameObject, duration + 2.0f);
    }

    private IEnumerator BombardmentRoutine()
    {
        while (Time.time < stopTime)
        {
            SpawnProjectile();

            // Calculate delay for next shot
            float baseDelay = 1f / spawnRate;
            float randomDelay = Random.Range(-timeVariance, timeVariance);
            yield return new WaitForSeconds(Mathf.Max(0.05f, baseDelay + randomDelay));
        }

        // Fade out audio when finished
        if (audioSource.isPlaying)
            StartCoroutine(FadeAudio(1, 0, ambienceFadeDuration));
    }

    private void SpawnProjectile()
    {
        if (projectilePrefab == null) return;

        // 1. Pick a random point within the circle (Horizontal Plane)
        Vector2 randomCircle = Random.insideUnitCircle * radius;
        Vector3 groundPos = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

        // 2. Spawn high above that point
        Vector3 spawnPos = groundPos + (Vector3.up * spawnHeight);

        // 3. Rotation: Facing downwards with slight variance
        Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
        if (rotationVariance > 0)
        {
            float randomX = Random.Range(-rotationVariance, rotationVariance);
            float randomZ = Random.Range(-rotationVariance, rotationVariance);
            rotation *= Quaternion.Euler(randomX, 0, randomZ);
        }

        // 4. Instantiate (Pooled)
        GameObject projObj = ObjectPooler.instance.Get(projectilePrefab, spawnPos, rotation);

        if (projObj != null)
        {
            // IMPORTANT: Initialize the projectile with the ABILITY data.
            // This ensures the projectile applies the correct Damage/Effects defined in the Ability ScriptableObject.
            if (projObj.TryGetComponent<Projectile>(out var projComponent))
            {
                projComponent.Initialize(sourceAbility, caster, casterLayer);
            }

            projObj.SetActive(true);
        }
    }

    private IEnumerator FadeAudio(float startVol, float endVol, float duration)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(startVol, endVol, t / duration);
            yield return null;
        }
        audioSource.volume = endVol;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.5f); // Cyan
        Gizmos.DrawWireSphere(transform.position, radius);

        // Draw falling lines visualization
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 origin = transform.position + new Vector3(r.x, spawnHeight, r.y);
            Gizmos.DrawLine(origin, origin + Vector3.down * 2f);
        }
    }
}