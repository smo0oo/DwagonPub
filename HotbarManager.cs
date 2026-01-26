using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class HotbarKeybind
{
    public KeyCode key;
    public string displayString;
}

public class HotbarManager : MonoBehaviour
{
    public static HotbarManager instance;

    [Header("UI References")]
    public List<HotbarSlot> hotbarSlots;

    [Header("Keybinds")]
    public List<HotbarKeybind> keybinds = new List<HotbarKeybind>
    {
        new HotbarKeybind { key = KeyCode.Alpha1, displayString = "1" },
        new HotbarKeybind { key = KeyCode.Alpha2, displayString = "2" },
        new HotbarKeybind { key = KeyCode.Alpha3, displayString = "3" },
        new HotbarKeybind { key = KeyCode.Alpha4, displayString = "4" },
        new HotbarKeybind { key = KeyCode.Alpha5, displayString = "5" },
        new HotbarKeybind { key = KeyCode.Alpha6, displayString = "6" },
        new HotbarKeybind { key = KeyCode.Alpha7, displayString = "7" },
        new HotbarKeybind { key = KeyCode.Alpha8, displayString = "8" }
    };

    [Header("Targeting")]
    public LayerMask enemyLayer;
    public LayerMask friendlyLayer;
    private GameObject mouseHoverTarget;
    private Camera mainCamera;

    public Inventory playerInventory => currentPlayerInventory;
    public PlayerAbilityHolder abilityHolder => currentAbilityHolder;

    private Inventory currentPlayerInventory;
    private PlayerHotbar currentPlayerHotbar;
    private PlayerStats currentPlayerStats;
    private PlayerAbilityHolder currentAbilityHolder;
    private PlayerMovement currentPlayerMovement;
    private UIDragDropController dragDropController;

    private ChanneledBeamController activeBeam = null;
    public Ability LockingAbility { get; set; } = null;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    void Start()
    {
        mainCamera = Camera.main;
        // Updated to use FindFirstObjectByType (non-obsolete)
        dragDropController = UnityEngine.Object.FindFirstObjectByType<UIDragDropController>();

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            hotbarSlots[i].Initialize(this, i);

            if (i < keybinds.Count)
            {
                hotbarSlots[i].SetKeybindText(keybinds[i].displayString);
            }
            else
            {
                hotbarSlots[i].SetKeybindText("");
            }
        }
    }

    void Update()
    {
        GameObject effectiveTarget = mouseHoverTarget ?? currentPlayerMovement?.TargetObject;

        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (currentPlayerHotbar == null || i >= currentPlayerHotbar.hotbarSlotAssignments.Length) continue;

            HotbarAssignment assignment = currentPlayerHotbar.hotbarSlotAssignments[i];
            if (assignment.type != HotbarAssignment.AssignmentType.Ability || assignment.ability == null)
            {
                hotbarSlots[i].IsOutOfRange = false;
                continue;
            }

            Ability ability = assignment.ability;
            if (IsTargetedAbility(ability))
            {
                if (effectiveTarget != null && currentAbilityHolder != null)
                {
                    float distance = Vector3.Distance(currentAbilityHolder.transform.position, effectiveTarget.transform.position);
                    hotbarSlots[i].IsOutOfRange = distance > ability.range;
                }
                else
                {
                    hotbarSlots[i].IsOutOfRange = true;
                }
            }
            else
            {
                hotbarSlots[i].IsOutOfRange = false;
            }
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask combinedMask = enemyLayer | friendlyLayer;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, combinedMask)) { mouseHoverTarget = hit.collider.gameObject; }
        else { mouseHoverTarget = null; }

        for (int i = 0; i < keybinds.Count; i++)
        {
            if (i >= hotbarSlots.Count) continue;
            HotbarSlot slot = hotbarSlots[i];
            if (slot == null || currentPlayerHotbar == null) continue;

            if (Input.GetKeyDown(keybinds[i].key))
            {
                slot.TriggerSlot(mouseHoverTarget);
            }

            HotbarAssignment assignment = currentPlayerHotbar.hotbarSlotAssignments[i];
            if (assignment != null && assignment.ability != null && assignment.ability.abilityType == AbilityType.ChanneledBeam)
            {
                if (Input.GetKeyUp(keybinds[i].key))
                {
                    if (activeBeam != null && activeBeam.sourceAbility == assignment.ability)
                    {
                        if (currentAbilityHolder != null && currentAbilityHolder.ActiveBeam == activeBeam)
                        {
                            currentAbilityHolder.CancelCast();
                        }
                    }
                }
            }
        }
    }

    private bool IsTargetedAbility(Ability ability)
    {
        return ability.abilityType == AbilityType.TargetedProjectile ||
               ability.abilityType == AbilityType.TargetedMelee ||
               ability.abilityType == AbilityType.Charge;
    }

    public void SetSlotRangeIndicator(Ability ability, bool outOfRange)
    {
        if (currentPlayerHotbar == null) return;
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (i < currentPlayerHotbar.hotbarSlotAssignments.Length)
            {
                if (currentPlayerHotbar.hotbarSlotAssignments[i].ability == ability)
                {
                    hotbarSlots[i].IsOutOfRange = outOfRange;
                }
            }
        }
    }

    public void HandleBeginDrag(HotbarSlot slot)
    {
        if (dragDropController == null) return;

        HotbarAssignment assignment = currentPlayerHotbar.hotbarSlotAssignments[slot.SlotIndex];
        Sprite icon = null;

        if (assignment.type == HotbarAssignment.AssignmentType.Item)
        {
            if (currentPlayerInventory != null && assignment.inventorySlotIndex >= 0 && assignment.inventorySlotIndex < currentPlayerInventory.items.Count)
            {
                ItemStack stack = currentPlayerInventory.items[assignment.inventorySlotIndex];
                if (stack != null && stack.itemData != null)
                {
                    icon = stack.itemData.icon;
                }
            }
        }
        else if (assignment.type == HotbarAssignment.AssignmentType.Ability)
        {
            if (assignment.ability != null)
            {
                icon = assignment.ability.icon;
            }
        }

        if (icon != null)
        {
            dragDropController.OnBeginDrag(slot, icon);
        }
    }

    public void HandleDrag(UnityEngine.EventSystems.PointerEventData eventData) { }

    public void HandleEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (dragDropController != null)
        {
            dragDropController.OnEndDrag();
        }
    }

    public void HandleDrop(HotbarSlot slot) { }

    public void SetActiveBeam(ChanneledBeamController beam) { activeBeam = beam; }

    public void DisplayHotbar(PlayerHotbar newHotbar, Inventory newInventory, PlayerAbilityHolder newAbilityHolder, PlayerStats newPlayerStats)
    {
        if (currentPlayerInventory != null) { currentPlayerInventory.OnInventoryChanged -= RefreshAllHotbarSlots; }

        currentPlayerInventory = newInventory;
        currentPlayerHotbar = newHotbar;
        currentAbilityHolder = newAbilityHolder;
        currentPlayerStats = newPlayerStats;

        if (newAbilityHolder != null) { currentPlayerMovement = newAbilityHolder.GetComponentInParent<PlayerMovement>(); }
        else { currentPlayerMovement = null; }

        if (currentPlayerInventory != null) { currentPlayerInventory.OnInventoryChanged += RefreshAllHotbarSlots; }

        foreach (var s in hotbarSlots) { s.LinkToPlayer(newAbilityHolder, newPlayerStats); }

        RefreshAllHotbarSlots();
    }

    public void SetHotbarSlotWithItem(int hotbarIndex, int inventorySlotIndex)
    {
        if (hotbarIndex == 0) return;
        if (currentPlayerHotbar == null || hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return;

        HotbarAssignment assignment = new HotbarAssignment { type = HotbarAssignment.AssignmentType.Item, inventorySlotIndex = inventorySlotIndex };
        currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex] = assignment;
        hotbarSlots[hotbarIndex].Assign(assignment);
        RefreshHotbarSlot(hotbarIndex);
    }

    public void SetHotbarSlotWithAbility(int hotbarIndex, Ability ability)
    {
        if (hotbarIndex == 0) return;
        if (currentPlayerHotbar == null || hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return;

        HotbarAssignment assignment = new HotbarAssignment { type = HotbarAssignment.AssignmentType.Ability, ability = ability };
        currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex] = assignment;
        hotbarSlots[hotbarIndex].Assign(assignment);
        RefreshHotbarSlot(hotbarIndex);
    }

    public void ClearHotbarSlot(int hotbarIndex)
    {
        if (hotbarIndex == 0) return;
        if (currentPlayerHotbar == null || hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return;

        currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex] = new HotbarAssignment();
        hotbarSlots[hotbarIndex].Assign(currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex]);
        RefreshHotbarSlot(hotbarIndex);
    }

    public void RefreshAllHotbarSlots()
    {
        if (currentPlayerHotbar == null) return;
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (i < currentPlayerHotbar.hotbarSlotAssignments.Length)
            {
                hotbarSlots[i].Assign(currentPlayerHotbar.hotbarSlotAssignments[i]);
            }
            RefreshHotbarSlot(i);
        }
    }

    private void RefreshHotbarSlot(int hotbarIndex)
    {
        if (hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return;

        HotbarSlot h_slot = hotbarSlots[hotbarIndex];
        if (currentPlayerHotbar == null)
        {
            // FIX: Ambiguous call resolved by explicit cast
            h_slot.UpdateSlot((HotbarAssignment)null);
            return;
        }

        HotbarAssignment assignment = currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex];
        h_slot.UpdateSlot(assignment);
    }
}