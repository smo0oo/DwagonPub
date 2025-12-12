using UnityEngine;

[System.Serializable]
public class HotbarAssignment
{
    public enum AssignmentType { Unassigned, Item, Ability }
    public AssignmentType type = AssignmentType.Unassigned;

    // Used if type is Item
    public int inventorySlotIndex = -1;

    // Used if type is Ability
    public Ability ability;
}