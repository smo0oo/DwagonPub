using UnityEngine;

public class StatefulEntity : MonoBehaviour
{
    [Tooltip("A unique ID for this object within this scene. Use the context menu to generate one.")]
    public string uniqueId;

    [ContextMenu("Generate Unique ID")]
    private void GenerateUniqueId()
    {
        uniqueId = System.Guid.NewGuid().ToString();
    }
}