using UnityEngine;

public enum WagonUpgradeType
{
    Wheel,
    Chassis,
    Cover,      // The canvas/roof
    Storage,    // Chests/Barrels
    Lantern,    // Lighting
    Interior,   // Comfort items (visible if camera cuts inside)
    Defense     // Spikes/Plating
}

[CreateAssetMenu(menuName = "Dwagon/Wagon Upgrade Data")]
public class WagonUpgradeData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public WagonUpgradeType type;

    [Header("Visuals")]
    [Tooltip("The mesh prefab to spawn on the wagon anchor.")]
    public GameObject visualPrefab;

    [Header("Cost")]
    public int goldCost;
    // You could add 'List<ItemQuantity> materialCost' here later for crafting

    [Header("Stats Modifiers")]
    [Tooltip("Adds to World Map movement speed.")]
    public float speedBonus = 0f;

    [Tooltip("Reduces resource consumption (Fuel/Rations).")]
    public float efficiencyBonus = 0f;

    [Tooltip("Adds extra slots to the Wagon Inventory.")]
    public int storageSlotsAdded = 0;

    [Tooltip("Adds to max Integrity or reduces damage.")]
    public int defenseBonus = 0;

    [Tooltip("Improves resting healing rates.")]
    public float comfortBonus = 0f;
}