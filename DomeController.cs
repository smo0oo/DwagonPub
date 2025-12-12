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

    // --- MODIFIED: This is now private, the DomeStateController will manage it ---
    private DomeUIManager uiManager;
    private List<GameObject> edgeMarkers = new List<GameObject>();
    private SphereCollider domeCollider;
    private Health domeHealth;

    private float minPower;
    private float maxPower;

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
        domeCollider.isTrigger = false;
    }

    // --- THIS METHOD HAS BEEN MODIFIED ---
    void Start()
    {
        gameObject.layer = LayerMask.NameToLayer("Dome");
        SpawnEdgeMarkers();

        // --- REMOVED: UI linking is now handled by DomeStateController ---
        // uiManager = FindObjectOfType<DomeUIManager>();
        // if (uiManager != null) { uiManager.InitializeAndShow(this); }

        // We still call these to ensure the internal values are correct
        HandlePartyStatsChanged(null);
        UpdateDomeHealthUI();
    }

    // This public method allows the DomeStateController to establish the link
    public void LinkUIManager(DomeUIManager manager)
    {
        uiManager = manager;
        // Now that the link is made, we can push the initial data to the UI
        HandlePartyStatsChanged(null);
        UpdateDomeHealthUI();
    }


    #region Unchanged Code
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

        domeHealth.UpdateMaxHealth((int)maxPower);
        domeHealth.SetToMaxHealth();

        if (uiManager != null)
        {
            uiManager.UpdateSliderRange(minPower, maxPower);
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
        if (maxPower <= minPower) return;
        float powerPercent = Mathf.InverseLerp(minPower, maxPower, currentPower);
        float currentRadius = Mathf.Lerp(minRadius, maxRadius, powerPercent);

        if (domeVisuals != null) { domeVisuals.localScale = new Vector3(currentRadius * 2, currentRadius * 2, currentRadius * 2); }
        if (domeCollider != null) { domeCollider.radius = currentRadius; }

        for (int i = 0; i < edgeMarkers.Count; i++)
        {
            float angle = i * (360f / markerCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            edgeMarkers[i].transform.position = transform.position + (direction * currentRadius);
        }

        domeHealth.damageReductionPercent = Mathf.Lerp(0.75f, 0.25f, powerPercent);

        if (uiManager != null)
        {
            uiManager.UpdateMitigationUI(domeHealth.damageReductionPercent);
        }
    }

    private void SpawnEdgeMarkers()
    {
        if (edgeMarkerPrefab == null) { Debug.LogError("Edge Marker Prefab is not assigned on the DomeController!"); return; }
        for (int i = 0; i < markerCount; i++)
        {
            float angle = i * (360f / markerCount);
            Vector3 position = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            GameObject marker = Instantiate(edgeMarkerPrefab, transform.position + position, Quaternion.identity, this.transform);
            Health markerHealth = marker.GetComponent<Health>();
            if (markerHealth != null) { markerHealth.forwardDamageTo = this.domeHealth; }
            edgeMarkers.Add(marker);
        }
    }
    #endregion
}