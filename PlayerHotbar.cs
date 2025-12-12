using UnityEngine;

public class PlayerHotbar : MonoBehaviour
{
    [Tooltip("The number of slots in the hotbar. This should match the UI.")]
    public int hotbarSize = 8;

    public HotbarAssignment[] hotbarSlotAssignments;

    // --- MODIFIED METHOD ---
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
}