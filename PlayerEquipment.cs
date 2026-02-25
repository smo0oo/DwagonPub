using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using MagicaCloth2;

[System.Serializable]
public class DefaultLoadoutItem
{
    public ItemData item;
    public EquipmentType targetSlot;
    public int quantity = 1;
}

[System.Serializable]
public class BodyPartDefault
{
    public EquipmentType slot;
    public GameObject prefab;
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
    public List<BodyPartDefault> defaultBodyParts;

    private Dictionary<EquipmentType, GameObject> equippedVisuals = new Dictionary<EquipmentType, GameObject>();
    private Inventory playerInventory;
    private PlayerStats playerStats;

    // Cache the exact bones found on your player character for 1:1 matching
    private Dictionary<string, Transform> boneMap = new Dictionary<string, Transform>();

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

        CacheSkeletonBones();
    }

    void Start()
    {
        InitializeNakedBody();
        EquipDefaults();
    }

    private void CacheSkeletonBones()
    {
        if (targetMesh == null) return;

        boneMap.Clear();
        foreach (Transform bone in targetMesh.bones)
        {
            if (bone != null && !boneMap.ContainsKey(bone.name))
            {
                boneMap.Add(bone.name, bone);
            }
        }

        if (targetMesh.rootBone != null && !boneMap.ContainsKey(targetMesh.rootBone.name))
        {
            boneMap.Add(targetMesh.rootBone.name, targetMesh.rootBone);
        }
    }

    private void InitializeNakedBody()
    {
        foreach (EquipmentType slot in Enum.GetValues(typeof(EquipmentType)))
        {
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

    private void EquipVisual(EquipmentType slot, ItemData itemData)
    {
        if (itemData == null || itemData.equippedPrefab == null) return;
        CreateAndBindVisual(slot, itemData.equippedPrefab, itemData.itemType == ItemType.Weapon);
    }

    private void RestoreNakedPart(EquipmentType slot)
    {
        var defaultPart = defaultBodyParts.Find(x => x.slot == slot);

        if (defaultPart != null && defaultPart.prefab != null)
        {
            CreateAndBindVisual(slot, defaultPart.prefab, false);
        }
    }

    private void CreateAndBindVisual(EquipmentType slot, GameObject prefab, bool isWeapon)
    {
        DestroyVisual(slot);

        if (isWeapon)
        {
            Transform attachPoint = GetAttachPoint(slot);
            if (attachPoint == null) return;
            GameObject visual = Instantiate(prefab, attachPoint);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            equippedVisuals[slot] = visual;
        }
        else
        {
            if (targetMesh == null) return;
            if (boneMap.Count == 0) CacheSkeletonBones();

            GameObject visual = Instantiate(prefab, targetMesh.transform.parent);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // --- AAA CLOTH FIX STEP 1: HALT AUTOMATIC BUILD ---
            MagicaCloth[] clothComponents = visual.GetComponentsInChildren<MagicaCloth>(true);
            foreach (var cloth in clothComponents)
            {
                // Force it to pause its startup sequence so it doesn't build on the dummy bones
                cloth.enabled = false;
            }

            SkinnedMeshRenderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            List<GameObject> skeletonRootsToCleanup = new List<GameObject>();

            foreach (var renderer in renderers)
            {
                renderer.updateWhenOffscreen = true;

                if (renderer.rootBone != null)
                {
                    Transform prefabSkeletonRoot = renderer.rootBone;
                    while (prefabSkeletonRoot.parent != null && prefabSkeletonRoot.parent != visual.transform)
                    {
                        prefabSkeletonRoot = prefabSkeletonRoot.parent;
                    }
                    if (!skeletonRootsToCleanup.Contains(prefabSkeletonRoot.gameObject))
                        skeletonRootsToCleanup.Add(prefabSkeletonRoot.gameObject);
                }

                Transform[] newBones = new Transform[renderer.bones.Length];
                for (int i = 0; i < renderer.bones.Length; i++)
                {
                    Transform originalBone = renderer.bones[i];
                    if (originalBone == null) continue;

                    if (boneMap.TryGetValue(originalBone.name, out Transform matchingBone))
                    {
                        newBones[i] = matchingBone;
                    }
                    else
                    {
                        newBones[i] = originalBone;
                        Debug.LogWarning($"[Skinning] Could not find bone '{originalBone.name}' on player rig!");
                    }
                }

                renderer.bones = newBones;

                if (renderer.rootBone != null && boneMap.TryGetValue(renderer.rootBone.name, out Transform matchingRoot))
                    renderer.rootBone = matchingRoot;
                else
                    renderer.rootBone = targetMesh.rootBone;

                renderer.localBounds = targetMesh.localBounds;
            }

            // AAA CLEANUP: Destroy redundant zombie bones
            foreach (var root in skeletonRootsToCleanup)
            {
                Destroy(root);
            }

            // --- AAA CLOTH COLLIDER INJECTION & REBUILD ---

            // 1. Gather all valid Magica colliders currently on the player's rig
            List<ColliderComponent> playerColliders = new List<ColliderComponent>();
            ColliderComponent[] allCols = targetMesh.transform.parent.GetComponentsInChildren<ColliderComponent>(true);

            foreach (var col in allCols)
            {
                // Ignore the dead colliders sitting inside the newly spawned armor visual
                if (!col.transform.IsChildOf(visual.transform))
                {
                    playerColliders.Add(col);
                }
            }

            foreach (var cloth in clothComponents)
            {
                // 2. Prevent stretching by updating internal transform references to the new player bones
                cloth.ReplaceTransform(boneMap);

                // 3. Inject the global collision hull into the simulation parameters
                if (cloth.SerializeData != null && cloth.SerializeData.colliderCollisionConstraint != null)
                {
                    cloth.SerializeData.colliderCollisionConstraint.colliderList.Clear();
                    cloth.SerializeData.colliderCollisionConstraint.colliderList.AddRange(playerColliders);
                }

                // 4. Safely wake up the physics and compile the updated constraints
                cloth.enabled = true;
                cloth.BuildAndRun();
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

    public ItemStack EquipItem(EquipmentType slot, ItemStack itemToEquip)
    {
        ItemStack previouslyEquippedItem = equippedItems[slot];
        DestroyVisual(slot);
        equippedItems[slot] = itemToEquip;

        if (itemToEquip != null) EquipVisual(slot, itemToEquip.itemData);
        else RestoreNakedPart(slot);

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
            DestroyVisual(slot);
            RestoreNakedPart(slot);

            playerStats?.CalculateFinalStats();
            OnEquipmentChanged?.Invoke(slot);
        }
        return removedItem;
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