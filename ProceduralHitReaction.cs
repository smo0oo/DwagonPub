using UnityEngine;

public class ProceduralHitReaction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The bone to rotate. Usually Spine, Chest, or Neck.")]
    public Transform spineBone;

    // We automatically grab this so we can read the maxHealth!
    private Health myHealth;

    [Header("Trauma Settings (Percentage Based)")]
    public float maxTraumaAngle = 60f;
    public float noiseSpeed = 30f;
    public float recoveryRate = 12f;

    [Tooltip("How many degrees of trauma to add per 1% of Max HP lost.")]
    public float traumaPerPercentHP = 2.0f;

    [Header("Poise Integration")]
    [Tooltip("If a single hit deals this % of Max HP, trigger a massive stagger.")]
    [Range(0f, 100f)]
    public float poiseBreakThresholdPct = 15f;
    public float poiseBreakMultiplier = 3.0f;

    private float currentTrauma = 0f;
    private float seedX, seedY, seedZ;

    void Awake()
    {
        myHealth = GetComponent<Health>();
        if (myHealth == null) myHealth = GetComponentInParent<Health>();
    }

    void OnEnable()
    {
        Health.OnDamageTaken += HandleGlobalDamage;
    }

    void OnDisable()
    {
        Health.OnDamageTaken -= HandleGlobalDamage;
    }

    void Start()
    {
        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(0f, 100f);
        seedZ = Random.Range(0f, 100f);
    }

    private void HandleGlobalDamage(DamageInfo info)
    {
        if (myHealth == null || info.Target != myHealth.gameObject) return;

        // Calculate the percentage of max health lost (e.g., 50 dmg / 500 max = 0.10)
        float percentLost = (float)info.Amount / myHealth.maxHealth;

        TriggerFlinch(percentLost);
    }

    [ContextMenu("Test Flinch (Small Hit - 5% HP)")]
    public void TestSmallHit() { TriggerFlinch(0.05f); }

    [ContextMenu("Test Flinch (Poise Break - 20% HP)")]
    public void TestMassiveHit() { TriggerFlinch(0.20f); }

    private void TriggerFlinch(float percentDamage)
    {
        // Convert the raw decimal (0.05) to a whole percentage number (5.0) for easier math
        float percentWhole = percentDamage * 100f;

        // Add trauma based on the percentage of health lost
        float addedTrauma = percentWhole * traumaPerPercentHP;

        // Did this single hit cross our percentage threshold?
        if (percentWhole >= poiseBreakThresholdPct)
        {
            addedTrauma *= poiseBreakMultiplier;
        }

        currentTrauma = Mathf.Clamp(currentTrauma + addedTrauma, 0f, maxTraumaAngle);
    }

    void LateUpdate()
    {
        if (currentTrauma <= 0.01f || spineBone == null) return;

        float time = Time.time * noiseSpeed;

        float noiseX = (Mathf.PerlinNoise(seedX, time) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(seedY, time) - 0.5f) * 2f;
        float noiseZ = (Mathf.PerlinNoise(seedZ, time) - 0.5f) * 2f;

        Quaternion traumaRotation = Quaternion.Euler(
            noiseX * currentTrauma,
            noiseY * currentTrauma,
            noiseZ * currentTrauma
        );

        spineBone.localRotation = spineBone.localRotation * traumaRotation;

        currentTrauma = Mathf.Lerp(currentTrauma, 0f, Time.deltaTime * recoveryRate);
    }
}