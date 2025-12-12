using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A Scriptable Object that defines a collection of items that can be dropped.
/// Create these as assets in your project via Create > RPG > Loot Table.
/// </summary>
[CreateAssetMenu(fileName = "New Loot Table", menuName = "RPG/Loot Table")]
public class LootTable : ScriptableObject
{
    [Header("Potential Drops")]
    [Tooltip("The list of all items that could potentially drop from this source.")]
    public List<LootDrop> potentialDrops;
}
