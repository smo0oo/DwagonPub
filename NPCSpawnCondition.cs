using UnityEngine;
using PixelCrushers.DialogueSystem;

public class NPCSpawnCondition : MonoBehaviour
{
    [Tooltip("Lua condition that must be TRUE for this NPC to appear.")]
    [TextArea(2, 3)]
    public string spawnCondition = "Variable[\"HasMetKing\"] == true";

    [Tooltip("If checked, the condition is evaluated every time the scene loads. If unchecked, it might only run once.")]
    public bool checkOnRestore = true;

    /// <summary>
    /// Returns true if the condition allows spawning, or if the string is empty.
    /// </summary>
    public bool ShouldSpawn()
    {
        if (string.IsNullOrEmpty(spawnCondition)) return true;
        return Lua.IsTrue(spawnCondition);
    }
}