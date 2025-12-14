using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DomeBattleManager : MonoBehaviour
{
    public static DomeBattleManager instance;

    [Header("Phase Settings")]
    public bool startInPrepPhase = true;
    public float autoStartDelayForAmbush = 2f;

    public bool IsBattleActive { get; private set; } = false;

    [Header("References")]
    public DomeWaveSpawner waveSpawner;
    public GameObject exitZoneObject;
    public WagonResourceManager resourceManager;

    [Header("UI References")]
    public GameObject prepPhasePanel;
    public Button startNightButton;
    public TextMeshProUGUI waveStatusText;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    void Start()
    {
        if (resourceManager == null)
        {
            resourceManager = WagonResourceManager.instance ?? FindAnyObjectByType<WagonResourceManager>();
        }

        if (exitZoneObject != null) exitZoneObject.SetActive(false);

        // --- NEW: Check for Dual Mode & Buffs ---
        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            // 1. Auto-start battle (skip prep phase for fluidity?)
            // Or keep prep phase to let player check Wagon inventory. Let's keep prep.

            // 2. Check for Boss Buff
            if (DualModeManager.instance.pendingBossBuff != null)
            {
                ApplyBossBuff(DualModeManager.instance.pendingBossBuff);
            }
        }
        // ----------------------------------------

        string spawnID = GetCurrentSpawnID();
        if (spawnID == "AmbushSpawn")
        {
            startInPrepPhase = false;
            StartCoroutine(StartAmbushSequence());
        }
        else
        {
            EnterPrepPhase();
        }

        if (startNightButton != null) startNightButton.onClick.AddListener(OnStartNightClicked);
    }

    // ... (GetCurrentSpawnID, EnterPrepPhase, OnStartNightClicked, StartAmbushSequence, StartBattle remain unchanged) ...
    private string GetCurrentSpawnID() { if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.lastSpawnPointID)) return GameManager.instance.lastSpawnPointID; return "WagonCenter"; }
    private void EnterPrepPhase() { IsBattleActive = false; if (prepPhasePanel != null) prepPhasePanel.SetActive(true); if (waveStatusText != null) waveStatusText.text = "Phase: <color=green>Preparation</color>"; if (waveSpawner != null) waveSpawner.StopSpawning(); }
    public void OnStartNightClicked() { StartBattle(); }
    private IEnumerator StartAmbushSequence() { if (prepPhasePanel != null) prepPhasePanel.SetActive(false); if (waveStatusText != null) waveStatusText.text = "<color=red>AMBUSH!</color>"; yield return new WaitForSeconds(autoStartDelayForAmbush); StartBattle(); }
    private void StartBattle() { IsBattleActive = true; if (prepPhasePanel != null) prepPhasePanel.SetActive(false); if (waveSpawner != null) waveSpawner.StartSpawning(); if (waveStatusText != null) waveStatusText.text = "Phase: <color=red>Night Defense</color>"; }

    public void OnVictory()
    {
        IsBattleActive = false;
        if (waveStatusText != null) waveStatusText.text = "<color=gold>VICTORY</color>";

        // --- MODIFIED: Dual Mode Logic ---
        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            Debug.Log("Dual Mode Defense Complete. Returning to Dungeon...");
            StartCoroutine(DualModeVictoryRoutine());
            return;
        }
        // ---------------------------------

        if (exitZoneObject != null)
        {
            exitZoneObject.SetActive(true);
            if (FloatingTextManager.instance != null)
                FloatingTextManager.instance.ShowAIStatus("Road Open!", exitZoneObject.transform.position + Vector3.up * 2);
        }
    }

    // --- NEW HELPER METHODS ---
    private IEnumerator DualModeVictoryRoutine()
    {
        yield return new WaitForSeconds(3.0f); // Give player time to see "Victory"
        DualModeManager.instance.SwitchToDungeon();
    }

    private void ApplyBossBuff(Ability buffAbility)
    {
        Debug.Log($"Applying Boss Buff: {buffAbility.name}");
        // Apply to all active Wagon Team members
        if (PartyAIManager.instance != null)
        {
            foreach (var member in PartyAIManager.instance.AllPartyAIs)
            {
                if (member.gameObject.activeInHierarchy)
                {
                    // Apply friendly effects from the buff ability
                    foreach (var effect in buffAbility.friendlyEffects)
                    {
                        effect.Apply(member.gameObject, member.gameObject);
                    }
                }
            }
        }
    }
}