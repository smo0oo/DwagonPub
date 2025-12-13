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

    // --- UPDATED: Property to remove "unused variable" warning ---
    public bool IsBattleActive { get; private set; } = false;

    [Header("References")]
    public DomeWaveSpawner waveSpawner;

    [Header("Victory & Exit")]
    [Tooltip("The WorldMapExit object (or the zone containing it) to enable when the battle is won.")]
    public GameObject exitZoneObject;

    // This is found automatically, but kept exposed for debug
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
        // 1. Auto-link to the Core Scene Manager
        if (resourceManager == null)
        {
            resourceManager = WagonResourceManager.instance;
            if (resourceManager == null)
            {
                resourceManager = FindAnyObjectByType<WagonResourceManager>();
            }
        }

        if (resourceManager == null)
        {
            Debug.LogError("DomeBattleManager: Critical Error - Could not find WagonResourceManager.");
        }

        // 2. Ensure Exit is HIDDEN at start
        if (exitZoneObject != null)
        {
            exitZoneObject.SetActive(false);
        }

        // 3. Determine State based on how we loaded in
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

        if (startNightButton != null)
        {
            startNightButton.onClick.AddListener(OnStartNightClicked);
        }
    }

    private string GetCurrentSpawnID()
    {
        // Check the GameManager for the exact ID used to load this scene
        if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.lastSpawnPointID))
        {
            return GameManager.instance.lastSpawnPointID;
        }
        return "WagonCenter"; // Default fallback
    }

    // --- Phases ---

    private void EnterPrepPhase()
    {
        IsBattleActive = false;

        if (prepPhasePanel != null) prepPhasePanel.SetActive(true);
        if (waveStatusText != null) waveStatusText.text = "Phase: <color=green>Preparation</color>";

        if (waveSpawner != null) waveSpawner.StopSpawning();

        Debug.Log("Entered Prep Phase. Build defenses!");
    }

    public void OnStartNightClicked()
    {
        StartBattle();
    }

    private IEnumerator StartAmbushSequence()
    {
        if (prepPhasePanel != null) prepPhasePanel.SetActive(false);
        if (waveStatusText != null) waveStatusText.text = "<color=red>AMBUSH!</color>";

        yield return new WaitForSeconds(autoStartDelayForAmbush);
        StartBattle();
    }

    private void StartBattle()
    {
        IsBattleActive = true;

        if (prepPhasePanel != null) prepPhasePanel.SetActive(false);

        if (waveSpawner != null)
        {
            waveSpawner.StartSpawning();
        }

        if (waveStatusText != null) waveStatusText.text = "Phase: <color=red>Night Defense</color>";
    }

    public void OnVictory()
    {
        IsBattleActive = false;
        if (waveStatusText != null) waveStatusText.text = "<color=gold>VICTORY</color>";

        Debug.Log("Night Survived. Looting phase begins.");

        // --- NEW: Activate the Exit ---
        if (exitZoneObject != null)
        {
            exitZoneObject.SetActive(true);

            // Optional visual feedback
            if (FloatingTextManager.instance != null)
            {
                FloatingTextManager.instance.ShowAIStatus("Road Open!", exitZoneObject.transform.position + Vector3.up * 2);
            }
        }
        else
        {
            Debug.LogWarning("DomeBattleManager: No 'ExitZoneObject' assigned! Player cannot leave.");
        }
        // ------------------------------
    }
}