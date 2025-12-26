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

    // --- REGISTRY LOGIC ---
    // We register as soon as the object wakes up.
    // We only unregister if the object is destroyed (scene unload), 
    // NOT when it is just disabled (SetActive false).
    void Awake()
    {
        SceneStateManager.RegisterEntity(this);
    }

    void OnDestroy()
    {
        SceneStateManager.UnregisterEntity(this);
    }
}