using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using System.Linq;

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

    [Header("Item Database (For Save/Load)")]
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
        // Don't generate slots here. We wait until DisplayInventory is called.
        // InitializeUI(); 

        HideTooltip();
        if (playerCurrencyText != null) playerCurrencyText.text = "";

        if (sortDropdown != null) { sortDropdown.onValueChanged.AddListener(OnSortValueChanged); }
        if (contextMenu != null) { contextMenu.Initialize(this); contextMenu.gameObject.SetActive(false); }
        if (stackSplitter != null) { stackSplitter.gameObject.SetActive(false); }
    }

    // --- UPDATED DISPLAY LOGIC ---
    public void DisplayInventory(Inventory inventoryToDisplay, GameObject playerObject)
    {
        if (currentlyDisplayedInventory != null)
        {
            currentlyDisplayedInventory.OnInventoryChanged -= RefreshUI;
        }

        currentlyDisplayedInventory = inventoryToDisplay;
        currentPlayerObject = playerObject;

        if (currentlyDisplayedInventory != null)
        {
            // 1. Resize UI to match the target inventory size
            AdjustSlotCount(currentlyDisplayedInventory.inventorySize);

            // 2. Subscribe to events
            currentlyDisplayedInventory.OnInventoryChanged += RefreshUI;
        }

        if (sortDropdown != null) sortDropdown.value = 0;
        RefreshUI();
    }

    // New helper to dynamically add/remove slots
    private void AdjustSlotCount(int targetSize)
    {
        // Add needed slots
        while (uiSlots.Count < targetSize)
        {
            GameObject slotGO = Instantiate(inventorySlotPrefab, inventorySlotsParent);
            InventorySlot newSlot = slotGO.GetComponent<InventorySlot>();
            // Initialize with temporary index, will be fixed in RefreshUI
            newSlot.Initialize(this, uiSlots.Count, currentlyDisplayedInventory);
            uiSlots.Add(newSlot);
        }

        // Enable/Disable based on count
        for (int i = 0; i < uiSlots.Count; i++)
        {
            uiSlots[i].gameObject.SetActive(i < targetSize);
        }
    }
    // -----------------------------

    private void RefreshUI()
    {
        if (currentlyDisplayedInventory == null)
        {
            foreach (var slot in uiSlots) slot.UpdateSlot(null);
            if (playerCurrencyText != null) playerCurrencyText.text = "";
        }
        else
        {
            // Ensure we have enough slots (just in case size changed dynamically)
            if (uiSlots.Count < currentlyDisplayedInventory.inventorySize)
            {
                AdjustSlotCount(currentlyDisplayedInventory.inventorySize);
            }

            for (int i = 0; i < uiSlots.Count; i++)
            {
                if (i < currentlyDisplayedInventory.inventorySize) // Check bounds against SIZE, not items count
                {
                    uiSlots[i].gameObject.SetActive(true);
                    uiSlots[i].Initialize(this, i, currentlyDisplayedInventory); // Re-link slot to inventory

                    // Check if there is an item at this index
                    if (i < currentlyDisplayedInventory.items.Count)
                    {
                        uiSlots[i].UpdateSlot(currentlyDisplayedInventory.items[i]);
                    }
                    else
                    {
                        uiSlots[i].UpdateSlot(null);
                    }
                }
                else
                {
                    uiSlots[i].gameObject.SetActive(false);
                }
            }

            if (playerCurrencyText != null && PartyManager.instance != null)
            {
                playerCurrencyText.text = $"Gold: {PartyManager.instance.currencyGold}";
            }
        }
    }

    // --- LOOT HANDLER (Unchanged) ---
    public bool HandleLoot(ItemData item, int quantity)
    {
        if (item is ResourceItemData resourceItem)
        {
            if (WagonResourceManager.instance != null)
            {
                WagonResourceManager.instance.AddResource(resourceItem.resourceType, resourceItem.restoreAmount * quantity);
                ShowFloatingText($"+{resourceItem.restoreAmount * quantity} {resourceItem.resourceType}", Color.cyan);
                return true;
            }
        }

        if (DualModeManager.instance != null && DualModeManager.instance.isDualModeActive)
        {
            DualModeManager.instance.AddItemToLootBag(item, quantity);
            ShowFloatingText($"+{quantity} {item.itemName} (Bag)", Color.yellow);
            return true;
        }

        if (currentPlayerObject == null && PartyManager.instance != null)
        {
            currentPlayerObject = PartyManager.instance.ActivePlayer;
        }

        if (currentPlayerObject == null) return false;

        Inventory targetInventory = currentPlayerObject.GetComponentInChildren<Inventory>();
        if (targetInventory == null) return false;

        return targetInventory.AddItem(item, quantity);
    }

    private void ShowFloatingText(string text, Color color)
    {
        if (FloatingTextManager.instance != null && PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
        {
            FloatingTextManager.instance.ShowEvent(text, PartyManager.instance.ActivePlayer.transform.position + Vector3.up * 2f);
        }
    }

    public ItemData GetItemByID(string targetID)
    {
        if (itemDatabase != null)
        {
            foreach (var item in itemDatabase)
            {
                if (item != null && item.id == targetID) return item;
            }
        }
        ItemData resItem = Resources.Load<ItemData>($"Items/{targetID}");
        if (resItem != null) return resItem;
        return Resources.Load<ItemData>(targetID);
    }

    public void ShowTooltip(InventorySlot slot) { if (slot.parentInventory == null) return; CharacterRoot root = slot.parentInventory.GetComponentInParent<CharacterRoot>(); if (root == null) return; PlayerStats viewerStats = root.PlayerStats; if (viewerStats == null) return; ItemStack itemStack = slot.parentInventory.items[slot.slotIndex]; bool requirementsMet = CheckRequirements(itemStack, viewerStats); if (TooltipManager.instance != null) { TooltipManager.instance.ShowItemTooltip(itemStack, requirementsMet, viewerStats); } }
    public void UseItem(InventorySlot slot) { if (currentlyDisplayedInventory == null || currentPlayerObject == null) return; ItemStack item = GetItemStackInSlot(slot.slotIndex); if (item == null || item.itemData == null) return; if (item.itemData.stats is ItemAbilityScrollStats scrollStats) { if (scrollStats.abilityToTeach == null) { return; } if (DomeController.instance == null) { return; } DomeAI domeAI = DomeController.instance.GetComponent<DomeAI>(); if (domeAI != null && !domeAI.defaultAbilities.Contains(scrollStats.abilityToTeach)) { domeAI.defaultAbilities.Add(scrollStats.abilityToTeach); currentlyDisplayedInventory.RemoveItem(slot.slotIndex, 1); } } else if (item.itemData.stats is ItemConsumableStats consumableStats) { PlayerAbilityHolder abilityHolder = currentPlayerObject.GetComponentInChildren<PlayerAbilityHolder>(); if (abilityHolder != null && consumableStats.usageAbility != null) { abilityHolder.UseAbility(consumableStats.usageAbility, currentPlayerObject); currentlyDisplayedInventory.RemoveItem(slot.slotIndex, 1); } } }

    private void OnSortValueChanged(int index) { if (currentlyDisplayedInventory != null) { currentlyDisplayedInventory.SortItems((Inventory.SortType)index); } }

    public void HandleDropOnSlot(int toSlotIndex, ItemStack droppedItemStack) { var source = FindAnyObjectByType<UIDragDropController>().currentSource; if (source == null || currentlyDisplayedInventory == null) return; if (source is EquipmentSlot sourceEquipSlot) { if (equipmentManager != null) { equipmentManager.HandleUnequip(sourceEquipSlot.slotType); } } }
    private bool CheckRequirements(ItemStack itemStack, PlayerStats viewerStats) { if (viewerStats == null || itemStack == null || itemStack.itemData == null) return true; ItemData item = itemStack.itemData; if (PartyManager.instance.partyLevel < item.levelRequirement) return false; if (item.allowedClasses.Count > 0) { return item.allowedClasses.Contains(viewerStats.characterClass); } else { if (item.stats is ItemWeaponStats weaponStats) { return viewerStats.characterClass.allowedWeaponCategories.Contains(weaponStats.weaponCategory); } else if (item.stats is ItemArmourStats armourStats) { return viewerStats.characterClass.allowedArmourCategories.Contains(armourStats.armourCategory); } else { return true; } } }
    public void ShowTooltipForExternalItem(ItemStack itemStack, PlayerStats viewerStats) { if (TooltipManager.instance != null) { bool requirementsMet = CheckRequirements(itemStack, viewerStats); TooltipManager.instance.ShowItemTooltip(itemStack, requirementsMet, viewerStats); } }
    public void ShowTooltipForAbility(Ability ability) { if (TooltipManager.instance != null) { TooltipManager.instance.ShowAbilityTooltip(ability, null); } }
    public void HideTooltip() { if (TooltipManager.instance != null) { TooltipManager.instance.HideTooltip(); } }
    public void OpenContextMenu(InventorySlot slot) { if (contextMenu != null) { contextMenu.Open(slot); } }
    public void OpenContextMenuForEquipment(ItemStack item, EquipmentType sourceSlot) { if (contextMenu != null) { sourceEquipmentSlot = sourceSlot; contextMenu.OpenForEquippedItem(item); } }
    public void DropItem(InventorySlot slot) { if (worldItemPrefab == null || currentPlayerObject == null) return; ItemStack itemToDrop = GetItemStackInSlot(slot.slotIndex); if (itemToDrop == null || itemToDrop.itemData == null) return; SpawnWorldItem(itemToDrop); currentlyDisplayedInventory.RemoveItem(slot.slotIndex, itemToDrop.quantity); }
    public void DropEquippedItem() { if (equipmentManager == null) return; if (currentPlayerObject == null) return; ItemStack itemToDrop = equipmentManager.RemoveItemFromSlot(sourceEquipmentSlot); if (itemToDrop != null) { SpawnWorldItem(itemToDrop); } }
    public void DestroyItem(InventorySlot slot) { if (currentlyDisplayedInventory != null) { currentlyDisplayedInventory.RemoveItem(slot.slotIndex, GetItemStackInSlot(slot.slotIndex).quantity); } }
    public void DestroyEquippedItem() { if (equipmentManager != null) { equipmentManager.RemoveItemFromSlot(sourceEquipmentSlot); } }
    public void UnequipItem() { if (equipmentManager != null) { equipmentManager.HandleUnequip(sourceEquipmentSlot); } }
    public void SendItem(InventorySlot slot, int targetPlayerIndex) { if (targetPlayerIndex < 0 || targetPlayerIndex >= playerInventories.Count) return; Inventory targetInventory = playerInventories[targetPlayerIndex]; ItemStack itemToSend = GetItemStackInSlot(slot.slotIndex); if (targetInventory.AddItem(itemToSend.itemData, itemToSend.quantity)) { currentlyDisplayedInventory.RemoveItem(slot.slotIndex, itemToSend.quantity); } }
    public void OpenStackSplitter(InventorySlot slot) { if (stackSplitter != null) { ItemStack stackToSplit = GetItemStackInSlot(slot.slotIndex); stackSplitter.Open(stackToSplit, (splitAmount) => { SplitStack(slot, splitAmount); }); } }
    public void SplitStack(InventorySlot slot, int splitAmount) { if (currentlyDisplayedInventory == null) return; ItemStack originalStack = GetItemStackInSlot(slot.slotIndex); if (originalStack == null || !originalStack.itemData.isStackable) return; if (currentlyDisplayedInventory.AddItemToNewStack(originalStack.itemData, splitAmount)) { currentlyDisplayedInventory.RemoveItem(slot.slotIndex, splitAmount); } }
    private void SpawnWorldItem(ItemStack itemToSpawn) { GameObject droppedItemGO = Instantiate(worldItemPrefab, currentPlayerObject.transform.position, Quaternion.identity); WorldItem worldItem = droppedItemGO.GetComponent<WorldItem>(); if (worldItem != null) { worldItem.itemData = itemToSpawn.itemData; worldItem.quantity = itemToSpawn.quantity; } }
    public ItemStack GetItemStackInSlot(int slotIndex) { if (currentlyDisplayedInventory == null || slotIndex < 0 || slotIndex >= currentlyDisplayedInventory.items.Count) return null; return currentlyDisplayedInventory.items[slotIndex]; }
    public Inventory GetCurrentInventory() => currentlyDisplayedInventory;
    public List<Inventory> GetAllPlayerInventories() => playerInventories;
    public GameObject GetCurrentPlayer() => currentPlayerObject;
}