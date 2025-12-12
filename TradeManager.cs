using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Unity.VisualScripting;
using System;
using System.Collections;
using TMPro;

public class TradeManager : MonoBehaviour
{
    public static TradeManager instance;

    [Header("Game State References")]
    public GameObject worldItemPrefab;
    public StackSplitter stackSplitter;
    public InventoryUIController inventoryUIController;
    public PlayerEquipment playerEquipment;

    [Header("UI Settings")]
    public TMP_Dropdown playerSortDropdown;
    [Header("Currency Settings")]
    public float buyPriceMultiplier = 1.5f;
    [Header("Currency Display")]
    public TextMeshProUGUI playerCurrencyText;
    public TextMeshProUGUI npcCurrencyText;
    [Header("Visual Scripting Hooks")]
    public GameObject uiManagerObject;
    public string dragInProgressVariableName = "UIDragInProgress";
    public string tradeInProgressVariableName = "TradeInProgress";
    [Header("UI Panels")]
    public GameObject tradePanel;
    [Header("Slot Containers")]
    public Transform playerSlotsParent;
    public Transform npcSlotsParent;
    [Header("UI Prefabs")]
    public GameObject tradeSlotPrefab;
    public GameObject draggedItemPrefab;
    [Header("Tooltip")]
    public TextMeshProUGUI tooltipNameText;
    public TextMeshProUGUI tooltipRarityText;
    public TextMeshProUGUI tooltipDescriptionText;
    public GameObject tooltipPanel;
    [Header("Context Menu")]
    public TradeContextMenu contextMenu;

    private Inventory playerInventory;
    private Inventory npcInventory;
    private GameObject currentPlayerObject;
    private GameObject npcObject;
    private List<TradeSlot> playerSlots = new List<TradeSlot>();
    private List<TradeSlot> npcSlots = new List<TradeSlot>();
    private Image draggedItemImage;
    private ItemStack draggedItemStack;
    private TradeSlot originalSlot;
    private bool dropWasSuccessful;
    private TradeSlot currentlyHoveredSlot;

    void Awake() { if (instance != null && instance != this) { Destroy(gameObject); } else { instance = this; } }
    void Start() { InitializeDraggedItem(); tradePanel.SetActive(false); if (contextMenu != null) contextMenu.Initialize(this); HideTooltip(); if (playerSortDropdown != null) { playerSortDropdown.onValueChanged.AddListener(OnSortValueChanged); } }
    void Update() { if (currentlyHoveredSlot != null && tooltipPanel.activeSelf) { UpdateTooltipPosition(); } }

    public void StartTradeSession(Inventory pInventory, Inventory nInventory, GameObject playerObject)
    {
        if (inventoryUIController != null) { inventoryUIController.CloseAllPanels(); }
        SetTradeInProgressFlag(true);

        this.playerInventory = pInventory;
        this.npcInventory = nInventory;
        this.currentPlayerObject = playerObject;
        this.npcObject = nInventory.transform.root.gameObject;

        playerInventory.OnInventoryChanged += RefreshTradeUI;
        npcInventory.OnInventoryChanged += RefreshTradeUI;

        tradePanel.SetActive(true);
        if (playerSortDropdown != null) playerSortDropdown.value = 0;

        StartCoroutine(SetupTradeUISequence());
    }

    private IEnumerator SetupTradeUISequence()
    {
        InitializeSlots();
        yield return new WaitForEndOfFrame();
        RefreshTradeUI();
    }

    public void EndTradeSession()
    {
        StopAllCoroutines();
        tradePanel.SetActive(false);
        SetTradeInProgressFlag(false); // <-- THIS IS THE FIX
        if (contextMenu != null) contextMenu.Close();
        if (playerInventory != null) playerInventory.OnInventoryChanged -= RefreshTradeUI;
        if (npcInventory != null) npcInventory.OnInventoryChanged -= RefreshTradeUI;
        if (playerCurrencyText != null) playerCurrencyText.text = "";
        if (npcCurrencyText != null) npcCurrencyText.text = "";
        this.playerInventory = null;
        this.npcInventory = null;
        this.currentPlayerObject = null;
        this.npcObject = null;
        HideTooltip();
    }

    private void RefreshTradeUI()
    {
        if (playerInventory == null || npcInventory == null) return;
        for (int i = 0; i < playerSlots.Count; i++)
        {
            if (i < playerInventory.items.Count) playerSlots[i].UpdateSlot(playerInventory.items[i]);
            else playerSlots[i].UpdateSlot(null);
        }
        for (int i = 0; i < npcSlots.Count; i++)
        {
            if (i < npcInventory.items.Count) npcSlots[i].UpdateSlot(npcInventory.items[i]);
            else npcSlots[i].UpdateSlot(null);
        }
        if (playerCurrencyText != null) { playerCurrencyText.text = $"Gold: {GetCurrency(PartyManager.instance.gameObject)}"; }
        if (npcCurrencyText != null) { npcCurrencyText.text = $"Gold: {GetCurrency(npcObject)}"; }
    }

    private int GetCurrency(GameObject target)
    {
        if (target == null) return 0;
        if (PartyManager.instance != null && target == PartyManager.instance.gameObject)
        {
            return PartyManager.instance.currencyGold;
        }
        NPCData npcData = target.GetComponentInChildren<NPCData>(true);
        if (npcData != null) { return npcData.currencyGold; }
        Debug.LogWarning($"Could not determine currency for {target.name}.", target);
        return 0;
    }

    private void ModifyCurrency(GameObject target, int amount)
    {
        if (target == null) return;
        if (PartyManager.instance != null && target == PartyManager.instance.gameObject)
        {
            PartyManager.instance.currencyGold += amount;
            return;
        }
        NPCData npcData = target.GetComponentInChildren<NPCData>(true);
        if (npcData != null) { npcData.currencyGold += amount; }
    }

    #region Unchanged Methods
    private void SetDragInProgressFlag(bool value) { if (uiManagerObject != null) { Variables.Object(uiManagerObject).Set(dragInProgressVariableName, value); } }
    private void SetTradeInProgressFlag(bool value) { if (uiManagerObject != null) { if (!Variables.Object(uiManagerObject).IsDefined(tradeInProgressVariableName)) { Variables.Object(uiManagerObject).Set(tradeInProgressVariableName, false); } Variables.Object(uiManagerObject).Set(tradeInProgressVariableName, value); } }
    private void OnSortValueChanged(int index) { if (playerInventory != null) { playerInventory.SortItems((Inventory.SortType)index); } }
    private void InitializeSlots() { foreach (Transform child in playerSlotsParent) Destroy(child.gameObject); foreach (Transform child in npcSlotsParent) Destroy(child.gameObject); playerSlots.Clear(); npcSlots.Clear(); for (int i = 0; i < playerInventory.inventorySize; i++) { GameObject slotGO = Instantiate(tradeSlotPrefab, playerSlotsParent); TradeSlot newSlot = slotGO.GetComponent<TradeSlot>(); newSlot.Initialize(this, i, true); playerSlots.Add(newSlot); } for (int i = 0; i < npcInventory.inventorySize; i++) { GameObject slotGO = Instantiate(tradeSlotPrefab, npcSlotsParent); TradeSlot newSlot = slotGO.GetComponent<TradeSlot>(); newSlot.Initialize(this, i, false); npcSlots.Add(newSlot); } }
    public void SellEquippedItem(EquipmentType slotType) { if (playerEquipment == null) return; ItemStack itemToSell = playerEquipment.equippedItems[slotType]; if (itemToSell == null || itemToSell.itemData == null) return; int totalStackValue = itemToSell.itemData.itemValue * itemToSell.quantity; if (GetCurrency(npcObject) < totalStackValue) { return; } if (npcInventory.AddItem(itemToSell.itemData, itemToSell.quantity)) { playerEquipment.RemoveItemFromSlot(slotType); ModifyCurrency(PartyManager.instance.gameObject, +totalStackValue); ModifyCurrency(npcObject, -totalStackValue); RefreshTradeUI(); } }
    public void SellItem(TradeSlot slot) { if (!slot.isPlayerSlot) return; ItemStack itemToSell = playerInventory.items[slot.slotIndex]; if (itemToSell == null || itemToSell.itemData == null) return; int totalStackValue = itemToSell.itemData.itemValue * itemToSell.quantity; if (GetCurrency(npcObject) < totalStackValue) { return; } if (npcInventory.AddItem(itemToSell.itemData, itemToSell.quantity)) { playerInventory.RemoveItem(slot.slotIndex, itemToSell.quantity); ModifyCurrency(PartyManager.instance.gameObject, +totalStackValue); ModifyCurrency(npcObject, -totalStackValue); RefreshTradeUI(); } }
    public void BuyItem(TradeSlot slot) { if (slot.isPlayerSlot) return; ItemStack itemToBuy = npcInventory.items[slot.slotIndex]; if (itemToBuy == null || itemToBuy.itemData == null) return; int baseValue = itemToBuy.itemData.itemValue * itemToBuy.quantity; int finalPrice = Mathf.CeilToInt(baseValue * buyPriceMultiplier); if (GetCurrency(PartyManager.instance.gameObject) < finalPrice) { return; } if (playerInventory.AddItem(itemToBuy.itemData, itemToBuy.quantity)) { npcInventory.RemoveItem(slot.slotIndex, itemToBuy.quantity); ModifyCurrency(PartyManager.instance.gameObject, -finalPrice); ModifyCurrency(npcObject, +finalPrice); RefreshTradeUI(); } }
    public void HandleBeginDrag(TradeSlot slot, PointerEventData eventData) { Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory; if (sourceInventory.items[slot.slotIndex].itemData == null) return; originalSlot = slot; dropWasSuccessful = false; draggedItemStack = new ItemStack(sourceInventory.items[slot.slotIndex].itemData, sourceInventory.items[slot.slotIndex].quantity); sourceInventory.SetItemStack(slot.slotIndex, new ItemStack(null, 0)); SetDragInProgressFlag(true); draggedItemImage.sprite = draggedItemStack.itemData.icon; draggedItemImage.color = Color.white; draggedItemImage.gameObject.SetActive(true); draggedItemImage.transform.position = Input.mousePosition; }
    public void HandleDrag(PointerEventData eventData) { if (draggedItemImage != null && draggedItemImage.gameObject.activeSelf) { draggedItemImage.transform.position = Input.mousePosition; } }
    public void HandleDrop(TradeSlot toSlot) { if (draggedItemStack == null) return; if (originalSlot.isPlayerSlot && !toSlot.isPlayerSlot) { int totalStackValue = draggedItemStack.itemData.itemValue * draggedItemStack.quantity; if (GetCurrency(npcObject) >= totalStackValue) { if (npcInventory.AddItem(draggedItemStack.itemData, draggedItemStack.quantity)) { ModifyCurrency(PartyManager.instance.gameObject, +totalStackValue); ModifyCurrency(npcObject, -totalStackValue); dropWasSuccessful = true; draggedItemStack = null; RefreshTradeUI(); } else { dropWasSuccessful = false; } } else { dropWasSuccessful = false; } return; } if (!originalSlot.isPlayerSlot && toSlot.isPlayerSlot) { int baseValue = draggedItemStack.itemData.itemValue * draggedItemStack.quantity; int finalPrice = Mathf.CeilToInt(baseValue * buyPriceMultiplier); if (GetCurrency(PartyManager.instance.gameObject) >= finalPrice) { if (playerInventory.AddItem(draggedItemStack.itemData, draggedItemStack.quantity)) { ModifyCurrency(PartyManager.instance.gameObject, -finalPrice); ModifyCurrency(npcObject, +finalPrice); dropWasSuccessful = true; draggedItemStack = null; RefreshTradeUI(); } else { dropWasSuccessful = false; } } else { dropWasSuccessful = false; } return; } if (originalSlot.isPlayerSlot == toSlot.isPlayerSlot) { Inventory targetInventory = toSlot.isPlayerSlot ? playerInventory : npcInventory; ItemStack toItemStack = targetInventory.items[toSlot.slotIndex]; if (toItemStack.itemData == null) { targetInventory.SetItemStack(toSlot.slotIndex, draggedItemStack); draggedItemStack = null; } else if (toItemStack.itemData == draggedItemStack.itemData && toItemStack.itemData.isStackable) { int spaceLeft = toItemStack.itemData.GetMaxStackSize() - toItemStack.quantity; int amountToTransfer = Mathf.Min(draggedItemStack.quantity, spaceLeft); toItemStack.quantity += amountToTransfer; draggedItemStack.quantity -= amountToTransfer; targetInventory.SetItemStack(toSlot.slotIndex, toItemStack); } else { targetInventory.SetItemStack(originalSlot.slotIndex, toItemStack); targetInventory.SetItemStack(toSlot.slotIndex, draggedItemStack); draggedItemStack = null; } dropWasSuccessful = true; } }
    public void HandleEndDrag(PointerEventData eventData) { if (draggedItemImage != null && draggedItemImage.gameObject.activeSelf) { if (!dropWasSuccessful && draggedItemStack != null) { ReturnItemToOriginalSlot(); } draggedItemImage.gameObject.SetActive(false); } draggedItemStack = null; SetDragInProgressFlag(false); }
    private void ReturnItemToOriginalSlot() { if (originalSlot == null || draggedItemStack == null) return; Inventory sourceInventory = originalSlot.isPlayerSlot ? playerInventory : npcInventory; sourceInventory.AddItem(draggedItemStack.itemData, draggedItemStack.quantity); }
    private void InitializeDraggedItem() { if (draggedItemPrefab == null) return; Canvas parentCanvas = GetComponentInParent<Canvas>(); GameObject draggedItemGO = Instantiate(draggedItemPrefab, parentCanvas.transform); draggedItemImage = draggedItemGO.GetComponent<Image>(); draggedItemGO.SetActive(false); }
    public void ShowTooltip(TradeSlot slot) { if (tooltipPanel == null) return; Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory; if (sourceInventory == null) return; ItemStack itemStack = sourceInventory.items[slot.slotIndex]; if (itemStack == null || itemStack.itemData == null) return; tooltipNameText.text = itemStack.itemData.displayName; ItemStats stats = itemStack.itemData.stats; if (stats != null) { tooltipRarityText.gameObject.SetActive(true); tooltipRarityText.text = stats.rarity.ToString(); tooltipRarityText.color = GetRarityColor(stats.rarity); } else { tooltipRarityText.gameObject.SetActive(false); } string description = itemStack.itemData.description; if (stats != null) { description += "\n\n" + stats.GetStatsDescription(); } if (itemStack.itemData.itemValue > 0) { int totalStackValue = itemStack.itemData.itemValue * itemStack.quantity; if (slot.isPlayerSlot) { description += $"\n\nSell Value: {totalStackValue}"; } else { int buyPrice = Mathf.CeilToInt(totalStackValue * buyPriceMultiplier); description += $"\n\nBuy Price: {buyPrice}"; } } tooltipDescriptionText.text = description; currentlyHoveredSlot = slot; tooltipPanel.SetActive(true); UpdateTooltipPosition(); }
    public void HideTooltip() { if (tooltipPanel != null) { currentlyHoveredSlot = null; tooltipPanel.SetActive(false); } }
    private void UpdateTooltipPosition() { if (tooltipPanel == null || !tooltipPanel.activeSelf) return; RectTransform tooltipRect = tooltipPanel.GetComponent<RectTransform>(); Vector2 mousePosition = Input.mousePosition; tooltipRect.pivot = new Vector2(mousePosition.x / Screen.width > 0.5f ? 1 : 0, mousePosition.y / Screen.height > 0.5f ? 1 : 0); tooltipRect.position = mousePosition; }
    private Color GetRarityColor(ItemStats.Rarity rarity) { switch (rarity) { case ItemStats.Rarity.Uncommon: return Color.green; case ItemStats.Rarity.Rare: return Color.blue; case ItemStats.Rarity.Epic: return new Color(0.5f, 0f, 0.5f); case ItemStats.Rarity.Legendary: return new Color(1f, 0.84f, 0f); default: return Color.white; } }
    public void OpenContextMenu(TradeSlot slot) { if (contextMenu != null) { contextMenu.Open(slot); } }
    public void SplitStack(TradeSlot slot, int splitAmount) { if (splitAmount <= 0) return; Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory; ItemStack originalStack = sourceInventory.items[slot.slotIndex]; if (originalStack == null || !originalStack.itemData.isStackable || originalStack.quantity <= splitAmount) return; if (sourceInventory.AddItemToNewStack(originalStack.itemData, splitAmount)) { sourceInventory.RemoveItem(slot.slotIndex, splitAmount); } }
    public void DestroyItem(TradeSlot slot) { if (!slot.isPlayerSlot) return; playerInventory.RemoveItem(slot.slotIndex, playerInventory.items[slot.slotIndex].quantity); }
    public void DropItem(TradeSlot slot) { if (!slot.isPlayerSlot || worldItemPrefab == null || currentPlayerObject == null) return; ItemStack itemToDrop = playerInventory.items[slot.slotIndex]; if (itemToDrop == null || itemToDrop.itemData == null) return; GameObject droppedItemGO = Instantiate(worldItemPrefab, currentPlayerObject.transform.position, Quaternion.identity); WorldItem worldItem = droppedItemGO.GetComponent<WorldItem>(); if (worldItem != null) { worldItem.itemData = itemToDrop.itemData; worldItem.quantity = itemToDrop.quantity; } playerInventory.RemoveItem(slot.slotIndex, itemToDrop.quantity); }
    public void OpenStackSplitter(TradeSlot slot) { if (stackSplitter != null) { Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory; ItemStack stackToSplit = sourceInventory.items[slot.slotIndex]; stackSplitter.Open(stackToSplit, (splitAmount) => { SplitStack(slot, splitAmount); }); } }
    public Inventory GetPlayerInventory() => playerInventory;
    public Inventory GetNpcInventory() => npcInventory;
    #endregion
}