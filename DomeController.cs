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
    [Tooltip("The layer(s) of enemies that should burn when entering the dome.")]
    public LayerMask enemyLayer;
    [Tooltip("How much damage per second enemies take while inside the dome.")]
    public float burnDamagePerSecond = 35f;
    [Tooltip("How often (in seconds) the burn damage applies. 0.5 = twice per second.")]
    public float burnTickRate = 0.5f;
    [Tooltip("Optional: A particle effect prefab to spawn on enemies when they burn.")]
    public GameObject burnVFX;

    private DomeUIManager uiManager;
    private List<GameObject> edgeMarkers = new List<GameObject>();
    private SphereCollider domeCollider;
    private Health domeHealth;

    private float minPower;
    private float maxPower;

    private float currentRadius;
    private Collider[] burnBuffer = new Collider[50];
    private float burnTimer = 0f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        domeHealth = GetComponent<Health>();
        domeCollider = GetComponent<SphereCollider>();

        if (domeCollider != null)
        {
            domeCollider.isTrigger = false;
        }
    }

    void Start()
    {
        // --- FIX: Removed the Race Condition Check ---
        // We no longer check GameManager scene type here because Start() runs 
        // before GameManager finishes updating its state during transitions.
        // We rely on DomeStateController to call SetDomeActive() at the right time.
        // ---------------------------------------------

        // Default to active initialization, DomeStateController will disable us next frame if needed.
        ActivateDome();
    }

    // --- NEW: Public Authority Method ---
    public void SetDomeActive(bool isActive)
    {
        if (isActive) ActivateDome();
        else DeactivateDome();
    }
    // ------------------------------------

    private void ActivateDome()
    {
        // 1. Re-enable this script so Update() runs for burning logic
        this.enabled = true;

        // 2. Set Layer to 'Dome' so enemies can target it
        gameObject.layer = LayerMask.NameToLayer("Dome");

        if (domeCollider != null) domeCollider.enabled = true;

        // 3. Ensure Health is vulnerable and on correct layer
        if (domeHealth != null)
        {
            domeHealth.isInvulnerable = false;
            domeHealth.gameObject.layer = LayerMask.NameToLayer("Dome");
        }

        SpawnEdgeMarkers();

        // 4. Force Stat & UI Refresh
        HandlePartyStatsChanged(null);
        if (domeHealth != null)
        {
            if (domeHealth.currentHealth <= 0) domeHealth.SetToMaxHealth();
            UpdateDomeHealthUI();
        }
    }

    private void DeactivateDome()
    {
        // 1. Hide from AI Targeting
        gameObject.layer = LayerMask.NameToLayer("Default");

        // 2. Disable Physics
        if (domeCollider != null) domeCollider.enabled = false;

        // 3. Make Invulnerable
        if (domeHealth != null) domeHealth.isInvulnerable = true;

        // 4. Cleanup Markers
        foreach (var marker in edgeMarkers) { if (marker != null) Destroy(marker); }
        edgeMarkers.Clear();

        // 5. Disable this script's Update loop
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

                if (burnVFX != null)
                {
                    Instantiate(burnVFX, enemyCollider.transform.position, Quaternion.identity);
                }
            }
        }
    }

    public void LinkUIManager(DomeUIManager manager)
    {
        uiManager = manager;
        HandlePartyStatsChanged(null);
        UpdateDomeHealthUI();
    }

    void OnEnable()
    {
        if (domeHealth != null) { domeHealth.OnHealthChanged += UpdateDomeHealthUI; }
        PartyManager.OnActivePlayerChanged += HandlePartyStatsChanged;
    }

    void OnDisable()
    {
        if (domeHealth != null) { domeHealth.OnHealthChanged -= UpdateDomeHealthUI; }
        PartyManager.OnActivePlayerChanged -= HandlePartyStatsChanged;
    }

    private void HandlePartyStatsChanged(GameObject activePlayer)
    {
        // Allow update if enabled OR if explicitly forcing active state logic
        if (!this.enabled) return;

        if (PartyManager.instance == null) return;

        minPower = PartyManager.instance.domeBasePower;

        int totalFaith = 0;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member != null && member.activeInHierarchy)
            {
                PlayerStats stats = member.GetComponentInChildren<PlayerStats>();
                if (stats != null)
                {
                    totalFaith += stats.finalFaith;
                }
            }
        }

        float faithBonus = totalFaith * healthPerFaithPoint;
        maxPower = minPower + faithBonus;

        if (domeHealth != null)
        {
            if (Mathf.Abs(domeHealth.maxHealth - (int)maxPower) > 1)
            {
                domeHealth.UpdateMaxHealth((int)maxPower);
                domeHealth.SetToMaxHealth();
            }
        }

        if (uiManager != null)
        {
            uiManager.UpdateSliderRange(minPower, maxPower);
        }

        if (domeHealth != null)
        {
            UpdateDomePower(domeHealth.currentHealth);
        }
    }

    private void UpdateDomeHealthUI()
    {
        if (uiManager != null && domeHealth != null)
        {
            uiManager.UpdateHealthUI(domeHealth.currentHealth, domeHealth.maxHealth);
        }
    }

    public void UpdateDomePower(float currentPower)
    {
        if (!this.enabled) return;

        float powerPercent = 0f;
        if (maxPower > minPower)
        {
            powerPercent = Mathf.InverseLerp(minPower, maxPower, currentPower);
        }

        currentRadius = Mathf.Lerp(minRadius, maxRadius, powerPercent);

        if (domeVisuals != null)
        {
            domeVisuals.localScale = new Vector3(currentRadius * 2, currentRadius * 2, currentRadius * 2);
        }
        if (domeCollider != null)
        {
            domeCollider.radius = currentRadius;
        }

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

            if (uiManager != null)
            {
                uiManager.UpdateMitigationUI(domeHealth.damageReductionPercent);
            }
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

            marker.layer = this.gameObject.layer;

            Health markerHealth = marker.GetComponent<Health>();
            if (markerHealth != null)
            {
                markerHealth.forwardDamageTo = this.domeHealth;
            }

            edgeMarkers.Add(marker);
        }
    }
}