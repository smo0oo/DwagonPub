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

    // --- REMOVED: Redundant local tooltip references ---
    // The TradeManager should not manage UI text fields directly if TooltipManager exists.

    [Header("Context Menu")]
    public TradeContextMenu contextMenu;

    // --- State ---
    private Inventory playerInventory;
    private Inventory npcInventory;
    private GameObject currentPlayerObject;
    private GameObject npcObject;
    private List<TradeSlot> playerSlots = new List<TradeSlot>();
    private List<TradeSlot> npcSlots = new List<TradeSlot>();

    // Drag State
    private Image draggedItemImage;
    private ItemStack draggedItemStack;
    private TradeSlot originalSlot;
    private bool dropWasSuccessful;
    private TradeSlot currentlyHoveredSlot;

    // Buyback State
    private List<ItemData> sessionBuybackItems = new List<ItemData>();

    void Awake() { if (instance != null && instance != this) { Destroy(gameObject); } else { instance = this; } }
    void Start() { InitializeDraggedItem(); tradePanel.SetActive(false); if (contextMenu != null) contextMenu.Initialize(this); HideTooltip(); if (playerSortDropdown != null) { playerSortDropdown.onValueChanged.AddListener(OnSortValueChanged); } }

    // Update is no longer needed for tooltip positioning since TooltipManager handles it
    void Update() { }

    public void StartTradeSession(Inventory pInventory, Inventory nInventory, GameObject playerObject)
    {
        if (inventoryUIController != null) { inventoryUIController.CloseAllPanels(); }
        SetTradeInProgressFlag(true);

        this.playerInventory = pInventory;
        this.npcInventory = nInventory;
        this.currentPlayerObject = playerObject;
        this.npcObject = nInventory.transform.root.gameObject;

        sessionBuybackItems.Clear();

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

        if (npcInventory != null && sessionBuybackItems.Count > 0)
        {
            foreach (var itemData in sessionBuybackItems)
            {
                int index = npcInventory.FindItemIndex(itemData);
                if (index != -1)
                {
                    npcInventory.RemoveItem(index, npcInventory.items[index].quantity);
                }
            }
        }
        sessionBuybackItems.Clear();

        tradePanel.SetActive(false);
        SetTradeInProgressFlag(false);
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

            playerSlots[i].SetAffordability(true);
        }

        int playerGold = GetCurrency(PartyManager.instance.gameObject);
        for (int i = 0; i < npcSlots.Count; i++)
        {
            if (i < npcInventory.items.Count)
            {
                ItemStack item = npcInventory.items[i];
                npcSlots[i].UpdateSlot(item);

                if (item != null && item.itemData != null)
                {
                    int cost = Mathf.CeilToInt(item.itemData.itemValue * buyPriceMultiplier);
                    npcSlots[i].SetAffordability(playerGold >= cost);
                }
                else
                {
                    npcSlots[i].SetAffordability(true);
                }
            }
            else
            {
                npcSlots[i].UpdateSlot(null);
                npcSlots[i].SetAffordability(true);
            }
        }

        if (playerCurrencyText != null) { playerCurrencyText.text = $"Gold: {GetCurrency(PartyManager.instance.gameObject):N0}"; }
        if (npcCurrencyText != null) { npcCurrencyText.text = $"Gold: {GetCurrency(npcObject):N0}"; }
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

    #region Trade Operations

    public void SellItem(TradeSlot slot)
    {
        if (!slot.isPlayerSlot) return;
        ItemStack itemToSell = playerInventory.items[slot.slotIndex];
        if (itemToSell == null || itemToSell.itemData == null) return;

        int totalStackValue = itemToSell.itemData.itemValue * itemToSell.quantity;
        if (GetCurrency(npcObject) < totalStackValue)
        {
            if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowEvent("Merchant cannot afford this!", npcSlotsParent.position);
            return;
        }

        if (npcInventory.AddItem(itemToSell.itemData, itemToSell.quantity))
        {
            if (!sessionBuybackItems.Contains(itemToSell.itemData))
            {
                sessionBuybackItems.Add(itemToSell.itemData);
            }

            playerInventory.RemoveItem(slot.slotIndex, itemToSell.quantity);
            ModifyCurrency(PartyManager.instance.gameObject, +totalStackValue);
            ModifyCurrency(npcObject, -totalStackValue);
            RefreshTradeUI();
        }
    }

    public void BuyItem(TradeSlot slot)
    {
        if (slot.isPlayerSlot) return;
        ItemStack itemToBuy = npcInventory.items[slot.slotIndex];
        if (itemToBuy == null || itemToBuy.itemData == null) return;

        int baseValue = itemToBuy.itemData.itemValue * itemToBuy.quantity;
        int finalPrice = Mathf.CeilToInt(baseValue * buyPriceMultiplier);

        if (GetCurrency(PartyManager.instance.gameObject) < finalPrice)
        {
            if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowEvent("Not enough Gold!", playerSlotsParent.position);
            return;
        }

        if (playerInventory.AddItem(itemToBuy.itemData, itemToBuy.quantity))
        {
            npcInventory.RemoveItem(slot.slotIndex, itemToBuy.quantity);
            ModifyCurrency(PartyManager.instance.gameObject, -finalPrice);
            ModifyCurrency(npcObject, +finalPrice);
            RefreshTradeUI();
        }
        else
        {
            if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowEvent("Inventory Full!", playerSlotsParent.position);
        }
    }

    public void HandleShiftClick(TradeSlot slot)
    {
        if (slot.isPlayerSlot) SellItem(slot);
        else BuyItem(slot);
    }

    #endregion

    #region Drag & Drop

    public void HandleBeginDrag(TradeSlot slot, PointerEventData eventData)
    {
        Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory;
        if (sourceInventory.items[slot.slotIndex].itemData == null) return;

        originalSlot = slot;
        dropWasSuccessful = false;
        draggedItemStack = new ItemStack(sourceInventory.items[slot.slotIndex].itemData, sourceInventory.items[slot.slotIndex].quantity);
        sourceInventory.SetItemStack(slot.slotIndex, new ItemStack(null, 0));

        SetDragInProgressFlag(true);
        draggedItemImage.sprite = draggedItemStack.itemData.icon;
        draggedItemImage.color = Color.white;
        draggedItemImage.gameObject.SetActive(true);
        draggedItemImage.transform.position = Input.mousePosition;
    }

    public void HandleDrag(PointerEventData eventData)
    {
        if (draggedItemImage != null && draggedItemImage.gameObject.activeSelf)
        {
            draggedItemImage.transform.position = Input.mousePosition;
        }
    }

    public void HandleDrop(TradeSlot toSlot)
    {
        if (draggedItemStack == null) return;

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
        {
            if (stackSplitter != null && draggedItemStack.quantity > 1)
            {
                ReturnItemToOriginalSlot();
                draggedItemImage.gameObject.SetActive(false);
                SetDragInProgressFlag(false);

                Inventory sourceInv = originalSlot.isPlayerSlot ? playerInventory : npcInventory;
                ItemStack restoredStack = sourceInv.items[originalSlot.slotIndex];

                stackSplitter.Open(restoredStack, (splitAmount) =>
                {
                    PerformSplitTrade(originalSlot, toSlot, splitAmount);
                });

                return;
            }
        }

        PerformTradeMove(toSlot, draggedItemStack.quantity);
    }

    private void PerformTradeMove(TradeSlot toSlot, int quantity)
    {
        if (originalSlot.isPlayerSlot && !toSlot.isPlayerSlot)
        {
            int totalValue = draggedItemStack.itemData.itemValue * quantity;
            if (GetCurrency(npcObject) >= totalValue)
            {
                if (npcInventory.AddItem(draggedItemStack.itemData, quantity))
                {
                    if (!sessionBuybackItems.Contains(draggedItemStack.itemData)) sessionBuybackItems.Add(draggedItemStack.itemData);

                    ModifyCurrency(PartyManager.instance.gameObject, +totalValue);
                    ModifyCurrency(npcObject, -totalValue);
                    dropWasSuccessful = true;
                    RefreshTradeUI();
                }
            }
            else
            {
                if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowEvent("Merchant cannot afford this!", npcSlotsParent.position);
            }
        }
        else if (!originalSlot.isPlayerSlot && toSlot.isPlayerSlot)
        {
            int baseVal = draggedItemStack.itemData.itemValue * quantity;
            int cost = Mathf.CeilToInt(baseVal * buyPriceMultiplier);

            if (GetCurrency(PartyManager.instance.gameObject) >= cost)
            {
                if (playerInventory.AddItem(draggedItemStack.itemData, quantity))
                {
                    ModifyCurrency(PartyManager.instance.gameObject, -cost);
                    ModifyCurrency(npcObject, +cost);
                    dropWasSuccessful = true;
                    RefreshTradeUI();
                }
                else
                {
                    if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowEvent("Inventory Full!", playerSlotsParent.position);
                }
            }
            else
            {
                if (FloatingTextManager.instance != null) FloatingTextManager.instance.ShowEvent("Not enough Gold!", playerSlotsParent.position);
            }
        }
        else if (originalSlot.isPlayerSlot == toSlot.isPlayerSlot)
        {
            Inventory targetInventory = toSlot.isPlayerSlot ? playerInventory : npcInventory;
            ItemStack targetStack = targetInventory.items[toSlot.slotIndex];

            if (targetStack.itemData == null)
            {
                targetInventory.SetItemStack(toSlot.slotIndex, draggedItemStack);
            }
            else if (targetStack.itemData == draggedItemStack.itemData && targetStack.itemData.isStackable)
            {
                int space = targetStack.itemData.GetMaxStackSize() - targetStack.quantity;
                int transfer = Mathf.Min(draggedItemStack.quantity, space);
                targetStack.quantity += transfer;
                draggedItemStack.quantity -= transfer;

                targetInventory.SetItemStack(toSlot.slotIndex, targetStack);

                if (draggedItemStack.quantity > 0)
                {
                    Inventory sourceInv = originalSlot.isPlayerSlot ? playerInventory : npcInventory;
                    sourceInv.SetItemStack(originalSlot.slotIndex, draggedItemStack);
                }
            }
            else
            {
                targetInventory.SetItemStack(originalSlot.slotIndex, targetStack);
                targetInventory.SetItemStack(toSlot.slotIndex, draggedItemStack);
            }
            dropWasSuccessful = true;
            draggedItemStack = null;
        }
    }

    private void PerformSplitTrade(TradeSlot source, TradeSlot dest, int amount)
    {
        Inventory sourceInv = source.isPlayerSlot ? playerInventory : npcInventory;
        Inventory destInv = dest.isPlayerSlot ? playerInventory : npcInventory;

        ItemStack sourceStack = sourceInv.items[source.slotIndex];
        if (sourceStack == null || sourceStack.quantity < amount) return;

        if (source.isPlayerSlot && !dest.isPlayerSlot) // Selling Split
        {
            int val = sourceStack.itemData.itemValue * amount;
            if (GetCurrency(npcObject) < val) { return; }

            if (destInv.AddItem(sourceStack.itemData, amount))
            {
                sourceInv.RemoveItem(source.slotIndex, amount);
                ModifyCurrency(PartyManager.instance.gameObject, +val);
                ModifyCurrency(npcObject, -val);

                if (!sessionBuybackItems.Contains(sourceStack.itemData)) sessionBuybackItems.Add(sourceStack.itemData);
            }
        }
        else if (!source.isPlayerSlot && dest.isPlayerSlot) // Buying Split
        {
            int baseVal = sourceStack.itemData.itemValue * amount;
            int cost = Mathf.CeilToInt(baseVal * buyPriceMultiplier);
            if (GetCurrency(PartyManager.instance.gameObject) < cost) { return; }

            if (destInv.AddItem(sourceStack.itemData, amount))
            {
                sourceInv.RemoveItem(source.slotIndex, amount);
                ModifyCurrency(PartyManager.instance.gameObject, -cost);
                ModifyCurrency(npcObject, +cost);
            }
        }

        RefreshTradeUI();
    }

    public void HandleEndDrag(PointerEventData eventData)
    {
        if (draggedItemImage != null && draggedItemImage.gameObject.activeSelf)
        {
            if (!dropWasSuccessful && draggedItemStack != null)
            {
                ReturnItemToOriginalSlot();
            }
            draggedItemImage.gameObject.SetActive(false);
        }
        draggedItemStack = null;
        SetDragInProgressFlag(false);
    }

    private void ReturnItemToOriginalSlot()
    {
        if (originalSlot == null || draggedItemStack == null) return;
        Inventory sourceInventory = originalSlot.isPlayerSlot ? playerInventory : npcInventory;
        sourceInventory.AddItem(draggedItemStack.itemData, draggedItemStack.quantity);
    }

    #endregion

    #region Helpers & Initializers
    private void InitializeDraggedItem() { if (draggedItemPrefab == null) return; Canvas parentCanvas = GetComponentInParent<Canvas>(); GameObject draggedItemGO = Instantiate(draggedItemPrefab, parentCanvas.transform); draggedItemImage = draggedItemGO.GetComponent<Image>(); draggedItemGO.SetActive(false); }
    private void SetDragInProgressFlag(bool value) { if (uiManagerObject != null) { Variables.Object(uiManagerObject).Set(dragInProgressVariableName, value); } }
    private void SetTradeInProgressFlag(bool value) { if (uiManagerObject != null) { if (!Variables.Object(uiManagerObject).IsDefined(tradeInProgressVariableName)) { Variables.Object(uiManagerObject).Set(tradeInProgressVariableName, false); } Variables.Object(uiManagerObject).Set(tradeInProgressVariableName, value); } }
    private void OnSortValueChanged(int index) { if (playerInventory != null) { playerInventory.SortItems((Inventory.SortType)index); } }

    private void InitializeSlots()
    {
        foreach (Transform child in playerSlotsParent) Destroy(child.gameObject);
        foreach (Transform child in npcSlotsParent) Destroy(child.gameObject);
        playerSlots.Clear();
        npcSlots.Clear();
        for (int i = 0; i < playerInventory.inventorySize; i++) { GameObject slotGO = Instantiate(tradeSlotPrefab, playerSlotsParent); TradeSlot newSlot = slotGO.GetComponent<TradeSlot>(); newSlot.Initialize(this, i, true); playerSlots.Add(newSlot); }
        for (int i = 0; i < npcInventory.inventorySize; i++) { GameObject slotGO = Instantiate(tradeSlotPrefab, npcSlotsParent); TradeSlot newSlot = slotGO.GetComponent<TradeSlot>(); newSlot.Initialize(this, i, false); npcSlots.Add(newSlot); }
    }

    // --- FIX: Delegate to TooltipManager to handle everything consistently ---
    public void ShowTooltip(TradeSlot slot)
    {
        if (TooltipManager.instance == null) return;

        Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory;
        if (sourceInventory == null) return;

        ItemStack itemStack = sourceInventory.items[slot.slotIndex];
        if (itemStack == null || itemStack.itemData == null) return;

        // Fetch PlayerStats for DPS calculations (optional)
        PlayerStats stats = null;
        if (currentPlayerObject != null) stats = currentPlayerObject.GetComponent<PlayerStats>();

        // Call the manager that works for Inventory
        TooltipManager.instance.ShowItemTooltip(itemStack, true, stats);

        currentlyHoveredSlot = slot;
    }

    public void HideTooltip()
    {
        if (TooltipManager.instance != null)
        {
            TooltipManager.instance.HideTooltip();
        }
        currentlyHoveredSlot = null;
    }

    // ------------------------------------------------------------------------

    public void OpenContextMenu(TradeSlot slot) { if (contextMenu != null) { contextMenu.Open(slot); } }

    public void SplitStack(TradeSlot slot, int splitAmount) { if (splitAmount <= 0) return; Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory; ItemStack originalStack = sourceInventory.items[slot.slotIndex]; if (originalStack == null || !originalStack.itemData.isStackable || originalStack.quantity <= splitAmount) return; if (sourceInventory.AddItemToNewStack(originalStack.itemData, splitAmount)) { sourceInventory.RemoveItem(slot.slotIndex, splitAmount); } }
    public void DestroyItem(TradeSlot slot) { if (!slot.isPlayerSlot) return; playerInventory.RemoveItem(slot.slotIndex, playerInventory.items[slot.slotIndex].quantity); }
    public void DropItem(TradeSlot slot) { if (!slot.isPlayerSlot || worldItemPrefab == null || currentPlayerObject == null) return; ItemStack itemToDrop = playerInventory.items[slot.slotIndex]; if (itemToDrop == null || itemToDrop.itemData == null) return; GameObject droppedItemGO = Instantiate(worldItemPrefab, currentPlayerObject.transform.position, Quaternion.identity); WorldItem worldItem = droppedItemGO.GetComponent<WorldItem>(); if (worldItem != null) { worldItem.itemData = itemToDrop.itemData; worldItem.quantity = itemToDrop.quantity; } playerInventory.RemoveItem(slot.slotIndex, itemToDrop.quantity); }
    public void OpenStackSplitter(TradeSlot slot) { if (stackSplitter != null) { Inventory sourceInventory = slot.isPlayerSlot ? playerInventory : npcInventory; ItemStack stackToSplit = sourceInventory.items[slot.slotIndex]; stackSplitter.Open(stackToSplit, (splitAmount) => { SplitStack(slot, splitAmount); }); } }

    public Inventory GetPlayerInventory() => playerInventory;
    public Inventory GetNpcInventory() => npcInventory;
    #endregion
}