using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Unity.VisualScripting;
using TMPro;
using System;
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
        InitializeUI();
        HideTooltip();
        if (playerCurrencyText != null) playerCurrencyText.text = "";

        if (sortDropdown != null) { sortDropdown.onValueChanged.AddListener(OnSortValueChanged); }
        if (contextMenu != null) { contextMenu.Initialize(this); contextMenu.gameObject.SetActive(false); }
        if (stackSplitter != null) { stackSplitter.gameObject.SetActive(false); }
    }

    // --- NEW: Centralized Loot Handler ---
    /// <summary>
    /// Checks the item type. If it's a Resource, sends it to the Wagon.
    /// If it's a normal item, tries to add it to the active player's inventory.
    /// Returns TRUE if the item was successfully taken (so WorldItem can destroy itself).
    /// </summary>
    public bool HandleLoot(ItemData item, int quantity)
    {
        // 1. Check for Resource Item
        if (item is ResourceItemData resourceItem)
        {
            // Only consume if we have a resource manager (e.g., we are near the wagon or it's global)
            // Note: If you want resources to be collected even in dungeons, WagonResourceManager should persist or be a Singleton.
            if (WagonResourceManager.instance != null)
            {
                WagonResourceManager.instance.AddResource(
                    resourceItem.resourceType,
                    resourceItem.restoreAmount * quantity
                );

                if (FloatingTextManager.instance != null && PartyManager.instance != null && PartyManager.instance.ActivePlayer != null)
                {
                    string text = $"+{resourceItem.restoreAmount * quantity} {resourceItem.resourceType}";
                    FloatingTextManager.instance.ShowEvent(text, PartyManager.instance.ActivePlayer.transform.position + Vector3.up * 2f);
                }
                return true;
            }
            // If no WagonResourceManager found, maybe fall through to inventory? 
            // Or just fail. For now, let's treat it as consumed.
        }

        // 2. Standard Inventory Logic
        // Find active player's inventory
        if (currentPlayerObject == null && PartyManager.instance != null)
        {
            currentPlayerObject = PartyManager.instance.ActivePlayer;
        }

        if (currentPlayerObject == null) return false;

        Inventory targetInventory = currentPlayerObject.GetComponentInChildren<Inventory>();
        if (targetInventory == null) return false;

        return targetInventory.AddItem(item, quantity);
    }
    // -------------------------------------

    // ... (All other existing methods remain unchanged) ...
    public void ShowTooltip(InventorySlot slot) { if (slot.parentInventory == null) return; CharacterRoot root = slot.parentInventory.GetComponentInParent<CharacterRoot>(); if (root == null) return; PlayerStats viewerStats = root.PlayerStats; if (viewerStats == null) return; ItemStack itemStack = slot.parentInventory.items[slot.slotIndex]; bool requirementsMet = CheckRequirements(itemStack, viewerStats); if (TooltipManager.instance != null) { TooltipManager.instance.ShowItemTooltip(itemStack, requirementsMet, viewerStats); } }
    public void UseItem(InventorySlot slot) { if (currentlyDisplayedInventory == null || currentPlayerObject == null) return; ItemStack item = GetItemStackInSlot(slot.slotIndex); if (item == null || item.itemData == null) return; if (item.itemData.stats is ItemAbilityScrollStats scrollStats) { if (scrollStats.abilityToTeach == null) { Debug.LogError("Ability Scroll has no ability assigned to teach.", item.itemData); return; } if (DomeController.instance == null) { Debug.LogWarning("Cannot use Dome ability scroll: Dome instance not found."); return; } DomeAI domeAI = DomeController.instance.GetComponent<DomeAI>(); if (domeAI != null && !domeAI.defaultAbilities.Contains(scrollStats.abilityToTeach)) { domeAI.defaultAbilities.Add(scrollStats.abilityToTeach); Debug.Log($"Dome has learned a new ability: {scrollStats.abilityToTeach.name}"); currentlyDisplayedInventory.RemoveItem(slot.slotIndex, 1); } else { Debug.Log("Dome already knows this ability."); } } else if (item.itemData.stats is ItemConsumableStats consumableStats) { PlayerAbilityHolder abilityHolder = currentPlayerObject.GetComponentInChildren<PlayerAbilityHolder>(); if (abilityHolder != null && consumableStats.usageAbility != null) { abilityHolder.UseAbility(consumableStats.usageAbility, currentPlayerObject); currentlyDisplayedInventory.RemoveItem(slot.slotIndex, 1); } } }
    private void RefreshUI() { if (currentlyDisplayedInventory == null) { for (int i = 0; i < uiSlots.Count; i++) uiSlots[i].UpdateSlot(null); if (playerCurrencyText != null) playerCurrencyText.text = ""; } else { for (int i = 0; i < uiSlots.Count; i++) { if (i < currentlyDisplayedInventory.items.Count) { uiSlots[i].Initialize(this, i, currentlyDisplayedInventory); uiSlots[i].UpdateSlot(currentlyDisplayedInventory.items[i]); } else { uiSlots[i].UpdateSlot(null); } } if (playerCurrencyText != null && PartyManager.instance != null) { playerCurrencyText.text = $"Gold: {PartyManager.instance.currencyGold}"; } } }
    private void OnSortValueChanged(int index) { if (currentlyDisplayedInventory != null) { currentlyDisplayedInventory.SortItems((Inventory.SortType)index); } }
    private void InitializeUI() { foreach (Transform child in inventorySlotsParent) { Destroy(child.gameObject); } uiSlots.Clear(); for (int i = 0; i < 24; i++) { GameObject slotGO = Instantiate(inventorySlotPrefab, inventorySlotsParent); InventorySlot newSlot = slotGO.GetComponent<InventorySlot>(); newSlot.Initialize(this, i, null); newSlot.UpdateSlot(null); uiSlots.Add(newSlot); } }
    public void DisplayInventory(Inventory inventoryToDisplay, GameObject playerObject) { if (currentlyDisplayedInventory != null) { currentlyDisplayedInventory.OnInventoryChanged -= RefreshUI; } currentlyDisplayedInventory = inventoryToDisplay; currentPlayerObject = playerObject; if (currentlyDisplayedInventory != null) { currentlyDisplayedInventory.OnInventoryChanged += RefreshUI; } if (sortDropdown != null) sortDropdown.value = 0; RefreshUI(); }
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