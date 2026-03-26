using UnityEngine;

public class ProceduralHitReaction : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The bone to rotate. Usually Spine, Chest, or Neck.")]
    public Transform spineBone;
    private Health myHealth;

    [Header("Trauma Settings (Percentage Based)")]
    public float maxTraumaAngle = 60f;
    public float noiseSpeed = 30f;
    public float recoveryRate = 12f;
    public float traumaPerPercentHP = 2.0f;

    [Header("Poise Integration")]
    [Range(0f, 100f)]
    public float poiseBreakThresholdPct = 15f;
    public float poiseBreakMultiplier = 3.0f;

    private float currentTrauma = 0f;
    private float seedX, seedY, seedZ;

    void Awake()
    {
        myHealth = GetComponent<Health>() ?? GetComponentInParent<Health>();

        seedX = Random.Range(0f, 100f);
        seedY = Random.Range(0f, 100f);
        seedZ = Random.Range(0f, 100f);

        // Start disabled to save CPU. We only wake up when hit.
        this.enabled = false;
    }

    void OnEnable()
    {
        if (myHealth != null) myHealth.OnTakeLocalDamage += HandleLocalDamage;
    }

    void OnDisable()
    {
        if (myHealth != null) myHealth.OnTakeLocalDamage -= HandleLocalDamage;
    }

    private void HandleLocalDamage(int rawDamage)
    {
        if (myHealth == null || myHealth.maxHealth <= 0) return;

        float percentLost = (float)rawDamage / myHealth.maxHealth;
        TriggerFlinch(percentLost);
    }

    [ContextMenu("Test Flinch (Small Hit - 5% HP)")]
    public void TestSmallHit() { TriggerFlinch(0.05f); }

    [ContextMenu("Test Flinch (Poise Break - 20% HP)")]
    public void TestMassiveHit() { TriggerFlinch(0.20f); }

    private void TriggerFlinch(float percentDamage)
    {
        float percentWhole = percentDamage * 100f;
        float addedTrauma = percentWhole * traumaPerPercentHP;

        if (percentWhole >= poiseBreakThresholdPct)
        {
            addedTrauma *= poiseBreakMultiplier;
        }

        currentTrauma = Mathf.Clamp(currentTrauma + addedTrauma, 0f, maxTraumaAngle);

        // Wake the script up so LateUpdate starts running
        this.enabled = true;
    }

    void LateUpdate()
    {
        if (currentTrauma <= 0.01f)
        {
            // Trauma is over. Go to sleep to save CPU.
            currentTrauma = 0f;
            this.enabled = false;
            return;
        }

        if (spineBone == null) return;

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