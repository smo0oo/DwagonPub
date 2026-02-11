using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class WagonWorkshopUI : MonoBehaviour
{
    public static WagonWorkshopUI instance;

    [Header("Configuration")]
    public string resourcesPath = "WagonUpgrades";

    [Header("Main Panels")]
    public GameObject workshopWindow;
    public Transform partsListContainer;
    public GameObject partSlotPrefab;

    [Header("Environment")]
    [Tooltip("Assign a parent object containing lights/props for the workshop scene.")]
    public GameObject workshopEnvironment; // [NEW]

    [Header("Details Panel")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemDescText;
    public TextMeshProUGUI itemStatsText;
    public TextMeshProUGUI playerGoldText;
    public Button buyEquipButton;
    public TextMeshProUGUI buyButtonText;

    private List<WagonUpgradeData> allUpgradesDatabase;
    private List<WagonPartSlotUI> instantiatedSlots = new List<WagonPartSlotUI>();
    private WagonUpgradeType currentCategory = WagonUpgradeType.Wheel;
    private WagonUpgradeData selectedUpgrade;
    private System.Action onCloseCallback;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        allUpgradesDatabase = Resources.LoadAll<WagonUpgradeData>(resourcesPath).ToList();

        if (workshopWindow != null) workshopWindow.SetActive(false);
        if (workshopEnvironment != null) workshopEnvironment.SetActive(false); // Ensure hidden at start
    }

    void Update()
    {
        if (IsShopOpen())
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseWorkshop();
        }
    }

    // [NEW] Helper for other scripts
    public bool IsShopOpen()
    {
        return workshopWindow != null && workshopWindow.activeSelf;
    }

    public void OpenWorkshop(System.Action onClose = null)
    {
        onCloseCallback = onClose;
        workshopWindow.SetActive(true);

        // [NEW] Activate the showroom props
        if (workshopEnvironment != null) workshopEnvironment.SetActive(true);

        if (GameManager.instance != null)
        {
            GameManager.instance.SetPlayerMovementComponentsActive(false);
        }

        RefreshGoldDisplay();
        SwitchCategory(WagonUpgradeType.Wheel);
    }

    public void CloseWorkshop()
    {
        workshopWindow.SetActive(false);

        // [NEW] Deactivate the showroom props
        if (workshopEnvironment != null) workshopEnvironment.SetActive(false);

        if (GameManager.instance != null)
        {
            GameManager.instance.SetPlayerMovementComponentsActive(true);
        }

        onCloseCallback?.Invoke();
    }

    // ... (Rest of the script: SwitchCategory, RefreshPartsList, OnBuyClicked, etc. remains the same) ...
    // Paste the previous SwitchCategory/RefreshPartsList/OnBuyClicked code here if you are replacing the whole file.

    // --- COPY OF THE PREVIOUS METHODS FOR COMPLETENESS ---
    public void SwitchCategoryInt(int categoryIndex) { SwitchCategory((WagonUpgradeType)categoryIndex); }

    public void SwitchCategory(WagonUpgradeType type)
    {
        currentCategory = type;
        RefreshPartsList();
        WagonUpgradeData installed = WagonManager.instance.GetInstalledUpgrade(type);
        if (installed != null) SelectPartData(installed);
        else ClearSelection();
    }

    private void RefreshPartsList()
    {
        foreach (var slot in instantiatedSlots) if (slot != null) Destroy(slot.gameObject);
        instantiatedSlots.Clear();
        if (allUpgradesDatabase == null) return;

        List<WagonUpgradeData> categoryParts = allUpgradesDatabase
            .Where(u => u.type == currentCategory).OrderBy(u => u.goldCost).ToList();
        WagonUpgradeData installed = WagonManager.instance.GetInstalledUpgrade(currentCategory);

        foreach (var part in categoryParts)
        {
            if (partSlotPrefab == null) continue;
            GameObject go = Instantiate(partSlotPrefab, partsListContainer);
            WagonPartSlotUI slotUI = go.GetComponent<WagonPartSlotUI>();
            bool isEquipped = (installed != null && installed.id == part.id);
            bool isOwned = WagonManager.instance.IsPartOwned(part.id);
            slotUI.Setup(part, this, isEquipped, isOwned);
            instantiatedSlots.Add(slotUI);
        }
    }

    public void SelectPart(WagonPartSlotUI slot)
    {
        foreach (var s in instantiatedSlots) s.SetSelected(false);
        slot.SetSelected(true);
        SelectPartData(slot.GetData());
    }

    private void SelectPartData(WagonUpgradeData data)
    {
        selectedUpgrade = data;
        if (itemNameText) itemNameText.text = data.upgradeName;
        if (itemDescText) itemDescText.text = data.description;

        string stats = "";
        if (data.speedBonus > 0) stats += $"Speed: +{data.speedBonus}\n";
        if (data.efficiencyBonus > 0) stats += $"Efficiency: +{data.efficiencyBonus}%\n";
        if (data.storageSlotsAdded > 0) stats += $"Storage: +{data.storageSlotsAdded} Slots\n";
        if (data.defenseBonus > 0) stats += $"Defense: +{data.defenseBonus}\n";
        if (data.comfortBonus > 0) stats += $"Comfort: +{data.comfortBonus}\n";
        if (string.IsNullOrEmpty(stats)) stats = "No Stat Changes";
        if (itemStatsText) itemStatsText.text = stats;

        UpdateBuyButton();
    }

    private void ClearSelection()
    {
        selectedUpgrade = null;
        if (itemNameText) itemNameText.text = "Select a Part";
        if (itemDescText) itemDescText.text = "";
        if (itemStatsText) itemStatsText.text = "";
        if (buyEquipButton) buyEquipButton.interactable = false;
        if (buyButtonText) buyButtonText.text = "-";
    }

    private void UpdateBuyButton()
    {
        if (selectedUpgrade == null || WagonManager.instance == null) return;
        WagonUpgradeData installed = WagonManager.instance.GetInstalledUpgrade(currentCategory);

        if (installed != null && installed.id == selectedUpgrade.id)
        {
            buyButtonText.text = "Equipped";
            buyEquipButton.interactable = false;
            return;
        }

        if (WagonManager.instance.IsPartOwned(selectedUpgrade.id))
        {
            buyButtonText.text = "Equip";
            buyEquipButton.interactable = true;
            return;
        }

        int currentGold = (PartyManager.instance != null) ? PartyManager.instance.currencyGold : 0;
        bool canAfford = currentGold >= selectedUpgrade.goldCost;
        if (selectedUpgrade.goldCost == 0)
        {
            buyButtonText.text = "Install";
            buyEquipButton.interactable = true;
        }
        else
        {
            buyButtonText.text = canAfford ? $"Buy ({selectedUpgrade.goldCost} G)" : "Not Enough Gold";
            buyEquipButton.interactable = canAfford;
        }
    }

    public void OnBuyClicked()
    {
        if (selectedUpgrade == null || WagonManager.instance == null) return;

        if (WagonManager.instance.IsPartOwned(selectedUpgrade.id))
        {
            WagonManager.instance.InstallUpgrade(selectedUpgrade);
            RefreshPartsList();
            SelectPartData(selectedUpgrade);
        }
        else
        {
            int cost = selectedUpgrade.goldCost;
            if (PartyManager.instance != null && PartyManager.instance.currencyGold >= cost)
            {
                PartyManager.instance.currencyGold -= cost;
                RefreshGoldDisplay();
                WagonManager.instance.InstallUpgrade(selectedUpgrade);
                RefreshPartsList();
                SelectPartData(selectedUpgrade);
            }
        }
    }

    private void RefreshGoldDisplay()
    {
        if (playerGoldText && PartyManager.instance != null)
            playerGoldText.text = $"{PartyManager.instance.currencyGold} G";
    }
}