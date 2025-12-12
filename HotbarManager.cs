using UnityEngine;
using System.Collections.Generic;
using System; // Required for [Serializable]

// --- NEW HELPER CLASS ---
// This simple class holds both the input key and the text to display on the UI.
[Serializable]
public class HotbarKeybind
{
    public KeyCode key;
    public string displayString;
}
// --- END NEW CLASS ---


public class HotbarManager : MonoBehaviour
{
    public static HotbarManager instance;

    [Header("UI References")]
    public List<HotbarSlot> hotbarSlots;

    [Header("Keybinds")]
    [Tooltip("The keys used to trigger the hotbar slots. Should match the number of slots.")]
    // --- THIS REPLACES THE OLD 'keyCodes' ARRAY ---
    public List<HotbarKeybind> keybinds = new List<HotbarKeybind>
    {
        // You can set up your defaults here
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

    private Inventory currentPlayerInventory;
    private PlayerHotbar currentPlayerHotbar;
    private PlayerStats currentPlayerStats;
    private PlayerAbilityHolder currentAbilityHolder;
    private PlayerMovement currentPlayerMovement;

    private ChanneledBeamController activeBeam = null;
    public Ability LockingAbility { get; set; } = null;

    // The old 'keyCodes' array is no longer needed.
    // private KeyCode[] keyCodes = { ... };

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); }
        else { instance = this; }
    }

    // --- MODIFIED METHOD ---
    void Start()
    {
        mainCamera = Camera.main;
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            hotbarSlots[i].Initialize(i);

            // Use the displayString from our new keybinds list
            if (i < keybinds.Count)
            {
                hotbarSlots[i].SetKeybindText(keybinds[i].displayString);
            }
            else
            {
                // Failsafe in case the lists don't match
                hotbarSlots[i].SetKeybindText("");
            }
        }
    }

    // --- MODIFIED METHOD ---
    void Update()
    {
        GameObject effectiveTarget = mouseHoverTarget ?? currentPlayerMovement?.TargetObject;
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            if (currentPlayerHotbar == null || i >= currentPlayerHotbar.hotbarSlotAssignments.Length) continue;

            // Range checks (unchanged)
            HotbarAssignment assignment = currentPlayerHotbar.hotbarSlotAssignments[i];
            if (assignment.type != HotbarAssignment.AssignmentType.Ability || assignment.ability == null) { hotbarSlots[i].IsOutOfRange = false; continue; }
            Ability ability = assignment.ability;
            switch (ability.abilityType) { case AbilityType.TargetedProjectile: case AbilityType.TargetedMelee: case AbilityType.Charge: if (effectiveTarget != null && currentAbilityHolder != null) { CharacterRoot casterRoot = currentAbilityHolder.GetComponentInParent<CharacterRoot>(); CharacterRoot targetRoot = effectiveTarget.GetComponentInParent<CharacterRoot>(); if (casterRoot == null || targetRoot == null) { hotbarSlots[i].IsOutOfRange = true; break; } bool isAlly = casterRoot.gameObject.layer == targetRoot.gameObject.layer; bool canTargetAllies = ability.friendlyEffects.Count > 0; bool canTargetEnemies = ability.hostileEffects.Count > 0; bool isValidTarget = (isAlly && canTargetAllies) || (!isAlly && canTargetEnemies); if (!isValidTarget) { hotbarSlots[i].IsOutOfRange = true; } else { float distance = Vector3.Distance(currentAbilityHolder.transform.position, effectiveTarget.transform.position); hotbarSlots[i].IsOutOfRange = distance > ability.range; } } else { hotbarSlots[i].IsOutOfRange = true; } break; default: hotbarSlots[i].IsOutOfRange = false; break; }
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask combinedMask = enemyLayer | friendlyLayer;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, combinedMask)) { mouseHoverTarget = hit.collider.gameObject; }
        else { mouseHoverTarget = null; }

        // Use the new 'keybinds' list for input
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
                // Also check for KeyUp using the new list
                if (Input.GetKeyUp(keybinds[i].key))
                {
                    if (activeBeam != null && activeBeam.sourceAbility == assignment.ability)
                    {
                        Destroy(activeBeam.gameObject);
                    }
                }
            }
        }
    }

    #region Unchanged Code
    public void SetSlotRangeIndicator(Ability ability, bool outOfRange) { if (currentPlayerHotbar == null) return; for (int i = 0; i < hotbarSlots.Count; i++) { if (currentPlayerHotbar.hotbarSlotAssignments[i].ability == ability) { hotbarSlots[i].IsOutOfRange = outOfRange; return; } } }
    public void SetActiveBeam(ChanneledBeamController beam) { activeBeam = beam; }
    public void DisplayHotbar(PlayerHotbar newHotbar, Inventory newInventory, PlayerAbilityHolder newAbilityHolder, PlayerStats newPlayerStats) { if (currentPlayerInventory != null) { currentPlayerInventory.OnInventoryChanged -= RefreshAllHotbarSlots; } currentPlayerInventory = newInventory; currentPlayerHotbar = newHotbar; currentAbilityHolder = newAbilityHolder; currentPlayerStats = newPlayerStats; if (newAbilityHolder != null) { currentPlayerMovement = newAbilityHolder.GetComponentInParent<PlayerMovement>(); } else { currentPlayerMovement = null; } if (currentPlayerInventory != null) { currentPlayerInventory.OnInventoryChanged += RefreshAllHotbarSlots; } foreach (var s in hotbarSlots) { s.LinkToPlayer(newAbilityHolder, newPlayerStats); } if (currentPlayerHotbar != null) { for (int i = 0; i < hotbarSlots.Count; i++) { if (i < currentPlayerHotbar.hotbarSlotAssignments.Length) { hotbarSlots[i].Assign(currentPlayerHotbar.hotbarSlotAssignments[i]); } } } RefreshAllHotbarSlots(); }
    public void SetHotbarSlotWithItem(int hotbarIndex, int inventorySlotIndex) { if (hotbarIndex == 0) return; if (currentPlayerHotbar == null || hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return; HotbarAssignment assignment = new HotbarAssignment { type = HotbarAssignment.AssignmentType.Item, inventorySlotIndex = inventorySlotIndex }; currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex] = assignment; hotbarSlots[hotbarIndex].Assign(assignment); RefreshHotbarSlot(hotbarIndex); }
    public void SetHotbarSlotWithAbility(int hotbarIndex, Ability ability) { if (hotbarIndex == 0) return; if (currentPlayerHotbar == null || hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return; HotbarAssignment assignment = new HotbarAssignment { type = HotbarAssignment.AssignmentType.Ability, ability = ability }; currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex] = assignment; hotbarSlots[hotbarIndex].Assign(assignment); RefreshHotbarSlot(hotbarIndex); }
    public void ClearHotbarSlot(int hotbarIndex) { if (hotbarIndex == 0) return; if (currentPlayerHotbar == null || hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return; currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex] = new HotbarAssignment(); hotbarSlots[hotbarIndex].Assign(currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex]); RefreshHotbarSlot(hotbarIndex); }
    public void RefreshAllHotbarSlots() { for (int i = 0; i < hotbarSlots.Count; i++) { RefreshHotbarSlot(i); } }
    private void RefreshHotbarSlot(int hotbarIndex) { if (hotbarIndex < 0 || hotbarIndex >= hotbarSlots.Count) return; HotbarSlot h_slot = hotbarSlots[hotbarIndex]; if (currentPlayerHotbar == null) { h_slot.UpdateSlot(null as ItemStack); return; } HotbarAssignment assignment = currentPlayerHotbar.hotbarSlotAssignments[hotbarIndex]; if (assignment.type == HotbarAssignment.AssignmentType.Item) { if (currentPlayerInventory != null && assignment.inventorySlotIndex >= 0 && assignment.inventorySlotIndex < currentPlayerInventory.items.Count) { h_slot.UpdateSlot(currentPlayerInventory.items[assignment.inventorySlotIndex]); } else { h_slot.UpdateSlot(null as ItemStack); } } else if (assignment.type == HotbarAssignment.AssignmentType.Ability) { h_slot.UpdateSlot(assignment.ability); } else { h_slot.UpdateSlot(null as ItemStack); } }
    #endregion
}