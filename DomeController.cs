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

    // References managed by DomeStateController or auto-linked
    private DomeUIManager uiManager;
    private List<GameObject> edgeMarkers = new List<GameObject>();
    private SphereCollider domeCollider;
    private Health domeHealth;

    private float minPower;
    private float maxPower;

    // Track current radius for the burning logic
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

        // Ensure the collider doesn't trigger physics events inadvertently
        if (domeCollider != null)
        {
            domeCollider.isTrigger = false;
        }
    }

    void Start()
    {
        // Set the Dome's layer
        gameObject.layer = LayerMask.NameToLayer("Dome");
        SpawnEdgeMarkers();

        // Initial setup
        HandlePartyStatsChanged(null);
        UpdateDomeHealthUI();
    }

    void Update()
    {
        HandleDomeDefense();
    }

    private void HandleDomeDefense()
    {
        if (currentRadius <= 0.1f) return;

        // Tick Timer Logic
        burnTimer += Time.deltaTime;
        if (burnTimer < burnTickRate) return;

        // Reset timer
        burnTimer = 0f;

        // Check for enemies inside the dome radius
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, currentRadius, burnBuffer, enemyLayer);

        // Calculate damage chunk for this tick
        int damagePerTick = Mathf.CeilToInt(burnDamagePerSecond * burnTickRate);

        for (int i = 0; i < hitCount; i++)
        {
            var enemyCollider = burnBuffer[i];
            Health enemyHealth = enemyCollider.GetComponentInChildren<Health>();

            if (enemyHealth != null && enemyHealth.currentHealth > 0)
            {
                // Apply the chunk of damage
                enemyHealth.TakeDamage(damagePerTick, DamageEffect.DamageType.Magical, false, this.gameObject);

                // Optional: Spawn VFX
                if (burnVFX != null)
                {
                    Instantiate(burnVFX, enemyCollider.transform.position, Quaternion.identity);
                }
            }
        }
    }

    // Called by DomeStateController to link the UI when the battle scene starts
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
            domeHealth.UpdateMaxHealth((int)maxPower);
            domeHealth.SetToMaxHealth();
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
        if (edgeMarkerPrefab == null)
        {
            Debug.LogError("Edge Marker Prefab is not assigned on the DomeController!");
            return;
        }

        foreach (var marker in edgeMarkers) { if (marker != null) Destroy(marker); }
        edgeMarkers.Clear();

        for (int i = 0; i < markerCount; i++)
        {
            float angle = i * (360f / markerCount);
            Vector3 position = Quaternion.Euler(0, angle, 0) * Vector3.forward;

            GameObject marker = Instantiate(edgeMarkerPrefab, transform.position + position, Quaternion.identity, this.transform);

            // --- NEW: Force the layer to match the Dome (for AI targeting) ---
            marker.layer = this.gameObject.layer;
            // -----------------------------------------------------------------

            Health markerHealth = marker.GetComponent<Health>();
            if (markerHealth != null)
            {
                markerHealth.forwardDamageTo = this.domeHealth;
            }

            edgeMarkers.Add(marker);
        }
    }
}