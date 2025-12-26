using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class DefaultLoadoutItem
{
    public ItemData item;
    public EquipmentType targetSlot;
    public int quantity = 1;
}

public class PlayerEquipment : MonoBehaviour
{
    public event Action<EquipmentType> OnEquipmentChanged;
    public Dictionary<EquipmentType, ItemStack> equippedItems = new Dictionary<EquipmentType, ItemStack>();

    [Header("Target Skeleton")]
    [Tooltip("A reference renderer that contains the correct bone structure (usually the Head or an invisible master mesh).")]
    public SkinnedMeshRenderer targetMesh;

    [Header("Attachment Points (Weapons)")]
    public Transform rightHandAttachPoint;
    public Transform leftHandAttachPoint;

    [Header("Defaults")]
    [Tooltip("Define items and exactly which slot they go into (e.g., Sword -> RightHand).")]
    // RENAMED to force Unity to reset the serialization data for this field
    public List<DefaultLoadoutItem> startingLoadout;

    private Dictionary<EquipmentType, GameObject> equippedVisuals = new Dictionary<EquipmentType, GameObject>();
    private Inventory playerInventory;
    private PlayerStats playerStats;

    void Awake()
    {
        CharacterRoot root = GetComponentInParent<CharacterRoot>();
        if (root != null)
        {
            playerInventory = root.Inventory;
            playerStats = root.PlayerStats;
        }

        // Initialize dictionary
        foreach (EquipmentType slot in Enum.GetValues(typeof(EquipmentType)))
        {
            equippedItems[slot] = null;
            equippedVisuals.Add(slot, null);
        }
    }

    void Start()
    {
        EquipDefaults();
    }

    private void EquipDefaults()
    {
        if (startingLoadout == null) return;

        foreach (var entry in startingLoadout)
        {
            if (entry.item == null) continue;

            // Create and Equip
            ItemStack newItem = new ItemStack(entry.item, entry.quantity);

            // 1. Update Data
            equippedItems[entry.targetSlot] = newItem;

            // 2. Create Visual
            EquipVisual(entry.targetSlot, entry.item);
        }

        playerStats?.CalculateFinalStats();
    }

    // --- VISUAL LOGIC ---

    private void EquipVisual(EquipmentType slot, ItemData itemData)
    {
        if (itemData == null || itemData.equippedPrefab == null) return;

        DestroyVisual(slot);

        if (itemData.itemType == ItemType.Weapon)
        {
            Transform attachPoint = GetAttachPoint(slot);
            if (attachPoint == null) return;
            GameObject visual = Instantiate(itemData.equippedPrefab, attachPoint);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            equippedVisuals[slot] = visual;
        }
        else if (itemData.itemType == ItemType.Armour)
        {
            if (targetMesh == null) return;

            GameObject visual = Instantiate(itemData.equippedPrefab, targetMesh.transform.parent);
            SkinnedMeshRenderer armorRenderer = visual.GetComponentInChildren<SkinnedMeshRenderer>();

            if (armorRenderer != null)
            {
                armorRenderer.bones = targetMesh.bones;
                armorRenderer.rootBone = targetMesh.rootBone;
            }
            equippedVisuals[slot] = visual;
        }
    }

    private void DestroyVisual(EquipmentType slot)
    {
        if (equippedVisuals.ContainsKey(slot) && equippedVisuals[slot] != null)
        {
            Destroy(equippedVisuals[slot]);
            equippedVisuals[slot] = null;
        }
    }

    private Transform GetAttachPoint(EquipmentType slot)
    {
        switch (slot)
        {
            case EquipmentType.RightHand: return rightHandAttachPoint;
            case EquipmentType.LeftHand: return leftHandAttachPoint;
            default: return null;
        }
    }

    // --- EQUIPMENT LOGIC ---

    public ItemStack EquipItem(EquipmentType slot, ItemStack itemToEquip)
    {
        ItemStack previouslyEquippedItem = equippedItems[slot];

        // 1. Remove old visual
        if (previouslyEquippedItem != null)
        {
            DestroyVisual(slot);
        }

        // 2. Set Data
        equippedItems[slot] = itemToEquip;

        // 3. Create new visual
        if (itemToEquip != null)
        {
            EquipVisual(slot, itemToEquip.itemData);
        }

        playerStats?.CalculateFinalStats();
        OnEquipmentChanged?.Invoke(slot);

        return previouslyEquippedItem;
    }

    public void EquipItem(ItemData item, int quantity = 1)
    {
        EquipmentType slot = GetEquipmentSlot(item);
        EquipItem(slot, new ItemStack(item, quantity));
    }

    public bool UnequipItem(EquipmentType slot)
    {
        if (playerInventory == null || equippedItems[slot] == null) return false;

        ItemStack itemToUnequip = equippedItems[slot];

        if (playerInventory.AddItem(itemToUnequip.itemData, itemToUnequip.quantity))
        {
            RemoveItemFromSlot(slot);
            return true;
        }
        return false;
    }

    public ItemStack RemoveItemFromSlot(EquipmentType slot)
    {
        ItemStack removedItem = equippedItems[slot];
        if (removedItem != null)
        {
            equippedItems[slot] = null;
            DestroyVisual(slot);
            playerStats?.CalculateFinalStats();
            OnEquipmentChanged?.Invoke(slot);
        }
        return removedItem;
    }

    private EquipmentType GetEquipmentSlot(ItemData item)
    {
        if (item.stats is ItemArmourStats armourStats)
        {
            switch (armourStats.armourSlot)
            {
                case ItemArmourStats.ArmourSlot.Head: return EquipmentType.Head;
                case ItemArmourStats.ArmourSlot.Chest: return EquipmentType.Chest;
                case ItemArmourStats.ArmourSlot.Hands: return EquipmentType.Hands;
                case ItemArmourStats.ArmourSlot.Belt: return EquipmentType.Belt;
                case ItemArmourStats.ArmourSlot.Legs: return EquipmentType.Legs;
                case ItemArmourStats.ArmourSlot.Feet: return EquipmentType.Feet;
            }
        }
        if (item.stats is ItemWeaponStats) return EquipmentType.RightHand;
        if (item.stats is ItemTrinketStats) return EquipmentType.Ring1;

        return EquipmentType.RightHand;
    }

    // Support for Load Game
    public void RestoreEquipment(Dictionary<EquipmentType, ItemStack> savedEquipment)
    {
        foreach (EquipmentType slot in Enum.GetValues(typeof(EquipmentType)))
        {
            equippedItems[slot] = null;
        }

        foreach (var kvp in savedEquipment)
        {
            if (kvp.Value != null && kvp.Value.itemData != null)
            {
                // Re-create the visual and data
                EquipItem(kvp.Key, kvp.Value);
            }
        }
    }
}