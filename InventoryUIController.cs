using UnityEngine;
using Unity.VisualScripting;
using System;
using System.Collections;
using System.Collections.Generic;

public class InventoryUIController : MonoBehaviour
{
    public static InventoryUIController instance;

    public GameObject ActivePlayer { get; private set; }
    public PlayerStats ActivePlayerStats { get; private set; }
    public PlayerAbilityHolder ActivePlayerAbilityHolder { get; private set; }
    public Inventory ActivePlayerInventory { get; private set; }

    public static event Action<GameObject> OnActivePlayerChanged;
    public static event Action<bool> OnPartyInventoryToggled;

    [Header("UI Panels")]
    public GameObject inventoryPanel;
    public GameObject abilityBookPanel;
    public GameObject partyInventoryPanel;
    public GameObject skillTreePanel;

    private List<GameObject> allToggleablePanels = new List<GameObject>();

    [Header("Game State References")]
    public GameObject uiManagerObject;
    public string tradeInProgressVariableName = "TradeInProgress";

    [Header("UI Managers")]
    public InventoryManager inventoryManager;
    public EquipmentManager equipmentManager;
    public StatsUIManager statsUIManager;
    public HotbarManager hotbarManager;
    public LevelUIManager levelUIManager;
    public LevelDebugUI levelDebugUI;
    public PartyManager partyManager;
    public AbilityBookManager abilityBookManager;
    public ManaUIManager manaUIManager;
    public PlayerHealthUIManager playerHealthUIManager;
    public SkillTreeUIManager skillTreeUIManager;

    void Awake() { if (instance != null && instance != this) { Destroy(gameObject); } else { instance = this; } }
    void Start()
    {
        if (inventoryPanel != null) allToggleablePanels.Add(inventoryPanel);
        if (abilityBookPanel != null) allToggleablePanels.Add(abilityBookPanel);
        if (partyInventoryPanel != null) allToggleablePanels.Add(partyInventoryPanel);
        if (skillTreePanel != null) allToggleablePanels.Add(skillTreePanel);
        foreach (var panel in allToggleablePanels) { panel.SetActive(false); }
    }
    void OnEnable() { PartyManager.OnActivePlayerChanged += RefreshAllPlayerDisplays; }
    void OnDisable() { PartyManager.OnActivePlayerChanged -= RefreshAllPlayerDisplays; }

    // --- THIS METHOD HAS BEEN UPDATED ---
    public void RefreshAllPlayerDisplays(GameObject newActivePlayer)
    {
        ActivePlayer = newActivePlayer;
        OnActivePlayerChanged?.Invoke(ActivePlayer);

        if (ActivePlayer == null || !ActivePlayer.activeInHierarchy)
        {
            // Clear all UI if there is no active player
            ActivePlayerStats = null; ActivePlayerAbilityHolder = null; ActivePlayerInventory = null;
            inventoryManager?.DisplayInventory(null, null); equipmentManager?.DisplayEquipmentForPlayer(null); statsUIManager?.DisplayStatsAndClass(null, null); hotbarManager?.DisplayHotbar(null, null, null, null); levelUIManager?.DisplayLevelInfo(null); abilityBookManager?.DisplayAbilities(null); manaUIManager?.DisplayMana(null); playerHealthUIManager?.DisplayHealth(null);
            return;
        }

        if (levelDebugUI != null) { levelDebugUI.DisplayPartyStats(partyManager); }

        CharacterRoot root = ActivePlayer.GetComponent<CharacterRoot>();
        if (root == null) { Debug.LogError($"The current player '{ActivePlayer.name}' is missing a CharacterRoot component! UI cannot be updated.", ActivePlayer); return; }

        // Using CharacterRoot properties for direct component access
        ActivePlayerInventory = root.Inventory;
        ActivePlayerStats = root.PlayerStats;
        ActivePlayerAbilityHolder = root.PlayerAbilityHolder;
        PlayerHotbar playerHotbar = root.GetComponentInChildren<PlayerHotbar>(true); // Hotbar is not a core component on the root yet
        Health playerHealth = root.Health;

        // Update all relevant UI managers with the new components
        inventoryManager?.DisplayInventory(ActivePlayerInventory, ActivePlayer);
        hotbarManager?.DisplayHotbar(playerHotbar, ActivePlayerInventory, ActivePlayerAbilityHolder, ActivePlayerStats);
        equipmentManager?.DisplayEquipmentForPlayer(ActivePlayer);
        statsUIManager?.DisplayStatsAndClass(ActivePlayerStats, playerHealth);
        levelUIManager?.DisplayLevelInfo(ActivePlayerStats);
        abilityBookManager?.DisplayAbilities(ActivePlayerStats);
        manaUIManager?.DisplayMana(ActivePlayerStats);
        playerHealthUIManager?.DisplayHealth(playerHealth);

        if (skillTreePanel != null && skillTreePanel.activeSelf) { if (skillTreeUIManager != null) { skillTreeUIManager.DisplaySkillTree(ActivePlayerStats); } }
        ActivePlayerStats?.CalculateFinalStats();
    }

    // ... (rest of the script is unchanged) ...
    #region Unchanged Code
    void Update()
    {
        if (GameManager.instance != null && GameManager.instance.currentSceneType == SceneType.MainMenu) { return; }
        if (uiManagerObject != null) { if (Variables.Object(uiManagerObject).IsDefined(tradeInProgressVariableName)) { object tradeFlag = Variables.Object(uiManagerObject).Get(tradeInProgressVariableName); if (tradeFlag is bool && (bool)tradeFlag) { return; } } }
        if (Input.GetKeyDown(KeyCode.I)) ToggleInventoryPanel();
        if (Input.GetKeyDown(KeyCode.P)) ToggleAbilityBookPanel();
        if (Input.GetKeyDown(KeyCode.K)) TogglePartyInventoryPanel();
        if (Input.GetKeyDown(KeyCode.N)) ToggleSkillTreePanel();
    }
    public void CloseAllPanels() { foreach (var panel in allToggleablePanels) { if (panel.activeSelf) { if (panel == skillTreePanel) skillTreeUIManager.HideSkillTree(); if (panel == partyInventoryPanel) OnPartyInventoryToggled?.Invoke(false); panel.SetActive(false); } } if (TooltipManager.instance != null) TooltipManager.instance.HideTooltip(); }
    private void TogglePanel(GameObject panelToToggle) { bool isOpening = !panelToToggle.activeSelf; foreach (var panel in allToggleablePanels) { if (panel != panelToToggle && panel.activeSelf) { if (panel == partyInventoryPanel) OnPartyInventoryToggled?.Invoke(false); if (panel == skillTreePanel) skillTreeUIManager.HideSkillTree(); panel.SetActive(false); } } panelToToggle.SetActive(isOpening); if (panelToToggle == partyInventoryPanel) OnPartyInventoryToggled?.Invoke(isOpening); if (!isOpening && TooltipManager.instance != null) { TooltipManager.instance.HideTooltip(); } }
    public void ToggleInventoryPanel() { if (inventoryPanel != null) TogglePanel(inventoryPanel); }
    public void ToggleAbilityBookPanel() { if (abilityBookPanel != null) TogglePanel(abilityBookPanel); }
    public void TogglePartyInventoryPanel() { if (partyInventoryPanel != null) TogglePanel(partyInventoryPanel); }
    public void ToggleSkillTreePanel() { if (skillTreePanel == null || skillTreeUIManager == null) return; bool isCurrentlyOpen = skillTreePanel.activeSelf; if (isCurrentlyOpen) { skillTreeUIManager.HideSkillTree(); skillTreePanel.SetActive(false); if (TooltipManager.instance != null) TooltipManager.instance.HideTooltip(); } else { CloseAllPanels(); GameObject currentPlayer = PartyManager.instance.ActivePlayer; if (currentPlayer == null) return; PlayerStats stats = currentPlayer.GetComponentInChildren<PlayerStats>(); if (stats == null) return; skillTreeUIManager.DisplaySkillTree(stats); skillTreePanel.SetActive(true); } }
    #endregion
}