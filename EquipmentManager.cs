using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager instance { get; private set; }

    private PlayerEquipment currentPlayerEquipment;

    [Header("Manager References")]
    public InventoryManager inventoryManager;

    [Header("UI References")]
    public List<EquipmentSlot> equipmentSlots;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            instance = this;
        }
    }

    void Start()
    {
        InitializeSlots();
    }

    public void ShowTooltip(EquipmentSlot slot)
    {
        if (inventoryManager == null || slot.parentEquipment == null) return;

        CharacterRoot root = slot.parentEquipment.GetComponentInParent<CharacterRoot>();
        if (root == null) return;

        PlayerStats stats = root.PlayerStats;
        if (stats == null) return;

        slot.parentEquipment.equippedItems.TryGetValue(slot.slotType, out ItemStack item);
        if (item != null)
        {
            inventoryManager.ShowTooltipForExternalItem(item, stats);
        }
    }

    public bool IsItemValidForSlot(GameObject playerObject, ItemData item, EquipmentType slotType)
    {
        if (item == null || playerObject == null) return false;

        CharacterRoot root = playerObject.GetComponentInParent<CharacterRoot>();
        if (root == null)
        {
            Debug.LogError($"Could not find CharacterRoot on {playerObject.name} or its parents.", playerObject);
            return false;
        }

        PlayerStats playerStats = root.PlayerStats;

        if (playerStats == null || playerStats.characterClass == null)
        {
            return false;
        }

        if (PartyManager.instance.partyLevel < item.levelRequirement)
        {
            return false;
        }

        bool classAndTypeAllowed = false;
        if (item.allowedClasses.Count > 0)
        {
            if (item.allowedClasses.Contains(playerStats.characterClass))
            {
                classAndTypeAllowed = true;
            }
        }
        else
        {
            if (item.stats is ItemWeaponStats weaponStats)
            {
                if (playerStats.characterClass.allowedWeaponCategories.Contains(weaponStats.weaponCategory))
                {
                    classAndTypeAllowed = true;
                }
            }
            else if (item.stats is ItemArmourStats armourStats)
            {
                if (playerStats.characterClass.allowedArmourCategories.Contains(armourStats.armourCategory))
                {
                    classAndTypeAllowed = true;
                }
            }
            else
            {
                classAndTypeAllowed = true;
            }
        }

        if (!classAndTypeAllowed) return false;

        switch (item.itemType)
        {
            case ItemType.Armour:
                if (item.stats is ItemArmourStats armourStats)
                {
                    // --- MODIFIED: Removed Shoulders and Arms cases ---
                    switch (armourStats.armourSlot)
                    {
                        case ItemArmourStats.ArmourSlot.Head: return slotType == EquipmentType.Head;
                        case ItemArmourStats.ArmourSlot.Chest: return slotType == EquipmentType.Chest;
                        case ItemArmourStats.ArmourSlot.Hands: return slotType == EquipmentType.Hands;
                        case ItemArmourStats.ArmourSlot.Belt: return slotType == EquipmentType.Belt;
                        case ItemArmourStats.ArmourSlot.Legs: return slotType == EquipmentType.Legs;
                        case ItemArmourStats.ArmourSlot.Feet: return slotType == EquipmentType.Feet;
                        default: return false;
                    }
                }
                return false;

            case ItemType.Weapon:
                return slotType == EquipmentType.LeftHand || slotType == EquipmentType.RightHand;

            case ItemType.Trinket:
                if (item.stats is ItemTrinketStats trinketStats)
                {
                    if (trinketStats.trinketSlot == ItemTrinketStats.TrinketSlot.Neck)
                        return slotType == EquipmentType.Neck;
                    if (trinketStats.trinketSlot == ItemTrinketStats.TrinketSlot.Ring)
                        return slotType == EquipmentType.Ring1 || slotType == EquipmentType.Ring2;
                }
                return false;
        }
        return false;
    }

    private void InitializeSlots()
    {
        foreach (var slot in equipmentSlots)
        {
            slot.Initialize(this, null);
        }
    }

    public void UnequipItemToSpecificSlot(EquipmentType sourceSlotType, Inventory targetInventory, int targetSlotIndex)
    {
        PlayerEquipment sourceEquipment = targetInventory.GetComponentInParent<PlayerEquipment>();
        if (sourceEquipment == null) return;

        ItemStack itemToUnequip = sourceEquipment.equippedItems[sourceSlotType];
        if (itemToUnequip == null) return;

        ItemStack itemInTargetSlot = targetInventory.items[targetSlotIndex];

        if (itemInTargetSlot != null && itemInTargetSlot.itemData != null && IsItemValidForSlot(sourceEquipment.gameObject, itemInTargetSlot.itemData, sourceSlotType))
        {
            ItemStack previouslyEquippedItem = sourceEquipment.EquipItem(sourceSlotType, itemInTargetSlot);
            targetInventory.SetItemStack(targetSlotIndex, previouslyEquippedItem);
        }
        else if (itemInTargetSlot == null || itemInTargetSlot.itemData == null)
        {
            ItemStack removedItem = sourceEquipment.RemoveItemFromSlot(sourceSlotType);
            targetInventory.SetItemStack(targetSlotIndex, removedItem);
        }
    }

    public void DisplayEquipmentForPlayer(GameObject playerObject)
    {
        PlayerEquipment newEquipment = null;
        if (playerObject != null)
        {
            newEquipment = playerObject.GetComponentInChildren<PlayerEquipment>();
        }

        if (currentPlayerEquipment != null)
        {
            currentPlayerEquipment.OnEquipmentChanged -= RefreshSlot;
        }

        currentPlayerEquipment = newEquipment;

        if (currentPlayerEquipment != null)
        {
            currentPlayerEquipment.OnEquipmentChanged += RefreshSlot;
        }

        foreach (var s in equipmentSlots)
        {
            s.Initialize(this, currentPlayerEquipment);
            RefreshSlot(s.slotType);
        }
    }

    private void RefreshSlot(EquipmentType slotType)
    {
        EquipmentSlot slot = equipmentSlots.Find(s => s.slotType == slotType);
        if (slot != null && currentPlayerEquipment != null)
        {
            currentPlayerEquipment.equippedItems.TryGetValue(slotType, out var item);
            slot.UpdateSlot(item);
        }
    }

    public void EquipItem(PlayerEquipment targetEquipment, EquipmentType slotType, ItemStack itemToEquip)
    {
        if (targetEquipment == null) return;

        var source = FindAnyObjectByType<UIDragDropController>().currentSource;
        if (source is InventorySlot sourceSlot)
        {
            ItemStack returnedItem = targetEquipment.EquipItem(slotType, itemToEquip);

            if (returnedItem != null)
            {
                sourceSlot.parentInventory.SetItemStack(sourceSlot.slotIndex, returnedItem);
            }
            else
            {
                sourceSlot.parentInventory.SetItemStack(sourceSlot.slotIndex, new ItemStack(null, 0));
            }
        }
        else if (source is EquipmentSlot sourceEquipSlot)
        {
            ItemStack returnedItem = targetEquipment.EquipItem(slotType, itemToEquip);
            if (returnedItem != null)
            {
                sourceEquipSlot.parentEquipment.EquipItem(sourceEquipSlot.slotType, returnedItem);
            }
            else
            {
                sourceEquipSlot.parentEquipment.RemoveItemFromSlot(sourceEquipSlot.slotType);
            }
        }
    }

    public void HandleUnequip(EquipmentType slotType)
    {
        if (currentPlayerEquipment != null)
        {
            currentPlayerEquipment.UnequipItem(slotType);
        }
    }

    public ItemStack RemoveItemFromSlot(EquipmentType slotType)
    {
        if (currentPlayerEquipment != null)
        {
            return currentPlayerEquipment.RemoveItemFromSlot(slotType);
        }
        return null;
    }

    public void HandleRightClick(EquipmentSlot slot)
    {
        if (slot.parentEquipment == null) return;
        ItemStack item = slot.parentEquipment.equippedItems[slot.slotType];
        if (item != null)
        {
            inventoryManager.OpenContextMenuForEquipment(item, slot.slotType);
        }
    }

    public void HideTooltip()
    {
        if (inventoryManager != null)
        {
            inventoryManager.HideTooltip();
        }
    }
}