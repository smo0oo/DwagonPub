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

// 1. NEW: A structure to define your "Naked" parts
[System.Serializable]
public class BodyPartDefault
{
    public EquipmentType slot;
    public GameObject prefab; // Drag your "Naked Hands", "Naked Feet" here
}

public class PlayerEquipment : MonoBehaviour
{
    public event Action<EquipmentType> OnEquipmentChanged;
    public Dictionary<EquipmentType, ItemStack> equippedItems = new Dictionary<EquipmentType, ItemStack>();

    [Header("Target Skeleton")]
    public SkinnedMeshRenderer targetMesh;

    [Header("Attachment Points")]
    public Transform rightHandAttachPoint;
    public Transform leftHandAttachPoint;

    [Header("Defaults")]
    public List<DefaultLoadoutItem> startingLoadout;

    [Header("Naked Body Parts")]
    [Tooltip("If a slot is empty, these prefabs will be shown (Purely visual, no stats).")]
    public List<BodyPartDefault> defaultBodyParts; // <--- 2. NEW FIELD

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

        foreach (EquipmentType slot in Enum.GetValues(typeof(EquipmentType)))
        {
            equippedItems[slot] = null;
            equippedVisuals.Add(slot, null);
        }
    }

    void Start()
    {
        // 3. First, ensure the body is fully "Naked"
        InitializeNakedBody();

        // Then equip any starting gear on top
        EquipDefaults();
    }

    private void InitializeNakedBody()
    {
        foreach (EquipmentType slot in Enum.GetValues(typeof(EquipmentType)))
        {
            // If the slot is empty, show the naked part
            if (!equippedItems.ContainsKey(slot) || equippedItems[slot] == null)
            {
                RestoreNakedPart(slot);
            }
        }
    }

    private void EquipDefaults()
    {
        if (startingLoadout == null) return;

        foreach (var entry in startingLoadout)
        {
            if (entry.item == null) continue;
            ItemStack newItem = new ItemStack(entry.item, entry.quantity);
            equippedItems[entry.targetSlot] = newItem;
            EquipVisual(entry.targetSlot, entry.item);
        }
        playerStats?.CalculateFinalStats();
    }

    // --- VISUAL LOGIC ---

    private void EquipVisual(EquipmentType slot, ItemData itemData)
    {
        if (itemData == null || itemData.equippedPrefab == null) return;

        // Delegate to the shared internal method
        CreateAndBindVisual(slot, itemData.equippedPrefab, itemData.itemType == ItemType.Weapon);
    }

    // 4. NEW: Logic to restore the default part
    private void RestoreNakedPart(EquipmentType slot)
    {
        // Find the matching default prefab
        var defaultPart = defaultBodyParts.Find(x => x.slot == slot);

        if (defaultPart != null && defaultPart.prefab != null)
        {
            // Treat it like Armour (Skinned Mesh)
            CreateAndBindVisual(slot, defaultPart.prefab, false);
        }
    }

    // 5. REFACTORED: Shared logic for Items AND Naked parts
    private void CreateAndBindVisual(EquipmentType slot, GameObject prefab, bool isWeapon)
    {
        // Always clean up the old visual first (whether it was armor or a naked arm)
        DestroyVisual(slot);

        if (isWeapon)
        {
            Transform attachPoint = GetAttachPoint(slot);
            if (attachPoint == null) return;
            GameObject visual = Instantiate(prefab, attachPoint);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            equippedVisuals[slot] = visual;
        }
        else
        {
            // ARMOUR / BODY PART LOGIC
            if (targetMesh == null) return;

            GameObject visual = Instantiate(prefab, targetMesh.transform.parent);
            SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();

            // Support multiple meshes (e.g., Left Boot + Right Boot in one prefab)
            foreach (var renderer in renderers)
            {
                renderer.bones = targetMesh.bones;
                renderer.rootBone = targetMesh.rootBone;
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
        DestroyVisual(slot);

        // 2. Set Data
        equippedItems[slot] = itemToEquip;

        // 3. Create new visual OR Restore Default
        if (itemToEquip != null)
        {
            EquipVisual(slot, itemToEquip.itemData);
        }
        else
        {
            // 6. CRITICAL FIX: If we equipped "Nothing", show Naked Part
            RestoreNakedPart(slot);
        }

        playerStats?.CalculateFinalStats();
        OnEquipmentChanged?.Invoke(slot);

        return previouslyEquippedItem;
    }

    public ItemStack RemoveItemFromSlot(EquipmentType slot)
    {
        ItemStack removedItem = equippedItems[slot];
        if (removedItem != null)
        {
            equippedItems[slot] = null;

            // 7. CRITICAL FIX: Destroy armor -> Restore Naked Part immediately
            DestroyVisual(slot);
            RestoreNakedPart(slot);

            playerStats?.CalculateFinalStats();
            OnEquipmentChanged?.Invoke(slot);
        }
        return removedItem;
    }

    // ... (Rest of the class methods like UnequipItem, RestoreEquipment remain the same) ...

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

    public void RestoreEquipment(Dictionary<EquipmentType, ItemStack> savedEquipment)
    {
        // Clear everything first (sets to naked)
        InitializeNakedBody();

        foreach (var kvp in savedEquipment)
        {
            if (kvp.Value != null && kvp.Value.itemData != null)
            {
                EquipItem(kvp.Key, kvp.Value);
            }
        }
    }
}