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

    private LootBagUI _lootBagRef;

    private float winCheckTimer = 0f;
    private const float WIN_CHECK_INTERVAL = 1.0f;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    void OnDestroy()
    {
        if (_lootBagRef != null) LootBagUI.OnLootBagClosed -= OnLootBagClosed;
    }

    void Start()
    {
        WagonHotbarManager hotbar = FindAnyObjectByType<WagonHotbarManager>();
        if (hotbar != null) hotbar.InitializeAndShow();

        if (resourceManager == null) resourceManager = WagonResourceManager.instance ?? FindAnyObjectByType<WagonResourceManager>();
        if (exitZoneObject != null) exitZoneObject.SetActive(false);

        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            if (DualModeManager.instance.pendingBossBuff != null) ApplyBossBuff(DualModeManager.instance.pendingBossBuff);

            // --- AAA FIX: Dynamic UI for Rescue Mode ---
            if (DualModeManager.instance.isRescueDefending)
            {
                if (startNightButton != null)
                {
                    TextMeshProUGUI btnText = startNightButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (btnText != null) btnText.text = "Deploy Rescue";
                    else
                    {
                        Text standardText = startNightButton.GetComponentInChildren<Text>();
                        if (standardText != null) standardText.text = "Deploy Rescue";
                    }
                }

                if (waveStatusText != null) waveStatusText.text = "Phase: <color=orange>Rescue Defense!</color>";
            }
        }

        string spawnID = GetCurrentSpawnID();
        if (spawnID == "AmbushSpawn")
        {
            startInPrepPhase = false;
            StartCoroutine(StartAmbushSequence());
        }
        else EnterPrepPhase();

        bool uiBlocked = false;
        var lootUI = FindFirstObjectByType<LootBagUI>();
        if (lootUI != null && lootUI.IsVisible)
        {
            uiBlocked = true;
            _lootBagRef = lootUI;
            LootBagUI.OnLootBagClosed += OnLootBagClosed;
        }

        var setupUI = FindFirstObjectByType<DualModeSetupUI>();
        if (setupUI != null && setupUI.IsVisible) uiBlocked = true;

        if (uiBlocked && prepPhasePanel != null) prepPhasePanel.SetActive(false);

        if (GameManager.instance != null && GameManager.instance.justExitedDungeon)
        {
            if (startNightButton != null)
            {
                TextMeshProUGUI btnText = startNightButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Ready";
                else
                {
                    Text standardText = startNightButton.GetComponentInChildren<Text>();
                    if (standardText != null) standardText.text = "Ready";
                }
            }

            if (waveStatusText != null) waveStatusText.text = "Phase: <color=yellow>Final Stand</color>";
        }

        if (startNightButton != null) startNightButton.onClick.AddListener(OnStartNightClicked);
    }

    void Update()
    {
        if (!IsBattleActive) return;

        winCheckTimer += Time.deltaTime;
        if (winCheckTimer >= WIN_CHECK_INTERVAL)
        {
            winCheckTimer = 0f;
            CheckForWinCondition();
        }
    }

    private void CheckForWinCondition()
    {
        if (waveSpawner != null && waveSpawner.IsSpawning) return;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        bool anyAlive = false;
        foreach (var enemy in enemies)
        {
            Health h = enemy.GetComponent<Health>();
            if (h != null && h.currentHealth > 0 && !h.isDowned) { anyAlive = true; break; }
        }

        if (!anyAlive) OnVictory();
    }

    private void OnLootBagClosed()
    {
        if (!IsBattleActive && prepPhasePanel != null) prepPhasePanel.SetActive(true);
        LootBagUI.OnLootBagClosed -= OnLootBagClosed;
    }

    private string GetCurrentSpawnID()
    {
        if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.lastSpawnPointID)) return GameManager.instance.lastSpawnPointID;
        return "WagonCenter";
    }

    private void EnterPrepPhase()
    {
        IsBattleActive = false;
        if (prepPhasePanel != null) prepPhasePanel.SetActive(true);

        // Preserve the orange warning text if we are in rescue mode!
        if (waveStatusText != null && (DualModeManager.instance == null || !DualModeManager.instance.isRescueDefending))
            waveStatusText.text = "Phase: <color=green>Preparation</color>";

        if (waveSpawner != null) waveSpawner.StopSpawning();
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

    public void StartBattle()
    {
        IsBattleActive = true;
        if (prepPhasePanel != null) prepPhasePanel.SetActive(false);
        if (waveSpawner != null) waveSpawner.StartSpawning();
        if (waveStatusText != null) waveStatusText.text = "Phase: <color=red>Night Defense</color>";
    }

    public void OnVictory()
    {
        IsBattleActive = false;
        if (waveStatusText != null) waveStatusText.text = "<color=gold>VICTORY</color>";

        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            Debug.Log("Dual Mode Defense Complete.");
            StartCoroutine(DualModeVictoryRoutine());
            return;
        }

        if (exitZoneObject != null)
        {
            exitZoneObject.SetActive(true);
            if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowAIStatus("Road Open!", exitZoneObject.transform.position + Vector3.up * 2);
        }
    }

    private IEnumerator DualModeVictoryRoutine()
    {
        yield return new WaitForSeconds(3.0f);

        // --- AAA FIX: Route Victory Correctly ---
        if (DualModeManager.instance.isRescueDefending)
        {
            Debug.Log("Rescue Defense Won! Deploying to Dungeon...");
            DualModeManager.instance.DeployRescueTeam();
        }
        else
        {
            DualModeManager.instance.SwitchToDungeon();
        }
    }

    private void ApplyBossBuff(Ability buffAbility)
    {
        if (PartyAIManager.instance != null)
        {
            foreach (var member in PartyAIManager.instance.AllPartyAIs)
            {
                if (member.gameObject.activeInHierarchy)
                {
                    foreach (var effect in buffAbility.friendlyEffects) effect.Apply(member.gameObject, member.gameObject);
                }
            }
        }
    }
}