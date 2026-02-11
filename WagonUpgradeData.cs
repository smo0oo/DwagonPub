using UnityEngine;
using System.Collections.Generic;

public enum WagonUpgradeType
{
    Wheel,
    Chassis,
    Cover,
    Storage,
    Lantern,
    Interior,
    Defense
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
    public GameObject visualPrefab;

    [Header("Cost")]
    public int goldCost;

    [Header("Stats Modifiers")]
    public float speedBonus = 0f;
    public float efficiencyBonus = 0f;
    public int storageSlotsAdded = 0;
    public int defenseBonus = 0;
    public float comfortBonus = 0f;

    [Header("Travel Capabilities")]
    [Tooltip("Tags that allow travel on specific terrain, e.g. 'Rocky', 'Snow', 'Water'.")]
    public List<string> traversalTags;
}