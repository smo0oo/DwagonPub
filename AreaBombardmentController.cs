using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class AreaBombardmentController : MonoBehaviour
{
    [Header("Storm Settings")]
    public float duration = 5f;
    public float radius = 6f;
    public bool useAbilityRadius = true;

    [Header("Spawning")]
    public float spawnRate = 8f;
    public float spawnHeight = 12f;
    [Tooltip("Fallback projectile if the Ability doesn't have one assigned.")]
    public GameObject projectilePrefab;

    [Header("Randomness (AAA Polish)")]
    public float rotationVariance = 15f;
    public float timeVariance = 0.05f;

    [Header("Visuals & Audio")]
    public GameObject areaIndicatorVFX;
    public GameObject projectileImpactVFX;
    public AudioClip ambienceLoop;
    public float ambienceFadeDuration = 1.0f;

    private GameObject caster;
    private Ability sourceAbility;
    private int casterLayer;
    private AudioSource audioSource;
    private float stopTime;

    public void Initialize(GameObject caster, Ability ability)
    {
        this.caster = caster;
        this.casterLayer = caster.GetComponentInParent<CharacterRoot>()?.gameObject.layer ?? caster.layer;
        this.sourceAbility = ability;

        if (useAbilityRadius && this.sourceAbility != null && this.sourceAbility.aoeRadius > 0)
            this.radius = this.sourceAbility.aoeRadius;

        audioSource = GetComponent<AudioSource>();
        if (ambienceLoop != null)
        {
            audioSource.clip = ambienceLoop;
            audioSource.loop = true;
            audioSource.volume = 0;
            audioSource.Play();
            StartCoroutine(FadeAudio(0, 1, ambienceFadeDuration));
        }

        if (areaIndicatorVFX != null)
        {
            var vfx = Instantiate(areaIndicatorVFX, transform.position, Quaternion.identity, transform);
            vfx.transform.localScale = new Vector3(radius * 2, 1, radius * 2);
        }

        stopTime = Time.time + duration;
        StartCoroutine(BombardmentRoutine());
        Destroy(gameObject, duration + 5.0f);
    }

    private IEnumerator BombardmentRoutine()
    {
        while (Time.time < stopTime)
        {
            SpawnProjectile();
            float baseDelay = 1f / spawnRate;
            float randomDelay = Random.Range(-timeVariance, timeVariance);
            yield return new WaitForSeconds(Mathf.Max(0.05f, baseDelay + randomDelay));
        }

        if (audioSource.isPlaying)
            StartCoroutine(FadeAudio(1, 0, ambienceFadeDuration));
    }

    private void SpawnProjectile()
    {
        // --- AAA FIX: Unified Prefab Logic ---
        GameObject prefabToSpawn = (sourceAbility != null && sourceAbility.projectilePrefab != null) ? sourceAbility.projectilePrefab : projectilePrefab;
        if (prefabToSpawn == null) return;

        Vector2 randomCircle = Random.insideUnitCircle * radius;
        Vector3 groundPos = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        Vector3 spawnPos = groundPos + (Vector3.up * spawnHeight);

        Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
        if (rotationVariance > 0)
        {
            float randomX = Random.Range(-rotationVariance, rotationVariance);
            float randomZ = Random.Range(-rotationVariance, rotationVariance);
            rotation *= Quaternion.Euler(randomX, 0, randomZ);
        }

        GameObject projObj = ObjectPooler.instance.Get(prefabToSpawn, spawnPos, rotation);

        if (projObj != null)
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int friendlyLayer = LayerMask.NameToLayer("Friendly");

            int targetRangedLayer = (casterLayer == playerLayer || casterLayer == friendlyLayer)
                ? LayerMask.NameToLayer("FriendlyRanged")
                : LayerMask.NameToLayer("HostileRanged");

            if (targetRangedLayer != -1)
            {
                projObj.layer = targetRangedLayer;
            }

            if (projObj.TryGetComponent<Projectile>(out var projComponent))
            {
                projComponent.vfxOverride = projectileImpactVFX;
                // The Projectile is fully autonomous now!
                projComponent.Initialize(sourceAbility, caster, casterLayer);
            }

            Collider pCol = projObj.GetComponent<Collider>();
            if (pCol != null && caster != null)
            {
                foreach (Collider c in caster.GetComponentsInParent<Collider>())
                {
                    Physics.IgnoreCollision(pCol, c);
                }
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
        Gizmos.color = new Color(0, 1, 1, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);

        Gizmos.color = new Color(0, 1, 1, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 origin = transform.position + new Vector3(r.x, spawnHeight, r.y);
            Gizmos.DrawLine(origin, origin + Vector3.down * 2f);
        }
    }
}