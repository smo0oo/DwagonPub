using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

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
    [Tooltip("Assign ItemData for the 'Naked' version of slots (e.g., Naked Chest, Naked Feet).")]
    public List<ItemData> defaultEquipment;

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
            equippedItems.Add(slot, null);
            equippedVisuals.Add(slot, null);
        }
    }

    void Start()
    {
        // Apply defaults on startup for any empty slots
        EquipDefaults();
    }

    private void EquipDefaults()
    {
        foreach (EquipmentType slot in Enum.GetValues(typeof(EquipmentType)))
        {
            if (equippedItems[slot] == null)
            {
                // Find a default for this slot
                ItemData defaultItem = defaultEquipment.FirstOrDefault(x => IsItemForSlot(x, slot));
                if (defaultItem != null)
                {
                    EquipVisual(slot, defaultItem);
                }
            }
        }
    }

    // Helper to check if an item matches a slot (checks Armour stats)
    private bool IsItemForSlot(ItemData item, EquipmentType slot)
    {
        if (item == null || item.stats is not ItemArmourStats armorStats) return false;

        // --- MODIFIED: Removed Shoulders and Arms cases ---
        switch (armorStats.armourSlot)
        {
            case ItemArmourStats.ArmourSlot.Head: return slot == EquipmentType.Head;
            case ItemArmourStats.ArmourSlot.Chest: return slot == EquipmentType.Chest;
            case ItemArmourStats.ArmourSlot.Hands: return slot == EquipmentType.Hands;
            case ItemArmourStats.ArmourSlot.Legs: return slot == EquipmentType.Legs;
            case ItemArmourStats.ArmourSlot.Feet: return slot == EquipmentType.Feet;
            default: return false;
        }
    }

    public ItemStack EquipItem(EquipmentType slot, ItemStack itemToEquip)
    {
        if (playerInventory == null) return itemToEquip;

        ItemStack previouslyEquippedItem = equippedItems[slot];

        // 1. Remove old visual
        if (previouslyEquippedItem != null)
        {
            DestroyVisual(slot);
        }
        else
        {
            // If empty, destroy default visual
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

    public bool UnequipItem(EquipmentType slot)
    {
        if (playerInventory == null || equippedItems[slot] == null) return false;

        ItemStack itemToUnequip = equippedItems[slot];

        if (playerInventory.AddItem(itemToUnequip.itemData, itemToUnequip.quantity))
        {
            // 1. Remove Data
            equippedItems[slot] = null;

            // 2. Destroy Armor Visual
            DestroyVisual(slot);

            // 3. Restore Default Visual
            ItemData defaultItem = defaultEquipment.FirstOrDefault(x => IsItemForSlot(x, slot));
            if (defaultItem != null)
            {
                EquipVisual(slot, defaultItem);
            }

            playerStats?.CalculateFinalStats();
            OnEquipmentChanged?.Invoke(slot);
            return true;
        }
        return false;
    }

    private void EquipVisual(EquipmentType slot, ItemData itemData)
    {
        if (itemData.equippedPrefab == null) return;

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

    public ItemStack RemoveItemFromSlot(EquipmentType slot)
    {
        ItemStack removedItem = equippedItems[slot];
        if (removedItem != null)
        {
            equippedItems[slot] = null;
            DestroyVisual(slot);

            ItemData defaultItem = defaultEquipment.FirstOrDefault(x => IsItemForSlot(x, slot));
            if (defaultItem != null) EquipVisual(slot, defaultItem);

            playerStats?.CalculateFinalStats();
            OnEquipmentChanged?.Invoke(slot);
        }
        return removedItem;
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
}