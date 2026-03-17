using UnityEngine;

public class PlayerHotbar : MonoBehaviour
{
    [Tooltip("The number of slots in the hotbar. This should match the UI.")]
    public int hotbarSize = 8;

    public HotbarAssignment[] hotbarSlotAssignments;

    void Awake()
    {
        if (hotbarSlotAssignments == null || hotbarSlotAssignments.Length != hotbarSize)
        {
            hotbarSlotAssignments = new HotbarAssignment[hotbarSize];
            for (int i = 0; i < hotbarSize; i++)
            {
                hotbarSlotAssignments[i] = new HotbarAssignment();
            }
        }

        // Force the first slot to always be the default attack ability.
        if (hotbarSlotAssignments.Length > 0)
        {
            PlayerMovement playerMovement = GetComponentInParent<PlayerMovement>();
            if (playerMovement != null && playerMovement.defaultAttackAbility != null)
            {
                hotbarSlotAssignments[0] = new HotbarAssignment
                {
                    type = HotbarAssignment.AssignmentType.Ability,
                    ability = playerMovement.defaultAttackAbility
                };
            }
        }
    }

    // --- MOVED HERE: Each player now privately assigns abilities to their own array! ---
    public void AutoAssignAbility(Ability ability)
    {
        if (ability == null || hotbarSlotAssignments == null) return;

        // 1. Prevent duplicates
        for (int i = 0; i < hotbarSlotAssignments.Length; i++)
        {
            if (hotbarSlotAssignments[i] != null &&
                hotbarSlotAssignments[i].type == HotbarAssignment.AssignmentType.Ability &&
                hotbarSlotAssignments[i].ability == ability)
            {
                return; // Already on the hotbar
            }
        }

        // 2. Find the first empty slot (Starting at index 1 so we don't overwrite the default attack!)
        for (int i = 1; i < hotbarSlotAssignments.Length; i++)
        {
            if (hotbarSlotAssignments[i] == null || hotbarSlotAssignments[i].type == HotbarAssignment.AssignmentType.Unassigned)
            {
                hotbarSlotAssignments[i] = new HotbarAssignment
                {
                    type = HotbarAssignment.AssignmentType.Ability,
                    ability = ability
                };

                // If this specific player happens to be the one currently on the UI screen, update the visual!
                if (HotbarManager.instance != null && HotbarManager.instance.IsActiveHotbar(this))
                {
                    HotbarManager.instance.RefreshAllHotbarSlots();
                }
                break;
            }
        }
    }
}