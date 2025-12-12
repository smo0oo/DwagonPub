using UnityEngine;

/// <summary>
/// A simple C# component to hold data for an NPC,
/// replacing the need for Visual Scripting variables on them.
/// </summary>
public class NPCData : MonoBehaviour
{
    [Tooltip("The amount of gold this NPC merchant has.")]
    public int currencyGold = 1000;
}