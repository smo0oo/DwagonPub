using UnityEngine;

public enum ResourceType
{
    None,
    Rations,
    Fuel,
    Integrity, // For Wagon Repairs
    Gold
}

[CreateAssetMenu(fileName = "New Resource Item", menuName = "Inventory/Resource Item")]
public class ResourceItemData : ItemData
{
    [Header("Resource Properties")]
    [Tooltip("Which wagon resource does this item refill?")]
    public ResourceType resourceType;

    [Tooltip("How much of the resource is restored (e.g., 10 Fuel).")]
    public int restoreAmount;
}