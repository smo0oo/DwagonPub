using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(DomeAbilityHolder))]
[RequireComponent(typeof(DomeAI))]
public class DomeController : MonoBehaviour
{
    public static DomeController instance;

    [Header("Scaling")]
    public float healthPerFaithPoint = 5f;

    [Header("References")]
    public GameObject edgeMarkerPrefab;
    public Transform domeVisuals;

    [Header("Dome Settings")]
    public float minRadius = 5f;
    public float maxRadius = 20f;
    private const int markerCount = 36;

    [Header("Defense Mechanism")]
    public LayerMask enemyLayer;
    public float burnDamagePerSecond = 35f;
    public float burnTickRate = 0.5f;
    public GameObject burnVFX;

    private DomeUIManager uiManager;
    private List<GameObject> edgeMarkers = new List<GameObject>();
    private SphereCollider domeCollider;
    private Health domeHealth;

    // --- AAA FIX: This is your "Temporary Variable" ---
    // It stores the calculated max power and persists across player switches.
    private float minPower;
    private float maxPower;
    // -------------------------------------------------

    private float currentRadius;
    private Collider[] burnBuffer = new Collider[50];
    private float burnTimer = 0f;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        domeHealth = GetComponent<Health>();
        domeCollider = GetComponent<SphereCollider>();
        if (domeCollider != null) domeCollider.isTrigger = false;
    }

    void Start()
    {
        ActivateDome();
        // Calculate stats ONCE at the start of the scene.
        RecalculateDomeStats();
    }

    public void SetDomeActive(bool isActive)
    {
        if (isActive) ActivateDome();
        else DeactivateDome();
    }

    private void ActivateDome()
    {
        this.enabled = true;
        gameObject.layer = LayerMask.NameToLayer("Dome");
        if (domeCollider != null) domeCollider.enabled = true;

        if (domeHealth != null)
        {
            domeHealth.isInvulnerable = false;
            domeHealth.gameObject.layer = LayerMask.NameToLayer("Dome");
        }

        SpawnEdgeMarkers();

        // Ensure UI is synced if we reactivate
        if (maxPower > 0) UpdateDomePower(domeHealth != null ? domeHealth.currentHealth : maxPower);
    }

    private void DeactivateDome()
    {
        gameObject.layer = LayerMask.NameToLayer("Default");
        if (domeCollider != null) domeCollider.enabled = false;
        if (domeHealth != null) domeHealth.isInvulnerable = true;
        foreach (var marker in edgeMarkers) { if (marker != null) Destroy(marker); }
        edgeMarkers.Clear();
        this.enabled = false;
    }

    void Update()
    {
        HandleDomeDefense();
    }

    private void HandleDomeDefense()
    {
        if (currentRadius <= 0.1f) return;
        burnTimer += Time.deltaTime;
        if (burnTimer < burnTickRate) return;
        burnTimer = 0f;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, currentRadius, burnBuffer, enemyLayer);
        int damagePerTick = Mathf.CeilToInt(burnDamagePerSecond * burnTickRate);

        for (int i = 0; i < hitCount; i++)
        {
            var enemyCollider = burnBuffer[i];
            if (enemyCollider == null) continue;
            Health enemyHealth = enemyCollider.GetComponentInChildren<Health>();
            if (enemyHealth != null && enemyHealth.currentHealth > 0)
            {
                enemyHealth.TakeDamage(damagePerTick, DamageEffect.DamageType.Magical, false, this.gameObject);
                if (burnVFX != null) Instantiate(burnVFX, enemyCollider.transform.position, Quaternion.identity);
            }
        }
    }

    public void LinkUIManager(DomeUIManager manager)
    {
        uiManager = manager;
        // Sync UI to the already calculated stats
        if (maxPower > 0)
        {
            uiManager.UpdateSliderRange(minPower, maxPower);
            if (domeHealth != null) uiManager.UpdateSliderValue(domeHealth.currentHealth);
        }
        else
        {
            RecalculateDomeStats();
        }
    }

    void OnEnable()
    {
        if (domeHealth != null) { domeHealth.OnHealthChanged += UpdateDomeHealthUI; }

        // AAA FIX: Subscribe ONLY to LevelUp. 
        // Do NOT subscribe to OnActivePlayerChanged.
        if (PartyManager.instance != null)
        {
            PartyManager.instance.OnLevelUp += RecalculateDomeStats;
        }
    }

    void OnDisable()
    {
        if (domeHealth != null) { domeHealth.OnHealthChanged -= UpdateDomeHealthUI; }

        if (PartyManager.instance != null)
        {
            PartyManager.instance.OnLevelUp -= RecalculateDomeStats;
        }
    }

    // AAA FIX: Renamed from HandlePartyStatsChanged to indicate it's a heavy calculation
    // designed to run rarely (Start or LevelUp), NOT on player switch.
    public void RecalculateDomeStats()
    {
        if (!this.enabled || PartyManager.instance == null) return;

        minPower = PartyManager.instance.domeBasePower;
        int totalFaith = 0;

        // Iterate all members (active or inactive) to get a stable "Party Faith" value
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member != null)
            {
                // 'true' includes disabled objects, ensuring we capture stats even if the GO is off
                PlayerStats stats = member.GetComponentInChildren<PlayerStats>(true);
                if (stats != null) totalFaith += stats.finalFaith;
            }
        }

        // Store the result in our class-level variable
        maxPower = minPower + (totalFaith * healthPerFaithPoint);

        // Apply to Dome Health
        if (domeHealth != null)
        {
            // Only update/heal if the max power actually changed (e.g. Level Up)
            if (domeHealth.maxHealth != (int)maxPower)
            {
                domeHealth.UpdateMaxHealth((int)maxPower);
                domeHealth.SetToMaxHealth(); // Refill on Level Up/Start
            }
        }

        // Update UI Range
        if (uiManager != null)
        {
            uiManager.UpdateSliderRange(minPower, maxPower);
            if (domeHealth != null)
            {
                uiManager.UpdateSliderValue(domeHealth.currentHealth);
                uiManager.UpdateHealthUI(domeHealth.currentHealth, domeHealth.maxHealth);
            }
        }

        // Force Visual Update
        if (domeHealth != null) UpdateDomePower(domeHealth.currentHealth);
    }

    private void UpdateDomeHealthUI()
    {
        if (uiManager != null && domeHealth != null)
        {
            uiManager.UpdateHealthUI(domeHealth.currentHealth, domeHealth.maxHealth);
            uiManager.UpdateSliderValue(domeHealth.currentHealth);
        }

        // AAA FIX: Ensure the physical dome shrinks when damage is taken
        if (domeHealth != null) UpdateDomePower(domeHealth.currentHealth);
    }

    public void UpdateDomePower(float currentPower)
    {
        if (!this.enabled) return;

        float powerPercent = 0f;

        // Robust Percentage Logic
        if (currentPower >= maxPower && maxPower > 0)
        {
            powerPercent = 1.0f;
        }
        else if (maxPower > minPower)
        {
            powerPercent = Mathf.InverseLerp(minPower, maxPower, currentPower);
        }
        else
        {
            powerPercent = 0f;
        }

        currentRadius = Mathf.Lerp(minRadius, maxRadius, powerPercent);

        if (domeVisuals != null) domeVisuals.localScale = new Vector3(currentRadius * 2, currentRadius * 2, currentRadius * 2);
        if (domeCollider != null) domeCollider.radius = currentRadius;

        for (int i = 0; i < edgeMarkers.Count; i++)
        {
            if (edgeMarkers[i] == null) continue;
            float angle = i * (360f / markerCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            edgeMarkers[i].transform.position = transform.position + (direction * currentRadius);
        }

        if (domeHealth != null)
        {
            domeHealth.damageReductionPercent = Mathf.Lerp(0.75f, 0.25f, powerPercent);
            if (uiManager != null) uiManager.UpdateMitigationUI(domeHealth.damageReductionPercent);
        }
    }

    private void SpawnEdgeMarkers()
    {
        if (edgeMarkerPrefab == null) return;
        foreach (var marker in edgeMarkers) { if (marker != null) Destroy(marker); }
        edgeMarkers.Clear();

        for (int i = 0; i < markerCount; i++)
        {
            float angle = i * (360f / markerCount);
            Vector3 position = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            GameObject marker = Instantiate(edgeMarkerPrefab, transform.position + position, Quaternion.identity, this.transform);
            Health markerHealth = marker.GetComponent<Health>();
            if (markerHealth != null) markerHealth.forwardDamageTo = this.domeHealth;
            edgeMarkers.Add(marker);
        }
    }
}