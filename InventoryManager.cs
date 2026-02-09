using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance { get; private set; }

    [Header("UI Settings")]
    public GameObject inventorySlotPrefab;
    public Transform inventorySlotsParent;
    public ItemContextMenu contextMenu;
    public StackSplitter stackSplitter;
    public TMP_Dropdown sortDropdown;

    [Header("World Item Prefab")]
    public GameObject worldItemPrefab;

    [Header("Player Inventories")]
    public List<Inventory> playerInventories = new List<Inventory>();

    [Header("Currency Display")]
    public TextMeshProUGUI playerCurrencyText;

    [Header("External References")]
    public EquipmentManager equipmentManager;
    public List<ItemData> itemDatabase = new List<ItemData>();

    private Inventory currentlyDisplayedInventory;
    private GameObject currentPlayerObject;
    private List<InventorySlot> uiSlots = new List<InventorySlot>();
    private EquipmentType sourceEquipmentSlot;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    void Start()
    {
        HideTooltip();
        if (playerCurrencyText != null) playerCurrencyText.text = "";
        if (sortDropdown != null) sortDropdown.onValueChanged.AddListener(OnSortValueChanged);
        if (contextMenu != null) { contextMenu.Initialize(this); contextMenu.gameObject.SetActive(false); }
        if (stackSplitter != null) stackSplitter.gameObject.SetActive(false);
    }

    public void DisplayInventory(Inventory inventoryToDisplay, GameObject playerObject)
    {
        if (currentlyDisplayedInventory != null) currentlyDisplayedInventory.OnInventoryChanged -= RefreshUI;

        currentlyDisplayedInventory = inventoryToDisplay;
        currentPlayerObject = playerObject;

        if (currentlyDisplayedInventory != null)
        {
            AdjustSlotCount(currentlyDisplayedInventory.inventorySize);
            currentlyDisplayedInventory.OnInventoryChanged += RefreshUI;
        }

        if (sortDropdown != null) sortDropdown.value = 0;
        RefreshUI();
    }

    private void AdjustSlotCount(int targetSize)
    {
        while (uiSlots.Count < targetSize)
        {
            GameObject slotGO = Instantiate(inventorySlotPrefab, inventorySlotsParent);
            InventorySlot newSlot = slotGO.GetComponent<InventorySlot>();
            newSlot.Initialize(this, uiSlots.Count, currentlyDisplayedInventory);
            uiSlots.Add(newSlot);
        }
        for (int i = 0; i < uiSlots.Count; i++) uiSlots[i].gameObject.SetActive(i < targetSize);
    }

    private void RefreshUI()
    {
        if (currentlyDisplayedInventory == null)
        {
            foreach (var slot in uiSlots) slot.UpdateSlot(null);
            if (playerCurrencyText != null) playerCurrencyText.text = "";
        }
        else
        {
            if (uiSlots.Count < currentlyDisplayedInventory.inventorySize) AdjustSlotCount(currentlyDisplayedInventory.inventorySize);

            for (int i = 0; i < uiSlots.Count; i++)
            {
                if (i < currentlyDisplayedInventory.inventorySize)
                {
                    uiSlots[i].gameObject.SetActive(true);
                    uiSlots[i].Initialize(this, i, currentlyDisplayedInventory);
                    if (i < currentlyDisplayedInventory.items.Count)
                        uiSlots[i].UpdateSlot(currentlyDisplayedInventory.items[i]);
                    else
                        uiSlots[i].UpdateSlot(null);
                }
                else uiSlots[i].gameObject.SetActive(false);
            }

            if (playerCurrencyText != null && PartyManager.instance != null)
                playerCurrencyText.text = $"Gold: {PartyManager.instance.currencyGold}";
        }
    }

    public void DropItem(InventorySlot slot)
    {
        if (worldItemPrefab == null) return;

        // [FIX] Use the slot's specific inventory, fallback to displayed if null
        Inventory targetInv = slot.parentInventory != null ? slot.parentInventory : currentlyDisplayedInventory;
        if (targetInv == null) return;

        // [FIX] Use Direct Index access on the correct inventory
        if (slot.slotIndex < 0 || slot.slotIndex >= targetInv.items.Count) return;
        ItemStack itemToDrop = targetInv.items[slot.slotIndex];

        if (itemToDrop == null || itemToDrop.itemData == null) return;

        SpawnWorldItem(itemToDrop);
        targetInv.RemoveItem(slot.slotIndex, itemToDrop.quantity);
    }

    public void UseItem(InventorySlot slot)
    {
        // [FIX] Target correct inventory
        Inventory targetInv = slot.parentInventory != null ? slot.parentInventory : currentlyDisplayedInventory;
        if (targetInv == null) return;

        ItemStack item = targetInv.items[slot.slotIndex];
        if (item == null || item.itemData == null) return;

        // Determine user (Owner of inventory)
        GameObject user = currentPlayerObject;
        if (targetInv.TryGetComponent<CharacterRoot>(out var root)) user = root.gameObject;
        else if (targetInv.transform.parent != null && targetInv.transform.parent.TryGetComponent<CharacterRoot>(out var parentRoot)) user = parentRoot.gameObject;

        if (item.itemData.stats is ItemAbilityScrollStats scrollStats)
        {
            if (scrollStats.abilityToTeach == null || DomeController.instance == null) return;
            DomeAI domeAI = DomeController.instance.GetComponent<DomeAI>();
            if (domeAI != null && !domeAI.defaultAbilities.Contains(scrollStats.abilityToTeach))
            {
                domeAI.defaultAbilities.Add(scrollStats.abilityToTeach);
                targetInv.RemoveItem(slot.slotIndex, 1);
            }
        }
        else if (item.itemData.stats is ItemConsumableStats consumableStats)
        {
            PlayerAbilityHolder abilityHolder = user.GetComponentInChildren<PlayerAbilityHolder>();
            if (abilityHolder != null && consumableStats.usageAbility != null)
            {
                abilityHolder.UseAbility(consumableStats.usageAbility, user);
                targetInv.RemoveItem(slot.slotIndex, 1);
            }
        }
    }

    public void DestroyItem(InventorySlot slot)
    {
        Inventory targetInv = slot.parentInventory != null ? slot.parentInventory : currentlyDisplayedInventory;
        if (targetInv != null)
        {
            targetInv.RemoveItem(slot.slotIndex, targetInv.items[slot.slotIndex].quantity);
        }
    }

    public void SplitStack(InventorySlot slot, int splitAmount)
    {
        Inventory targetInv = slot.parentInventory != null ? slot.parentInventory : currentlyDisplayedInventory;
        if (targetInv == null) return;

        ItemStack originalStack = targetInv.items[slot.slotIndex];
        if (originalStack == null || !originalStack.itemData.isStackable) return;

        if (targetInv.AddItemToNewStack(originalStack.itemData, splitAmount))
        {
            targetInv.RemoveItem(slot.slotIndex, splitAmount);
        }
    }

    private void SpawnWorldItem(ItemStack itemToSpawn)
    {
        if (itemToSpawn == null || itemToSpawn.itemData == null) return;

        // Spawn at active player position (safe bet)
        Vector3 spawnPos = (currentPlayerObject != null) ? currentPlayerObject.transform.position : transform.position;

        GameObject droppedItemGO = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
        WorldItem worldItem = droppedItemGO.GetComponent<WorldItem>();
        if (worldItem == null) worldItem = droppedItemGO.GetComponentInChildren<WorldItem>();

        if (worldItem != null)
        {
            // [FIXED] Explicit initialization to prevent race conditions
            worldItem.Initialize(itemToSpawn.itemData, itemToSpawn.quantity);
        }
    }

    // --- Standard Helpers (Unchanged) ---
    public bool HandleLoot(ItemData item, int quantity)
    {
        if (item is ResourceItemData resourceItem && WagonResourceManager.instance != null) { WagonResourceManager.instance.AddResource(resourceItem.resourceType, resourceItem.restoreAmount * quantity); ShowFloatingText($"+{resourceItem.restoreAmount * quantity} {resourceItem.resourceType}", Color.cyan); return true; }
        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive) { DualModeManager.instance.AddItemToLootBag(item, quantity); ShowFloatingText($"+{quantity} {item.itemName} (Bag)", Color.yellow); return true; }
        if (currentPlayerObject == null && PartyManager.instance != null) currentPlayerObject = PartyManager.instance.ActivePlayer;
        if (currentPlayerObject == null) return false;
        Inventory targetInventory = currentPlayerObject.GetComponentInChildren<Inventory>();
        if (targetInventory == null) return false;
        return targetInventory.AddItem(item, quantity);
    }
    private void ShowFloatingText(string text, Color color) { if (FloatingTextManager.instance != null && PartyManager.instance != null && PartyManager.instance.ActivePlayer != null) FloatingTextManager.instance.ShowEvent(text, PartyManager.instance.ActivePlayer.transform.position + Vector3.up * 2f); }
    public ItemData GetItemByID(string targetID) { foreach (var item in itemDatabase) { if (item != null && item.id == targetID) return item; } ItemData resItem = Resources.Load<ItemData>($"Items/{targetID}"); if (resItem != null) return resItem; return Resources.Load<ItemData>(targetID); }
    public void ShowTooltip(InventorySlot slot) { if (slot.parentInventory == null) return; CharacterRoot root = slot.parentInventory.GetComponentInParent<CharacterRoot>(); if (root == null) return; PlayerStats viewerStats = root.PlayerStats; ItemStack itemStack = slot.parentInventory.items[slot.slotIndex]; bool requirementsMet = CheckRequirements(itemStack, viewerStats); if (TooltipManager.instance != null) TooltipManager.instance.ShowItemTooltip(itemStack, requirementsMet, viewerStats); }
    private void OnSortValueChanged(int index) { if (currentlyDisplayedInventory != null) currentlyDisplayedInventory.SortItems((Inventory.SortType)index); }
    public void HandleDropOnSlot(int toSlotIndex, ItemStack droppedItemStack) { var source = FindAnyObjectByType<UIDragDropController>().currentSource; if (source is EquipmentSlot sourceEquipSlot && equipmentManager != null) equipmentManager.HandleUnequip(sourceEquipSlot.slotType); }
    private bool CheckRequirements(ItemStack itemStack, PlayerStats viewerStats) { if (viewerStats == null || itemStack == null || itemStack.itemData == null) return true; ItemData item = itemStack.itemData; if (PartyManager.instance.partyLevel < item.levelRequirement) return false; if (item.allowedClasses.Count > 0) return item.allowedClasses.Contains(viewerStats.characterClass); if (item.stats is ItemWeaponStats w) return viewerStats.characterClass.allowedWeaponCategories.Contains(w.weaponCategory); if (item.stats is ItemArmourStats a) return viewerStats.characterClass.allowedArmourCategories.Contains(a.armourCategory); return true; }
    public void ShowTooltipForExternalItem(ItemStack itemStack, PlayerStats viewerStats) { if (TooltipManager.instance != null) { bool requirementsMet = CheckRequirements(itemStack, viewerStats); TooltipManager.instance.ShowItemTooltip(itemStack, requirementsMet, viewerStats); } }
    public void ShowTooltipForAbility(Ability ability) { if (TooltipManager.instance != null) TooltipManager.instance.ShowAbilityTooltip(ability, null); }
    public void HideTooltip() { if (TooltipManager.instance != null) TooltipManager.instance.HideTooltip(); }
    public void OpenContextMenu(InventorySlot slot) { if (contextMenu != null) contextMenu.Open(slot); }
    public void OpenContextMenuForEquipment(ItemStack item, EquipmentType sourceSlot) { if (contextMenu != null) { sourceEquipmentSlot = sourceSlot; contextMenu.OpenForEquippedItem(item); } }
    public void DropEquippedItem() { if (equipmentManager != null && currentPlayerObject != null) { ItemStack itemToDrop = equipmentManager.RemoveItemFromSlot(sourceEquipmentSlot); if (itemToDrop != null) SpawnWorldItem(itemToDrop); } }
    public void DestroyEquippedItem() { if (equipmentManager != null) equipmentManager.RemoveItemFromSlot(sourceEquipmentSlot); }
    public void UnequipItem() { if (equipmentManager != null) equipmentManager.HandleUnequip(sourceEquipmentSlot); }
    public void SendItem(InventorySlot slot, int targetPlayerIndex) { if (targetPlayerIndex < 0 || targetPlayerIndex >= playerInventories.Count) return; Inventory targetInventory = playerInventories[targetPlayerIndex]; ItemStack itemToSend = slot.parentInventory.items[slot.slotIndex]; if (targetInventory.AddItem(itemToSend.itemData, itemToSend.quantity)) slot.parentInventory.RemoveItem(slot.slotIndex, itemToSend.quantity); }
    public void OpenStackSplitter(InventorySlot slot) { if (stackSplitter != null) { ItemStack stackToSplit = slot.parentInventory.items[slot.slotIndex]; stackSplitter.Open(stackToSplit, (splitAmount) => { SplitStack(slot, splitAmount); }); } }
    public ItemStack GetItemStackInSlot(int slotIndex) { if (currentlyDisplayedInventory == null || slotIndex < 0 || slotIndex >= currentlyDisplayedInventory.items.Count) return null; return currentlyDisplayedInventory.items[slotIndex]; }
    public Inventory GetCurrentInventory() => currentlyDisplayedInventory;
    public List<Inventory> GetAllPlayerInventories() => playerInventories;
    public GameObject GetCurrentPlayer() => currentPlayerObject;
}