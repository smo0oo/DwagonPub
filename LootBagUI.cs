using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System; // Required for Action

public class LootBagUI : MonoBehaviour
{
    // --- NEW: Event to notify other systems (like BattleManager) ---
    public static event Action OnLootBagClosed;

    // --- NEW: Property to check visibility ---
    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    [Header("UI References")]
    public GameObject panelRoot;
    public Transform itemsContainer;
    public Button claimButton;

    [Header("Settings")]
    public GameObject itemSlotPrefab;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(OnClaimClicked);
            claimButton.onClick.AddListener(OnClaimClicked);
        }
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckAndOpenLootBag();
    }

    private void CheckAndOpenLootBag()
    {
        if (GameManager.instance != null && GameManager.instance.justExitedDungeon)
        {
            OpenLootBag();
        }
    }

    public void OpenLootBag()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        RefreshUI();
    }

    private void RefreshUI()
    {
        foreach (Transform child in itemsContainer) Destroy(child.gameObject);

        if (DualModeManager.instance == null) return;

        foreach (var stack in DualModeManager.instance.dungeonLootBag)
        {
            if (stack.itemData != null && itemSlotPrefab != null)
            {
                GameObject slotObj = Instantiate(itemSlotPrefab, itemsContainer);

                Image icon = slotObj.transform.Find("Icon")?.GetComponent<Image>();
                TextMeshProUGUI qty = slotObj.transform.Find("Quantity")?.GetComponent<TextMeshProUGUI>();
                TextMeshProUGUI name = slotObj.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();

                if (icon != null) icon.sprite = stack.itemData.icon;
                if (qty != null) qty.text = stack.quantity > 1 ? stack.quantity.ToString() : "";
                if (name != null) name.text = stack.itemData.displayName;
            }
        }
    }

    private void OnClaimClicked()
    {
        if (DualModeManager.instance != null)
        {
            DualModeManager.instance.FinalizeDungeonRun();
        }

        if (panelRoot != null) panelRoot.SetActive(false);

        // --- NEW: Fire event so DomeBattleManager knows to wake up ---
        OnLootBagClosed?.Invoke();
    }
}