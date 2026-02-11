using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WagonPartSlotUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;

    [Header("Indicators")]
    public GameObject equippedIndicator; // Green Checkmark (Icon)
    public GameObject ownedIndicator;    // Box/Inventory Icon

    [Header("Interaction")]
    public Button selectButton;
    public Image selectionHighlight;

    private WagonUpgradeData myData;
    private WagonWorkshopUI manager;

    public void Setup(WagonUpgradeData data, WagonWorkshopUI uiManager, bool isEquipped, bool isOwned)
    {
        myData = data;
        manager = uiManager;

        if (iconImage != null) iconImage.sprite = data.icon;
        if (nameText != null) nameText.text = data.upgradeName;

        // --- UPDATED TEXT LOGIC ---
        if (costText != null)
        {
            if (isEquipped)
            {
                costText.text = "Equipped";
                costText.color = Color.green; // Distinct color for active item
            }
            else if (isOwned)
            {
                costText.text = "Owned";
                costText.color = Color.white;
            }
            else
            {
                // Not owned, show price
                costText.text = $"{data.goldCost} G";
                costText.color = Color.yellow;
            }
        }

        // 1. Equipped Indicator (The Icon)
        if (equippedIndicator != null)
            equippedIndicator.SetActive(isEquipped);

        // 2. Owned Indicator (The Box)
        // Show if owned BUT NOT equipped (to avoid cluttering the active item)
        if (ownedIndicator != null)
            ownedIndicator.SetActive(isOwned && !isEquipped);

        // Button Setup
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => manager.SelectPart(this));
        }
    }

    public WagonUpgradeData GetData() => myData;

    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null) selectionHighlight.enabled = selected;
    }
}