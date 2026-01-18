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

    private float minPower;
    private float maxPower;

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

    void Start() { ActivateDome(); }

    public void SetDomeActive(bool isActive)
    {
        if (isActive) ActivateDome();
        else DeactivateDome();
    }

    private void ActivateDome()
    {
        this.enabled = true;
        gameObject.layer = LayerMask.NameToLayer("Dome"); // Center is "Dome"
        if (domeCollider != null) domeCollider.enabled = true;

        if (domeHealth != null)
        {
            domeHealth.isInvulnerable = false;
            domeHealth.gameObject.layer = LayerMask.NameToLayer("Dome");
        }

        SpawnEdgeMarkers();

        HandlePartyStatsChanged(null);
        if (domeHealth != null)
        {
            if (domeHealth.currentHealth <= 0) domeHealth.SetToMaxHealth();
            UpdateDomeHealthUI();
        }
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
        if (!this.enabled || PartyManager.instance == null) return;

        minPower = PartyManager.instance.domeBasePower;
        int totalFaith = 0;
        foreach (var member in PartyManager.instance.partyMembers)
        {
            if (member != null && member.activeInHierarchy)
            {
                PlayerStats stats = member.GetComponentInChildren<PlayerStats>();
                if (stats != null) totalFaith += stats.finalFaith;
            }
        }

        maxPower = minPower + (totalFaith * healthPerFaithPoint);
        if (domeHealth != null && Mathf.Abs(domeHealth.maxHealth - (int)maxPower) > 1)
        {
            domeHealth.UpdateMaxHealth((int)maxPower);
            domeHealth.SetToMaxHealth();
        }

        if (uiManager != null) uiManager.UpdateSliderRange(minPower, maxPower);
        if (domeHealth != null) UpdateDomePower(domeHealth.currentHealth);
    }

    private void UpdateDomeHealthUI()
    {
        if (uiManager != null && domeHealth != null) uiManager.UpdateHealthUI(domeHealth.currentHealth, domeHealth.maxHealth);
    }

    public void UpdateDomePower(float currentPower)
    {
        if (!this.enabled) return;
        float powerPercent = (maxPower > minPower) ? Mathf.InverseLerp(minPower, maxPower, currentPower) : 0f;
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

            // --- FIX: Removed the line that overwrote the layer ---
            // marker.layer = this.gameObject.layer; <--- DELETED THIS LINE
            // Now the marker keeps its own Layer (16/DomeMarker) and Tag
            // ----------------------------------------------------

            Health markerHealth = marker.GetComponent<Health>();
            if (markerHealth != null)
            {
                markerHealth.forwardDamageTo = this.domeHealth;
            }

            edgeMarkers.Add(marker);
        }
    }
}